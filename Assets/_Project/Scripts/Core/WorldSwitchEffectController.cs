using System.Collections;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Anima el material fullscreen del glitch durante 0.3s al cambiar de mundo.
    /// El epicentro del efecto es la posición del jugador en pantalla.
    /// </summary>
    public class WorldSwitchEffectController : MonoBehaviour
    {
        [SerializeField] private Material glitchMaterial;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Camera targetCamera;

        [Tooltip("Debe coincidir con la transición del WorldPostProcessController.")]
        [SerializeField, Min(0.05f)] private float duration = 0.3f;

        [Tooltip("Color cyan suave al ir hacia Simulación, blanco/azul al ir hacia Real.")]
        [SerializeField] private Color tintToSimulation = new Color(0.4f, 0.9f, 1f);
        [SerializeField] private Color tintToReal = new Color(0.6f, 0.6f, 0.8f);

        private static readonly int ProgressID = Shader.PropertyToID("_Progress");
        private static readonly int PlayerPosID = Shader.PropertyToID("_PlayerScreenPos");
        private static readonly int AspectID = Shader.PropertyToID("_AspectRatio");
        private static readonly int TintID = Shader.PropertyToID("_TintColor");
        // Semilla por disparo (shader WorldSwitchGlitchBrutal): cada cambio glitchea
        // distinto. Con guard: el shadergraph anterior no tiene la propiedad.
        private static readonly int SeedID = Shader.PropertyToID("_Seed");

        private Coroutine activeFx;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            // Importante: arrancar invisible.
            if (glitchMaterial != null) glitchMaterial.SetFloat(ProgressID, 0f);
        }

        private void OnEnable() => WorldManager.OnWorldChanged += OnWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= OnWorldChanged;

        private void OnWorldChanged(WorldState newWorld)
        {
            if (glitchMaterial == null || playerTransform == null) return;

            if (activeFx != null) StopCoroutine(activeFx);
            activeFx = StartCoroutine(PlayGlitch(newWorld));
        }

        private IEnumerator PlayGlitch(WorldState newWorld)
        {
            glitchMaterial.SetColor(TintID,
                newWorld == WorldState.Simulation ? tintToSimulation : tintToReal);
            if (glitchMaterial.HasProperty(SeedID))
                glitchMaterial.SetFloat(SeedID, Random.Range(0f, 1000f));

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);

                // Recalcular cada frame: el jugador se mueve durante el efecto.
                Vector2 screenPos = targetCamera.WorldToViewportPoint(playerTransform.position);
                glitchMaterial.SetVector(PlayerPosID, screenPos);
                glitchMaterial.SetFloat(AspectID, (float)Screen.width / Screen.height);
                glitchMaterial.SetFloat(ProgressID, k);

                yield return null;
            }

            glitchMaterial.SetFloat(ProgressID, 0f);
            activeFx = null;
        }

        private void OnApplicationQuit()
        {
            // Reset por si se queda con valor activado al cerrar Play.
            // Valores DETERMINISTAS en todas las propiedades que animamos: lo que
            // quede escrito distinto persiste en el .mat compartido y ensucia git
            // tras cada sesión de Play (bug visto el 2026-07-11 con _Seed).
            if (glitchMaterial == null) return;
            glitchMaterial.SetFloat(ProgressID, 0f);
            glitchMaterial.SetVector(PlayerPosID, new Vector4(0.5f, 0.5f, 0f, 0f));
            glitchMaterial.SetFloat(AspectID, 16f / 9f);
            if (glitchMaterial.HasProperty(SeedID))
                glitchMaterial.SetFloat(SeedID, 0f);
        }
    }
}