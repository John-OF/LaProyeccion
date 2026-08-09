using TMPro;
using UnityEngine;
using LaProyeccion.Player;

namespace LaProyeccion.UI
{
    /// <summary>
    /// Cartelito diegético sobre un objeto: **"(T) coger"**, y en mando **"(X) coger"**
    /// o **"(Cuadrado) coger"** según con qué se esté jugando (GDD §7: "prompt de
    /// interacción solo por proximidad", sin HUD permanente).
    ///
    /// De dónde salen las teclas: NO se escriben a mano. Se le pasan las **rutas de
    /// binding reales** del verbo (`Configurar`) y `GlifosDeEntrada` las traduce al
    /// vocabulario del mando activo. Si un día se rebindea el verbo, el cartel cambia
    /// solo — mismo criterio que `ControlsPanel`, que dibuja desde los bindings en vez
    /// de pintar un diagrama que mentiría.
    ///
    /// Aparece solo dentro de `radio` y se redibuja al cambiar de dispositivo.
    /// El texto de la acción lo fija el dueño del verbo (`Accion`), porque solo él
    /// sabe si toca "coger" o "soltar".
    /// </summary>
    public class PromptDeTecla : MonoBehaviour
    {
        [Tooltip("Verbo al que pertenece. Su dueño llama a Configurar() en Awake con " +
                 "las rutas reales, para que no haya dos sitios donde cambiar la tecla.")]
        [SerializeField] private VerboLab verbo = VerboLab.PiezaDesactivador;

        [Tooltip("Qué se hace al pulsar. Lo puede cambiar el dueño en runtime.")]
        [SerializeField] private string accion = "coger";

        [Header("Presencia")]
        [Tooltip("A qué distancia aparece.")]
        [SerializeField, Min(0.5f)] private float radio = 2.2f;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.1f, 0f);
        [SerializeField] private float tamanoFuente = 2.2f;
        [SerializeField] private Color color = new Color(0.92f, 0.96f, 1f, 0.95f);

        /// <summary>A qué verbo pertenece el cartel: su dueño lo busca por aquí.</summary>
        // Se AÑADE al final a propósito: `verbo` se serializa por índice, así que
        // meter un valor en medio reetiquetaría todos los prompts ya colocados.
        public enum VerboLab { PiezaDesactivador, Piedra, Agarre }

        public VerboLab Verbo => verbo;

        private string rutaTeclado = "<Keyboard>/t";
        private string rutaMando = "<Gamepad>/buttonWest";
        private TextMeshPro texto;
        private Transform jugador;
        private bool visible;
        private bool forzadoOculto;

        /// <summary>Lo llama el dueño del verbo con SUS rutas: única fuente de verdad.</summary>
        public void Configurar(string teclado, string mando)
        {
            if (rutaTeclado == teclado && rutaMando == mando) return;
            rutaTeclado = teclado;
            rutaMando = mando;
            Redibujar();
        }

        /// <summary>
        /// Para verbos que SÍ están en `PlayerInputActions` (interactuar, saltar…):
        /// saca del propio asset la primera ruta de teclado y la primera de mando, así
        /// que un rebinding se refleja en el cartel sin tocar nada.
        /// </summary>
        public void Configurar(UnityEngine.InputSystem.InputAction accion)
        {
            if (accion == null) return;
            string tec = null, man = null;
            foreach (var b in accion.bindings)
            {
                if (b.isComposite || b.isPartOfComposite) continue;
                string ruta = b.effectivePath;
                if (string.IsNullOrEmpty(ruta)) continue;
                if (tec == null && ruta.StartsWith("<Keyboard>")) tec = ruta;
                if (man == null && ruta.StartsWith("<Gamepad>")) man = ruta;
            }
            Configurar(tec ?? rutaTeclado, man ?? rutaMando);
        }

        /// <summary>"coger" / "soltar" / "colocar" / "retirar" — lo decide el dueño.</summary>
        public string Accion
        {
            get => accion;
            set { if (accion == value) return; accion = value; Redibujar(); }
        }

        /// <summary>Permite ocultarlo aunque el jugador esté cerca (p. ej. zócalo inútil).</summary>
        public void Ocultar(bool ocultar)
        {
            forzadoOculto = ocultar;
            if (ocultar) Mostrar(false);
        }

        private void Awake()
        {
            var go = new GameObject("Prompt");
            go.transform.SetParent(transform, false);

            texto = go.AddComponent<TextMeshPro>();
            texto.alignment = TextAlignmentOptions.Center;
            texto.fontSize = tamanoFuente;
            texto.color = color;
            texto.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(6f, 1.2f);

            // El objeto al que se engancha suele estar ESCALADO (las piezas son 0.5,
            // los zócalos 1×0.7...). Sin compensar, el cartel saldría encogido y
            // aplastado, y el offset no mediría lo que dice. Se contrarresta la
            // escala heredada para que el texto se lea igual cuelgue de donde cuelgue.
            Vector3 s = transform.lossyScale;
            float sx = Mathf.Approximately(s.x, 0f) ? 1f : s.x;
            float sy = Mathf.Approximately(s.y, 0f) ? 1f : s.y;
            go.transform.localScale = new Vector3(1f / sx, 1f / sy, 1f);

            // OJO: se coloca por anchoredPosition3D, NO por transform.position. Un
            // TextMeshPro lleva RectTransform y su rebuild de layout recalcula la
            // posición desde las anclas, pisando lo asignado por transform.position
            // y dejando el objeto en el origen del padre (bug de los letreros, bugs.md).
            // Se divide por la escala del padre para que `offset` sean unidades de MUNDO.
            rt.anchoredPosition3D = new Vector3(offset.x / sx, offset.y / sy, 0f);

            texto.GetComponent<MeshRenderer>().sortingOrder = 30;   // por delante de todo
            go.SetActive(false);
        }

        private void OnEnable()
        {
            DispositivoActivo.OnCambio += AlCambiarDispositivo;
            Redibujar();
        }

        private void OnDisable() => DispositivoActivo.OnCambio -= AlCambiarDispositivo;

        private void AlCambiarDispositivo(TipoDispositivo _) => Redibujar();

        private void Update()
        {
            if (jugador == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc == null) return;
                jugador = pc.transform;
            }

            bool cerca = !forzadoOculto &&
                         Vector2.Distance(jugador.position, transform.position) <= radio;
            if (cerca != visible) Mostrar(cerca);
        }

        private void Mostrar(bool v)
        {
            visible = v;
            if (texto != null) texto.gameObject.SetActive(v);
            if (v) Redibujar();
        }

        private void Redibujar()
        {
            if (texto == null) return;
            texto.text = "(" + GlifosDeEntrada.Etiqueta(rutaTeclado, rutaMando) + ") " + accion;
        }
    }
}
