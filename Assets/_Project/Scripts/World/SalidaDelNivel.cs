using UnityEngine;
using UnityEngine.SceneManagement;
using LaProyeccion.Core;
using LaProyeccion.Narrative;
using LaProyeccion.Player;
using LaProyeccion.Puzzles;

namespace LaProyeccion.World
{
    /// <summary>
    /// SISTEMA DEL JUEGO (`Zona1` desde 2026-08-14). **Salir del nivel es un VERBO**, no
    /// un borde de pantalla: te plantas en la puerta y pulsas E.
    ///
    /// Qué se está probando (decisión del autor, 2026-08-12): que el final del nivel sea
    /// una **decisión explícita**. Mientras la salida sea "caminar de más", el jugador puede
    /// perder el contenido opcional sin haber elegido nada — y este nivel pasa a tener
    /// contenido opcional detrás de la primera pieza (ideas 1+5 de `_docs/ideas.md`). Con la
    /// puerta como interactuable, **cruzar es algo que se hace a propósito**.
    ///
    /// Subclase de <see cref="Interactable"/> en vez de UnityEvent en el Inspector: el verbo
    /// tiene una condición (la puerta abierta) y un efecto fijo, así que vive en código y no
    /// puede quedarse a medio cablear. `PlayerInteractor` hace `GetComponent&lt;Interactable&gt;()`,
    /// que encuentra subclases sin cambios.
    ///
    /// **2026-08-14 — ahora el nivel no termina en la puerta, sino BAJANDO AL TÚNEL.** La
    /// puerta vuelve a abrirse de verdad (la placa manda sobre `PuertaSimple`) y este verbo se
    /// muda a la boca del túnel, con una condición nueva del autor: **no se baja sin la
    /// pulsera ENCENDIDA** (`etapasMinimas = 1`). La razón es diegética y por eso la condición
    /// vive aquí y no en un Gate: ahí abajo no se ve nada, y la pulsera es la única luz.
    ///
    /// Una sola condición cierra los dos agujeros del nivel, y ese es el motivo de que se pida
    /// la ETAPA y no el objeto: la pieza no existe hasta que llevas la pulsera, así que exigir
    /// la etapa 1 exige también la pulsera. Sin esto, los dos objetos eran saltables — la
    /// pulsera vive en un recoveco opcional y la pieza está de paso — y salir sin ellos dejaría
    /// al nivel 2 arrancando con un aparato muerto, que es un fallo carísimo de diagnosticar
    /// hacia atrás. Con UN motivo de rechazo, además, el mismo aviso vale para los dos casos y
    /// no miente en ninguno: apagada la pulsera alumbra 1,2 u, y el hueco es más hondo.
    ///
    /// ⚠️ Mientras la puerta está cerrada **se apaga su propio collider**, no se limita a
    /// rechazar la pulsación: `PlayerInteractor` busca por `OverlapCircleAll`, así que sin
    /// collider el objeto no existe para él y **el cartel de tecla tampoco aparece**. Ofrecer
    /// un verbo que luego dice que no es exactamente lo que Pilar 3 prohíbe.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SalidaDelNivel : Interactable
    {
        [Header("Cuándo se puede salir")]
        [Tooltip("La puerta. Mientras esté apagado, este verbo no se ofrece siquiera.")]
        [SerializeField] private DualSwitch requiere;

        [Tooltip("Opcional. Con esto puesto, el verbo tampoco se ofrece hasta llevar la " +
                 "pulsera: al túnel no se baja a oscuras (decisión del autor, 2026-08-14).")]
        [SerializeField] private PulseraDelPredecesor requierePulsera;

        [Tooltip("Etapas encendidas que hace falta llevar. 0 = basta con tenerla puesta.\n\n" +
                 "1 pide la pulsera ENCENDIDA, y con eso una sola condición cierra dos agujeros: " +
                 "como la pieza no existe hasta que llevas la pulsera, exigir la etapa 1 exige las " +
                 "dos cosas. Y hay UN solo motivo de rechazo, así que el mismo aviso sirve para " +
                 "los dos casos y además es cierto en ambos: apagada alumbra 1,2 u y el hueco es " +
                 "más hondo que eso.")]
        [SerializeField, Min(0)] private int etapasMinimas = 0;

        [Header("Por qué NO se puede (Pilar 3: el verbo no falla en silencio)")]
        [Tooltip("Se piensa UNA vez al acercarse sin cumplir el requisito. Sin collider no " +
                 "hay cartel de tecla, así que sin esto el hueco no diría nada. TEXTO DEL " +
                 "AUTOR: aquí va el placeholder de la convención.")]
        [SerializeField, TextArea] private string pensamientoSinRequisito =
            "[TEXTO PENDIENTE: se asoma al hueco y no ve el fondo; no baja a oscuras]";
        [Tooltip("A qué distancia se piensa ese aviso.")]
        [SerializeField, Min(0.5f)] private float radioAviso = 2.5f;

        [Header("A dónde")]
        [Tooltip("Escena que carga al salir. VACÍO = recarga esta misma, que es lo que " +
                 "quieres en el laboratorio (repetible y no escribe en la partida). En el " +
                 "juego real aquí irá el nivel 2 de la Zona 1.")]
        [SerializeField] private string escenaSiguiente = "";

        private Collider2D disparador;
        private Transform jugador;
        private bool yaAvisado;

        /// <summary>Las dos condiciones a la vez. Un null es "esa condición no aplica":
        /// es un laboratorio, y un campo sin cablear no debe romper la prueba.</summary>
        private bool Permitido =>
            (requiere == null || requiere.IsOn) &&
            (requierePulsera == null ||
             (PulseraDelPredecesor.Llevada && PulseraDelPredecesor.Etapas >= etapasMinimas));

        private void Awake()
        {
            disparador = GetComponent<Collider2D>();
        }

        private void Start()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) jugador = pc.transform;

