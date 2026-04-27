using System;
using UnityEngine;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Define cuál de los dos mundos está activo en este momento.
    /// </summary>
    public enum WorldState
    {
        Simulation, // IlusionMotion3500: limpio, neón, NPCs activos
        Real        // Mundo Real: oscuro, deteriorado, silencio
    }

    /// <summary>
    /// Singleton central que gestiona el mundo activo.
    /// No hace efectos visuales ni audio por sí mismo: emite un evento
    /// y deja que cada sistema (plataformas, post-process, audio, NPCs)
    /// reaccione por su cuenta. Patrón Observer.
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        public static WorldManager Instance { get; private set; }

        [Header("Configuración")]
        [SerializeField] private WorldState startingWorld = WorldState.Simulation;
        [Tooltip("Tiempo mínimo entre cambios consecutivos. Evita spam y respeta la transición visual de 0.3s.")]
        [SerializeField, Min(0f)] private float switchCooldown = 0.4f;
        [Tooltip("Si está desactivado, TrySwitchWorld no hace nada. Util para Zona 0 (intro) y secuencias scriptadas.")]
        [SerializeField] private bool switchEnabled = false;

        public WorldState CurrentWorld { get; private set; }

        /// <summary>
        /// Se dispara cuando el mundo cambia. Cualquier componente puede suscribirse:
        /// WorldManager.OnWorldChanged += MiMetodo;
        /// </summary>
        public static event Action<WorldState> OnWorldChanged;

        /// <summary>Se dispara una vez cuando el switch se desbloquea por primera vez.
        /// Lo usaremos para mostrar el primer mensaje de Keplin en Zona 0.</summary>
        public static event System.Action OnSwitchUnlocked;

        private float lastSwitchTime = -999f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WorldManager] Ya existe otra instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CurrentWorld = startingWorld;
        }

        private void Start()
        {
            // Notificamos al arranque para que todos los listeners
            // (plataformas, post-process, etc.) se sincronicen con el estado inicial.
            OnWorldChanged?.Invoke(CurrentWorld);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Alterna entre Simulation y Real respetando el cooldown.
        /// Devuelve true si el cambio realmente ocurrió.
        /// </summary>
        public bool TrySwitchWorld()
        {
            if (!switchEnabled) return false;
            if (Time.time - lastSwitchTime < switchCooldown) return false;

            CurrentWorld = CurrentWorld == WorldState.Simulation
                ? WorldState.Real
                : WorldState.Simulation;

            lastSwitchTime = Time.time;
            OnWorldChanged?.Invoke(CurrentWorld);
            return true;
        }

        /// <summary>
        /// Fuerza un mundo específico ignorando cooldown.
        /// Útil para checkpoints, captura por Keplin, o secuencias scriptadas.
        /// </summary>
        public void ForceWorld(WorldState newWorld)
        {
            if (CurrentWorld == newWorld) return;
            CurrentWorld = newWorld;
            lastSwitchTime = Time.time;
            OnWorldChanged?.Invoke(CurrentWorld);
        }

        public bool IsSwitchEnabled => switchEnabled;

        public void EnableSwitch()
        {
            if (switchEnabled) return;
            switchEnabled = true;
            OnSwitchUnlocked?.Invoke();
        }

        public void DisableSwitch()
        {
            switchEnabled = false;
        }
    }

}