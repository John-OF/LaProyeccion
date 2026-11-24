using UnityEngine;
using UnityEngine.Rendering.Universal;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, pase de arte): decorado que solo se ve en uno
    /// de los dos mundos. Enciende y apaga los Renderer y las Light2D de sus hijos.
    ///
    /// Por qué no se usa <c>WorldExclusivePresence</c>: ese componente es para cosas
    /// que EXISTEN (colliders) y por eso participa en el pulso del radar, que dibuja
    /// como fantasma lo que hay en el otro mundo. El decorado no debe salir en el
    /// radar: un fondo entero revelado taparía lo único que el pulso tiene que
    /// enseñar, que es la geometría y los interactuables (GDD §3.2). Este componente
    /// no tiene colliders ni fantasma: solo pinta o no pinta.
    ///
    /// Sigue el patrón del proyecto: se suscribe a WorldManager.OnWorldChanged, no
    /// consulta CurrentWorld en Update.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArtePorMundo : MonoBehaviour
    {
        [SerializeField] private WorldState perteneceA = WorldState.Simulation;

        private Renderer[] renderers;
        private Light2D[] luces;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            luces = GetComponentsInChildren<Light2D>(includeInactive: true);
        }

        private void OnEnable()
        {
            WorldManager.OnWorldChanged += AlCambiarDeMundo;
            Aplicar();
        }

        private void OnDisable()
        {
            WorldManager.OnWorldChanged -= AlCambiarDeMundo;
        }

        private void AlCambiarDeMundo(WorldState _) => Aplicar();

        private void Aplicar()
        {
            // Orden de Awake: si el WorldManager aún no existe, el disparo de
            // OnWorldChanged que hace en su Start() sincroniza el estado inicial.
            if (WorldManager.Instance == null) return;
            bool presente = WorldManager.Instance.CurrentWorld == perteneceA;

            foreach (var r in renderers) if (r != null) r.enabled = presente;
            foreach (var l in luces) if (l != null) l.enabled = presente;
        }
    }
}
