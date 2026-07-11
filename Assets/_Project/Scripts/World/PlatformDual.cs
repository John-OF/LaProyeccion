using System.Collections.Generic;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.World
{
    /// <summary>
    /// Componente base de cualquier objeto que cambia de presencia entre mundos.
    /// Activa/desactiva el Renderer y el Collider según el WorldState actual.
    /// No destruye el GameObject: lo mantiene vivo para que pueda volver a aparecer.
    ///
    /// Extensión F1.P5 (retrocompatible): <see cref="SetGhostReveal"/> — silueta
    /// fantasma del pulso del radar cuando el objeto no existe en el mundo actual
    /// (solo renderers; el color depende del mundo donde SÍ existe).
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

        private bool ghostReveal;
        private readonly Dictionary<Renderer, Material[]> ghostStore = new();

        private void HandleWorldChanged(WorldState newWorld) => Apply(newWorld);

        /// <summary>Activa/desactiva la silueta fantasma (la llama RadarPulseController).</summary>
        public void SetGhostReveal(bool on)
        {
            ghostReveal = on;
            if (WorldManager.Instance != null) Apply(WorldManager.Instance.CurrentWorld);
        }

        private void Apply(WorldState currentWorld)
        {
            bool shouldExist = currentWorld == WorldState.Simulation
                ? (existsIn & Presence.Simulation) != 0
                : (existsIn & Presence.Real) != 0;

            foreach (var c in colliders) if (c != null) c.enabled = shouldExist;

            // Mundo cuya silueta se revela: aquel donde el objeto SÍ existe
            // (si existe en ambos o en ninguno, no hay nada que revelar).
            WorldState ghostWorld = currentWorld == WorldState.Simulation
                ? WorldState.Real : WorldState.Simulation;
            bool existsInGhostWorld = ghostWorld == WorldState.Simulation
                ? (existsIn & Presence.Simulation) != 0
                : (existsIn & Presence.Real) != 0;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (ghostReveal && !shouldExist && existsInGhostWorld && GhostReveal.Ready)
                {
                    GhostReveal.ApplyGhost(r, ghostWorld, ghostStore);
                }
                else
                {
                    GhostReveal.Restore(r, ghostStore);
                    r.enabled = shouldExist;
                }
            }
        }
    }
}