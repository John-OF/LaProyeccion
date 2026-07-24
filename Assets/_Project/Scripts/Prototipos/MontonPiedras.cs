using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): fuente de piedras. Al interactuar
    /// (se cablea <c>Interactable.OnInteract → MontonPiedras.Recoger()</c> desde el
    /// Inspector, como <c>DualSwitch.Toggle</c>), rellena el <see cref="InventarioPiedras"/>
    /// del jugador hasta su capacidad.
    ///
    /// Por defecto INFINITO: la escasez la da la COLOCACIÓN (pocos montones) + la capacidad
    /// del inventario, no el agotamiento. Con <see cref="infinito"/> = false lleva un stock
    /// que se agota y desactiva el montón (para probar racionamiento duro).
    /// </summary>
    public class MontonPiedras : MonoBehaviour
    {
        [Tooltip("Rellena el inventario hasta su capacidad al recoger.")]
        [SerializeField] private bool rellenarAlMaximo = true;

        [Tooltip("Si no rellena al máximo, cuántas piedras da por interacción.")]
        [SerializeField, Min(1)] private int porRecogida = 3;

        [Tooltip("Fuente inagotable. La escasez la da la colocación, no el stock.")]
        [SerializeField] private bool infinito = true;

        [Tooltip("Piedras totales si NO es infinito. Al llegar a 0 el montón se agota.")]
        [SerializeField, Min(0)] private int stock = 9;

        /// <summary>Cablea aquí <c>Interactable.OnInteract</c> desde el Inspector.</summary>
        public void Recoger()
        {
            var inv = FindAnyObjectByType<InventarioPiedras>();
            if (inv == null) return;

            int libre = inv.Capacity - inv.Count;
            if (libre <= 0) return; // ya va lleno: no desperdicia el montón

            int dar = rellenarAlMaximo ? libre : Mathf.Min(porRecogida, libre);
            if (!infinito) dar = Mathf.Min(dar, stock);
            if (dar <= 0) return;

            for (int i = 0; i < dar; i++)
                if (!inv.TryAdd()) break;

            AudioManager.Instance?.PlaySwitchActivate(); // feedback provisional de recoger

            if (!infinito)
            {
                stock -= dar;
                if (stock <= 0) gameObject.SetActive(false); // agotado
            }
        }
    }
}
