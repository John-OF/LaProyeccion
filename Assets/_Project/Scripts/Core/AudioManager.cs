using UnityEngine;
using UnityEngine.Audio;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Singleton central de audio. Cualquier sistema (PlayerController, DualSwitch,
    /// Gate, KeplinMessageController) llama a métodos como AudioManager.Instance.PlayJump()
    /// sin saber nada del Mixer ni de los AudioSources subyacentes.
    ///
    /// Mantiene 4 AudioSources persistentes:
    /// - musicSimSource  : reproduce loop de Music_Simulation, ruteado al grupo MusicSim
    /// - musicRealSource : reproduce loop de Music_Real,       ruteado al grupo MusicReal
    /// - sfxSource       : one-shots no posicionales (UI, Keplin)
    /// - sfxWorldSource  : one-shots del mundo (jump, door, switch). Separado para que
    ///                     un SFX largo no tape un click rápido.
    ///
    /// El crossfade entre mundos NO se hace aquí: lo hace WorldMusicController
    /// disparando snapshots del Mixer. Aquí solo hay reproducción.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mixer")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicSimGroup;
        [SerializeField] private AudioMixerGroup musicRealGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("Music Clips")]
        [SerializeField] private AudioClip musicSimulation;
        [SerializeField] private AudioClip musicReal;

        [Header("SFX Clips")]
        [SerializeField] private AudioClip sfxJump;
        [SerializeField] private AudioClip sfxWorldSwitch;
        [SerializeField] private AudioClip sfxDoorOpen;
        [SerializeField] private AudioClip sfxSwitchActivate;
        [SerializeField] private AudioClip sfxKeplinMessage;
        [Tooltip("Muerte/corrección del jugador (Correctores, vigilancia letal, suelo glicheado, caída, MuerteCorreccion).")]
        [SerializeField] private AudioClip sfxDeath;
        [Tooltip("Recoger Semilla. Si está vacío, usa sfxSwitchActivate con el pitch de abajo (provisional F1.P4; clip real en F6.P2).")]
        [SerializeField] private AudioClip sfxSeedPickup;
        [Tooltip("Pulso del radar. Si está vacío, usa sfxWorldSwitch a pitch 0.6 (provisional F1.P5; clip real en F6.P2 — debe sentirse 'caro', GDD §8).")]
        [SerializeField] private AudioClip sfxRadarPulse;

        [Header("Volumes (per-clip multiplier, 0..1)")]
        [Range(0f, 1f)][SerializeField] private float jumpVolume = 0.6f;
        [Range(0f, 1f)][SerializeField] private float worldSwitchVolume = 0.8f;
        [Range(0f, 1f)][SerializeField] private float doorOpenVolume = 0.9f;
        [Range(0f, 1f)][SerializeField] private float switchActivateVolume = 0.8f;
        [Range(0f, 1f)][SerializeField] private float keplinMessageVolume = 0.7f;
        [Range(0f, 1f)][SerializeField] private float deathVolume = 0.8f;
        [Range(0f, 1f)][SerializeField] private float seedPickupVolume = 0.7f;
        [Range(0f, 1f)][SerializeField] private float radarPulseVolume = 0.9f;
        [Range(0f, 1f)][SerializeField] private float musicVolume = 0.6f;

        [Tooltip("Pitch del SFX de semilla (≈1.4 mientras el clip provisional sea el del switch).")]
        [SerializeField, Range(0.5f, 2f)] private float seedPickupPitch = 1.4f;
        [Tooltip("Pitch del pulso del radar (≈0.6 mientras el clip provisional sea el del cambio de mundo).")]
        [SerializeField, Range(0.3f, 2f)] private float radarPulsePitch = 0.6f;

        // AudioSources creados en runtime
        AudioSource musicSimSource;
        AudioSource musicRealSource;
        AudioSource sfxSource;       // UI / narrativa
        AudioSource sfxWorldSource;  // mundo (jump, door, switch)
        AudioSource sfxPitchedSource; // one-shots con pitch alterado (semilla); fuente
                                      // propia para no desafinar los one-shots normales

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Music sources: loop continuo, ruteados a sus grupos respectivos.
            // Ambos suenan SIEMPRE; el snapshot del Mixer mutea uno u otro.
            musicSimSource = CreateSource("MusicSim_Source", musicSimGroup, musicSimulation, loop: true, volume: musicVolume, autoPlay: true);
            musicRealSource = CreateSource("MusicReal_Source", musicRealGroup, musicReal, loop: true, volume: musicVolume, autoPlay: true);

            // SFX sources: one-shots, no loop.
            sfxSource = CreateSource("SFX_Source", sfxGroup, null, loop: false, volume: 1f, autoPlay: false);
            sfxWorldSource = CreateSource("SFX_World_Source", sfxGroup, null, loop: false, volume: 1f, autoPlay: false);
            sfxPitchedSource = CreateSource("SFX_Pitched_Source", sfxGroup, null, loop: false, volume: 1f, autoPlay: false);

            ApplySavedVolumes();
        }

        /// <summary>
        /// Reaplica los volúmenes guardados por el menú de Opciones al mixer.
        /// Los nombres de parámetro y las claves de PlayerPrefs coinciden con
        /// LaProyeccion.UI.OptionsController, para que la config valga también
        /// dentro del juego (los valores del mixer no persisten entre sesiones).
        /// </summary>
        void ApplySavedVolumes()
        {
            if (mixer == null) return;
            ApplyVolume("MasterVol", PlayerPrefs.GetFloat("opt.vol.master", 0.8f));
            ApplyVolume("MusicVol", PlayerPrefs.GetFloat("opt.vol.music", 0.8f));
            ApplyVolume("SFXVol", PlayerPrefs.GetFloat("opt.vol.sfx", 0.8f));
        }

        void ApplyVolume(string param, float value01)
        {
            float dB = Mathf.Log10(Mathf.Clamp(value01, 0.0001f, 1f)) * 20f;
            mixer.SetFloat(param, dB);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        AudioSource CreateSource(string name, AudioMixerGroup group, AudioClip clip, bool loop, float volume, bool autoPlay)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = group;
            src.clip = clip;
            src.loop = loop;
            src.volume = volume;
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D
            if (autoPlay && clip != null) src.Play();
            return src;
        }

        // ==================== API pública ====================

        public void PlayJump() => sfxWorldSource.PlayOneShot(sfxJump, jumpVolume);
        public void PlayWorldSwitch() => sfxWorldSource.PlayOneShot(sfxWorldSwitch, worldSwitchVolume);
        public void PlayDoorOpen() => sfxWorldSource.PlayOneShot(sfxDoorOpen, doorOpenVolume);
        public void PlaySwitchActivate() => sfxWorldSource.PlayOneShot(sfxSwitchActivate, switchActivateVolume);
        public void PlayKeplinMessage() => sfxSource.PlayOneShot(sfxKeplinMessage, keplinMessageVolume);
        public void PlayDeath() => sfxWorldSource.PlayOneShot(sfxDeath, deathVolume);

        /// <summary>Recoger Semilla (F1.P4). Provisional: clip del switch a pitch ≈1.4.</summary>
        public void PlaySeedPickup()
        {
            var clip = sfxSeedPickup != null ? sfxSeedPickup : sfxSwitchActivate;
            if (clip == null) return;
            // Con clip propio (F6.P2) el pitch vuelve a 1; mientras, agudiza el provisional.
            sfxPitchedSource.pitch = sfxSeedPickup != null ? 1f : seedPickupPitch;
            sfxPitchedSource.PlayOneShot(clip, seedPickupVolume);
        }

        /// <summary>Pulso del radar (F1.P5). Debe sentirse "caro". Provisional: cambio de mundo a pitch ≈0.6.</summary>
        public void PlayRadarPulse()
        {
            var clip = sfxRadarPulse != null ? sfxRadarPulse : sfxWorldSwitch;
            if (clip == null) return;
            sfxPitchedSource.pitch = sfxRadarPulse != null ? 1f : radarPulsePitch;
            sfxPitchedSource.PlayOneShot(clip, radarPulseVolume);
        }
    }
}