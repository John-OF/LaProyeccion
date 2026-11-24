using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, pase de arte): da a la luz global 2D un color
    /// y una intensidad distintos en cada mundo. Con la luz global a 1 y blanca las
    /// luces puntuales no se notan; bajándola, las farolas y el neón pasan a ser lo
    /// que ilumina la escena.
    ///
    /// Solo puede haber una luz global activa, así que no se duplica la luz: se anima
    /// la que ya hay. La duración por defecto es la del cruce de post-proceso
    /// (WorldPostProcessController, 0,3 s) para que las dos transiciones vayan a la vez.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class LuzGlobalPorMundo : MonoBehaviour
    {
        [Header("Simulación")]
        [SerializeField] private Color colorSimulacion = Color.white;
        [SerializeField, Min(0f)] private float intensidadSimulacion = 1f;

        [Header("Real")]
        [SerializeField] private Color colorReal = new Color(0.55f, 0.6f, 0.7f);
        [SerializeField, Min(0f)] private float intensidadReal = 0.5f;

        [SerializeField, Min(0f)] private float duracion = 0.3f;

        private Light2D luz;
        private Coroutine cruce;

        private void Awake()
        {
            luz = GetComponent<Light2D>();
        }

        private void OnEnable() => WorldManager.OnWorldChanged += AlCambiarDeMundo;
        private void OnDisable() => WorldManager.OnWorldChanged -= AlCambiarDeMundo;

        private void Start()
        {
            // Estado inicial sin animar.
            var mundo = WorldManager.Instance != null ? WorldManager.Instance.CurrentWorld : WorldState.Simulation;
            Fijar(mundo);
        }

        private void AlCambiarDeMundo(WorldState mundo)
        {
            if (cruce != null) StopCoroutine(cruce);
            if (duracion <= 0f || !isActiveAndEnabled) { Fijar(mundo); return; }
            cruce = StartCoroutine(Cruzar(mundo));
        }

        private void Fijar(WorldState mundo)
        {
            bool sim = mundo == WorldState.Simulation;
            luz.color = sim ? colorSimulacion : colorReal;
            luz.intensity = sim ? intensidadSimulacion : intensidadReal;
        }

        private IEnumerator Cruzar(WorldState mundo)
        {
            bool sim = mundo == WorldState.Simulation;
            Color colorDestino = sim ? colorSimulacion : colorReal;
            float intensidadDestino = sim ? intensidadSimulacion : intensidadReal;
            Color colorInicio = luz.color;
            float intensidadInicio = luz.intensity;

            float t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duracion);
                luz.color = Color.Lerp(colorInicio, colorDestino, k);
                luz.intensity = Mathf.Lerp(intensidadInicio, intensidadDestino, k);
                yield return null;
            }
            luz.color = colorDestino;
            luz.intensity = intensidadDestino;
            cruce = null;
        }
    }
}
