using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using LaProyeccion.Core;

namespace LaProyeccion.World
{
    /// <summary>
    /// Activa o desactiva el render y la colisión de un tilemap entero
    /// según el WorldState. Un componente por tilemap (uno en Sim, uno en Real).
    ///
    /// Extensión F1.P5 (retrocompatible): <see cref="SetGhostReveal"/> — durante
    /// el pulso del radar, si el tilemap NO existe en el mundo actual, se enciende
    /// SOLO su renderer con el material fantasma (nunca el collider). Al cambiar
    /// de mundo con el revelado activo, el fantasma se re-engancha a la capa que
    /// ahora es la opuesta (el radar siempre enseña "el otro lado del actual").
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class TilemapDualLayer : MonoBehaviour
    {
        [SerializeField] private WorldState belongsTo = WorldState.Simulation;

        private TilemapRenderer tilemapRenderer;
        private Collider2D tilemapCollider;

        private bool ghostReveal;
        private readonly Dictionary<Renderer, Material[]> ghostStore = new();

        private void Awake()
        {
            tilemapRenderer = GetComponent<TilemapRenderer>();
            tilemapCollider = GetComponent<Collider2D>();
        }

        private void OnEnable() => WorldManager.OnWorldChanged += HandleWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= HandleWorldChanged;

        private void HandleWorldChanged(WorldState newWorld)
        {
            bool active = newWorld == belongsTo;
            if (tilemapCollider != null) tilemapCollider.enabled = active;
            ApplyRenderState(active);
        }

        /// <summary>Activa/desactiva la silueta fantasma (la llama RadarPulseController).</summary>
        public void SetGhostReveal(bool on)
        {
            ghostReveal = on;
            if (WorldManager.Instance == null) return;
            ApplyRenderState(WorldManager.Instance.CurrentWorld == belongsTo);
        }

        private void ApplyRenderState(bool present)
        {
            if (tilemapRenderer == null) return;

            if (ghostReveal && !present && GhostReveal.Ready)
            {
                GhostReveal.ApplyGhost(tilemapRenderer, belongsTo, ghostStore);
            }
            else
            {
                GhostReveal.Restore(tilemapRenderer, ghostStore);
                tilemapRenderer.enabled = present;
            }
        }
    }
}
