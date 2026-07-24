using UnityEngine;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): chorro/cascada que empuja (T6,
    /// ideas.md §Trampas). Volumen de fuerza LATERAL que descabalga al jugador de las
    /// cornisas: mientras está dentro, lo desplaza en <see cref="direccion"/>. Se cruza
    /// con IMPULSO (cruzar rápido/decidido) o se DESVÍA metiendo un <see cref="Penasco"/>
    /// delante de la boca (lo empujas con el jugador) → el chorro se apaga.
    ///
    /// El empuje se aplica por posición (rb.position), NO por velocidad: PlayerController
    /// sobrescribe velocity.x cada FixedUpdate, así que una fuerza por velocidad se
    /// perdería. El volumen VISIBLE = el volumen de fuerza (Pilar 3: el peligro se lee).
    ///
    /// [PENDIENTE]: arte/shader de la corriente (hoy un rect unlit que pulsa); SFX del chorro.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class ChorroEmpuje : MonoBehaviour
    {
        [Header("Empuje")]
        [Tooltip("Dirección del empuje (se normaliza).")]
        [SerializeField] private Vector2 direccion = Vector2.right;
        [Tooltip("Velocidad de deriva que impone al jugador dentro (u/s).")]
        [SerializeField, Min(0f)] private float fuerza = 5f;

        [Header("Boca del chorro (donde se mete el peñasco para taparlo)")]
        [Tooltip("Centro de la zona de bloqueo, relativo al centro del volumen (unidades de mundo).")]
        [SerializeField] private Vector2 bocaOffset = new Vector2(-5.5f, -0.6f);
        [SerializeField] private Vector2 bocaSize = new Vector2(2f, 2f);

        [Header("Visual")]
        [SerializeField] private Color colorChorro = new Color(0.8f, 0.9f, 1f, 0.35f);

        private SpriteRenderer sr;
        private bool bloqueado;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            GetComponent<BoxCollider2D>().isTrigger = true;
            sr.color = colorChorro;
        }

        private void FixedUpdate()
        {
            // ¿un peñasco tapa la boca? -> chorro apagado (se decide antes de los triggers).
            Vector2 c = (Vector2)transform.position + bocaOffset;
            var hits = Physics2D.OverlapBoxAll(c, bocaSize, 0f);
            bloqueado = false;
            foreach (var h in hits)
                if (h.GetComponentInParent<Penasco>() != null) { bloqueado = true; break; }

            if (sr == null) return;
            if (bloqueado)
            {
                sr.enabled = false;
            }
            else
            {
                sr.enabled = true;
                var col = colorChorro;
                col.a = colorChorro.a * (0.7f + 0.3f * Mathf.Sin(Time.time * 12f)); // "flujo"
                sr.color = col;
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (bloqueado) return;
            var pc = other.GetComponentInParent<PlayerController>();
            if (pc == null) return;
            var rb = pc.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position += direccion.normalized * fuerza * Time.fixedDeltaTime;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireCube((Vector2)transform.position + bocaOffset, bocaSize);
        }
    }
}
