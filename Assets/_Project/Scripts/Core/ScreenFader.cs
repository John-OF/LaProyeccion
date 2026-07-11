using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Fundido a negro para transiciones de escena (F1.P3). Una instancia por
    /// escena (prefab PF_ScreenFader). API estática:
    /// <c>ScreenFader.FadeOutAndLoad("Zona2")</c> funde a negro (~0.5 s, tiempo
    /// NO escalado) y carga la escena; al cargar una escena que también tenga
    /// fader, esta arranca en negro y funde a transparente.
    /// Si la escena no tiene fader, carga directa sin fundido (fallback).
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField, Min(0.05f)] private float fadeDuration = 0.5f;
        [Tooltip("Imagen negra a pantalla completa que hace el fundido.")]
        [SerializeField] private Image blackout;

        // Cruza escenas: la escena destino arranca en negro si venimos de un fade.
        private static bool arrivingFromFade;

        private bool busy;

        private void Awake()
        {
            Instance = this;
            if (blackout != null)
            {
                SetAlpha(arrivingFromFade ? 1f : 0f);
                blackout.raycastTarget = arrivingFromFade;
            }
        }

        private void Start()
        {
            if (arrivingFromFade)
            {
                arrivingFromFade = false;
                StartCoroutine(Fade(1f, 0f, null));
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Funde a negro y carga la escena. Ignora llamadas repetidas durante el fundido.</summary>
        public static void FadeOutAndLoad(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (Instance != null)
            {
                Instance.BeginFadeOut(sceneName);
            }
            else
            {
                // Fallback sin fader en la escena actual: carga directa.
                arrivingFromFade = true;
                SceneManager.LoadScene(sceneName);
            }
        }

        private void BeginFadeOut(string sceneName)
        {
            if (busy) return;
            busy = true;
            StartCoroutine(Fade(0f, 1f, sceneName));
        }

        private IEnumerator Fade(float from, float to, string sceneToLoad)
        {
            if (blackout != null)
            {
                // Mientras oscurece, bloquear interacción de UI debajo.
                blackout.raycastTarget = to > from;

                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.unscaledDeltaTime;
                    SetAlpha(Mathf.Lerp(from, to, t / fadeDuration));
                    yield return null;
                }
                SetAlpha(to);
                if (to <= 0f) blackout.raycastTarget = false;
            }

            if (sceneToLoad != null)
            {
                arrivingFromFade = true;
                SceneManager.LoadScene(sceneToLoad);
            }
        }

        private void SetAlpha(float a)
        {
            if (blackout == null) return;
            var c = blackout.color;
            c.a = a;
            blackout.color = c;
        }
    }
}
