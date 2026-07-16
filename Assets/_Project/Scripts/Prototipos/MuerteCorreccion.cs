using System.Collections;
using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE v1.1;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Muerte como "corrección" (idea #6 del autor, 2026-07-15): en vez del
    /// respawn seco, ~0.4 s en los que el jugador se DES-RENDERIZA (el glitch
    /// fullscreen M_WorldSwitchGlitch con epicentro en él, reusado) y se
    /// RE-RENDERIZA en el checkpoint. No mueres: te corrigen.
    ///
    /// Se cuelga del seam <see cref="GameSession.RespawnInterceptor"/> (extensión
    /// retrocompatible): TODAS las muertes existentes (Corrector, vigilancia
    /// letal, suelo glicheado, caída al vacío) pasan por
    /// GameSession.RespawnPlayer, así que todas ganan la secuencia sin tocar
    /// ningún script de peligro. Con este componente ausente o sin material,
    /// el respawn vuelve a ser el clásico.
    ///
    /// Limitación conocida (aceptable en laboratorio): si el jugador muere en
    /// mitad de un cambio de mundo, este efecto y WorldSwitchEffectController
    /// escriben el MISMO material compartido a la vez y el glitch se mezcla.
    /// </summary>
    public class MuerteCorreccion : MonoBehaviour
    {
        [Tooltip("El mismo M_WorldSwitchGlitch del cambio de mundo (se reusa).")]
        [SerializeField] private Material glitchMaterial;
        [SerializeField] private Camera targetCamera;

        [Tooltip("Duración total de la corrección: mitad des-render, mitad re-render.")]
        [SerializeField, Min(0.1f)] private float duracion = 0.4f;

        [Tooltip("Tinte del glitch al corregir. Distinto de los tintes del cambio " +
                 "de mundo para que la muerte se LEA como otra cosa (Pilar 3).")]
        [SerializeField] private Color tintCorreccion = new Color(1f, 0.15f, 0.6f);

        [Header("Narrativa y sonido (paridad con PlayerSafePush: sonido + mensaje en CADA corrección)")]
        [SerializeField, TextArea] private string mensajeCorreccion =
            "[TEXTO PENDIENTE: mensaje de Keplin al corregir/detectar al jugador — tono administrativo, sin amenazar, tipo \"Keplin te ha detectado\"]";

        private static readonly int ProgressID = Shader.PropertyToID("_Progress");
        private static readonly int PlayerPosID = Shader.PropertyToID("_PlayerScreenPos");
        private static readonly int AspectID = Shader.PropertyToID("_AspectRatio");
        private static readonly int TintID = Shader.PropertyToID("_TintColor");
        private static readonly int SeedID = Shader.PropertyToID("_Seed");

        private PlayerController player;
        private Rigidbody2D playerRb;
        private SpriteRenderer playerSprite;
        private System.Func<bool> interceptor;
        private Coroutine activa;

        private void Awake()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerRb = player.GetComponent<Rigidbody2D>();
                playerSprite = player.GetComponent<SpriteRenderer>();
            }
            if (targetCamera == null) targetCamera = Camera.main;
            interceptor = InterceptarRespawn;
        }

        private void OnEnable() => GameSession.RespawnInterceptor = interceptor;

        private void OnDisable()
        {
            if (GameSession.RespawnInterceptor == interceptor)
                GameSession.RespawnInterceptor = null;
        }

        private bool InterceptarRespawn()
        {
            // Corrección en curso: tragar las llamadas repetidas (los peligros
            // matan también desde OnTriggerStay, cada frame).
            if (activa != null) return true;

            if (glitchMaterial == null || player == null || playerSprite == null ||
                targetCamera == null || GameSession.Instance == null)
                return false; // sin piezas: que corra el respawn clásico

            activa = StartCoroutine(Corregir());
            return true;
        }

        private IEnumerator Corregir()
        {
            // Sonido de muerte + mensaje de Keplin al momento de la detección,
            // igual que PlayerSafePush.Respawn() con el atrapamiento en paredes:
            // en CADA corrección, no solo la primera (distinto de
            // KeplinFirstDeathReaction, que es un aviso narrativo one-shot).
            AudioManager.Instance?.PlayDeath();
            LaProyeccion.Narrative.KeplinMessageController.Instance?.ShowMessage(mensajeCorreccion);

            // Congelar: sin física (no re-dispara triggers letales) y sin input
            // (PlayerController apagado también detiene su chequeo de caída).
            playerRb.simulated = false;
            player.enabled = false;

            glitchMaterial.SetColor(TintID, tintCorreccion);
            if (glitchMaterial.HasProperty(SeedID))
                glitchMaterial.SetFloat(SeedID, Random.Range(0f, 1000f));

            float mitad = duracion * 0.5f;

            // FASE 1 — des-render: glitch 0→1 con epicentro donde murió.
            for (float t = 0f; t < mitad; t += Time.deltaTime)
            {
                AplicarGlitch(Mathf.Clamp01(t / mitad));
                yield return null;
            }

            // Pico del glitch: el jugador desaparece y ocurre el respawn clásico
            // (teletransporte + OnPlayerRespawned, como siempre).
            playerSprite.enabled = false;
            GameSession.Instance.RespawnPlayerImmediate();

            // FASE 2 — re-render: glitch 1→0 con epicentro en el checkpoint;
            // el sprite reaparece a media disolución (se "materializa").
            for (float t = 0f; t < mitad; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Clamp01(t / mitad);
                if (k < 0.5f && !playerSprite.enabled) playerSprite.enabled = true;
                AplicarGlitch(k);
                yield return null;
            }

            glitchMaterial.SetFloat(ProgressID, 0f);
            playerSprite.enabled = true;
            player.enabled = true;
            playerRb.simulated = true;
            activa = null;
        }

        private void AplicarGlitch(float progreso)
        {
            // Epicentro recalculado cada frame: la cámara sigue moviéndose
            // (Cinemachine viaja hacia el checkpoint durante la fase 2).
            Vector2 screenPos = targetCamera.WorldToViewportPoint(player.transform.position);
            glitchMaterial.SetVector(PlayerPosID, screenPos);
            glitchMaterial.SetFloat(AspectID, (float)Screen.width / Screen.height);
            glitchMaterial.SetFloat(ProgressID, progreso);
        }

        private void OnApplicationQuit()
        {
            // Mismos valores DETERMINISTAS que escribe WorldSwitchEffectController
            // en su propio OnApplicationQuit: el .mat es compartido y el orden de
            // ejecución entre ambos no está garantizado — si escribieran valores
            // distintos, el material quedaría sucio en git según quién corra último
            // (bug visto el 2026-07-11 con _Seed).
            if (glitchMaterial == null) return;
            glitchMaterial.SetFloat(ProgressID, 0f);
            glitchMaterial.SetVector(PlayerPosID, new Vector4(0.5f, 0.5f, 0f, 0f));
            glitchMaterial.SetFloat(AspectID, 16f / 9f);
            glitchMaterial.SetColor(TintID, new Color(0.6f, 0.6f, 0.8f));
            if (glitchMaterial.HasProperty(SeedID))
                glitchMaterial.SetFloat(SeedID, 0f);
        }
    }
}
