using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using LaProyeccion.Core;

namespace LaProyeccion.World
{
    /// <summary>
    /// Tiñe un tilemap "común" (presente en ambos mundos) con el color
    /// adecuado según el WorldState. Da contraste visual sin duplicar geometría.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class TilemapCommonTinter : MonoBehaviour
    {
        [SerializeField] private Color simulationTint = new Color(0.47f, 0.90f, 1f);  // cyan
        [SerializeField] private Color realTint = new Color(0.35f, 0.39f, 0.47f);     // gris azulado
        [SerializeField, Min(0f)] private float transitionDuration = 0.3f;

        private Tilemap tilemap;
        private Coroutine activeFade;

        private void Awake()
        {
            tilemap = GetComponent<Tilemap>();
        }

        private void Start()
        {
            // Sincroniza color con estado actual sin animar.
            var current = WorldManager.Instance != null
                ? WorldManager.Instance.CurrentWorld
                : WorldState.Simulation;
            tilemap.color = current == WorldState.Simulation ? simulationTint : realTint;
        }

        private void OnEnable() => WorldManager.OnWorldChanged += HandleWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= HandleWorldChanged;

        private void HandleWorldChanged(WorldState newWorld)
        {
            Color target = newWorld == WorldState.Simulation ? simulationTint : realTint;
            if (activeFade != null) StopCoroutine(activeFade);
            activeFade = StartCoroutine(FadeTo(target));
        }

        private IEnumerator FadeTo(Color target)
        {
            Color start = tilemap.color;
            float t = 0f;
            while (t < transitionDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / transitionDuration);
                tilemap.color = Color.Lerp(start, target, k);
                yield return null;
            }
            tilemap.color = target;
            activeFade = null;
        }
    }
}