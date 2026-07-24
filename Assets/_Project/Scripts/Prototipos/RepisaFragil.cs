using System.Collections;
using UnityEngine;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): repisa que CEDE poco después
    /// de ser pisada. Es peligro FÍSICO indiferente (no del sistema): se telegrafía
    /// con TEMBLOR + oscurecimiento tipo polvo, nunca con un color de estado
    /// (magenta/rojo son del sistema — Pilar 3). Al ceder deja de ser sólida y el
    /// jugador cae; se reconstruye tras unos segundos para que el respawn encuentre
    /// el nivel intacto.
    ///
    /// Debe estar en la capa "Ground" (capa 6) para que el groundCheck del jugador
    /// la detecte y para que colisione: así se puede saltar desde ella y pisarla.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class RepisaFragil : MonoBehaviour
    {
        [Tooltip("Segundos que aguanta tras ser pisada antes de caer (ventana telegrafiada).")]
        [SerializeField, Min(0f)] private float retardoCae = 0.6f;

        [Tooltip("Segundos hasta reconstruirse (para que el respawn la encuentre entera).")]
        [SerializeField, Min(0.1f)] private float retardoReaparece = 3f;

        [Tooltip("Amplitud del temblor de aviso (unidades de mundo).")]
        [SerializeField, Min(0f)] private float temblor = 0.04f;

        private BoxCollider2D col;
        private SpriteRenderer sr;
        private Vector3 posOriginal;
        private Color colorOriginal;
        private Coroutine rutina;

        private void Awake()
        {
            col = GetComponent<BoxCollider2D>();
            sr = GetComponent<SpriteRenderer>();
            posOriginal = transform.localPosition;
            colorOriginal = sr.color;
        }

        private void OnCollisionEnter2D(Collision2D c)
        {
            if (rutina != null) return;
            var pc = c.collider.GetComponentInParent<PlayerController>();
            if (pc == null) return;
            // Solo si el jugador la PISA (viene desde arriba), no al golpearla de lado
            // o de cabeza desde abajo.
            if (pc.transform.position.y < transform.position.y) return;
            rutina = StartCoroutine(Ceder());
        }

        private IEnumerator Ceder()
        {
            // FASE 1 — aviso: temblor + oscurecimiento (polvo cayendo), sin color de estado.
            float t = 0f;
            while (t < retardoCae)
            {
                t += Time.deltaTime;
                Vector2 j = Random.insideUnitCircle * temblor;
                transform.localPosition = posOriginal + new Vector3(j.x, j.y, 0f);
                float k = Mathf.PingPong(t * 9f, 1f) * 0.4f;
                sr.color = new Color(colorOriginal.r * (1f - k), colorOriginal.g * (1f - k),
                                     colorOriginal.b * (1f - k), colorOriginal.a);
                yield return null;
            }

            // FASE 2 — cede: deja de ser sólida y desaparece; el jugador cae.
            transform.localPosition = posOriginal;
            col.enabled = false;
            sr.enabled = false;

            // FASE 3 — reconstrucción (para que el respawn no encuentre un hueco).
            yield return new WaitForSeconds(retardoReaparece);
            sr.color = colorOriginal;
            sr.enabled = true;
            col.enabled = true;
            rutina = null;
        }
    }
}
