using UnityEngine;
using UnityEngine.Rendering.Universal;
using LaProyeccion.Core;

namespace LaProyeccion.Player
{
    /// <summary>
    /// LA PULSERA, VISTA (`ALCANCE.md` §4 v1.6). Vive en el JUGADOR y es lo único que traduce
    /// <see cref="EstadoDelAparato"/> a algo que se ve. No guarda estado: lo escucha.
    ///
    /// **Por qué está en el jugador y no en un objeto suelto:** a PPU 32 el jugador mide 19 × 38 px
    /// y una muñeca son 2-3 px. La pulsera **no se lee como un objeto**, se lee como un punto de luz
    /// sobre la silueta — así que colgar un sprite del personaje era ruido que no aportaba. Lo que se
    /// lee es **intensidad y radio**, que es exactamente lo que ALCANCE pide que sea el contador.
    ///
    /// **Dos salidas, y la segunda es para el arte que aún no existe:**
    ///   1. **La luz** (`Light2D`): funciona hoy, con el arte placeholder.
    ///   2. **Un `int` en el Animator** (`etapaPulsera`): 0 = sin pulsera · 1 = pulsera apagada ·
    ///      2..5 = pulsera + 1..4 piezas. El día que existan las hojas de sprites con la pulsera
    ///      dibujada, se enchufa un `AnimatorOverrideController` por etapa y **no se toca una línea
    ///      de código**. Si el parámetro no existe en el controlador, no se escribe: nada de llenar
    ///      la consola de avisos por un canal que todavía no está montado.
    ///
    /// Sin HUD, sin contador, sin nada colgando: el brillo ES el contador, y es el único.
    /// </summary>
    public class PulseraVisual : MonoBehaviour
    {
        [Header("La luz (canal de hoy)")]
        [Tooltip("Vacío = se busca un Light2D en los hijos. Se apaga si no llevas la pulsera.")]
        [SerializeField] private Light2D luz;

        [Tooltip("Intensidad por etapa: [0]=pulsera sin piezas … [4]=aparato completo.")]
        [SerializeField]
        private float[] intensidadPorEtapa = { 0.30f, 1.10f, 1.35f, 1.60f, 1.90f };

        [Tooltip("Radio exterior por etapa, mismo orden.")]
        [SerializeField]
        private float[] radioPorEtapa = { 1.20f, 2.60f, 3.00f, 3.40f, 3.80f };

        [Tooltip("Lo que tarda en subir el brillo al sumar una pieza. Que se vea SUBIR.")]
        [SerializeField, Min(0.05f)] private float subida = 0.9f;

        [Header("El punto en la mano (provisional, hasta que haya arte)")]
        [Tooltip("Un cuadradito de ~3 px que se enciende al llevar la pulsera. NO es el arte " +
                 "definitivo: el día que la silueta lleve la pulsera dibujada, esto se apaga y " +
                 "manda el sprite. Mientras tanto, que se vea DÓNDE está la pulsera y no solo su " +
                 "resplandor.")]
        [SerializeField] private SpriteRenderer punto;
        [Tooltip("A qué distancia del centro va la mano. Se espeja según hacia dónde mira.")]
        [SerializeField] private Vector2 offsetMano = new Vector2(0.20f, -0.05f);
        [Tooltip("Renderer del jugador, para saber hacia dónde mira. El giro es por flipX, no " +
                 "por escala, así que un hijo NO se voltea solo: hay que espejarlo a mano.")]
        [SerializeField] private SpriteRenderer visualJugador;
        [SerializeField] private Color colorApagada = new Color(0.45f, 0.75f, 0.92f);
        [SerializeField] private Color colorCompleta = new Color(0.95f, 1f, 1f);

        [Header("El sprite (canal de mañana)")]
        [Tooltip("Vacío = se busca en este objeto o en sus hijos.")]
        [SerializeField] private Animator animator;
        [SerializeField] private string parametroEtapa = "etapaPulsera";

