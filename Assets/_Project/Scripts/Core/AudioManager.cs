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

        [Header("Volumes (per-clip multiplier, 0..1)")]
        [Range(0f, 1f)][SerializeField] private float jumpVolume = 0.6f;
        [Range(0f, 1f)][SerializeField] private float worldSwitchVolume = 0.8f;
        [Range(0f, 1f)][SerializeField] private float doorOpenVolume = 0.9f;
        [Range(0f, 1f)][SerializeField] private float switchActivateVolume = 0.8f;
        [Range(0f, 1f)][SerializeField] private float keplinMessageVolume = 0.7f;
        [Range(0f, 1f)][SerializeField] private float musicVolume = 0.6f;

        // AudioSources creados en runtime
        AudioSource musicSimSource;
        AudioSource musicRealSource;
        AudioSource sfxSource;       // UI / narrativa
        AudioSource sfxWorldSource;  // mundo (jump, door, switch)

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
    }
}