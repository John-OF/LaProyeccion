using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): espora fosforescente (T5, ideas.md
    /// §Trampas). La ÚNICA luz de la sala ES lo que te daña. En reposo brilla suave y
    /// tocarla no mata; al entrar el jugador en su halo FLORECE (~1,5-2 s: más brillo,
    /// pulso más rápido) y REVIENTA → corrige a quien esté dentro. SALIR del halo cancela
    /// la rampa. El indicador de peligro es la propia luz: cuanto mejor ves, más cerca de
    /// morir estás. Patrón de presencia (SueloGlicheadoParpadeante: el peligro es estar
    /// dentro cuando letaliza), reloj → presencia.
    ///
    /// Capa de puzzle: una PIEDRA lanzada dentro del halo la COMPROMETE — revienta sí o sí,
    /// aunque el jugador esté lejos → cruzas seguro pero dejas esa zona a oscuras.
    ///
    /// El REVENTÓN se telegrafía con fogonazo de luz + estallido de partículas (código, sin
    /// arte aún) para que se LEA la muerte; la corrección se retrasa un instante para que el
    /// fogonazo se vea antes del corte de cámara. SIN sistema de vida (un reventón = una
    /// corrección).
    ///
    /// [PENDIENTE]: arte propio de la espora + shader/partículas definitivos; SFX de
    /// carga ascendente y de reventón (hoy el aviso es la luz y solo suena la corrección).
    /// </summary>
    public class EsporaFosforescente : MonoBehaviour
    {
        [Header("Halo (detección y reventón)")]
        [Tooltip("Radio del halo: dispara la floración y define a quién corrige al reventar.")]
        [SerializeField, Min(0.5f)] private float radioHalo = 2.2f;

        [Header("Luz")]
        [SerializeField] private Color color = new Color(0.55f, 1f, 0.6f, 1f);
        [SerializeField] private float intensidadReposo = 0.6f;
        [SerializeField] private float radioLuzReposo = 2.5f;
        [SerializeField] private float intensidadMax = 2.6f;
        [SerializeField] private float radioLuzMax = 5f;

        [Header("Floración")]
        [Tooltip("Tiempo dentro del halo hasta reventar. Salir cancela la rampa.")]
        [SerializeField, Min(0.2f)] private float duracionFloracion = 1.8f;
        [Tooltip("Al salir del halo, la rampa baja este factor más rápido de lo que sube.")]
        [SerializeField, Min(0.5f)] private float factorCancelacion = 2f;
        [Tooltip("Segundos a oscuras (gastada) antes de volver a reposo.")]
        [SerializeField, Min(0.1f)] private float retardoReaparece = 4f;

        [Header("Reventón")]
        [Tooltip("Retardo entre el fogonazo y la corrección, para que se vea el estallido.")]
        [SerializeField, Min(0f)] private float retardoCorreccion = 0.1f;
        [SerializeField, Min(4)] private int particulas = 34;
        [Tooltip("Si está marcado, la espora desaparece para siempre tras reventar (un solo uso). " +
                 "Si no, se recarga de forma infinita tras 'retardoReaparece'.")]
        [SerializeField] private bool desapareceAlReventar = false;

        private Light2D luz;
        private Material matParticulas;
        private float rampa;        // 0..1 (reposo → reventón)
        private bool comprometida;  // detonada por piedra: revienta pase lo que pase
        private bool gastada;

        private void Awake()
        {
            luz = gameObject.AddComponent<Light2D>();
            luz.lightType = Light2D.LightType.Point;
            luz.color = color;
            matParticulas = new Material(Shader.Find("Sprites/Default"));
            AplicarLuz(0f);
        }

        private void Update()
        {
            if (gastada) return;

            bool jugadorDentro = false;
            var hits = Physics2D.OverlapCircleAll(transform.position, radioHalo);
            foreach (var h in hits)
            {
                if (h.GetComponentInParent<PlayerController>() != null) jugadorDentro = true;
                if (h.GetComponent<PiedraLanzada>() != null) comprometida = true;
            }

            if (jugadorDentro || comprometida)
                rampa += Time.deltaTime / duracionFloracion;
            else
                rampa -= Time.deltaTime / duracionFloracion * factorCancelacion;
            rampa = Mathf.Clamp01(rampa);

            AplicarLuz(rampa);

            if (rampa >= 1f) StartCoroutine(Reventar(jugadorDentro));
        }

        private void AplicarLuz(float k)
        {
            // Pulso que se acelera con la rampa (telegrafía: florece "más rápido").
            float freq = Mathf.Lerp(2f, 14f, k);
            float pulso = 1f + Mathf.Sin(Time.time * freq) * 0.25f * k;
            luz.intensity = Mathf.Lerp(intensidadReposo, intensidadMax, k) * pulso;
            luz.pointLightOuterRadius = Mathf.Lerp(radioLuzReposo, radioLuzMax, k);
            luz.pointLightInnerRadius = luz.pointLightOuterRadius * 0.15f;
        }

        private IEnumerator Reventar(bool letal)
        {
            gastada = true; // corta reentradas desde Update

            MostrarRadioReventon();  // disco que marca el ALCANCE exacto (para que la muerte sea justa)
            EstallidoDeParticulas();

            // Fogonazo con decaimiento; a mitad se dispara la corrección para que se vea.
            float t = 0f, dur = 0.28f;
            float iPeak = intensidadMax * 2f, rPeak = radioLuzMax * 1.2f;
            bool corregido = false;
            while (t < dur)
            {
                t += Time.deltaTime;
                luz.intensity = iPeak * (1f - t / dur);
                luz.pointLightOuterRadius = rPeak;
                if (!corregido && letal && t >= retardoCorreccion)
                {
                    corregido = true;
                    AudioManager.Instance?.PlayDeath();
                    GameSession.Instance?.RespawnPlayer();
                }
                yield return null;
            }

            // Gastada: a oscuras (si la reventaste con piedra, dejaste la zona sin luz).
            luz.intensity = 0f;
            luz.pointLightOuterRadius = 0f;

            if (desapareceAlReventar)
            {
                // Un solo uso: se apaga el marcador y no se reconstruye.
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;
                yield break;
            }

            yield return new WaitForSeconds(retardoReaparece);

            rampa = 0f;
            comprometida = false;
            gastada = false;
            AplicarLuz(0f);
        }

        private static Sprite discoSprite;

        /// <summary>
        /// Textura circular (una vez, compartida): relleno translúcido + borde brillante.
        /// El borde marca el LÍMITE exacto del radio; el corte es a radio 1 (nada fuera).
        /// </summary>
        private static Sprite DiscoSprite()
        {
            if (discoSprite != null) return discoSprite;
            const int S = 128;
            float R = S * 0.5f;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - R, dy = y + 0.5f - R;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / R;
                    float a;
                    if (d > 1.02f) a = 0f;
                    else
                    {
                        float fill = 0.16f;                                        // interior: "todo esto mata"
                        float ring = Mathf.SmoothStep(0f, 1f, (d - 0.72f) / 0.28f) * 0.8f; // borde: el límite
                        a = Mathf.Max(fill, ring);
                        if (d > 1f) a *= Mathf.Clamp01((1.02f - d) / 0.02f);        // corte limpio en el radio
                    }
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            discoSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
            return discoSprite;
        }

        /// <summary>Disco que cubre EXACTAMENTE el radio letal (radioHalo) y se desvanece.</summary>
        private void MostrarRadioReventon()
        {
            var go = new GameObject("EsporaReventonRadio");
            go.transform.position = transform.position;
            go.transform.localScale = Vector3.one * (radioHalo * 2f); // sprite = 1 u ⇒ diámetro = 2·radioHalo
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DiscoSprite();
            sr.sharedMaterial = matParticulas;               // Sprites/Default (unlit): visible en la negrura
            sr.color = Color.Lerp(color, Color.white, 0.4f); // flash de reventón, más brillante que el reposo
            sr.sortingOrder = 34;
            StartCoroutine(FundirDisco(sr, go));
        }

        private IEnumerator FundirDisco(SpriteRenderer sr, GameObject go)
        {
            float t = 0f, dur = 0.4f;
            Color baseC = sr.color;
            while (t < dur && go != null)
            {
                t += Time.deltaTime;
                float k = 1f - t / dur;
                var c = baseC; c.a = k; sr.color = c;
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        /// <summary>Estallido radial de partículas de espora (código; arte definitivo pendiente).</summary>
        private void EstallidoDeParticulas()
        {
            var go = new GameObject("EsporaReventon");
            go.transform.position = transform.position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = 0.55f;
            main.startSpeed = 4.5f;
            main.startSize = 0.28f;
            main.startColor = color;
            main.gravityModifier = 0.35f;
            main.maxParticles = particulas + 10;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particulas) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = matParticulas;
            rend.sortingOrder = 35;

            ps.Play();
            Destroy(go, 1.2f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 1f, 0.6f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radioHalo);
        }
    }
}
