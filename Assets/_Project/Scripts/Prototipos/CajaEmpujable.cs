using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Caja empujable con presencia asimétrica (idea #14): existe y es sólida
    /// en AMBOS mundos, pero solo se puede empujar en <see cref="mundoEmpujable"/>
    /// (la Simulación por defecto: todo nuevo y ligero). En el otro mundo está
    /// clavada — Rigidbody2D estático, tinte óxido — pero sigue siendo un apoyo
    /// firme. Ordenas el mundo desde la mentira para pisar firme en la verdad.
    ///
    /// Física del empuje (la advertencia de la idea: "tuning de fricción,
    /// prototipar antes de enamorarse"): cuerpo dinámico con rotación congelada
    /// (una caja que vuelca no se lee), <see cref="masa"/> y <see cref="frenado"/>
    /// (linearDamping) como diales — el frenado alto corta el deslizamiento en
    /// cuanto dejas de empujar (determinismo/legibilidad sobre inercia).
    ///
    /// Al clavarse conserva la posición EXACTA en la que estaba, incluso en el
    /// aire (coherente con el lenguaje de la congelación; si en el juego real
    /// se abusa, se limita ahí). Volver al mundo empujable la libera hacia la
    /// gravedad normal. No combinar con WorldExclusivePresence/PlatformDual:
    /// su gracia es existir en ambos mundos con reglas distintas.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class CajaEmpujable : MonoBehaviour
    {
        [Header("Presencia asimétrica")]
        [Tooltip("Mundo en el que la caja es dinámica y se empuja; en el otro queda clavada (estática).")]
        [SerializeField] private WorldState mundoEmpujable = WorldState.Simulation;
        [Tooltip("Tinte mientras está clavada (oxidada: la versión vieja de sí misma).")]
        [SerializeField] private Color tintClavada = new Color(0.95f, 0.40f, 0.22f);

        [Header("Física del empuje (diales del prototipo)")]
        [SerializeField, Min(0.1f)] private float masa = 2f;
        [Tooltip("linearDamping: alto = la caja se planta al soltarla (legible), bajo = patina.")]
        [SerializeField, Min(0f)] private float frenado = 3f;

        private Rigidbody2D rb;
        private SpriteRenderer sprite;
        private Color colorOriginal;
        private Vector2 posInicial;
        private bool clavada;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.mass = masa;
            rb.linearDamping = frenado;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            sprite = GetComponent<SpriteRenderer>();
            colorOriginal = sprite.color;
        }

        private void OnEnable() => WorldManager.OnWorldChanged += OnWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= OnWorldChanged;

        private void Start() => posInicial = rb.position;

        /// <summary>
        /// Vuelve a la posición de arranque, quieta. Convención de FASE CERO
        /// del grabador/replay de pasadas.
        /// </summary>
        public void ReiniciarFase()
        {
            transform.position = posInicial;
            rb.position = posInicial;
            if (!clavada) rb.linearVelocity = Vector2.zero;
        }

        private void OnWorldChanged(WorldState nuevo)
        {
            // También sincroniza el estado inicial (OnWorldChanged dispara en Start).
            clavada = nuevo != mundoEmpujable;
            // Static zerea la velocidad: al liberarla no arrastra inercia vieja.
            rb.bodyType = clavada ? RigidbodyType2D.Static : RigidbodyType2D.Dynamic;
            sprite.color = clavada ? tintClavada : colorOriginal;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.40f, 0.22f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            var box = GetComponent<BoxCollider2D>();
            if (box != null) Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
