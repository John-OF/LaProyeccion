using UnityEngine;
using UnityEngine.Rendering.Universal;
using LaProyeccion.Puzzles;

namespace LaProyeccion.World
{
    /// <summary>
    /// SISTEMA DEL JUEGO (`Zona1` desde 2026-08-14). LA PULSERA DEL PREDECESOR
    /// (`ALCANCE.md` §4 v1.6, decisión del autor 2026-08-13): el aparato del cambio deja de
    /// ser un banco de zócalos y pasa a ser **un objeto que se lleva puesto**; cada pieza
    /// recogida **sube su brillo**, y ese brillo es el único contador que existe (sin HUD).
    ///
    /// Dónde vive en el nivel 1 (decisión del autor 2026-08-14): en el **recoveco de la P3**,
    /// la planta que Keplin no mantiene. Encontrarla ahí, apagada y sin dueño, es la lectura
    /// que §4 v1.6 pide ("sobre sus restos"): explica sin una línea de texto por qué seguía
    /// allí. Y obliga a ver la P3 **vacía antes** de que la pieza le traiga los rastros, que
    /// era el agujero del beat 1→5 (`pendientes.md`, 12-ago).
    ///
    /// **La pieza NO EXISTE hasta que llevas la pulsera** (decisión del autor, 2026-08-14): no es
    /// un candado que te dice que no, es que todavía no hay nada que coger. Cierra el orden sin
    /// negarle nada al jugador — y de paso el vestíbulo repite el gesto de la P3: un sitio que no
    /// tenía nada pasa a tener algo.
    ///
    /// DOS TIEMPOS, y son el motivo de que esto no sea un pickup cualquiera:
    ///   1. **La coges y no hace nada.** Un cacharro muerto de otra persona.
    ///   2. **La pieza del vestíbulo la enciende** (Etapa 1 · Deshilacha). El destello de la
    ///      idea 1 deja de ser "el mundo parpadea" y pasa a ser *esto que llevas puesto se ha
    ///      encendido*.
    ///
    /// A escala real la muñeca son 2-3 px (PPU 32, jugador 19 × 38), así que **al ponérsela se
    /// apaga su sprite y lo único que queda es la luz**: un punto sobre la silueta, que es el
    /// vocabulario Inside que pide `ALCANCE.md`. El sprite existe solo mientras está en el suelo,
    /// para poder verla y cogerla.
    ///
    /// Subclase de <see cref="Interactable"/> y no UnityEvent en el Inspector, por lo mismo que
    /// <see cref="SalidaDelNivel"/>: el verbo tiene efecto fijo y no puede quedarse a medio
    /// cablear. Vigila la pieza por sondeo en vez de engancharse a ella (`PiezaDesactivador` no
    /// expone evento y es código validado: no se toca para probar una idea).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PulseraDelPredecesor : Interactable
    {
        [Header("Quién la lleva")]
        [Tooltip("El jugador. Al cogerla se cuelga de aquí para que la luz lo siga.")]
        [SerializeField] private Transform portador;
        [SerializeField] private Vector3 offsetLocal = new Vector3(0.15f, 0.10f, 0f);

        [Header("El brillo ES el contador (ALCANCE §4 v1.6)")]
        [Tooltip("Vacío = se busca un Light2D en este objeto o en sus hijos.")]
        [SerializeField] private Light2D luz;
        [Tooltip("Etapa 0: encontrada y puesta, pero sin ninguna pieza. Tiene que verse " +
                 "POCO: es lo que hace que encenderla signifique algo.")]
        [SerializeField, Min(0f)] private float intensidadApagada = 0.30f;
        [SerializeField, Min(0f)] private float radioApagado = 1.20f;
        [Tooltip("Etapa 1 (Deshilacha), al recoger la primera pieza.")]
        [SerializeField, Min(0f)] private float intensidadEtapa1 = 1.10f;
        [SerializeField, Min(0f)] private float radioEtapa1 = 2.60f;
        [Tooltip("Lo que tarda en subir el brillo. Que se vea SUBIR, no que aparezca.")]
        [SerializeField, Min(0.05f)] private float subida = 0.9f;

        [Header("Qué la enciende")]
        [Tooltip("La pieza del vestíbulo. Al dejar de estar Suelta, sube el brillo.")]
        [SerializeField] private PiezaDesactivador pieza;

        [Header("Qué APARECE al cogerla (decisión del autor, 2026-08-14)")]
        [Tooltip("Objetos que no existen hasta que se lleva la pulsera — hoy, la pieza del " +
                 "vestíbulo. No es un candado que dice que no: es que todavía no hay nada que " +
                 "coger, que es la forma limpia de cerrar un verbo (Pilar 3: no se ofrece algo " +
                 "para luego negarlo).\n\n" +
                 "Array de GameObjects y no UnityEvent por lo mismo que `DestelloAlRecoger`: un " +
                 "evento a medio cablear no da error, y aquí un fallo silencioso deja la pieza " +
                 "cogible sin pulsera.")]
        [SerializeField] private GameObject[] revela;

        /// <summary>¿La lleva puesta el jugador? Lo consulta el verbo de bajar al túnel.</summary>
        public static bool Llevada { get; private set; }

        /// <summary>Etapas encendidas (hoy 0 o 1). Preparado para cuando cuente de verdad.</summary>
        public static int Etapas { get; private set; }

        private Collider2D disparador;
        private SpriteRenderer sr;
        private bool encendida;
        private float t = -1f;
        private float desdeIntensidad;
        private float desdeRadio;

        private void Awake()
        {
            // Estático + laboratorio que RECARGA la escena al salir: sin esto, la segunda
            // partida arrancaría con la pulsera puesta sin haberla cogido, y el túnel abierto.
            Llevada = false;
            Etapas = 0;

            disparador = GetComponent<Collider2D>();
            sr = GetComponent<SpriteRenderer>();
            if (luz == null) luz = GetComponentInChildren<Light2D>(true);

            AplicarLuz(intensidadApagada, radioApagado);

            // Se apagan AQUÍ y no solo en la escena: dejar uno encendido por descuido en el
            // Inspector abriría el candado sin que nada avise, y el laboratorio recarga la
            // escena al salir. Lo que manda es el estado del juego, no cómo quedó guardado.
            Ocultar();
        }

        private void Ocultar()
        {
            if (revela == null) return;
            foreach (var go in revela)
                if (go != null) go.SetActive(false);
        }

        public override void Interact()
        {
            if (Llevada) return;

            // base.Interact() dispara OnInteract y consume el oneShot: `ProximityHint` se
            // auto-engancha ahí para apagarse.
            base.Interact();

            Llevada = true;
            if (disparador != null) disparador.enabled = false;
            if (sr != null) sr.enabled = false;

            if (portador != null)
            {
                transform.SetParent(portador, false);
                transform.localPosition = offsetLocal;
                transform.localScale = Vector3.one;
            }

            if (revela != null)
            {
                foreach (var go in revela)
                    if (go != null) go.SetActive(true);
            }
        }

        private void Update()
        {
            if (!encendida)
            {
                if (!Llevada || pieza == null) return;
                if (pieza.EstadoActual == PiezaDesactivador.Estado.Suelta) return;

                encendida = true;
                Etapas = 1;
                t = 0f;
                desdeIntensidad = luz != null ? luz.intensity : 0f;
                desdeRadio = luz != null ? luz.pointLightOuterRadius : 0f;
                return;
            }

            if (t < 0f || t >= subida) return;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / subida);
            AplicarLuz(Mathf.Lerp(desdeIntensidad, intensidadEtapa1, k),
                       Mathf.Lerp(desdeRadio, radioEtapa1, k));
        }

        private void AplicarLuz(float intensidad, float radio)
        {
            if (luz == null) return;
            luz.intensity = intensidad;
            luz.pointLightOuterRadius = radio;
            luz.pointLightInnerRadius = radio * 0.25f;
        }
    }
}
