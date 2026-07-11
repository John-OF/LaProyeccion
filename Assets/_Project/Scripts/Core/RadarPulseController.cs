using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using LaProyeccion.World;

namespace LaProyeccion.Core
{
    /// <summary>
    /// El pulso del radar (F1.P5, GDD §3.2 v1.1): la acción Sondear (Q / botón
    /// Norte) consume una Semilla y revela ~4 s la silueta fantasma de la
    /// geometría E interactuables del mundo opuesto (vía SetGhostReveal de los
    /// componentes de presencia; solo renderers, sin colisión).
    ///
    /// Reglas de borde (validadas en el prototipo web, _docs/radar/RadarPulse.ts):
    /// - Sin semilla → "fizzle": anillo gris tenue de 180 ms. Nada de mensajes.
    /// - Cambiar de mundo durante el pulso NO cancela: los componentes de
    ///   presencia re-enganchan la silueta a la capa opuesta del mundo nuevo.
    /// - Morir durante el revelado → cancela (GameSession.OnPlayerRespawned).
    /// - Pausa → el temporizador respeta la pausa (tiempo ESCALADO) y Sondear
    ///   no dispara en pausa.
    /// - Dos pulsos no se solapan: pulsar durante un revelado no consume.
    ///
    /// El anillo queda anclado al punto donde se usó la Semilla (no sigue al
    /// jugador). El fade del fantasma se hace animando _Reveal en los materiales
    /// compartidos (0→1 al aparecer, 1→0 al expirar; la disolución en trazo roto
    /// la hace el shader). _Reveal se restaura a 1 al terminar, cancelar y en
    /// OnApplicationQuit (gotcha del material sucio entre sesiones).
    /// </summary>
    public class RadarPulseController : MonoBehaviour
    {
        [Header("Materiales fantasma (Assets/_Project/Shaders)")]
        [SerializeField] private Material ghostSimMaterial;
        [SerializeField] private Material ghostRealMaterial;

        [Header("Revelado")]
        [Tooltip("Duración total de la silueta. Rango a calibrar 3–5 s (GDD §3.2).")]
        [SerializeField, Range(3f, 5f)] private float revealDuration = 4f;
        [Tooltip("Fade de entrada/salida animando _Reveal (~0.25 s por lado).")]
        [SerializeField, Range(0.05f, 1f)] private float fadeTime = 0.25f;

        [Header("Anillo del pulso")]
        [SerializeField] private Sprite ringSprite;
        [Tooltip("Radio final del anillo en unidades de mundo.")]
        [SerializeField] private float ringMaxRadius = 12f;
        [SerializeField] private float ringDuration = 1.5f;
        [SerializeField] private Color ringColorSim = new Color(0.35f, 0.95f, 1f, 0.9f);
        [SerializeField] private Color ringColorReal = new Color(0.95f, 0.4f, 0.22f, 0.9f);

        [Header("Fizzle (Sondear sin semilla)")]
        [SerializeField] private Color fizzleColor = new Color(0.65f, 0.65f, 0.65f, 0.5f);
        [SerializeField] private float fizzleStartRadius = 0.3f;
        [SerializeField] private float fizzleEndRadius = 1f;
        [SerializeField] private float fizzleDuration = 0.18f;

        private static readonly int RevealID = Shader.PropertyToID("_Reveal");

        private SeedInventory inventory;
        private PlayerInputActions input;
        private Coroutine activePulse;
        private bool revealing;

        private void Awake()
        {
            inventory = GetComponent<SeedInventory>();
            input = new PlayerInputActions();

            // Inyectar los materiales al registro que usan los componentes de presencia.
            GhostReveal.SimMaterial = ghostSimMaterial;
            GhostReveal.RealMaterial = ghostRealMaterial;
            SetRevealValue(1f); // arranque limpio (material compartido)
        }

        private void OnEnable()
        {
            // Guard del domain reload durante Play (patrón del proyecto).
            if (input == null) return;
            input.Player.Sondear.performed += OnSondear;
            input.Player.Sondear.Enable();
            GameSession.OnPlayerRespawned += OnPlayerRespawned;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Player.Sondear.performed -= OnSondear;
                input.Player.Sondear.Disable();
            }
            GameSession.OnPlayerRespawned -= OnPlayerRespawned;
            CancelReveal(); // descarga de escena en pleno revelado: dejar todo limpio
        }

