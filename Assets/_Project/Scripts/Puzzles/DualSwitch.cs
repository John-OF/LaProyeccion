using UnityEngine;
using UnityEngine.Events;

namespace LaProyeccion.Puzzles
{
    /// <summary>
    /// Palanca activable. Mantiene un estado on/off persistente y emite
    /// eventos al cambiar. Se conecta al Interactable existente vía
    /// UnityEvents en Inspector: Interactable.OnInteract -> DualSwitch.Toggle().
    /// </summary>
    [DisallowMultipleComponent]
    public class DualSwitch : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool isOn = false;
        [SerializeField] private bool oneWay = true; // true = una vez activada, no se puede desactivar

        [Header("Visuals (optional)")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color offColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color onColor = new Color(0.3f, 1.0f, 0.9f); // cyan

        [Header("Events")]
        public UnityEvent OnActivated;
        public UnityEvent OnDeactivated;
        public UnityEvent<bool> OnStateChanged;

        public bool IsOn => isOn;

        void Start()
        {
            ApplyVisual();
        }

        /// <summary>Llamar desde Interactable.OnInteract (Inspector).</summary>
        public void Toggle()
        {
            if (oneWay && isOn) return;
            SetState(!isOn);
        }

        public void SetState(bool value)
        {
            if (isOn == value) return;
            isOn = value;
            ApplyVisual();
            OnStateChanged?.Invoke(isOn);
            if (isOn) OnActivated?.Invoke();
            else OnDeactivated?.Invoke();
        }

        void ApplyVisual()
        {
            if (spriteRenderer != null)
                spriteRenderer.color = isOn ? onColor : offColor;
        }
    }
}