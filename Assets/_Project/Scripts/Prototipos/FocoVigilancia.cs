using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Narrative;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/): Foco de vigilancia de Keplin —
    /// variante "searchlight" de la Zona Vigilada. Un haz cónico barre en
    /// péndulo (determinista, se frena en los extremos por el seno) y
    /// **SOLO el haz detecta**: vigilancia ESPACIAL, no temporal.
    /// La regla sigue siendo una: estar iluminado no castiga; CAMBIAR DE MUNDO
    /// dentro del haz = anomalía → respawn. Sondear no es anomalía.
    /// El área de detección usa la MISMA geometría que dibuja el shader
    /// (FocoVigilanciaHaz) — el visual nunca miente (Pilar 3).
    /// </summary>
    public class FocoVigilancia : MonoBehaviour
    {
        [Header("Haz (determinista)")]
        [SerializeField, Min(1f)] private float largo = 8f;
        [Tooltip("Semi-apertura del cono, en grados.")]
        [SerializeField, Range(3f, 40f)] private float aperturaMediaDeg = 11f;
        [Tooltip("Ángulo máximo del péndulo a cada lado de la vertical, en grados.")]
        [SerializeField, Range(5f, 75f)] private float barridoMaxDeg = 32f;
        [Tooltip("Segundos de un ciclo completo del péndulo (ida y vuelta).")]
        [SerializeField, Min(0.5f)] private float periodo = 4f;

        [Header("Visual (hijos; se cablean al construir)")]
        [SerializeField] private SpriteRenderer haz;
        [SerializeField] private SpriteRenderer ojo;

        [Header("Narrativa")]
        [SerializeField, TextArea] private string mensajeDeteccion =
            "[TEXTO PENDIENTE: Keplin registra una anomalía bajo el foco — tono administrativo, sin amenazar]";

        private static readonly int AnguloID = Shader.PropertyToID("_Angulo");
        private static readonly int AperturaID = Shader.PropertyToID("_Apertura");
        private static readonly int QuadSizeID = Shader.PropertyToID("_QuadSize");

        private MaterialPropertyBlock mpb;
        private Transform player;
        private float fase;
        private float anguloActual; // radianes; 0 = recto abajo, + = derecha
        private bool avisoDado;

        private void OnEnable() => WorldManager.OnWorldChanged += OnWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= OnWorldChanged;

        private void Start()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;

            if (ojo != null) ojo.color = new Color(1f, 0.25f, 0.2f, 1f); // siempre activo

            if (haz != null)
            {
                // Dimensionar el quad para contener el barrido completo.
                float aperturaRad = aperturaMediaDeg * Mathf.Deg2Rad;
                float barridoRad = barridoMaxDeg * Mathf.Deg2Rad;
                float ancho = 2f * largo * Mathf.Sin(barridoRad + aperturaRad) + 0.6f;
                haz.transform.localScale = new Vector3(ancho, largo, 1f);
                haz.transform.localPosition = new Vector3(0f, -largo * 0.5f, 0f);

                mpb = new MaterialPropertyBlock();
                haz.GetPropertyBlock(mpb);
                mpb.SetFloat(AperturaID, aperturaRad);
                mpb.SetVector(QuadSizeID, new Vector4(ancho, largo, 0f, 0f));
                haz.SetPropertyBlock(mpb);
            }
        }

        private void Update()
        {
            // Péndulo determinista; tiempo escalado (se congela en pausa).
            fase += Time.deltaTime * (Mathf.PI * 2f / periodo);
            anguloActual = barridoMaxDeg * Mathf.Deg2Rad * Mathf.Sin(fase);

            if (haz != null && mpb != null)
            {
                mpb.SetFloat(AnguloID, anguloActual);
                haz.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Misma geometría que el shader; margen 0.9 a favor del jugador.</summary>
        private bool PlayerEnHaz()
        {
            if (player == null) return false;
            float dx = player.position.x - transform.position.x;
            float dyAbajo = transform.position.y - player.position.y;
            if (dyAbajo <= 0f) return false; // por encima del foco no hay haz

            float dist = Mathf.Sqrt(dx * dx + dyAbajo * dyAbajo);
            if (dist > largo) return false;

            float ang = Mathf.Atan2(dx, dyAbajo);
            return Mathf.Abs(ang - anguloActual) <= aperturaMediaDeg * Mathf.Deg2Rad * 0.9f;
        }

        private void OnWorldChanged(WorldState _)
        {
            if (Time.timeSinceLevelLoad < 0.5f) return; // sync inicial del WorldManager
            if (!PlayerEnHaz()) return;

            if (!avisoDado)
            {
                avisoDado = true;
                KeplinMessageController.Instance?.ShowMessage(mensajeDeteccion);
            }
            GameSession.Instance?.RespawnPlayer();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.7f);
            float barridoRad = barridoMaxDeg * Mathf.Deg2Rad;
            Vector3 o = transform.position;
            Vector3 izq = o + new Vector3(Mathf.Sin(-barridoRad), -Mathf.Cos(-barridoRad), 0f) * largo;
            Vector3 der = o + new Vector3(Mathf.Sin(barridoRad), -Mathf.Cos(barridoRad), 0f) * largo;
            Gizmos.DrawLine(o, izq);
            Gizmos.DrawLine(o, der);
            Gizmos.DrawLine(o, o + Vector3.down * largo);
        }
    }
}
