using UnityEngine;
using UnityEngine.Events;

namespace LaProyeccion.Puzzles
{
    /// <summary>
    /// Componente base para objetos con los que el jugador puede interactuar.
    /// Subclase para comportamientos específicos (puertas, terminales, palancas)
    /// o usa OnInteract directamente desde el Inspector con UnityEvent.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [Tooltip("Si es true, solo puede interactuarse una vez. Útil para puertas que abren narrativa.")]
        [SerializeField] private bool oneShot = false;

        [Tooltip("Disparado cuando el jugador pulsa la tecla de interactuar dentro del rango.")]
        public UnityEvent OnInteract;

        [Tooltip("Opcional: feedback visual cuando el jugador entra al rango.")]
        public UnityEvent OnEnterRange;
        public UnityEvent OnExitRange;

        private bool consumed;

        public bool IsAvailable => !oneShot || !consumed;

        public virtual void Interact()
        {
            if (!IsAvailable) return;

            OnInteract?.Invoke();
            if (oneShot) consumed = true;
        }

        public void OnPlayerEnterRange() => OnEnterRange?.Invoke();
        public void OnPlayerExitRange() => OnExitRange?.Invoke();
    }
}