            if (requiere != null) requiere.OnStateChanged.AddListener(Aplicar);
            Reevaluar();
        }

        private void OnDestroy()
        {
            if (requiere != null) requiere.OnStateChanged.RemoveListener(Aplicar);
        }

        private void Update()
        {
            if (requierePulsera == null) return;

            // Sondeo, como `DestelloAlRecoger` con la pieza: la pulsera no expone evento.
            // Se compara contra el collider en vez de guardar una copia del estado — así no
            // hay una segunda verdad que pueda quedarse vieja al añadir condiciones.
            if (disparador != null && disparador.enabled != Permitido) Reevaluar();

            if (Permitido || yaAvisado || jugador == null) return;
            if (string.IsNullOrWhiteSpace(pensamientoSinRequisito)) return;
            if (Vector2.Distance(jugador.position, transform.position) > radioAviso) return;

            var pensamiento = PensamientoController.Instance;
            if (pensamiento == null) return;

            // Importante: es información que el jugador necesita para no quedarse mirando
            // un hueco que no responde. Se piensa una vez por vida de la escena.
            pensamiento.Pensar(pensamientoSinRequisito, null, PesoPensamiento.Importante);
            yaAvisado = true;
        }

        /// <summary>Firma que pide el UnityEvent&lt;bool&gt; del DualSwitch.</summary>
        private void Aplicar(bool _) => Reevaluar();

        private void Reevaluar()
        {
            if (disparador != null) disparador.enabled = Permitido;
        }

        public override void Interact()
        {
            if (!Permitido) return;

            // Dispara OnInteract y consume el oneShot: `ProximityHint` se auto-engancha a
            // ese evento para apagarse, así que saltárselo dejaría el hint creyendo que
            // el jugador nunca usó la puerta.
            base.Interact();

            // Que no se pueda seguir jugando por encima del fundido.
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                pc.enabled = false;
                var pi = pc.GetComponent<PlayerInteractor>();
                if (pi != null) pi.enabled = false;
            }

            string destino = string.IsNullOrEmpty(escenaSiguiente)
                ? SceneManager.GetActiveScene().name
                : escenaSiguiente;

            ScreenFader.FadeOutAndLoad(destino);
        }
    }
}
