using UnityEngine;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Inventario de Semillas del jugador (F1.P4, GDD §3.2 / ALCANCE §4).
    /// Vive en PF_Player. La cuenta es ESTÁTICA de sesión: sobrevive a la muerte
    /// (regla constitucional: el respawn no reinicia el inventario) y a los
    /// cambios de zona (cada escena instancia su propio PF_Player).
    /// La capacidad sí es por instancia/escena (default 1; Zona 4 usará 2).
    /// GameSession guarda/restaura la cuenta vía SaveSystem (save.seeds).
    /// </summary>
    public class SeedInventory : MonoBehaviour
    {
        [Tooltip("Máximo de semillas que caben. Por escena: Z2/Z3 = 1, Z4 = 2.")]
        [SerializeField, Min(1)] private int capacity = 1;

        // Cuenta de sesión: persiste entre muertes y escenas dentro del mismo Play.
        private static int sessionCount = 0;

        /// <summary>Cuenta actual (accesor estático para guardado sin buscar la instancia).</summary>
        public static int SessionCount => sessionCount;

        public int Count => sessionCount;
        public int Capacity => capacity;

        /// <summary>Disparado al recoger o consumir (el indicador diegético se cuelga aquí).</summary>
        public event System.Action OnChanged;

        /// <summary>Intenta añadir una semilla. False si el inventario está lleno.</summary>
        public bool TryAdd()
        {
            if (sessionCount >= capacity) return false;
            sessionCount++;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Intenta consumir una semilla (la usará el pulso del radar, F1.P5).</summary>
        public bool TryConsume()
        {
            if (sessionCount <= 0) return false;
            sessionCount--;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Nueva partida: inventario a cero (lo llama GameSession).</summary>
        public static void ResetSession() => sessionCount = 0;

        /// <summary>Continuar: restaura la cuenta guardada (lo llama GameSession).</summary>
        public static void RestoreSession(int count) => sessionCount = Mathf.Max(0, count);

        private void Start()
        {
            // Sincroniza al indicador con la cuenta de sesión al entrar en la escena.
            OnChanged?.Invoke();
        }
    }
}
