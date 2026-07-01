using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LaProyeccion.Puzzles
{
    /// <summary>
    /// Puerta/compuerta que se abre cuando todas las DualSwitch
    /// referenciadas están activas. Sirve para Puzzle 2 (1 switch)
    /// y Puzzle 3 (N switches) sin necesitar dos componentes distintos.
    /// </summary>
    [DisallowMultipleComponent]
    public class Gate : MonoBehaviour
    {
        [Header("Required Switches")]
        [SerializeField] private List<DualSwitch> requiredSwitches = new();

        [Header("Body")]
        [Tooltip("Collider que bloquea el paso. Se desactiva al abrir.")]
        [SerializeField] private Collider2D blockingCollider;
        [Tooltip("Renderer del cuerpo de la puerta. Se desactiva al abrir.")]
        [SerializeField] private SpriteRenderer body;

        [Header("Events")]
        public UnityEvent OnOpened;
        public UnityEvent OnClosed;

        bool isOpen = false;
        bool started = false;

        void Start()
        {
            // Suscribirse a los switches
            foreach (var s in requiredSwitches)
            {
                if (s == null) continue;
                s.OnStateChanged.AddListener(_ => Evaluate());
            }
            Evaluate();
            started = true; // a partir de aquí, abrir la puerta cuenta como "puzzle resuelto"
        }

        void Evaluate()
        {
            bool allOn = requiredSwitches.Count > 0;
            foreach (var s in requiredSwitches)
            {
                if (s == null || !s.IsOn) { allOn = false; break; }
            }

            if (allOn && !isOpen) Open();
            else if (!allOn && isOpen) Close();
        }

        public void Open()
        {
            isOpen = true;
            if (blockingCollider != null) blockingCollider.enabled = false;
            if (body != null) body.enabled = false;
            OnOpened?.Invoke();

            // Autoguardado: abrir la puerta significa que el puzzle quedó resuelto.
            // No guardamos en la evaluación inicial del Start (estado ya-abierto por diseño)
            // ni mientras se restaura un guardado (evita autoguardados en cadena).
            if (started && !LaProyeccion.Core.GameSession.IsRestoring)
                LaProyeccion.Core.GameSession.AutoSave();
        }

        public void Close()
        {
            isOpen = false;
            if (blockingCollider != null) blockingCollider.enabled = true;
            if (body != null) body.enabled = true;
            OnClosed?.Invoke();
        }
    }
}