        private float intensidadDestino;
        private float radioDestino;
        private float intensidadOrigen;
        private float radioOrigen;
        private float t = -1f;
        private bool tieneParametro;

        private void Awake()
        {
            if (luz == null) luz = GetComponentInChildren<Light2D>(true);
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (visualJugador == null)
            {
                var pc = GetComponent<PlayerController>();
                if (pc != null) visualJugador = pc.GetComponentInChildren<SpriteRenderer>(true);
            }

            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var p in animator.parameters)
                    if (p.type == AnimatorControllerParameterType.Int && p.name == parametroEtapa)
                        tieneParametro = true;
        }

        private void OnEnable()
        {
            EstadoDelAparato.OnCambiado += Aplicar;
            Aplicar(instantaneo: true);
        }

        private void OnDisable()
        {
            EstadoDelAparato.OnCambiado -= Aplicar;
        }

        private void Aplicar() => Aplicar(instantaneo: false);

        private void Aplicar(bool instantaneo)
        {
            int etapa = EstadoDelAparato.TienePulsera ? EstadoDelAparato.Piezas : -1;

            if (tieneParametro) animator.SetInteger(parametroEtapa, etapa + 1);

            if (punto != null)
            {
                punto.enabled = etapa >= 0;
                if (etapa >= 0)
                {
                    float k = EstadoDelAparato.PiezasTotales > 0
                        ? Mathf.Clamp01(etapa / (float)EstadoDelAparato.PiezasTotales) : 0f;
                    punto.color = Color.Lerp(colorApagada, colorCompleta, k);
                }
            }

            if (luz == null) return;

            if (etapa < 0)
            {
                // Sin pulsera no hay luz que valga: se apaga el componente entero para que no
                // quede un halo tenue delatando algo que el jugador todavía no tiene.
                luz.enabled = false;
                t = -1f;
                return;
            }

            luz.enabled = true;
            intensidadDestino = Leer(intensidadPorEtapa, etapa, 1f);
            radioDestino = Leer(radioPorEtapa, etapa, 2.5f);

            if (instantaneo)
            {
                luz.intensity = intensidadDestino;
                luz.pointLightOuterRadius = radioDestino;
                luz.pointLightInnerRadius = radioDestino * 0.25f;
                t = -1f;
                return;
            }

            intensidadOrigen = luz.intensity;
            radioOrigen = luz.pointLightOuterRadius;
            t = 0f;
        }

        private void Update()
        {
            if (t < 0f || luz == null) return;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / subida);
            luz.intensity = Mathf.Lerp(intensidadOrigen, intensidadDestino, k);
            luz.pointLightOuterRadius = Mathf.Lerp(radioOrigen, radioDestino, k);
            luz.pointLightInnerRadius = luz.pointLightOuterRadius * 0.25f;

            if (k >= 1f) t = -1f;
        }

        /// <summary>
        /// La pulsera va en UNA mano, así que tiene que cambiar de lado al girarse. Se hace aquí
        /// y no colgando el punto del nodo visual porque el giro del jugador es `flipX` del
        /// renderer, y `flipX` **no voltea a los hijos**: solo afecta a su propio sprite.
        /// En LateUpdate para leer el giro que `PlayerController` acaba de decidir en su Update.
        /// </summary>
        private void LateUpdate()
        {
            if (!EstadoDelAparato.TienePulsera || visualJugador == null) return;

            float signo = visualJugador.flipX ? -1f : 1f;
            var p = new Vector3(offsetMano.x * signo, offsetMano.y, 0f);
            if (punto != null) punto.transform.localPosition = p;
            if (luz != null) luz.transform.localPosition = p;
        }

        private static float Leer(float[] tabla, int i, float porDefecto)
        {
            if (tabla == null || tabla.Length == 0) return porDefecto;
            return tabla[Mathf.Clamp(i, 0, tabla.Length - 1)];
        }
    }
}
