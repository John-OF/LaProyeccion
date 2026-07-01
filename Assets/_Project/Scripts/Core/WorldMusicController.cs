using UnityEngine;
using UnityEngine.Audio;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Escucha WorldManager.OnWorldChanged y dispara la transición de
    /// snapshots del Mixer. Una snapshot mutea MusicSim, la otra mutea MusicReal.
    /// El crossfade es de duration segundos.
    ///
    /// Se mantiene separado del AudioManager porque su única responsabilidad
    /// es "música según mundo" — los SFX no le incumben.
    /// </summary>
    public class WorldMusicController : MonoBehaviour
    {
        [Header("Mixer")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerSnapshot snapshotSimulation;
        [SerializeField] private AudioMixerSnapshot snapshotReal;

        [Header("Crossfade")]
        [Tooltip("Tiempo de transición entre mundos. Empata bien con el glitch visual de 0.3s.")]
        [SerializeField, Min(0.05f)] private float duration = 0.4f;

        void OnEnable()
        {
            WorldManager.OnWorldChanged += HandleWorldChanged;
        }

        void OnDisable()
        {
            WorldManager.OnWorldChanged -= HandleWorldChanged;
        }

        void HandleWorldChanged(WorldState s)
        {
            var target = s == WorldState.Simulation ? snapshotSimulation : snapshotReal;
            if (target == null || mixer == null) return;
            target.TransitionTo(duration);
        }
    }
}