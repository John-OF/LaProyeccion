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

        private Vector2 objetivo;
        private float pausaRestante;

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

        private void Update()
        {
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
        }
    }
}