        private void OnApplicationQuit()
        {
            SetRevealValue(1f);
        }

        // ==================== Disparo ====================

        private void OnSondear(InputAction.CallbackContext _)
        {
            // En pausa no se sondea (el resto del input del jugador ya está
            // desactivado por PauseMenuController; este componente no).
            if (Time.timeScale == 0f) return;

            // Dos pulsos no se solapan: pulsar durante un revelado NO consume.
            if (revealing) return;

            if (inventory == null || !inventory.TryConsume())
            {
                // Fizzle: fallo silencioso y barato. Nada de mensajes de error.
                SpawnRing(fizzleColor, fizzleStartRadius, fizzleEndRadius, fizzleDuration);
                return;
            }

            activePulse = StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            revealing = true;

            AudioManager.Instance?.PlayRadarPulse();

            // El anillo se tiñe del color del mundo que se revela (el opuesto).
            WorldState current = WorldManager.Instance != null
                ? WorldManager.Instance.CurrentWorld : WorldState.Simulation;
            Color ringColor = current == WorldState.Simulation ? ringColorReal : ringColorSim;
            SpawnRing(ringColor, 0.4f, ringMaxRadius, ringDuration);

            SetAllGhostReveal(true);
            yield return AnimateReveal(0f, 1f, fadeTime);

            float hold = Mathf.Max(0f, revealDuration - fadeTime * 2f);
            yield return new WaitForSeconds(hold); // tiempo escalado: respeta la pausa

            yield return AnimateReveal(1f, 0f, fadeTime);
            EndReveal();
        }

        private void OnPlayerRespawned() => CancelReveal();

        private void CancelReveal()
        {
            if (!revealing) return;
            if (activePulse != null) StopCoroutine(activePulse);
            EndReveal();
        }

        private void EndReveal()
        {
            SetAllGhostReveal(false);
            SetRevealValue(1f); // contrato: restaurar _Reveal=1 al terminar/cancelar
            revealing = false;
            activePulse = null;
        }

        // ==================== Revelado ====================

        private void SetAllGhostReveal(bool on)
        {
            foreach (var t in FindObjectsByType<TilemapDualLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                t.SetGhostReveal(on);
            foreach (var w in FindObjectsByType<WorldExclusivePresence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                w.SetGhostReveal(on);
            foreach (var p in FindObjectsByType<PlatformDual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                p.SetGhostReveal(on);
        }

        private IEnumerator AnimateReveal(float from, float to, float time)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime; // escalado: se congela en pausa
                SetRevealValue(Mathf.Lerp(from, to, t / time));
                yield return null;
            }
            SetRevealValue(to);
        }

        private void SetRevealValue(float v)
        {
            if (ghostSimMaterial != null) ghostSimMaterial.SetFloat(RevealID, v);
            if (ghostRealMaterial != null) ghostRealMaterial.SetFloat(RevealID, v);
        }

        // ==================== Anillo ====================

        private void SpawnRing(Color color, float startRadius, float endRadius, float duration)
        {
            if (ringSprite == null) return;

            var go = new GameObject("RadarRing");
            // Anclado al punto de disparo: NO es hijo del jugador.
            go.transform.position = transform.position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ringSprite;
            sr.color = color;
            sr.sortingOrder = 40;

            StartCoroutine(RingRoutine(go.transform, sr, color, startRadius, endRadius, duration));
        }

        private IEnumerator RingRoutine(Transform ring, SpriteRenderer sr, Color baseColor,
            float startRadius, float endRadius, float duration)
        {
            float t = 0f;
            while (t < duration && ring != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                // Expansión desacelerada + fade out (estética del prototipo web).
                float eased = 1f - (1f - k) * (1f - k);
                float radius = Mathf.Lerp(startRadius, endRadius, eased);
                ring.localScale = Vector3.one * radius;
                var c = baseColor;
                c.a = baseColor.a * (1f - k);
                sr.color = c;
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }
    }
}
