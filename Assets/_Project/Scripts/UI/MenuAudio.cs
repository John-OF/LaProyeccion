using UnityEngine;
using UnityEngine.Audio;

namespace LaProyeccion.UI
{
    /// <summary>
    /// Audio del menú principal: música de fondo en loop y one-shots de UI
    /// (hover / click). Vive solo en la escena de menú (no es el AudioManager
    /// del juego). Rutea al mismo MainMixer para que los sliders de Opciones
    /// también afecten al menú.
    /// </summary>
    public class MenuAudio : MonoBehaviour
    {
        public static MenuAudio Instance { get; private set; }

        [Header("Mixer")]
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("Clips")]
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;

        [Header("Volúmenes (0..1)")]
        [Range(0f, 1f)][SerializeField] private float musicVolume = 0.6f;
        [Range(0f, 1f)][SerializeField] private float hoverVolume = 0.5f;
        [Range(0f, 1f)][SerializeField] private float clickVolume = 0.7f;

        private AudioSource musicSource;
        private AudioSource sfxSource;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = musicClip;
            musicSource.outputAudioMixerGroup = musicGroup;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f; // 2D
            musicSource.volume = musicVolume;
            if (musicClip != null) musicSource.Play();

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.outputAudioMixerGroup = sfxGroup;
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void PlayHover()
        {
            if (hoverClip != null) sfxSource.PlayOneShot(hoverClip, hoverVolume);
        }

        public void PlayClick()
        {
            if (clickClip != null) sfxSource.PlayOneShot(clickClip, clickVolume);
        }
    }
}
