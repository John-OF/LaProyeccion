using System.Collections;
using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Zonas de no-cambio / cambio forzado (idea #4 de Claude, 2026-07-13,
    /// ideas.md): un trigger que aplica reglas locales sobre la API existente de
    /// WorldManager. Dos diales componibles:
    /// - <see cref="forzarAlEntrar"/>: al entrar, ForceWorld(mundo) — pasillos
    ///   que te obligan al mundo "malo".
    /// - <see cref="bloquearCambioDentro"/>: mientras estás dentro, el cambio
    ///   queda bloqueado — antesalas donde no puedes escapar cambiando: decides
    ///   ANTES de entrar.
    ///
    /// Pegamento puro: no toca el WorldManager más allá de su API pública.
    /// Restaurar al salir usa RestoreSwitchEnabled (nunca EnableSwitch, que
    /// dispararía OnSwitchUnlocked y repetiría el primer mensaje de Keplin).
    ///
    /// Legibilidad (Pilar 3): la zona es un overlay visible SIEMPRE (el jugador
    /// ve dónde empieza la regla), y al intentar cambiar dentro estando
    /// bloqueado, el overlay FLASHEA (suscrito a WorldManager.OnSwitchDenied,
    /// seam retrocompatible añadido con este prototipo) — la tecla nunca falla
    /// en silencio.
    ///
    /// Limitación conocida (aceptable en laboratorio): dos zonas bloqueantes
    /// SOLAPADAS se pisan el estado guardado (la segunda recordaría "bloqueado"
    /// como estado previo). No solapar zonas bloqueantes en el diseño.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ZonaDeCambio : MonoBehaviour
    {
        public enum Forzado { NoForzar, Simulacion, Real }

        [Header("Reglas (componibles)")]
        [Tooltip("Al entrar el jugador, fuerza este mundo (ignora cooldown; no suena como cambio del jugador).")]
        [SerializeField] private Forzado forzarAlEntrar = Forzado.NoForzar;
        [Tooltip("Mientras el jugador está dentro, TrySwitchWorld queda bloqueado.")]
        [SerializeField] private bool bloquearCambioDentro = true;

        [Header("Visual (overlay siempre visible — la regla se ve ANTES de pisarla)")]
        [Tooltip("SpriteRenderer del área. Si se deja vacío, se busca en este GameObject.")]
        [SerializeField] private SpriteRenderer overlay;
        [Tooltip("Tinte del flash cuando se intenta cambiar dentro y está bloqueado.")]
        [SerializeField] private Color tintDenegado = new Color(1f, 0.25f, 0.25f, 0.45f);
        [SerializeField, Min(0.05f)] private float duracionFlash = 0.3f;

        [Header("Latido (shader ZonaDeCambioOverlay — via MPB, el .mat nunca se ensucia)")]
        [Tooltip("Segundos por latido lub-dub. La zona forzada puede latir más urgente que la antesala.")]
        [SerializeField, Range(0.3f, 4f)] private float periodoLatido = 1.6f;

        private static readonly int ZoneSizeID = Shader.PropertyToID("_ZoneSize");
        private static readonly int PulsoPeriodoID = Shader.PropertyToID("_PulsoPeriodo");

        private BoxCollider2D zona;
        private Transform player;
        private bool jugadorDentro;
        private bool cambioEstabaHabilitado;
        private Color colorOriginal;
        private Coroutine flash;

        private void Awake()
        {
            zona = GetComponent<BoxCollider2D>();
            zona.isTrigger = true;
            if (overlay == null) overlay = GetComponent<SpriteRenderer>();
            if (overlay != null)
            {
                colorOriginal = overlay.color;
                // Contrato con el shader del latido: tamaño en unidades de mundo y
                // período, POR INSTANCIA (MPB) — dos zonas de distinto tamaño/ritmo
                // comparten el mismo .mat sin escribirlo jamás en runtime.
                var mpb = new MaterialPropertyBlock();
                overlay.GetPropertyBlock(mpb);
                mpb.SetVector(ZoneSizeID, new Vector4(
                    overlay.transform.lossyScale.x, overlay.transform.lossyScale.y, 0f, 0f));
                mpb.SetFloat(PulsoPeriodoID, periodoLatido);
                overlay.SetPropertyBlock(mpb);
            }

            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
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
            // Zona apagada/destruida con el jugador dentro: no dejar el mundo bloqueado.
            if (jugadorDentro) Salir();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (jugadorDentro || other.GetComponentInParent<PlayerController>() == null) return;
            jugadorDentro = true;

            if (forzarAlEntrar != Forzado.NoForzar && WorldManager.Instance != null)
                WorldManager.Instance.ForceWorld(
                    forzarAlEntrar == Forzado.Simulacion ? WorldState.Simulation : WorldState.Real);

            if (bloquearCambioDentro && WorldManager.Instance != null)
            {
                cambioEstabaHabilitado = WorldManager.Instance.IsSwitchEnabled;
                WorldManager.Instance.DisableSwitch();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!jugadorDentro || other.GetComponentInParent<PlayerController>() == null) return;
            Salir();
        }

        private void Salir()
        {
            jugadorDentro = false;
            if (bloquearCambioDentro && WorldManager.Instance != null)
                WorldManager.Instance.RestoreSwitchEnabled(cambioEstabaHabilitado);
        }

        /// <summary>
        /// Cinturón y tirantes del respawn: MuerteCorreccion congela la física
        /// (rb.simulated=false) y el teletransporte puede no disparar
        /// OnTriggerExit2D de forma fiable. Si tras reaparecer el jugador ya no
        /// está dentro del área, se libera la regla a mano.
        /// </summary>
        private void OnPlayerRespawned()
        {
            if (!jugadorDentro || player == null) return;
            if (!zona.OverlapPoint(player.position)) Salir();
        }

        private void OnSwitchDenied()
        {
            // Solo la zona que está causando el bloqueo da el feedback.
            if (!jugadorDentro || !bloquearCambioDentro || overlay == null) return;
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            overlay.color = tintDenegado;
            for (float t = 0f; t < duracionFlash; t += Time.deltaTime)
            {
                overlay.color = Color.Lerp(tintDenegado, colorOriginal, t / duracionFlash);
                yield return null;
            }
            overlay.color = colorOriginal;
            flash = null;
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            bool fuerza = forzarAlEntrar != Forzado.NoForzar;
            Gizmos.color = fuerza
                ? new Color(0.95f, 0.6f, 0.2f, 0.9f)   // naranja: fuerza mundo
                : new Color(0.8f, 0.8f, 0.9f, 0.9f);   // gris: solo bloquea
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
