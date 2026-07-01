using TMPro;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Narrative
{
    /// <summary>
    /// Texto diegético (en el mundo del juego, no UI overlay) que cambia
    /// según el WorldState. Útil para pantallas de Keplin: en Simulación
    /// muestra el mensaje "limpio", en Real muestra la versión corrupta
    /// (verdad oculta).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class DiegeticTextSwitcher : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField, TextArea] private string simulationText = "TODO ESTA BIEN";
        [SerializeField, TextArea] private string realText = "T0DO  ES B|EN.";

        [Header("Colors")]
        [SerializeField] private Color simulationColor = new Color(0.7f, 1f, 1f);
        [SerializeField] private Color realColor = new Color(1f, 0.4f, 0.4f);

        TMP_Text label;

        void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            // OnWorldChanged es estático en tu WorldManager: nos suscribimos al tipo, no a la instancia.
            WorldManager.OnWorldChanged += HandleWorldChanged;
            ApplyState();
        }

        void OnDisable()
        {
            WorldManager.OnWorldChanged -= HandleWorldChanged;
        }

        void HandleWorldChanged(WorldState s) => ApplyState();

        void ApplyState()
        {
            // Si el WorldManager aún no completó Awake (orden de ejecución), esperamos.
            // El Start() del WorldManager dispara OnWorldChanged y nos llegará el estado entonces.
            if (WorldManager.Instance == null) return;

            bool sim = WorldManager.Instance.CurrentWorld == WorldState.Simulation;
            label.text = sim ? simulationText : realText;
            label.color = sim ? simulationColor : realColor;
        }
    }
}