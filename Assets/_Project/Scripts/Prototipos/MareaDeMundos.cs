using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Marea de mundos (idea #12): zona donde el mundo alterna SOLO, con un
    /// reloj compartido (patrón de SueloGlicheadoParpadeante: fase desde
    /// Time.time contra un origen, no corrutinas — zonas con las mismas
    /// duraciones van sincronizadas; el desfase crea sectores a contratiempo).
    /// El jugador no controla el cambio dentro (DisableSwitch): lo surfea.
    /// Lectura narrativa: Keplin reorganizando el sector en vivo.
    ///
    /// El RELOJ corre siempre y el overlay se ve siempre (Pilar 3: la marea se
    /// LEE desde fuera y se decide cuándo entrar), pero el forzado solo aplica
    /// con el jugador DENTRO — la marea no secuestra el resto del nivel.
    /// Preaviso audiovisual antes de cada vuelco: flicker del color del mundo
    /// ENTRANTE con frecuencia creciente 4→14 Hz (lenguaje ya validado) +
    /// tick acelerando (solo con el jugador dentro; misma excepción de
    /// AudioSource local que el suelo parpadeante, solo laboratorio).
    ///
    /// Pegamento puro sobre la API pública de WorldManager (ForceWorld /
    /// DisableSwitch / RestoreSwitchEnabled), con la robustez de ZonaDeCambio:
    /// flash de "cambio denegado" (OnSwitchDenied) y liberación manual de la
    /// regla si el respawn saca al jugador sin OnTriggerExit2D. Misma
    /// limitación conocida: no solapar zonas (se pisan el estado guardado).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class MareaDeMundos : MonoBehaviour
    {
        [Header("Ritmo (reloj compartido: mismas duraciones = zonas sincronizadas)")]
        [Tooltip("Segundos que dura cada mundo antes de volcar al otro.")]
        [SerializeField, Min(1f)] private float duracionPorMundo = 3.5f;
        [Tooltip("Últimos segundos de cada fase con flicker+ticks acelerando (Pilar 3).")]
        [SerializeField, Min(0.2f)] private float duracionPreaviso = 1.2f;
        [Tooltip("Desplazamiento del ciclo en segundos (sectores a contratiempo).")]
        [SerializeField, Min(0f)] private float desfase = 0f;
        [Tooltip("Mundo de la primera mitad del ciclo.")]
        [SerializeField] private WorldState mundoFaseA = WorldState.Simulation;

        [Header("Visual (overlay siempre visible: la marea se lee desde fuera)")]
        [SerializeField] private Color tintSim = new Color(0.35f, 0.95f, 1f, 0.10f);
        [SerializeField] private Color tintReal = new Color(0.95f, 0.40f, 0.22f, 0.10f);
        [Tooltip("Alpha de los destellos del preaviso (color del mundo entrante).")]
        [SerializeField, Range(0f, 1f)] private float alphaFlicker = 0.22f;
        [SerializeField, Min(1f)] private float flickerHzInicial = 4f;
        [SerializeField, Min(1f)] private float flickerHzFinal = 14f;
        [Tooltip("Tinte del flash cuando se intenta cambiar dentro (la marea manda).")]
        [SerializeField] private Color tintDenegado = new Color(1f, 0.25f, 0.25f, 0.45f);
        [SerializeField, Min(0.05f)] private float duracionFlash = 0.3f;

        [Header("Audio del preaviso (tick por flanco; solo con el jugador dentro)")]
        [Tooltip("Clip corto para el tick. Vacío = preaviso mudo.")]
        [SerializeField] private AudioClip tickClip;
        [SerializeField, Range(0f, 1f)] private float tickVolumen = 0.3f;
        [SerializeField, Range(0.5f, 3f)] private float tickPitch = 1.4f;

        private BoxCollider2D zona;
        private SpriteRenderer overlay;
        private AudioSource tickSource;
        private Transform player;

        private float origen;             // t=0 del reloj (ReiniciarFase lo re-ancla)
        private float acumuladorFlicker;  // integra la frecuencia frame a frame
        private bool flickerEncendido;
        private float flashRestante;
        private bool jugadorDentro;
        private bool cambioEstabaHabilitado;

        private float Periodo => duracionPorMundo * 2f;

        private void Awake()
        {
            zona = GetComponent<BoxCollider2D>();
            zona.isTrigger = true;
            overlay = GetComponent<SpriteRenderer>();

            if (tickClip != null)
            {
                tickSource = gameObject.AddComponent<AudioSource>();
                tickSource.playOnAwake = false;
                tickSource.spatialBlend = 0f;
                tickSource.volume = tickVolumen;
            }

            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;

            origen = Time.time;
        }

        private void OnEnable()
        {
            WorldManager.OnSwitchDenied += OnSwitchDenied;
            GameSession.OnPlayerRespawned += OnPlayerRespawned;
        }

        private void OnDisable()
        {
            WorldManager.OnSwitchDenied -= OnSwitchDenied;
            GameSession.OnPlayerRespawned -= OnPlayerRespawned;
            // Zona apagada/destruida con el jugador dentro: no dejar el cambio bloqueado.
            if (jugadorDentro) Salir();
        }

        /// <summary>
        /// Re-ancla el reloj: el ciclo vuelve a empezar en la fase A (más su
        /// desfase). Convención de FASE CERO del grabador/replay de pasadas.
        /// </summary>
        public void ReiniciarFase()
        {
            origen = Time.time;
            acumuladorFlicker = 0f;
            flickerEncendido = false;
        }

        private void Update()
        {
            float t = Mathf.Repeat(Time.time - origen - desfase, Periodo);
            bool faseA = t < duracionPorMundo;
            WorldState mundoMarea = faseA ? mundoFaseA : Otro(mundoFaseA);
            float restante = (faseA ? duracionPorMundo : Periodo) - t;
            bool preaviso = restante <= duracionPreaviso;

            // El forzado solo aplica con el jugador dentro. Entrar a mitad de
            // fase también sincroniza aquí (primer frame dentro con mundo ≠ marea).
            if (jugadorDentro && WorldManager.Instance != null &&
                WorldManager.Instance.CurrentWorld != mundoMarea)
                WorldManager.Instance.ForceWorld(mundoMarea);

            // ---- Overlay: tinte del mundo que la marea sostiene AHORA ----
            Color c = mundoMarea == WorldState.Simulation ? tintSim : tintReal;

            if (preaviso)
            {
                // k va de 0 a 1 en el preaviso; el flicker integra la frecuencia
                // con acumulador (sube limpia, sin artefactos de barrido).
                float k = 1f - restante / duracionPreaviso;
                float hz = Mathf.Lerp(flickerHzInicial, flickerHzFinal, k);
                acumuladorFlicker += Time.deltaTime * hz;
                bool encendido = Mathf.Repeat(acumuladorFlicker, 1f) < 0.5f;

                if (encendido && !flickerEncendido && jugadorDentro && tickSource != null)
                {
                    tickSource.pitch = tickPitch * (1f + 0.25f * k);
                    tickSource.PlayOneShot(tickClip, tickVolumen);
                }
                flickerEncendido = encendido;

                if (encendido)
                {
                    // Destello del color del mundo ENTRANTE: se ve QUÉ viene.
                    c = mundoMarea == WorldState.Simulation ? tintReal : tintSim;
                    c.a = alphaFlicker;
                }
            }
            else
            {
                acumuladorFlicker = 0f;
                flickerEncendido = false;
            }

            if (flashRestante > 0f)
            {
                flashRestante -= Time.deltaTime;
                c = Color.Lerp(c, tintDenegado, Mathf.Clamp01(flashRestante / duracionFlash));
            }

            overlay.color = c;
        }

        private static WorldState Otro(WorldState w) =>
            w == WorldState.Simulation ? WorldState.Real : WorldState.Simulation;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (jugadorDentro || other.GetComponentInParent<PlayerController>() == null) return;
            jugadorDentro = true;
            if (WorldManager.Instance != null)
            {
                cambioEstabaHabilitado = WorldManager.Instance.IsSwitchEnabled;
                WorldManager.Instance.DisableSwitch();
            }
            // El vuelco al mundo de la marea lo hace el próximo Update (misma frame).
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!jugadorDentro || other.GetComponentInParent<PlayerController>() == null) return;
            Salir();
        }

        private void Salir()
        {
            jugadorDentro = false;
            if (WorldManager.Instance != null)
                WorldManager.Instance.RestoreSwitchEnabled(cambioEstabaHabilitado);
            // El mundo queda como la marea lo dejó: fuera de la zona vuelve a ser tuyo.
        }

        /// <summary>
        /// Cinturón y tirantes del respawn (lección de ZonaDeCambio): el
        /// teletransporte puede no disparar OnTriggerExit2D. Si tras reaparecer
        /// el jugador ya no está dentro, se libera la regla a mano.
        /// </summary>
        private void OnPlayerRespawned()
        {
            if (!jugadorDentro || player == null) return;
            if (!zona.OverlapPoint(player.position)) Salir();
        }

        private void OnSwitchDenied()
        {
            // Solo la zona que está causando el bloqueo da el feedback.
            if (!jugadorDentro) return;
            flashRestante = duracionFlash;
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
