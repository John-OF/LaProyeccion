using System.Collections;
using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): estalactita por vibración (T1,
    /// ideas.md §Trampas). Cuelga sobre un pasillo y se suelta cuando algo entra en la
    /// zona de bajo ella: el JUGADOR (reflejo: cruzar con el tiempo justo) o una PIEDRA
    /// lanzada (la vibración la provoca desde lejos → decisión: gastar piedra y cruzar
    /// seguro, o arriesgar el cruce).
    ///
    /// Peligro FÍSICO indiferente (no del sistema): se telegrafía con TEMBLOR antes de
    /// caer (Pilar 3), nunca con color de estado. Al caer, es letal al jugador (corrección)
    /// y se rompe contra el suelo; reconstruye tras unos segundos para que el respawn la
    /// encuentre entera.
    ///
    /// [PENDIENTE]: en la oscuridad de la cueva el temblor apenas se ve — hace falta un
    /// SFX de goteo/crujido como aviso audible (no hay clip aún).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class Estalactita : MonoBehaviour
    {
        [Header("Zona de disparo (bajo la estalactita)")]
        [Tooltip("Centro de la zona relativo a la estalactita.")]
        [SerializeField] private Vector2 zonaOffset = new Vector2(0f, -1.9f);
        [SerializeField] private Vector2 zonaSize = new Vector2(2.5f, 2.5f);

        [Header("Telegrafía y caída")]
        [Tooltip("Aviso (temblor) antes de soltarse. Corto = 'cruzar con el tiempo justo'.")]
        [SerializeField, Min(0f)] private float retardoAviso = 0.5f;
        [SerializeField, Min(0.1f)] private float gravedadCaida = 3f;
        [Tooltip("Amplitud del temblor de aviso.")]
        [SerializeField, Min(0f)] private float temblor = 0.05f;
        [Tooltip("Segundos hasta reconstruirse (para que el respawn la encuentre entera).")]
        [SerializeField, Min(0.1f)] private float retardoReaparece = 4f;

        private Rigidbody2D rb;
        private Collider2D col;
        private SpriteRenderer sr;
        private Vector3 posOriginal;
        private bool armada = true;   // colgando, lista para dispararse
        private bool cayendo;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            sr = GetComponent<SpriteRenderer>();
            col.isTrigger = true;
            rb.bodyType = RigidbodyType2D.Static; // colgada, quieta
            posOriginal = transform.position;
        }

        private void Update()
        {
            if (!armada) return;
            Vector2 c = (Vector2)transform.position + zonaOffset;
            var hits = Physics2D.OverlapBoxAll(c, zonaSize, 0f);
            foreach (var h in hits)
            {
                bool esJugador = h.GetComponentInParent<PlayerController>() != null;
                bool esPiedra = h.GetComponent<PiedraLanzada>() != null;
                if (esJugador || esPiedra)
                {
                    armada = false;
                    StartCoroutine(AvisarYCaer());
                    break;
                }
            }
        }

        private IEnumerator AvisarYCaer()
        {
            // FASE 1 — aviso: temblor (sin color de estado, Pilar 3).
            float t = 0f;
            while (t < retardoAviso)
            {
                t += Time.deltaTime;
                Vector2 j = Random.insideUnitCircle * temblor;
                transform.position = posOriginal + new Vector3(j.x, j.y, 0f);
                yield return null;
            }
            transform.position = posOriginal;

            // FASE 2 — se suelta y cae por gravedad.
            cayendo = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = gravedadCaida;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!cayendo) return;

            if (other.GetComponentInParent<PlayerController>() != null)
            {
                AudioManager.Instance?.PlayDeath();
                GameSession.Instance?.RespawnPlayer();
                Romper();
                return;
            }

            if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                AudioManager.Instance?.PlayPiedraGolpe(); // impacto pétreo (provisional)
                Romper();
            }
        }

        private void Romper()
        {
            if (!cayendo) return;
            cayendo = false;
            StartCoroutine(Reconstruir());
        }

        private IEnumerator Reconstruir()
        {
            // Oculta e inerte durante la reconstrucción.
            sr.enabled = false;
            col.enabled = false;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
            transform.position = posOriginal;

            yield return new WaitForSeconds(retardoReaparece);

            sr.enabled = true;
            col.enabled = true;
            col.isTrigger = true;
            armada = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
            Vector2 c = (Vector2)transform.position + zonaOffset;
            Gizmos.DrawWireCube(c, zonaSize);
        }
    }
}
