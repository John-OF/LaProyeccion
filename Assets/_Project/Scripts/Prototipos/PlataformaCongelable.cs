using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Objetos que se congelan al cambiar (idea #3 de Claude, 2026-07-13,
    /// ideas.md): plataforma que se mueve por su raíl A↔B mientras estás en su
    /// mundo activo (por defecto la Simulación) y queda CONGELADA exactamente
    /// donde estaba al cambiar al otro mundo. El cambio de mundo se vuelve un
    /// botón de pausa posicional: detienes la plataforma a mitad de trayecto
    /// y la usas de escalón. Volver a su mundo la reanuda hacia el mismo
    /// objetivo (determinista, sin estados ocultos).
    ///
    /// La plataforma EXISTE Y ES SÓLIDA EN AMBOS MUNDOS — ese es el puzzle:
    /// congelada sigue siendo un escalón. No combinar con PlatformDual /
    /// WorldExclusivePresence (se anularían: una plataforma que no existe en
    /// el mundo destino no puede ser escalón congelado).
    ///
    /// Legibilidad (Pilar 3): congelada se tiñe con <see cref="tintCongelada"/>
    /// (el estado se LEE de un vistazo, como el magenta del suelo glicheado).
    /// Movimiento en FixedUpdate vía MovePosition (kinematic) y arrastre
    /// explícito del jugador que va encima: PlayerController pisa la velocidad
    /// horizontal cada FixedUpdate, así que la fricción no lo llevaría sola.
    /// </summary>
    // Después de PlayerController (orden 0): él escribe la velocidad del jugador
    // cada FixedUpdate y el arrastre de abajo se le SUMA — si corriéramos antes,
    // nos pisaría la suma y el jugador se quedaría atrás.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlataformaCongelable : MonoBehaviour
    {
        [Header("Raíl (posiciones de mundo, como el Corrector)")]
        [SerializeField] private Vector2 puntoA;
        [SerializeField] private Vector2 puntoB;
        [SerializeField, Min(0.1f)] private float velocidad = 2f;
        [Tooltip("Pausa en cada extremo: hace el patrón legible (Pilar 3).")]
        [SerializeField, Min(0f)] private float pausaEnExtremos = 0.4f;

        [Header("Congelación")]
        [Tooltip("Mundo en el que la plataforma SE MUEVE; en el otro queda congelada donde esté.")]
        [SerializeField] private WorldState mundoActivo = WorldState.Simulation;
        [Tooltip("Tinte del sprite mientras está congelada (estado legible de un vistazo).")]
        [SerializeField] private Color tintCongelada = new Color(0.55f, 0.75f, 0.9f);
        [Tooltip("Animator opcional (decoración animada): congelarse también lo pausa.")]
        [SerializeField] private Animator animator;

        private Rigidbody2D rb;
        private BoxCollider2D col;
        private SpriteRenderer sprite;
        private Color colorOriginal;
        private Rigidbody2D playerRb;

        private Vector2 objetivo;
        private float pausaRestante;
        private bool congelada;

        private void Awake()
        {
            col = GetComponent<BoxCollider2D>();
            // Plataforma sólida en movimiento: kinematic para empujar bien al
            // jugador dinámico (mismo motivo que el Corrector, pero SIN trigger).
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            // Física a 50 Hz vs render a 60+ fps: sin interpolar, la plataforma
            // (y el jugador encima) se ve a saltitos/borrosa en movimiento.
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) colorOriginal = sprite.color;

            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) playerRb = pc.GetComponent<Rigidbody2D>();
        }

        private void OnEnable() => WorldManager.OnWorldChanged += OnWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= OnWorldChanged;

        private void Start()
        {
            transform.position = puntoA;
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
            congelada = nuevo != mundoActivo;
            if (sprite != null) sprite.color = congelada ? tintCongelada : colorOriginal;
            if (animator != null) animator.speed = congelada ? 0f : 1f;
        }

        private void FixedUpdate()
        {
            if (congelada) return;

            if (pausaRestante > 0f)
            {
                pausaRestante -= Time.fixedDeltaTime;
                return;
            }

            Vector2 antes = rb.position;
            Vector2 nueva = Vector2.MoveTowards(antes, objetivo, velocidad * Time.fixedDeltaTime);
            rb.MovePosition(nueva);

            // Arrastrar al jugador que va encima SUMANDO velocidad (no escribiendo
            // posición: eso salta la interpolación del Rigidbody y se ve borroso).
            // Solo X: en vertical ya lo lleva el empuje físico del collider.
            // PlayerController pisa la velocidad X cada FixedUpdate, pero corre
            // ANTES que nosotros (DefaultExecutionOrder) — la suma sobrevive.
            if (playerRb != null && JugadorEncima())
                playerRb.linearVelocity += new Vector2((nueva.x - antes.x) / Time.fixedDeltaTime, 0f);

            if (Vector2.Distance(nueva, objetivo) < 0.01f)
            {
                objetivo = objetivo == puntoB ? puntoA : puntoB;
                pausaRestante = pausaEnExtremos;
            }
        }

        /// <summary>
        /// ¿El jugador está de pie sobre la plataforma? Caja fina pegada al
        /// borde superior del collider, ligeramente más estrecha (los roces
        /// laterales no cuentan como "encima").
        /// </summary>
        private bool JugadorEncima()
        {
            Bounds b = col.bounds;
            Vector2 centro = new Vector2(b.center.x, b.max.y + 0.06f);
            Vector2 tam = new Vector2(b.size.x * 0.95f, 0.12f);
            foreach (var hit in Physics2D.OverlapBoxAll(centro, tam, 0f))
                if (hit.attachedRigidbody == playerRb) return true;
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.55f, 0.75f, 0.9f, 0.9f);
            Gizmos.DrawLine(puntoA, puntoB);
            Gizmos.DrawWireSphere(puntoA, 0.25f);
            Gizmos.DrawWireSphere(puntoB, 0.25f);
        }
    }
}
