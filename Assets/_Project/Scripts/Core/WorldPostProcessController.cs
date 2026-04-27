using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using LaProyeccion.Core;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Cruza fade entre dos Global Volumes según el WorldState.
    /// La duración (0.3s) coincide con el efecto de glitch del GDD §4.1.
    /// </summary>
    public class WorldPostProcessController : MonoBehaviour
    {
        [SerializeField] private Volume simulationVolume;
        [SerializeField] private Volume realVolume;

        [Tooltip("Debe coincidir con el efecto visual del cambio. GDD pide 0.3s.")]
        [SerializeField, Min(0f)] private float transitionDuration = 0.3f;

        private Coroutine currentTransition;

        private void OnEnable() => WorldManager.OnWorldChanged += HandleWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= HandleWorldChanged;

        private void HandleWorldChanged(WorldState newWorld)
        {
            if (currentTransition != null) StopCoroutine(currentTransition);
            currentTransition = StartCoroutine(TransitionTo(newWorld));
        }

        private IEnumerator TransitionTo(WorldState target)
        {
            float startSim = simulationVolume.weight;
            float startReal = realVolume.weight;
            float endSim = target == WorldState.Simulation ? 1f : 0f;
            float endReal = target == WorldState.Real ? 1f : 0f;

            float t = 0f;
            while (t < transitionDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / transitionDuration);
                // Curva ease-out: cambio rápido al inicio, asienta al final.
                float eased = 1f - Mathf.Pow(1f - k, 3f);

                simulationVolume.weight = Mathf.Lerp(startSim, endSim, eased);
                realVolume.weight = Mathf.Lerp(startReal, endReal, eased);
                yield return null;
            }

            simulationVolume.weight = endSim;
            realVolume.weight = endReal;
            currentTransition = null;
        }
    }
}