using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace LaProyeccion.UI
{
    /// <summary>
    /// Panel de opciones: volumen (Master/Music/SFX), resolución y pantalla completa.
    /// Todo persiste en PlayerPrefs y se reaplica al abrir el menú. Los volúmenes
    /// se escriben en los parámetros expuestos del MainMixer, que AudioManager
    /// también lee al arrancar el juego (mismos nombres de parámetro).
    /// </summary>
    public class OptionsController : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Pantalla")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;

        // Nombres compartidos con AudioManager. Deben coincidir con los parámetros
        // expuestos del MainMixer.
        public const string MasterParam = "MasterVol";
        public const string MusicParam = "MusicVol";
        public const string SFXParam = "SFXVol";

        const string PrefMaster = "opt.vol.master";
        const string PrefMusic = "opt.vol.music";
        const string PrefSFX = "opt.vol.sfx";
        const string PrefResolution = "opt.resolution";
        const string PrefFullscreen = "opt.fullscreen";

        private List<Resolution> resolutions = new();

        private void Start()
        {
            SetupResolutions();
            LoadAndApply();
            WireCallbacks();
        }

        // ==================== Setup ====================

        private void SetupResolutions()
        {
            if (resolutionDropdown == null) return;

            resolutions.Clear();
            resolutionDropdown.ClearOptions();

            var seen = new HashSet<string>();
            var labels = new List<string>();
            int currentIndex = 0;

            foreach (var r in Screen.resolutions)
            {
                string label = $"{r.width} x {r.height}";
                if (!seen.Add(label)) continue; // deduplicar por tasa de refresco

                resolutions.Add(r);
                labels.Add(label);

                if (r.width == Screen.width && r.height == Screen.height)
                    currentIndex = resolutions.Count - 1;
            }

            resolutionDropdown.AddOptions(labels);
            resolutionDropdown.value = PlayerPrefs.GetInt(PrefResolution, currentIndex);
            resolutionDropdown.RefreshShownValue();
        }

        private void LoadAndApply()
        {
            SetVolume(masterSlider, MasterParam, PrefMaster);
            SetVolume(musicSlider, MusicParam, PrefMusic);
            SetVolume(sfxSlider, SFXParam, PrefSFX);

            if (fullscreenToggle != null)
            {
                bool fs = PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) == 1;
                fullscreenToggle.isOn = fs;
                Screen.fullScreen = fs;
            }

            ApplyResolution(resolutionDropdown != null ? resolutionDropdown.value : -1);
        }

        private void SetVolume(Slider slider, string param, string pref)
        {
            float v = PlayerPrefs.GetFloat(pref, 0.8f);
            if (slider != null) slider.SetValueWithoutNotify(v);
            ApplyVolume(param, v);
        }

        private void WireCallbacks()
        {
            if (masterSlider != null) masterSlider.onValueChanged.AddListener(v => OnVolumeChanged(MasterParam, PrefMaster, v));
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => OnVolumeChanged(MusicParam, PrefMusic, v));
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => OnVolumeChanged(SFXParam, PrefSFX, v));
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }

        // ==================== Callbacks ====================

        private void OnVolumeChanged(string param, string pref, float value)
        {
            ApplyVolume(param, value);
            PlayerPrefs.SetFloat(pref, value);
            PlayerPrefs.Save();
        }

        private void OnResolutionChanged(int index)
        {
            ApplyResolution(index);
            PlayerPrefs.SetInt(PrefResolution, index);
            PlayerPrefs.Save();
        }

        private void OnFullscreenChanged(bool value)
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(PrefFullscreen, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ==================== Aplicación ====================

        private void ApplyVolume(string param, float value01)
        {
            if (mixer == null) return;
            // 0..1 lineal -> dB logarítmico. Clamp para evitar log10(0) = -infinito.
            float dB = Mathf.Log10(Mathf.Clamp(value01, 0.0001f, 1f)) * 20f;
            mixer.SetFloat(param, dB);
        }

        private void ApplyResolution(int index)
        {
            if (index < 0 || index >= resolutions.Count) return;
            var r = resolutions[index];
            Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
        }
    }
}
