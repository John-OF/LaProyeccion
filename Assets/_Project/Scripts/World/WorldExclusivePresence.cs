using System.Collections.Generic;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.World
{
    /// <summary>
    /// Hace que un GameObject (con sus colliders y renderers) exista solo
    /// en uno de los dos mundos. A diferencia de TilemapDualLayer (que es
    /// para tilemaps enteros), esto es para entidades sueltas:
    /// switches, props, NPCs Real-only, etc.
    ///
    /// Afecta a TODOS los Collider2D y Renderer del GameObject y sus hijos.
    ///
    /// Extensión F1.P5 (retrocompatible): <see cref="SetGhostReveal"/> — durante
    /// el pulso del radar, si el objeto NO existe en el mundo actual, sus
    /// renderers se encienden con el material fantasma (colliders y luces
    /// siguen apagados). Esencia v1.1: los interactuables del otro mundo
    /// también se revelan ("hay algo que ACTIVAR ahí").
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldExclusivePresence : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private WorldState belongsTo = WorldState.Real;

        Collider2D[] colliders;
        Renderer[] renderers;
        // Extensión retrocompatible (F1.P4): las Light2D también se apagan en el
        // mundo donde el objeto no existe (una semilla solo-Real no debe brillar
        // en la Simulación). Light2D no es Renderer, por eso va aparte.
        UnityEngine.Rendering.Universal.Light2D[] lights;

        void Awake()
        {
            colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
            renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            lights = GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(includeInactive: true);
        }

        void OnEnable()
        {
            WorldManager.OnWorldChanged += HandleWorldChanged;
            ApplyState();
        }

        void OnDisable()
        {
            WorldManager.OnWorldChanged -= HandleWorldChanged;
        }

        void HandleWorldChanged(WorldState s) => ApplyState();

        bool ghostReveal;
        readonly Dictionary<Renderer, Material[]> ghostStore = new();

        /// <summary>Activa/desactiva la silueta fantasma (la llama RadarPulseController).</summary>
        public void SetGhostReveal(bool on)
        {
            ghostReveal = on;
            ApplyState();
        }

        void ApplyState()
        {
            if (WorldManager.Instance == null) return;
            bool present = WorldManager.Instance.CurrentWorld == belongsTo;

            foreach (var c in colliders) if (c != null) c.enabled = present;
            foreach (var l in lights) if (l != null) l.enabled = present;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (ghostReveal && !present && GhostReveal.Ready)
                {
                    GhostReveal.ApplyGhost(r, belongsTo, ghostStore);
                }
                else
                {
                    GhostReveal.Restore(r, ghostStore);
                    r.enabled = present;
                }
            }
        }
    }
}