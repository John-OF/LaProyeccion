using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Narrative;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/ — fuera de ALCANCE v1.1; si se valida,
    /// exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Zona vigilada por Keplin: regla ÚNICA — estar dentro no castiga; CAMBIAR
    /// DE MUNDO dentro mientras el ojo vigila → anomalía detectada → respawn.
    /// Ciclo determinista y legible (Pilar 3): VIGILA (rojo) → DESCANSA (gris)
    /// → PREAVISO (parpadeo rápido) → VIGILA...  El jugador resuelve esperando
    /// la ventana o buscando puntos ciegos fuera de la región.
    /// Sondear (Q) NO es anomalía: solo el cambio de mundo.
    ///
    /// Variante LETAL (prototipo 2026-07-12): con <see cref="letal"/> activo,
    /// TOCAR la zona mientras VIGILA mata por contacto (como un guardia) — la
    /// ventana gris pasa de "momento seguro para cambiar" a paso OBLIGATORIO.
    /// Preaviso y descanso nunca matan (el telegrafiado es sagrado, Pilar 3).
    /// El overlay letal debe usar M_ZonaLetal (franjas de peligro incandescentes)
    /// para que la variante se lea a primera vista.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ZonaVigilada : MonoBehaviour
    {
        [Header("Ciclo (determinista)")]
        [SerializeField, Min(0.5f)] private float vigilaTiempo = 3f;
        [SerializeField, Min(0.5f)] private float descansaTiempo = 2.5f;
        [SerializeField, Min(0.1f)] private float preavisoTiempo = 0.7f;

        [Header("Letalidad (prototipo)")]
        [Tooltip("Si está activo, TOCAR la zona mientras VIGILA mata por contacto. Preaviso y descanso nunca matan. Usar M_ZonaLetal en el overlay.")]
        [SerializeField] private bool letal = false;

        [Header("Visual (hijos; se cablean al construir)")]
        [SerializeField] private SpriteRenderer overlay;
        [SerializeField] private SpriteRenderer ojo;

        [Header("Narrativa")]
        [SerializeField, TextArea] private string mensajeDeteccion =
            "[TEXTO PENDIENTE: Keplin registra una anomalía en zona vigilada — tono administrativo, sin amenazar]";

        private enum Fase { Vigila, Descansa, Preaviso }
        private Fase fase;
        private float faseRestante;
        private bool playerInside;
        private bool avisoDado;

        // Soporte del shader ZonaVigiladaOverlay (si el material del overlay lo usa):
        // el estado se inyecta por MaterialPropertyBlock (una instancia por zona,
        // sin duplicar materiales). Fallback: tinte plano de color (comportamiento previo).
        private static readonly int EstadoID = Shader.PropertyToID("_Estado");
        private static readonly int ZoneSizeID = Shader.PropertyToID("_ZoneSize");
        private MaterialPropertyBlock mpb;
        private bool usaShader;
        private float estadoActual;

        private static readonly Color OjoVigila = new Color(1f, 0.25f, 0.2f, 1f);
        private static readonly Color OjoDescansa = new Color(0.35f, 0.38f, 0.42f, 0.8f);
        private static readonly Color OverlayVigila = new Color(1f, 0.2f, 0.15f, 0.13f);
        private static readonly Color OverlayDescansa = new Color(1f, 1f, 1f, 0.02f);

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnEnable()
        {
            WorldManager.OnWorldChanged += OnWorldChanged;
        }

        private void OnDisable()
        {
            WorldManager.OnWorldChanged -= OnWorldChanged;
        }

        private void Start()
        {
            // Shader del overlay disponible?
            if (overlay != null && overlay.sharedMaterial != null && overlay.sharedMaterial.HasProperty(EstadoID))
            {
                usaShader = true;
                mpb = new MaterialPropertyBlock();
                overlay.GetPropertyBlock(mpb);
                var box = GetComponent<BoxCollider2D>();
                mpb.SetVector(ZoneSizeID, new Vector4(box.size.x, box.size.y, 0f, 0f));
                overlay.SetPropertyBlock(mpb);
            }

            // Arranca vigilando: el jugador llega, lo ve rojo y lee la regla.
            SetFase(Fase.Vigila);
            estadoActual = 1f;
        }

        private void Update()
        {
            faseRestante -= Time.deltaTime; // escalado: respeta la pausa
            if (faseRestante <= 0f)
            {
                switch (fase)
                {
                    case Fase.Vigila: SetFase(Fase.Descansa); break;
                    case Fase.Descansa: SetFase(Fase.Preaviso); break;
                    case Fase.Preaviso: SetFase(Fase.Vigila); break;
                }
            }
            ApplyVisual();

            // Variante letal: tocar la luz mientras vigila = muerte por contacto.
            if (letal && fase == Fase.Vigila && playerInside)
                GameSession.Instance?.RespawnPlayer();
        }

        private void SetFase(Fase f)
        {
            fase = f;
            faseRestante = f == Fase.Vigila ? vigilaTiempo
                         : f == Fase.Descansa ? descansaTiempo
                         : preavisoTiempo;
        }

        private void ApplyVisual()
        {
            bool rojo;
            switch (fase)
            {
                case Fase.Vigila: rojo = true; break;
                case Fase.Preaviso:
                    // Parpadeo rápido: anticipa que el ojo va a abrirse.
                    rojo = Mathf.PingPong(Time.time * 8f, 1f) > 0.5f;
                    break;
                default: rojo = false; break;
            }
            if (ojo != null) ojo.color = rojo ? OjoVigila : OjoDescansa;

            if (usaShader)
            {
                // Transición suavizada hacia el estado objetivo: el preaviso (parpadeo
                // a ~8 Hz) produce pulsos parciales — se lee como "calentando".
                estadoActual = Mathf.MoveTowards(estadoActual, rojo ? 1f : 0f, Time.deltaTime * 10f);
                mpb.SetFloat(EstadoID, estadoActual);
                overlay.SetPropertyBlock(mpb);
            }
            else if (overlay != null)
            {
                overlay.color = rojo ? OverlayVigila : OverlayDescansa;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null) playerInside = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null) playerInside = false;
        }

        private void OnWorldChanged(WorldState _)
        {
            // El OnWorldChanged inicial del Start() del WorldManager no cuenta.
            if (Time.timeSinceLevelLoad < 0.5f) return;
            if (fase != Fase.Vigila || !playerInside) return;

            // Anomalía detectada.
            if (!avisoDado)
            {
                avisoDado = true;
                KeplinMessageController.Instance?.ShowMessage(mensajeDeteccion);
            }
            GameSession.Instance?.RespawnPlayer();
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
