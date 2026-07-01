using LaProyeccion.Core;
using System.Collections;
using TMPro;
using UnityEngine;

namespace LaProyeccion.Narrative
{
    /// <summary>
    /// Muestra mensajes de Keplin con fade in/out.
    /// Voz neutra, calmada, jamás eleva el tono. GDD §3.2.
    /// </summary>
    public class KeplinMessageController : MonoBehaviour
    {
        public static KeplinMessageController Instance { get; private set; }

        [SerializeField] private TMP_Text messageText;
        [SerializeField] private float fadeInDuration = 0.6f;
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeOutDuration = 1f;

        private Coroutine activeMessage;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (messageText != null) messageText.gameObject.SetActive(false);
        }

        public void ShowMessage(string text)
        {
            // Reproducir sonido de mensaje de Keplin
            AudioManager.Instance?.PlayKeplinMessage();

            if (messageText == null) { Debug.LogWarning("[Keplin] Sin TMP_Text asignado."); return; }

            if (activeMessage != null) StopCoroutine(activeMessage);
            activeMessage = StartCoroutine(MessageRoutine(text));
        }

        private IEnumerator MessageRoutine(string text)
        {
            messageText.text = text;
            messageText.gameObject.SetActive(true);

            // Fade in
            yield return Fade(0f, 1f, fadeInDuration);

            // Hold
            yield return new WaitForSeconds(displayDuration);

            // Fade out
            yield return Fade(1f, 0f, fadeOutDuration);

            messageText.gameObject.SetActive(false);
            activeMessage = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            Color c = messageText.color;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                c.a = Mathf.Lerp(from, to, k);
                messageText.color = c;
                yield return null;
            }
            c.a = to;
            messageText.color = c;
        }
    }
}