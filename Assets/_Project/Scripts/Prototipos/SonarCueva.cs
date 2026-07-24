using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): el pulso del radar se vuelve
    /// SONAR. En la oscuridad de la cueva, Sondear (Q / botón Norte — la MISMA
    /// acción que el radar) emite un PULSO DE LUZ expansivo que revela la geometría
    /// un instante y se apaga, más el anillo/ping visible (reusa RadarRing).
    ///
    /// No revela "el otro mundo" (de eso se encarga <see cref="RadarPulseController"/>):
    /// en la cueva no hay simulación que ver, solo el Real a oscuras. Por eso en esta
    /// escena se DESACTIVA el RadarPulseController del jugador y este toma la tecla.
    ///
    /// Gratis en el prototipo (el coste — Semilla u otro — es decisión de diseño
    /// posterior). Cooldown para que no sea una linterna permanente: el sonar es un
    /// vistazo, no iluminación sostenida.
    /// </summary>
    public class SonarCueva : MonoBehaviour
    {
        [Header("Pulso de luz (el revelado)")]
        [Tooltip("Radio máximo del pulso de luz en unidades de mundo.")]
        [SerializeField] private float radioMax = 14f;
        [SerializeField, Min(0.1f)] private float duracion = 0.9f;
        [SerializeField] private float intensidadInicial = 1.3f;
        [Tooltip("Menta viva (#8CFFBF): la única luz de la cueva.")]
        [SerializeField] private Color colorSonar = new Color(0.6f, 1f, 0.8f, 1f);
        [Tooltip("Tiempo mínimo entre pulsos: el sonar es un vistazo, no una linterna.")]
        [SerializeField, Min(0f)] private float cooldown = 1.2f;

        [Header("Anillo (ping visible)")]
        [SerializeField] private Sprite ringSprite;
        [SerializeField] private float ringRadioMax = 14f;
        [SerializeField, Min(0.1f)] private float ringDuracion = 1.1f;

        private PlayerInputActions input;
        private float ultimoPulso = -999f;
        private bool activo;

        private void Awake()
        {
            input = new PlayerInputActions();
        }

        private void OnEnable()
        {
            if (input == null) return; // guard del domain reload en Play (patrón del proyecto)
            input.Player.Sondear.performed += OnSondear;
            input.Player.Sondear.Enable();
        }

        private void OnDisable()
        {
            if (input == null) return;
            input.Player.Sondear.performed -= OnSondear;
            input.Player.Sondear.Disable();
        }

        private void OnSondear(InputAction.CallbackContext _)
        {
            if (Time.timeScale == 0f) return;                 // no en pausa
            if (activo || Time.time - ultimoPulso < cooldown) return;
            ultimoPulso = Time.time;
            AudioManager.Instance?.PlayRadarPulse();           // debe "sonar caro"
            StartCoroutine(Pulso());
            SpawnRing();
        }

        private IEnumerator Pulso()
        {
            activo = true;
            var go = new GameObject("SonarPulse");
            go.transform.position = transform.position;        // anclado al punto de disparo
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = colorSonar;

            float t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duracion);
                float eased = 1f - (1f - k) * (1f - k);        // expansión desacelerada
                float r = Mathf.Lerp(1.5f, radioMax, eased);
                l.pointLightOuterRadius = r;
                l.pointLightInnerRadius = r * 0.2f;
                l.intensity = intensidadInicial * (1f - k);     // se apaga al expandirse
                yield return null;
            }
            Destroy(go);
            activo = false;
        }

        private void SpawnRing()
        {
            if (ringSprite == null) return;
            var go = new GameObject("SonarRing");
            go.transform.position = transform.position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ringSprite;
            sr.color = colorSonar;
            sr.sortingOrder = 40;
            StartCoroutine(RingRoutine(go.transform, sr));
        }

        private IEnumerator RingRoutine(Transform ring, SpriteRenderer sr)
        {
            Color baseC = sr.color;
            float t = 0f;
            while (t < ringDuracion && ring != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / ringDuracion);
                float eased = 1f - (1f - k) * (1f - k);
                ring.localScale = Vector3.one * Mathf.Lerp(0.4f, ringRadioMax, eased);
                var c = baseC;
                c.a = baseC.a * (1f - k);
                sr.color = c;
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }
    }
}
