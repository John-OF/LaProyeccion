using UnityEngine;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): grava suelta / barro (T8, ideas.md
    /// §Trampas). Superficie donde te HUNDES (mueves más lento) y el SALTO se RECORTA.
    /// Legible por textura (color de barro, no de estado) y sonido (squelch al pisar).
    /// Floja sola; buena compuesta con T1/T7 (presión temporal).
    ///
    /// Corre DESPUÉS de PlayerController ([DefaultExecutionOrder] alto): el controlador
    /// fija velocity.x = input*moveSpeed y el salto fija velocity.y en su propio update;
    /// aquí, ya después, se escala la velocidad horizontal y se topa la vertical mientras
    /// el jugador pisa la grava. Así el efecto no lo pisa el controlador.
    ///
    /// La superficie es SÓLIDA (piso real); el efecto se aplica a quien esté en la banda
    /// justo encima (sus pies). [PENDIENTE]: arte de la textura y SFX de squelch por paso
    /// (hoy opcional al entrar).
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class SueloGrava : MonoBehaviour
    {
        [Header("Efecto")]
        [Tooltip("Multiplica la velocidad horizontal mientras pisas la grava (hundirse).")]
        [SerializeField, Range(0.1f, 1f)] private float factorVelocidad = 0.45f;
        [Tooltip("Tope de la velocidad vertical al saltar (recorta el salto). jumpForce del jugador ≈ 21.")]
        [SerializeField, Min(0.5f)] private float saltoMax = 14f;

        [Header("Detección (banda sobre la superficie)")]
        [Tooltip("Altura de la banda de detección sobre el techo de la superficie.")]
        [SerializeField, Min(0.2f)] private float bandaAltura = 1.2f;

        [Header("Textura (barro; no color de estado)")]
        [SerializeField] private Color colorGrava = new Color(0.34f, 0.27f, 0.19f, 1f);

        [Header("Audio (opcional)")]
        [SerializeField] private AudioClip squelchClip;
        [SerializeField, Range(0f, 1f)] private float squelchVolumen = 0.4f;

        private BoxCollider2D solido;
        private SpriteRenderer sr;
        private AudioSource src;
        private bool dentroPrev;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            solido = GetComponent<BoxCollider2D>();
            solido.isTrigger = false; // superficie sólida real
            sr.color = colorGrava;
            if (squelchClip != null)
            {
                src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.volume = squelchVolumen;
            }
        }

        private void FixedUpdate()
        {
            Bounds b = solido.bounds;
            Vector2 centro = new Vector2(b.center.x, b.max.y + bandaAltura * 0.5f);
            Vector2 size = new Vector2(b.size.x, bandaAltura);

            var hits = Physics2D.OverlapBoxAll(centro, size, 0f);
            Rigidbody2D prb = null;
            foreach (var h in hits)
            {
                var pc = h.GetComponentInParent<PlayerController>();
                if (pc != null) { prb = pc.GetComponent<Rigidbody2D>(); break; }
            }
            bool dentro = prb != null;

            if (dentro)
            {
                var v = prb.linearVelocity;
                v.x *= factorVelocidad;          // te hundes: avanzas lento
                if (v.y > saltoMax) v.y = saltoMax; // recorta el salto
                prb.linearVelocity = v;
            }

            if (dentro && !dentroPrev && src != null)
                src.PlayOneShot(squelchClip, squelchVolumen); // squelch al pisar

            dentroPrev = dentro;
        }

        private void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Bounds b = box.bounds;
            Gizmos.color = new Color(0.6f, 0.45f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(new Vector3(b.center.x, b.max.y + bandaAltura * 0.5f, 0f),
                                new Vector3(b.size.x, bandaAltura, 0.1f));
        }
    }
}
