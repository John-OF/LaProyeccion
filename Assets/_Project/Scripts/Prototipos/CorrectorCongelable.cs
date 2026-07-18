using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Corrector congelable (idea #11): PlataformaCongelable aplicada al guardia.
    /// En su mundo activo patrulla su raíl A↔B y corrige al tocarte (collider
    /// trigger, como el Corrector). Al cambiar al otro mundo queda CONGELADO
    /// exactamente donde estaba: inofensivo y SÓLIDO (el collider deja de ser
    /// trigger) — te subes encima. El guardia como plataforma móvil: esperar a
    /// que el raíl lo lleve donde conviene → cambiar → escalar sobre él.
    ///
    /// Existe y se VE en ambos mundos; lo que cambia es el ESTADO, y el estado
    /// se lee por color (Pilar 3): color base = te caza AHORA; tinte hielo =
    /// escalón. Despertarlo mientras lo tocas (cambiar a su mundo estando
    /// encima) te corrige — la regla única "tocar un corrector activo =
    /// corrección" no tiene excepciones. No combinar con WorldExclusivePresence
    /// (un guardia que no existe en el mundo destino no puede ser escalón).
    ///
    /// Notas de composición: PlacaDePresion NO lo detecta como pisador (chequea
    /// Corrector y CorrectorVigilante por tipo); si un puzzle lo necesita, se
    /// añade allí. Congelarlo mientras solapa al jugador lo vuelve sólido
    /// dentro de él: el solver depenetra y, si queda atrapado de verdad,
    /// PlayerSafePush aplica su protocolo normal de paredes.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CorrectorCongelable : MonoBehaviour
    {
        [Header("Patrulla (posiciones de mundo, como el Corrector)")]
        [SerializeField] private Vector2 puntoA;
        [SerializeField] private Vector2 puntoB;
        [SerializeField, Min(0.1f)] private float velocidad = 2.5f;
        [Tooltip("Pausa en cada extremo: hace el patrón legible (Pilar 3).")]
        [SerializeField, Min(0f)] private float pausaEnExtremos = 0.4f;

        [Header("Congelación")]
        [Tooltip("Mundo en el que el guardia PATRULLA Y MATA; en el otro queda congelado y sólido.")]
        [SerializeField] private WorldState mundoActivo = WorldState.Simulation;
        [Tooltip("Tinte mientras está congelado (mismo lenguaje que PlataformaCongelable).")]
        [SerializeField] private Color tintCongelado = new Color(0.55f, 0.75f, 0.9f);

        private Rigidbody2D rb;
        private BoxCollider2D col;
        private SpriteRenderer sprite;
        private Color colorOriginal;

        private Vector2 objetivo;
        private float pausaRestante;
        private bool congelado;

        private void Awake()
        {
            col = GetComponent<BoxCollider2D>();
            // Kinematic como el Corrector; interpolado como la plataforma (se ve
            // fluido en movimiento y empuja limpio al jugador cuando es sólido).
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) colorOriginal = sprite.color;
        }

        private void OnEnable() => WorldManager.OnWorldChanged += OnWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= OnWorldChanged;

        private void Start()
        {
            transform.position = puntoA;
            rb.position = puntoA;
            objetivo = puntoB;
        }

        /// <summary>
        /// Vuelve al estado de arranque (posición A, rumbo a B, sin pausa).
        /// Convención de FASE CERO del grabador/replay de pasadas.
        /// </summary>
        public void ReiniciarFase()
        {
            transform.position = puntoA;
            rb.position = puntoA;
            objetivo = puntoB;
            pausaRestante = 0f;
        }

        private void OnWorldChanged(WorldState nuevo)
        {
            // También sincroniza el estado inicial (OnWorldChanged dispara en Start).
            congelado = nuevo != mundoActivo;
            // Congelado = plataforma (sólido); despierto = peligro (trigger).
            col.isTrigger = !congelado;
            if (sprite != null) sprite.color = congelado ? tintCongelado : colorOriginal;
        }

        private void FixedUpdate()
        {
            if (congelado) return;

            if (pausaRestante > 0f)
            {
                pausaRestante -= Time.fixedDeltaTime;
                return;
            }

            Vector2 nueva = Vector2.MoveTowards(rb.position, objetivo, velocidad * Time.fixedDeltaTime);
            rb.MovePosition(nueva);

            if (Vector2.Distance(nueva, objetivo) < 0.01f)
            {
                objetivo = objetivo == puntoB ? puntoA : puntoB;
                pausaRestante = pausaEnExtremos;
            }
        }

        // Despierto es trigger: el toque corrige (regla única del Corrector).
        private void OnTriggerEnter2D(Collider2D other) => TryKill(other);
        private void OnTriggerStay2D(Collider2D other) => TryKill(other);

        private void TryKill(Collider2D other)
        {
            if (congelado) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            GameSession.Instance?.RespawnPlayer();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawLine(puntoA, puntoB);
            Gizmos.DrawWireSphere(puntoA, 0.25f);
            Gizmos.DrawWireSphere(puntoB, 0.25f);
            Gizmos.color = new Color(0.55f, 0.75f, 0.9f, 0.8f);
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
    }
}
