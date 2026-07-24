using UnityEngine;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): munición de PIEDRAS del jugador.
    /// La piedra es el objeto-puzzle de la cueva (PLAN_REDISENO §4): se lanza para
    /// "ver con el oído" (T3/T4) — golpe seco = suelo, silencio = vacío, chapoteo = agua.
    ///
    /// Munición LIMITADA (decisión del autor 2026-07-23): se recoge de un
    /// <see cref="MontonPiedras"/> y se gasta al lanzar (<see cref="LanzadorPiedras"/>).
    /// La escasez viene de la CAPACIDAD + la colocación de los montones, no de un HUD.
    ///
    /// Cuenta por-instancia (no estática): a diferencia de las Semillas, la munición de
    /// piedras es local del prototipo; el destino tras muerte/zona es decisión posterior.
    /// </summary>
    public class InventarioPiedras : MonoBehaviour
    {
        [Tooltip("Cuántas piedras caben a la vez.")]
        [SerializeField, Min(1)] private int capacidad = 5;

        private int cuenta;

        public int Count => cuenta;
        public int Capacity => capacidad;

        /// <summary>Disparado al recoger o gastar (para un indicador diegético futuro).</summary>
        public event System.Action OnChanged;

        /// <summary>Intenta añadir una piedra. False si el inventario está lleno.</summary>
        public bool TryAdd()
        {
            if (cuenta >= capacidad) return false;
            cuenta++;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Intenta gastar una piedra al lanzar. False si no quedan.</summary>
        public bool TryConsume()
        {
            if (cuenta <= 0) return false;
            cuenta--;
            OnChanged?.Invoke();
            return true;
        }
    }
}
