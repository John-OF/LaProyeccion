using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE v1.1;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Corrector: patrullero DETERMINISTA de una sola regla, estilo Inside:
    /// va y viene entre puntoA y puntoB a velocidad constante (con pausa breve
    /// en los extremos, para que el patrón se lea). Si toca al jugador → respawn.
    /// Sin persecución, sin aleatoriedad, sin estados: el jugador aprende el
    /// patrón o usa el cambio de mundo como escudo (combinar con
    /// WorldExclusivePresence para que exista en un solo mundo; el pulso del
    /// radar revela su silueta en movimiento gratis, vía ese mismo componente).
    ///
    /// Extensión OPCIONAL (prototipo P_Eco, apagada por defecto — los labs
    /// validados no cambian): con <see cref="atiendeEcos"/>, el eco que deja el
    /// jugador al cambiar de mundo (<see cref="EcoDeCambio"/>) lo atrae como
    /// imán posicional. Sigue sin perseguir al jugador y sin salirse del raíl.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Corrector : MonoBehaviour
    {
        [Header("Patrulla (posiciones de mundo)")]
        [SerializeField] private Vector2 puntoA;
        [SerializeField] private Vector2 puntoB;
        [SerializeField, Min(0.1f)] private float velocidad = 2.5f;
        [Tooltip("Pausa en cada extremo: hace el patrón legible (Pilar 3).")]
        [SerializeField, Min(0f)] private float pausaEnExtremos = 0.4f;

        [Header("Eco del cambio (prototipo P_Eco — apagado por defecto)")]
        [Tooltip("Si hay un eco del jugador (EcoDeCambio) a rangoEco o menos, el " +
                 "corrector suspende la patrulla y acude al punto de su raíl A-B " +
                 "más cercano al eco; al disolverse el eco, pausa breve y retoma. " +
                 "Regla única y determinista: persigue al ECO, jamás al jugador, " +
                 "y NUNCA abandona el raíl.")]
        [SerializeField] private bool atiendeEcos = false;
        [Tooltip("Distancia máxima corrector→eco para que el eco lo atraiga.")]
        [SerializeField, Min(1f)] private float rangoEco = 6f;

        private Vector2 objetivo;
        private float pausaRestante;
        private bool investigando;

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            // Collider en movimiento: kinematic para que los triggers disparen bien.
            var rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        private void Start()
        {
            transform.position = puntoA;
            objetivo = puntoB;
        }

        /// <summary>
        /// Vuelve al estado de arranque (posición A, rumbo a B, sin pausa ni
        /// investigación). Convención de FASE CERO del grabador/replay de
        /// pasadas: grabación y reproducción parten de guardias idénticos.
        /// </summary>
        public void ReiniciarFase()
        {
            transform.position = puntoA;
            objetivo = puntoB;
            pausaRestante = 0f;
            investigando = false;
        }

        private void Update()
        {
            if (AtenderEco()) return;

            // Tiempo escalado: se congela en pausa, como todo el mundo de juego.
            if (pausaRestante > 0f)
            {
                pausaRestante -= Time.deltaTime;
                return;
            }

            Vector2 nueva = Vector2.MoveTowards(
                transform.position, objetivo, velocidad * Time.deltaTime);
            transform.position = nueva;

            if (Vector2.Distance(nueva, objetivo) < 0.01f)
            {
                objetivo = objetivo == puntoB ? puntoA : puntoB;
                pausaRestante = pausaEnExtremos;
            }
        }

        /// <summary>
        /// Estado "investigar" del eco (prototipo P_Eco). Devuelve true si la
        /// patrulla queda suspendida este frame. El rango solo gatilla el INICIO:
        /// una vez alertado, el corrector sigue al eco vivo aunque uno nuevo
        /// aparezca más lejos (ya está en alerta; regla legible). El destino se
        /// proyecta SIEMPRE sobre el segmento A-B — el corrector nunca deja su
        /// raíl, solo se desvía por él (conserva las garantías del diseño de
        /// nivel determinista).
        /// </summary>
        private bool AtenderEco()
        {
            if (!atiendeEcos) return false;

            Vector2? eco = EcoDeCambio.PosicionActiva;
            if (!eco.HasValue)
            {
                if (investigando)
                {
                    // El eco se disolvió: "mirar alrededor" y retomar la patrulla
                    // hacia el objetivo que ya tenía (determinismo intacto).
                    investigando = false;
                    pausaRestante = pausaEnExtremos;
                }
                return false;
            }

            if (!investigando)
            {
                if (Vector2.Distance(transform.position, eco.Value) > rangoEco)
                    return false;
                investigando = true;
            }

            Vector2 destino = PuntoDelRailMasCercano(eco.Value);
            transform.position = Vector2.MoveTowards(
                transform.position, destino, velocidad * Time.deltaTime);
            return true;
        }

        private Vector2 PuntoDelRailMasCercano(Vector2 p)
        {
            Vector2 ab = puntoB - puntoA;
            float sqr = ab.sqrMagnitude;
            if (sqr < 1e-6f) return puntoA;
            float t = Mathf.Clamp01(Vector2.Dot(p - puntoA, ab) / sqr);
            return puntoA + ab * t;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryKill(other);
        private void OnTriggerStay2D(Collider2D other) => TryKill(other);

        private void TryKill(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            GameSession.Instance?.RespawnPlayer();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawLine(puntoA, puntoB);
            Gizmos.DrawWireSphere(puntoA, 0.25f);
            Gizmos.DrawWireSphere(puntoB, 0.25f);
            if (atiendeEcos)
            {
                Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, rangoEco);
            }
        }
    }
}
