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
    ///
    /// Variante LETAL (prototipo 2026-07-12): con <see cref="letal"/> activo,
    /// TOCAR el haz mata por contacto (como tocar a un guardia) — el barrido
    /// pasa de "zona de no-cambio" a pared móvil letal. El contacto se evalúa
    /// con el jugador como CUERPO de radio <see cref="radioJugador"/>, no como
    /// punto (lección del Corrector Vigilante: cerca del ojo el cono es más
    /// fino que el cuerpo). El haz letal debe usar M_FocoLetal (franjas de
    /// peligro incandescentes) para leerse a primera vista.
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

        [Header("Letalidad (prototipo)")]
        [Tooltip("Si está activo, TOCAR el haz mata por contacto. Usar M_FocoLetal en el haz.")]
        [SerializeField] private bool letal = false;
        [Tooltip("Solo en modo letal: el jugador cuenta como cuerpo de este radio (si el haz toca el cuerpo, mata).")]
        [SerializeField, Min(0f)] private float radioJugador = 0.5f;

        [Header("Oclusión (línea de visión)")]
        [Tooltip("Muros que tapan la mirada del foco: con uno entre el ojo y el jugador, el haz no lo toca (detección y letalidad) y el DIBUJO se recorta en el muro (sombra 1D). Default: layer Ground.")]
        [SerializeField] private LayerMask paredes = 1 << 6;

        [Header("Visual (hijos; se cablean al construir)")]
        [SerializeField] private SpriteRenderer haz;
        [SerializeField] private SpriteRenderer ojo;

        [Header("Narrativa")]
        [SerializeField, TextArea] private string mensajeDeteccion =
            "[TEXTO PENDIENTE: Keplin registra una anomalía bajo el foco — tono administrativo, sin amenazar]";

        private static readonly int AnguloID = Shader.PropertyToID("_Angulo");
        private static readonly int AperturaID = Shader.PropertyToID("_Apertura");
        private static readonly int QuadSizeID = Shader.PropertyToID("_QuadSize");
        private static readonly int SombraTexID = Shader.PropertyToID("_SombraTex");

        // Sombra 1D (oclusión VISUAL): un abanico de raycasts a lo ancho del cono
        // escribe la distancia máxima de la mirada por ángulo; el shader recorta
        // el dibujo justo en el muro — el visual coincide con la detección.
        private const int SombraMuestras = 64;
        private Texture2D sombraTex;
        private byte[] sombraBuf;

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

                sombraTex = new Texture2D(SombraMuestras, 1, TextureFormat.R8, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                sombraBuf = new byte[SombraMuestras];
                mpb.SetTexture(SombraTexID, sombraTex);

                haz.SetPropertyBlock(mpb);
            }
        }

        private void OnDestroy()
        {
            if (sombraTex != null) Destroy(sombraTex);
        }

        /// <summary>
        /// Reinicia el péndulo (fase 0 = haz recto abajo, empezando hacia la
        /// derecha). Convención de FASE CERO del grabador/replay de pasadas:
        /// grabación y reproducción parten de focos idénticos.
        /// </summary>
        public void ReiniciarFase()
        {
            fase = 0f;
            anguloActual = 0f;
        }

        private void Update()
        {
            // Péndulo determinista; tiempo escalado (se congela en pausa).
            fase += Time.deltaTime * (Mathf.PI * 2f / periodo);
            anguloActual = barridoMaxDeg * Mathf.Deg2Rad * Mathf.Sin(fase);

            if (haz != null && mpb != null)
            {
                ActualizarSombra();
                mpb.SetFloat(AnguloID, anguloActual);
                haz.SetPropertyBlock(mpb);
            }

            // Variante letal: tocar el haz = muerte por contacto.
            if (letal && PlayerTocaHaz())
                GameSession.Instance?.RespawnPlayer();
        }

        /// <summary>
        /// Abanico de raycasts a lo ancho del cono actual → distancia máxima de
        /// la mirada por ángulo, normalizada a largo, en la textura de sombra.
        /// De paso el haz muere en el suelo en vez de atravesarlo.
        /// </summary>
        private void ActualizarSombra()
        {
            if (sombraTex == null) return;

            float apertura = aperturaMediaDeg * Mathf.Deg2Rad;
            Vector2 origen = transform.position;
            for (int i = 0; i < SombraMuestras; i++)
            {
                float ang = anguloActual - apertura + 2f * apertura * i / (SombraMuestras - 1);
                var dir = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
                var hit = Physics2D.Raycast(origen, dir, largo, paredes);
                float d = hit.collider != null ? hit.distance : largo;
                sombraBuf[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(d / largo * 255f), 0, 255);
            }
            sombraTex.SetPixelData(sombraBuf, 0);
            sombraTex.Apply(false);
        }

        /// <summary>
        /// Contacto letal: el jugador como CUERPO de radio radioJugador contra
        /// la misma geometría del shader — el borde del haz toca el borde del
        /// cuerpo = muerte. (La comprobación puntual de PlayerEnHaz dejaría un
        /// punto ciego cerca del ojo, donde el cono es más fino que el cuerpo.)
        /// </summary>
        private bool PlayerTocaHaz()
        {
            if (player == null) return false;
            Vector2 delta = (Vector2)player.position - (Vector2)transform.position;

            // Eje del haz: anguloActual desde la vertical hacia abajo (+ = derecha).
            Vector2 eje = new Vector2(Mathf.Sin(anguloActual), -Mathf.Cos(anguloActual));
            float alFrente = Vector2.Dot(delta, eje);
            if (alFrente <= 0f || alFrente > largo) return false;

            float perp = Mathf.Abs(delta.x * -eje.y + delta.y * eje.x); // |cruz| = dist al eje
            float semiAncho = Mathf.Tan(aperturaMediaDeg * Mathf.Deg2Rad) * alFrente + radioJugador;
            if (perp > semiAncho) return false;

            return SinMuroHastaElJugador();
        }

        /// <summary>Misma geometría que el shader; margen 0.9 a favor del jugador.
        /// Oclusión: un muro entre el ojo y el jugador lo cubre (la mirada no
        /// atraviesa paredes — decisión del autor 2026-07-12).</summary>
        private bool PlayerEnHaz()
        {
            if (player == null) return false;
            float dx = player.position.x - transform.position.x;
            float dyAbajo = transform.position.y - player.position.y;
            if (dyAbajo <= 0f) return false; // por encima del foco no hay haz

            float dist = Mathf.Sqrt(dx * dx + dyAbajo * dyAbajo);
            if (dist > largo) return false;

            float ang = Mathf.Atan2(dx, dyAbajo);
            if (Mathf.Abs(ang - anguloActual) > aperturaMediaDeg * Mathf.Deg2Rad * 0.9f) return false;

            return SinMuroHastaElJugador();
        }

        /// <summary>Línea de visión ojo→jugador contra el layer de muros.</summary>
        private bool SinMuroHastaElJugador() =>
            Physics2D.Linecast(transform.position, player.position, paredes).collider == null;

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
