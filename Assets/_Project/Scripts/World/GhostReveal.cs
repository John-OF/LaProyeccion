using System.Collections.Generic;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.World
{
    /// <summary>
    /// Soporte del revelado fantasma del pulso del radar (F1.P5).
    /// Registro estático de los materiales fantasma (los inyecta
    /// RadarPulseController en Awake desde sus campos serializados) y helpers
    /// de intercambio de materiales que usan los tres componentes de presencia
    /// (TilemapDualLayer, WorldExclusivePresence, PlatformDual).
    /// Regla: el fantasma SOLO toca renderers, jamás colliders.
    /// </summary>
    public static class GhostReveal
    {
        /// <summary>M_GhostReveal_Sim — cyan, revela geometría de la Simulación.</summary>
        public static Material SimMaterial;

        /// <summary>M_GhostReveal_Real — óxido, revela geometría del Real.</summary>
        public static Material RealMaterial;

        public static bool Ready => SimMaterial != null && RealMaterial != null;

        public static Material For(WorldState world)
            => world == WorldState.Simulation ? SimMaterial : RealMaterial;

        /// <summary>
        /// Pinta un renderer con el material fantasma del mundo al que pertenece
        /// y lo enciende. Guarda los materiales originales en <paramref name="store"/>
        /// (solo la primera vez) para poder restaurarlos.
        /// </summary>
        public static void ApplyGhost(Renderer r, WorldState belongsTo,
            Dictionary<Renderer, Material[]> store)
        {
            if (r == null || !Ready) return;

            if (!store.ContainsKey(r))
                store[r] = r.sharedMaterials;

            var ghost = For(belongsTo);
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = ghost;
            r.sharedMaterials = mats;
            r.enabled = true;
        }

        /// <summary>Devuelve al renderer sus materiales originales si estaban guardados.</summary>
        public static void Restore(Renderer r, Dictionary<Renderer, Material[]> store)
        {
            if (r == null) return;
            if (store.TryGetValue(r, out var mats))
            {
                r.sharedMaterials = mats;
                store.Remove(r);
            }
        }
    }
}
