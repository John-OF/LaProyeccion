using UnityEngine;
using UnityEngine.Tilemaps;
using LaProyeccion.Core;

namespace LaProyeccion.World
{
    /// <summary>
    /// Activa o desactiva el render y la colisión de un tilemap entero
    /// según el WorldState. Un componente por tilemap (uno en Sim, uno en Real).
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class TilemapDualLayer : MonoBehaviour
    {
        [SerializeField] private WorldState belongsTo = WorldState.Simulation;

        private TilemapRenderer tilemapRenderer;
        private Collider2D tilemapCollider;

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
            if (tilemapRenderer != null) tilemapRenderer.enabled = active;
            if (tilemapCollider != null) tilemapCollider.enabled = active;
        }
    }
}