using UnityEngine;
using UnityEngine.InputSystem;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// Resultado de la última reproducción verificada. Estático y consultable
    /// desde fuera (MCP/tests): el agente reproduce una pasada y lee esto.
    /// </summary>
    public class ResultadoReplay
    {
        public string pasada;
        public bool completado;     // llegó al final de las muestras
        public bool exito;          // completado y sin desvío sobre tolerancia
        public float maxDesvio;
        public int tickPrimerDesvio = -1; // -1 = nunca superó la tolerancia
        public int ticksTotales;
        public string motivoFallo = "";

        public override string ToString() => exito
            ? $"ÉXITO — '{pasada}' reproducida ({ticksTotales} ticks), desvío máx {maxDesvio:F2} u"
            : $"FALLO — '{pasada}': {motivoFallo} (desvío máx {maxDesvio:F2} u, primer desvío en tick {tickPrimerDesvio}/{ticksTotales})";
    }

    /// <summary>
    /// PROTOTIPO (herramienta de laboratorio, idea #8 de Claude, ideas.md).
    ///
    /// Reproductor de pasadas (<see cref="PasadaGrabada"/>). Dos modos:
    ///
    /// F10 — REPLAY VERIFICADO: toma el control del jugador (seam
    /// PlayerController.InputDelegado, mismo patrón que CambioDeMundoDelegado)
    /// y reinyecta los inputs grabados tick a tick por los MISMOS caminos de
    /// código que el teclado (InyectarSalto→buffer, TrySwitchWorld,
    /// InteractuarAhora, SondearAhora) — si una mecánica se rompe, el replay
    /// diverge y lo delata. En cada tick compara la posición real contra la
    /// trayectoria grabada; si se desvía más de <see cref="tolerancia"/>, la
    /// pasada FALLA con el tick exacto de divergencia. Resultado en
    /// <see cref="UltimoResultado"/> (estático — lo lee el agente por MCP).
    /// Morir/respawnear durante el replay = fallo. F10 de nuevo = cancelar.
    ///
    /// F11 — GHOST: una silueta semitransparente recorre las POSICIONES
    /// grabadas, sin física ni inputs — referencia visual 100% fiel, convive
    /// con el juego normal. F11 de nuevo = quitarlo.
    ///
    /// El replay parte del spawn y mundo grabados, pero NO resetea el resto de
    /// la escena (switches, semillas): para verificación de regresión, cargar
    /// la escena fresca y reproducir antes de tocar nada.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class ReproductorDePasada : MonoBehaviour
    {
        [Header("Pasada")]
        [Tooltip("La pasada que disparan F10 (replay verificado) y F11 (ghost).")]
        [SerializeField] private PasadaGrabada pasadaAsignada;

        [Header("Verificación")]
        [Tooltip("Desvío máximo (u) entre la posición real y la grabada antes de declarar FALLO.")]
        [SerializeField, Min(0.05f)] private float tolerancia = 0.5f;

        public static ResultadoReplay UltimoResultado { get; private set; }
        public bool Reproduciendo { get; private set; }

        private PlayerController player;
        private PlayerInteractor interactor;
        private RadarPulseController radar;
        private Rigidbody2D rb;

        private PasadaGrabada pasada;
        private int tick;
        private float maxDesvio;
        private int tickPrimerDesvio;

        private PasadaGrabada pasadaGhost;
        private int tickGhost;
        private GameObject ghost;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            interactor = GetComponent<PlayerInteractor>();
            radar = GetComponent<RadarPulseController>();
            rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable() => GameSession.OnPlayerRespawned += OnRespawn;

        private void OnDisable()
        {
            GameSession.OnPlayerRespawned -= OnRespawn;
            if (Reproduciendo) Finalizar(false, "escena descargada durante el replay");
            QuitarGhost();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f10Key.wasPressedThisFrame)
            {
                if (Reproduciendo) Finalizar(false, "cancelado por el usuario");
                else Reproducir(pasadaAsignada);
            }
            if (kb.f11Key.wasPressedThisFrame)
            {
                if (ghost != null) QuitarGhost();
                else ReproducirGhost(pasadaAsignada);
            }
        }

        // ==================== Fase cero ====================

        /// <summary>
        /// Convención de FASE CERO: los guardias, focos y zonas vigiladas son
        /// deterministas pero ESTATEFULES — su posición/ciclo depende de cuánto
        /// lleva corriendo la escena. Una pasada grabada a mitad de sesión se
        /// bifurca contra una escena fresca (lección del primer replay real de
        /// P_Mixto: divergencia en tick 287 y muerte por ZonaVigilada). Por eso
        /// grabación Y reproducción empiezan reseteando TODOS a su estado
        /// inicial: ambas parten del mismo mundo.
        /// </summary>
        public static void SincronizarFasesDeGuardias()
        {
            foreach (var c in FindObjectsByType<Corrector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                c.ReiniciarFase();
            foreach (var c in FindObjectsByType<CorrectorVigilante>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                c.ReiniciarFase();
            foreach (var f in FindObjectsByType<FocoVigilancia>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                f.ReiniciarFase();
            foreach (var z in FindObjectsByType<ZonaVigilada>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                z.ReiniciarFase();
        }

        // ==================== Replay verificado ====================

        public bool Reproducir(PasadaGrabada p)
        {
            if (Reproduciendo) return false;
            if (p == null || p.muestras.Count == 0)
            {
                Debug.LogWarning("[ReproductorDePasada] Sin pasada asignada o pasada vacía.");
                return false;
            }
            if (p.escena != gameObject.scene.name)
            {
                Debug.LogWarning($"[ReproductorDePasada] La pasada es de '{p.escena}', no de '{gameObject.scene.name}'.");
                return false;
            }
            if (Mathf.Abs(p.fixedDeltaTime - Time.fixedDeltaTime) > 1e-4f)
                Debug.LogWarning("[ReproductorDePasada] fixedDeltaTime distinto al de la grabación: el replay puede divergir.");

            pasada = p;
            tick = 0;
            maxDesvio = 0f;
            tickPrimerDesvio = -1;
            UltimoResultado = null;

            // Estado inicial grabado. El respawn apunta al spawn de la pasada:
            // una muerte durante el replay no debe teleportar a otro lab-checkpoint.
            transform.position = p.spawn;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            WorldManager.Instance?.ForceWorld(p.mundoInicial);
            GameSession.Instance?.SetRespawnPoint(p.spawn);
            SincronizarFasesDeGuardias(); // fase cero: mismo mundo que al grabar

            player.InputDelegado = true;
            Reproduciendo = true;
            Debug.Log($"[ReproductorDePasada] REPRODUCIENDO '{p.name}' ({p.muestras.Count} ticks, {p.Duracion:F1} s) — F10 cancela.");
            return true;
        }

        private void FixedUpdate()
        {
            if (Reproduciendo) TickReplay();
            if (ghost != null) TickGhost();
        }

        private void TickReplay()
        {
            if (tick >= pasada.muestras.Count)
            {
                Finalizar(true, null);
                return;
            }

            var m = pasada.muestras[tick];

            // La posición se compara al inicio del tick, simétrico a cómo se grabó.
            // Divergencia sobre tolerancia = FALLO INMEDIATO: seguir reproduciendo
            // tras la bifurcación solo produce ruido (muertes en sitios sin sentido).
            float desvio = Vector2.Distance(transform.position, m.pos);
            if (desvio > maxDesvio) maxDesvio = desvio;
            if (desvio > tolerancia)
            {
                tickPrimerDesvio = tick;
                Finalizar(false, $"desvío {desvio:F2} u en tick {tick} (esperado {m.pos}, real {(Vector2)transform.position})");
                return;
            }

            player.InyectarMove(m.moveX);
            var b = (PasadaGrabada.Botones)m.botones;
            if ((b & PasadaGrabada.Botones.Salto) != 0) player.InyectarSalto();
            if ((b & PasadaGrabada.Botones.CambioMundo) != 0) WorldManager.Instance?.TrySwitchWorld();
            if ((b & PasadaGrabada.Botones.Interactuar) != 0) interactor?.InteractuarAhora();
            if ((b & PasadaGrabada.Botones.Sondear) != 0) radar?.SondearAhora();

            tick++;
        }

        private void OnRespawn()
        {
            if (Reproduciendo) Finalizar(false, $"muerte/respawn durante el replay (tick {tick})");
        }

        private void Finalizar(bool completado, string motivo)
        {
            player.InyectarMove(0f);
            player.InputDelegado = false;
            Reproduciendo = false;

            bool exito = completado && tickPrimerDesvio < 0;
            UltimoResultado = new ResultadoReplay
            {
                pasada = pasada != null ? pasada.name : "?",
                completado = completado,
                exito = exito,
                maxDesvio = maxDesvio,
                tickPrimerDesvio = tickPrimerDesvio,
                ticksTotales = pasada != null ? pasada.muestras.Count : 0,
                motivoFallo = exito ? "" : (motivo ?? "la trayectoria superó la tolerancia")
            };
            Debug.Log("[ReproductorDePasada] " + UltimoResultado);
            pasada = null;
        }

        // ==================== Ghost ====================

        public bool ReproducirGhost(PasadaGrabada p)
        {
            if (ghost != null) return false;
            if (p == null || p.muestras.Count == 0)
            {
                Debug.LogWarning("[ReproductorDePasada] Sin pasada asignada o pasada vacía.");
                return false;
            }

            var srPlayer = GetComponentInChildren<SpriteRenderer>();
            pasadaGhost = p;
            tickGhost = 0;
            ghost = new GameObject("Ghost_Pasada");
            var sr = ghost.AddComponent<SpriteRenderer>();
            if (srPlayer != null)
            {
                sr.sprite = srPlayer.sprite;
                sr.sortingLayerID = srPlayer.sortingLayerID;
                sr.sortingOrder = srPlayer.sortingOrder - 1;
                ghost.transform.localScale = srPlayer.transform.lossyScale;
            }
            sr.color = new Color(0.5f, 0.9f, 1f, 0.45f);
            ghost.transform.position = p.spawn;
            return true;
        }

        private void TickGhost()
        {
            if (tickGhost >= pasadaGhost.muestras.Count)
            {
                QuitarGhost();
                return;
            }
            ghost.transform.position = pasadaGhost.muestras[tickGhost].pos;
            tickGhost++;
        }

        private void QuitarGhost()
        {
            if (ghost != null) Destroy(ghost);
            ghost = null;
            pasadaGhost = null;
        }
    }
}
