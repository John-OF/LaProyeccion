using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using LaProyeccion.Core;
using LaProyeccion.Player;
using LaProyeccion.World;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Vistazo al otro mundo — "peek" (idea #1 de Claude, 2026-07-13, ideas.md):
    /// la tecla de cambio de mundo gana dos verbos:
    ///   TAP  (soltar antes de <see cref="umbralHold"/>) → cambio de mundo normal.
    ///   HOLD (mantener)                                 → vistazo: fantasma LOCAL
    ///        del otro mundo alrededor del jugador mientras se mantenga. Al soltar
    ///        se cierra SIN cambiar de mundo.
    ///
    /// Diferencia de diseño con el radar (GDD §3.2): el radar consume Semilla y
    /// revela TODO ~4 s; el vistazo es gratuito pero local (radio corto), breve y
    /// te ancla la tecla (no puedes cambiar de mundo mientras miras). Solo
    /// renderers, jamás colliders — mismo contrato que el radar.
    ///
    /// Implementación: reusa el canal completo del radar (registro estático
    /// GhostReveal + SetGhostReveal de los tres componentes de presencia)
    /// sustituyendo temporalmente los materiales del registro por los del peek
    /// (SpritePeekReveal: máscara radial _Centro/_Radio alimentada cada frame).
    ///
    /// Este componente asume la tecla SwitchWorld: pone
    /// <see cref="PlayerController.CambioDeMundoDelegado"/> a true mientras vive,
    /// para que PlayerController no dispare TrySwitchWorld en paralelo.
    ///
    /// LIMITACIÓN CONOCIDA (aceptada en laboratorio): peek y pulso del radar no
    /// coordinan — si un pulso está activo, el peek no arranca (y viceversa el
    /// radar ignora al peek). Si la mecánica entra al juego, unificar ambos bajo
    /// un único dueño del registro GhostReveal.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class WorldPeekController : MonoBehaviour
    {
        [Header("Materiales del vistazo (Assets/_Project/Shaders)")]
        [SerializeField] private Material peekSimMaterial;
        [SerializeField] private Material peekRealMaterial;

        [Header("Tap vs Hold")]
        [Tooltip("Soltar antes de este tiempo = tap (cambio de mundo). Mantener = vistazo.")]
        [SerializeField, Range(0.1f, 0.6f)] private float umbralHold = 0.25f;

        [Header("Vistazo")]
        [Tooltip("Radio del fantasma alrededor del jugador, en unidades de mundo.")]
        [SerializeField, Min(1f)] private float radio = 6f;
        [Tooltip("Fade de apertura/cierre animando _Reveal.")]
        [SerializeField, Range(0.05f, 0.5f)] private float fadeTime = 0.12f;

        private static readonly int RevealID = Shader.PropertyToID("_Reveal");
        private static readonly int CentroID = Shader.PropertyToID("_Centro");
        private static readonly int RadioID = Shader.PropertyToID("_Radio");

        private PlayerController player;
        private RadarPulseController radar;
        private PlayerInputActions input;

        private bool pressed;
        private float pressTime;
        private bool peeking;
        private Coroutine fade;

        // Materiales del radar guardados mientras el registro apunta a los del peek.
        private Material radarSim, radarReal;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            radar = GetComponent<RadarPulseController>();
            input = new PlayerInputActions();
            SetRevealValue(1f); // arranque limpio (gotcha del material sucio entre Plays)
        }

        private void OnEnable()
        {
            // Guard del domain reload durante Play (patrón del proyecto).
            if (input == null) return;
            input.Player.SwitchWorld.started += OnSwitchDown;
            input.Player.SwitchWorld.canceled += OnSwitchUp;
            input.Player.SwitchWorld.Enable();
            GameSession.OnPlayerRespawned += OnPlayerRespawned;
            player.CambioDeMundoDelegado = true;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Player.SwitchWorld.started -= OnSwitchDown;
                input.Player.SwitchWorld.canceled -= OnSwitchUp;
                input.Player.SwitchWorld.Disable();
            }
            GameSession.OnPlayerRespawned -= OnPlayerRespawned;
            if (player != null) player.CambioDeMundoDelegado = false;
            CancelPeek(); // descarga de escena en pleno vistazo: dejar todo limpio
        }

        private void OnApplicationQuit()
        {
            SetRevealValue(1f);
        }

        // ==================== Input ====================

        private void OnSwitchDown(InputAction.CallbackContext _)
        {
            if (Time.timeScale == 0f) return; // en pausa ni tap ni peek
            pressed = true;
            pressTime = Time.time;
        }

        private void OnSwitchUp(InputAction.CallbackContext _)
        {
            if (!pressed) return;
            pressed = false;

            if (peeking)
            {
                EndPeek();               // hold: cerrar el vistazo, SIN cambiar
            }
            else if (Time.time - pressTime < umbralHold)
            {
                WorldManager.Instance?.TrySwitchWorld(); // tap: cambio normal
            }
            // Soltar tras el umbral sin peek activo (switch bloqueado, pausa...):
            // intención de mirar, no de cambiar → no hacemos nada.
        }

        private void Update()
        {
            // Apertura del vistazo al superar el umbral de hold.
            if (pressed && !peeking
                && Time.time - pressTime >= umbralHold
                && Time.timeScale > 0f
                && WorldManager.Instance != null && WorldManager.Instance.IsSwitchEnabled
                && (radar == null || !radar.Revelando)
                && GhostReveal.SimMaterial != peekSimMaterial)
            {
                StartPeek();
            }

            // El vistazo sigue al jugador.
            if (peeking)
            {
                Vector4 centro = transform.position;
                if (peekSimMaterial != null) peekSimMaterial.SetVector(CentroID, centro);
                if (peekRealMaterial != null) peekRealMaterial.SetVector(CentroID, centro);
            }
        }

        // ==================== Vistazo ====================

        private void StartPeek()
        {
            peeking = true;

            // Sustituir el registro del radar por los materiales del peek.
            radarSim = GhostReveal.SimMaterial;
            radarReal = GhostReveal.RealMaterial;
            GhostReveal.SimMaterial = peekSimMaterial;
            GhostReveal.RealMaterial = peekRealMaterial;

            Vector4 centro = transform.position;
            foreach (var m in new[] { peekSimMaterial, peekRealMaterial })
            {
                if (m == null) continue;
                m.SetVector(CentroID, centro);
                m.SetFloat(RadioID, radio);
            }

            SetRevealValue(0f);
            SetAllGhostReveal(true);
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(AnimateReveal(0f, 1f, fadeTime, null));
        }

        private void EndPeek()
        {
            if (!peeking) return;
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(AnimateReveal(
                peekSimMaterial != null ? peekSimMaterial.GetFloat(RevealID) : 1f,
                0f, fadeTime, FinishPeek));
        }

        /// <summary>Cierre inmediato sin fade (muerte, descarga de escena).</summary>
        private void CancelPeek()
        {
            if (!peeking) return;
            if (fade != null) StopCoroutine(fade);
            FinishPeek();
        }

        private void FinishPeek()
        {
            SetAllGhostReveal(false);
            SetRevealValue(1f); // contrato: _Reveal limpio al terminar

            // Devolver el registro al radar.
            if (radarSim != null) GhostReveal.SimMaterial = radarSim;
            if (radarReal != null) GhostReveal.RealMaterial = radarReal;
            radarSim = radarReal = null;

            peeking = false;
            fade = null;
        }

        private void OnPlayerRespawned()
        {
            pressed = false; // que el soltar póstumo no dispare un cambio
            CancelPeek();
        }

        // ==================== Helpers (espejo de RadarPulseController) ====================

        private void SetAllGhostReveal(bool on)
        {
            foreach (var t in FindObjectsByType<TilemapDualLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                t.SetGhostReveal(on);
            foreach (var w in FindObjectsByType<WorldExclusivePresence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                w.SetGhostReveal(on);
            foreach (var p in FindObjectsByType<PlatformDual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                p.SetGhostReveal(on);
        }

        private IEnumerator AnimateReveal(float from, float to, float time, System.Action alTerminar)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime; // escalado: se congela en pausa
                SetRevealValue(Mathf.Lerp(from, to, t / time));
                yield return null;
            }
            SetRevealValue(to);
            alTerminar?.Invoke();
        }

        private void SetRevealValue(float v)
        {
            if (peekSimMaterial != null) peekSimMaterial.SetFloat(RevealID, v);
            if (peekRealMaterial != null) peekRealMaterial.SetFloat(RevealID, v);
        }
    }
}
