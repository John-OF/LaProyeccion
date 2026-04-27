using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.World
{
    /// <summary>
    /// Componente base de cualquier objeto que cambia de presencia entre mundos.
    /// Activa/desactiva el Renderer y el Collider según el WorldState actual.
    /// No destruye el GameObject: lo mantiene vivo para que pueda volver a aparecer.
    /// </summary>
    public class PlatformDual : MonoBehaviour
    {
        [System.Flags]
        public enum Presence
        {
            None = 0,
            Simulation = 1 << 0,
            Real = 1 << 1,
            Both = Simulation | Real
        }

        [SerializeField] private Presence existsIn = Presence.Simulation;

        [Tooltip("Si está marcado, también afecta a los hijos. Útil para plataformas con decoración.")]
        [SerializeField] private bool affectChildren = false;

        private Renderer[] renderers;
        private Collider2D[] colliders;

        private void Awake()
        {
            renderers = affectChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();
            colliders = affectChildren
                ? GetComponentsInChildren<Collider2D>(true)
                : GetComponents<Collider2D>();
        }

        private void OnEnable()
        {
            WorldManager.OnWorldChanged += HandleWorldChanged;
        }

        private void OnDisable()
        {
            WorldManager.OnWorldChanged -= HandleWorldChanged;
        }

        private void HandleWorldChanged(WorldState newWorld)
        {
            bool shouldExist = newWorld == WorldState.Simulation
                ? (existsIn & Presence.Simulation) != 0
                : (existsIn & Presence.Real) != 0;

            SetVisible(shouldExist);
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in renderers) r.enabled = visible;
            foreach (var c in colliders) c.enabled = visible;
        }
    }
}