using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LaProyeccion.UI
{
    /// <summary>
    /// Pantalla de "Controles" al estilo de las referencias de `_docs/imagenes/`:
    /// el dispositivo dibujado en el centro y llamadas (etiqueta + línea guía)
    /// hacia la tecla o el botón correspondiente. Dos páginas: Teclado y Mando.
    ///
    /// El diagrama se genera en runtime a partir de los bindings reales de
    /// <see cref="PlayerInputActions"/>: qué teclas se resaltan, qué etiqueta
    /// apunta a cada una y qué botón de mando lleva cada llamada salen del asset
    /// de Input, no de valores escritos a mano. Si mañana cambia una tecla, el
    /// diagrama cambia con ella (es la razón de no dibujar esto como una imagen).
    ///
    /// El teclado es 100% procedural (rectángulos + texto, sin arte). La silueta
    /// del mando SÍ necesita un sprite: mientras no exista, el hueco se ve como
    /// un marco vacío y las llamadas siguen colocadas en su sitio.
    ///
    /// El mismo prefab se usa desde el menú principal y desde el menú de pausa;
    /// el botón "Volver" lo cablea el controlador contenedor vía <see cref="BotonVolver"/>.
    /// </summary>
    public class ControlsPanel : MonoBehaviour
    {
        // ==================== Datos serializados ====================

        [System.Serializable]
        public class EntradaControl
        {
            [Tooltip("Nombre de la acción en el mapa Player del asset de Input.")]
            public string accion;
            [Tooltip("Texto que ve el jugador.")]
            public string etiqueta;
            [Tooltip("Segunda línea pequeña, opcional (matiz o condición).")]
            public string detalle;
            [Tooltip("Si está marcado, la llamada va a la columna derecha del diagrama.")]
            public bool ladoDerecho;
        }

        [System.Serializable]
        public class AnclaMando
        {
            [Tooltip("Nombre del control tal cual aparece en el binding: buttonSouth, leftStick, start…")]
            public string control;
            [Tooltip("Posición del punto en píxeles, relativa al centro de la silueta.")]
            public Vector2 posicion;
            [Tooltip("Columna a la que va la etiqueta de esa llamada.")]
            public bool ladoDerecho;
        }

        [Header("Jerarquía")]
        [SerializeField] private RectTransform paginaTeclado;
        [SerializeField] private RectTransform paginaMando;
        [Tooltip("Marco donde va el sprite del mando. Las anclas se miden desde su centro.")]
        [SerializeField] private RectTransform siluetaMando;
        [SerializeField] private Button botonTabTeclado;
        [SerializeField] private Button botonTabMando;
        [SerializeField] private Button botonVolver;
        [SerializeField] private TextMeshProUGUI textoNota;

        [Header("Contenido")]
        [SerializeField]
        private List<EntradaControl> entradas = new()
        {
            new EntradaControl { accion = "Move",        etiqueta = "Moverse" },
            new EntradaControl { accion = "SwitchWorld", etiqueta = "Cambiar de mundo" },
            new EntradaControl { accion = "Pause",       etiqueta = "Pausa" },
            new EntradaControl { accion = "Sondear",     etiqueta = "Sondear", detalle = "Consume una Semilla", ladoDerecho = true },
            new EntradaControl { accion = "Interact",    etiqueta = "Interactuar", ladoDerecho = true },
            new EntradaControl { accion = "Jump",        etiqueta = "Saltar", ladoDerecho = true },
        };

        [SerializeField] private string notaTeclado = "Las teclas resaltadas son las que usa el juego.";
        [SerializeField] private string notaMando = "Nombres de botón según mando de Xbox.";

        [Header("Anclas del mando")]
        [Tooltip("Respaldo: solo se usa para los controles que el dibujo procedural NO pinta. " +
                 "Los que sí se dibujan toman su posición (y su lado) de la pieza dibujada.")]
        [SerializeField]
        private List<AnclaMando> anclasMando = new()
        {
            new AnclaMando { control = "leftTrigger",   posicion = new Vector2(-155f,  150f) },
            new AnclaMando { control = "leftShoulder",  posicion = new Vector2(-140f,  110f) },
            new AnclaMando { control = "leftStick",     posicion = new Vector2(-125f,  -15f) },
            new AnclaMando { control = "dpad",          posicion = new Vector2( -55f,  -95f) },
            new AnclaMando { control = "select",        posicion = new Vector2( -35f,   35f) },
            new AnclaMando { control = "start",         posicion = new Vector2(  35f,   35f), ladoDerecho = true },
            new AnclaMando { control = "rightTrigger",  posicion = new Vector2( 155f,  150f), ladoDerecho = true },
            new AnclaMando { control = "rightShoulder", posicion = new Vector2( 140f,  110f), ladoDerecho = true },
            new AnclaMando { control = "buttonNorth",   posicion = new Vector2( 140f,   40f), ladoDerecho = true },
            new AnclaMando { control = "buttonWest",    posicion = new Vector2( 110f,    5f), ladoDerecho = true },
            new AnclaMando { control = "buttonEast",    posicion = new Vector2( 170f,    5f), ladoDerecho = true },
            new AnclaMando { control = "buttonSouth",   posicion = new Vector2( 140f,  -30f), ladoDerecho = true },
            new AnclaMando { control = "rightStick",    posicion = new Vector2(  55f,  -95f), ladoDerecho = true },
        };

        [Header("Estilo")]
        [Tooltip("Sprite de caja redondeada de las teclas (UISprite del UI de Unity). " +
                 "Si se deja vacío, las teclas salen como rectángulos rectos.")]
        [SerializeField] private Sprite spriteTecla;
        [Tooltip("Sprite circular (Knob del UI de Unity), para sticks y botones del mando. " +
                 "Si se deja vacío, salen cuadrados.")]
        [SerializeField] private Sprite spriteCirculo;
        [Tooltip("Lado de una tecla de 1u, en píxeles.")]
        [SerializeField] private float unidadTecla = 40f;
        [SerializeField] private float separacionTeclas = 4f;
        [SerializeField] private Vector2 desplazamientoDiagrama = new(0f, -40f);
        [SerializeField] private float margenColumna = 60f;
        [SerializeField] private float anchoColumna = 300f;
        [SerializeField] private Color colorTecla = new(0.10f, 0.13f, 0.17f, 0.85f);
        [SerializeField] private Color colorTextoTecla = new(0.45f, 0.55f, 0.60f, 1f);
        [SerializeField] private Color colorTeclaActiva = new(0.30f, 0.92f, 0.85f, 0.30f);
        [SerializeField] private Color colorTextoActiva = new(0.60f, 1f, 0.98f, 1f);
        [SerializeField] private Color colorEtiqueta = new(0.85f, 1f, 0.98f, 1f);
        [SerializeField] private Color colorDetalle = new(0.50f, 0.66f, 0.70f, 1f);
        [SerializeField] private Color colorLinea = new(0.30f, 0.70f, 0.72f, 0.55f);
        [SerializeField] private Color colorCuerpoMando = new(0.10f, 0.13f, 0.17f, 0.88f);
        [SerializeField] private Color colorPiezaMando = new(0.16f, 0.42f, 0.44f, 0.95f);
        [SerializeField] private float tamanoEtiqueta = 26f;
        [SerializeField] private float tamanoDetalle = 18f;

        /// <summary>Botón "Volver": lo cablea el menú que abre este panel.</summary>
        public Button BotonVolver => botonVolver;

        private PlayerInputActions input;
        private bool construido;

        // ==================== Distribución del teclado ====================
        // Formato por tecla: "etiqueta|ancho en u|nombre de la tecla en el Input System".
        // Nombre vacío = separación sin dibujo (los huecos del teclado real).

        private static readonly string[][] FilasTeclado =
        {
            new[]
            {
                "Esc|1|escape", "|0.5|",
                "F1|1|f1", "F2|1|f2", "F3|1|f3", "F4|1|f4", "|0.25|",
                "F5|1|f5", "F6|1|f6", "F7|1|f7", "F8|1|f8", "|0.25|",
                "F9|1|f9", "F10|1|f10", "F11|1|f11", "F12|1|f12",
            },
            new[]
            {
                "º|1|backquote", "1|1|1", "2|1|2", "3|1|3", "4|1|4", "5|1|5",
                "6|1|6", "7|1|7", "8|1|8", "9|1|9", "0|1|0",
                "'|1|minus", "¡|1|equals", "←|2|backspace",
            },
            new[]
            {
                "Tab|1.5|tab", "Q|1|q", "W|1|w", "E|1|e", "R|1|r", "T|1|t", "Y|1|y",
                "U|1|u", "I|1|i", "O|1|o", "P|1|p", "`|1|leftBracket", "+|1|rightBracket",
                "\\|1.5|backslash",
            },
            new[]
            {
                "Bloq|1.75|capsLock", "A|1|a", "S|1|s", "D|1|d", "F|1|f", "G|1|g",
                "H|1|h", "J|1|j", "K|1|k", "L|1|l", "Ñ|1|semicolon", "´|1|quote",
                "Intro|2.25|enter",
            },
            new[]
            {
                "Shift|2.25|leftShift", "Z|1|z", "X|1|x", "C|1|c", "V|1|v", "B|1|b",
                "N|1|n", "M|1|m", ",|1|comma", ".|1|period", "-|1|slash",
                "Shift|2.75|rightShift",
            },
            new[]
            {
                "Ctrl|1.25|leftCtrl", "Win|1.25|leftMeta", "Alt|1.25|leftAlt",
                "Espacio|7.5|space", "AltGr|1.25|rightAlt", "Menú|1.25|contextMenu",
                "Ctrl|1.25|rightCtrl",
            },
        };

        // Bloque de flechas, a la derecha del teclado: columna en u, fila, tecla.
        private static readonly string[] Flechas =
        {
            "↑|16|4|upArrow",
            "←|15|5|leftArrow", "↓|16|5|downArrow", "→|17|5|rightArrow",
        };

        /// <summary>
        /// Traducción de los nombres que devuelve el Input System (en inglés) al
        /// vocabulario en español del juego. Lo que no esté aquí se muestra tal cual.
        /// </summary>
        private static readonly Dictionary<string, string> Traduccion = new()
        {
            // Teclado
            { "Space", "Espacio" },
            { "Escape", "Esc" },
            { "Enter", "Intro" },
            { "Left Shift", "Shift izq." },
            { "Right Shift", "Shift der." },
            { "Left Ctrl", "Ctrl izq." },
            { "Right Ctrl", "Ctrl der." },
            { "Left Alt", "Alt izq." },
            { "Up Arrow", "↑" },
            { "Down Arrow", "↓" },
            { "Left Arrow", "←" },
            { "Right Arrow", "→" },
            // Mando (nomenclatura Xbox, aclarada en la nota al pie)
            { "Button South", "A" },
            { "Button East", "B" },
            { "Button West", "X" },
            { "Button North", "Y" },
            { "Left Shoulder", "LB" },
            { "Right Shoulder", "RB" },
            { "Left Trigger", "LT" },
            { "Right Trigger", "RT" },
            { "Left Stick", "Stick izq." },
            { "Right Stick", "Stick der." },
            { "D-Pad", "Cruceta" },
            { "Start", "Menú" },
            { "Select", "Vista" },
        };

        // ==================== Ciclo de vida ====================

        private void Awake()
        {
            Construir();
            if (botonTabTeclado != null) botonTabTeclado.onClick.AddListener(MostrarTeclado);
            if (botonTabMando != null) botonTabMando.onClick.AddListener(MostrarMando);
        }

        private void OnEnable() => MostrarTeclado();

        private void OnDestroy() => input?.Dispose();

        // ==================== Páginas ====================

        public void MostrarTeclado() => MostrarPagina(true);
        public void MostrarMando() => MostrarPagina(false);

        private void MostrarPagina(bool teclado)
        {
            if (paginaTeclado != null) paginaTeclado.gameObject.SetActive(teclado);
            if (paginaMando != null) paginaMando.gameObject.SetActive(!teclado);
            if (textoNota != null) textoNota.text = teclado ? notaTeclado : notaMando;

            PintarTab(botonTabTeclado, teclado);
            PintarTab(botonTabMando, !teclado);
        }

        private void PintarTab(Button tab, bool activo)
        {
            if (tab == null) return;
            var t = tab.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.color = activo ? colorTextoActiva : colorDetalle;
        }

        // ==================== Construcción ====================

        /// <summary>
        /// Vuelca las dos páginas una sola vez. Se hace en Awake (y no en OnEnable)
        /// para que el panel exista completo antes de que el menú fije la selección
        /// inicial de teclado/gamepad.
        /// </summary>
        private void Construir()
        {
            if (construido) return;
            construido = true;

            input = new PlayerInputActions();

            ConstruirTeclado();
            ConstruirMando();
        }

        // ---------- Teclado ----------

        private void ConstruirTeclado()
        {
            if (paginaTeclado == null) return;

            float paso = unidadTecla + separacionTeclas;
            int filas = FilasTeclado.Length;
            // El bloque de flechas cuelga de las filas 4 y 5, así que no añade alto.
            float anchoTotal = 18f * paso;
            float altoTotal = filas * paso;
            Vector2 origen = desplazamientoDiagrama + new Vector2(-anchoTotal * 0.5f, altoTotal * 0.5f);

            // tecla -> centro de su rectángulo, para que las líneas guía sepan a dónde apuntar.
            var centros = new Dictionary<string, Vector2>();

            for (int f = 0; f < filas; f++)
            {
                float x = 0f;
                foreach (var def in FilasTeclado[f])
                {
                    var partes = def.Split('|');
                    string etiqueta = partes[0];
                    float ancho = float.Parse(partes[1], System.Globalization.CultureInfo.InvariantCulture);
                    string tecla = partes.Length > 2 ? partes[2] : string.Empty;

                    if (!string.IsNullOrEmpty(tecla))
                    {
                        Vector2 centro = origen + new Vector2(
                            x * paso + ancho * paso * 0.5f - separacionTeclas * 0.5f,
                            -(f * paso) - paso * 0.5f);
                        CrearTecla(etiqueta, ancho, centro);
                        centros[tecla] = centro;
                    }

                    x += ancho;
                }
            }

            foreach (var def in Flechas)
            {
                var partes = def.Split('|');
                float col = float.Parse(partes[1], System.Globalization.CultureInfo.InvariantCulture);
                float fila = float.Parse(partes[2], System.Globalization.CultureInfo.InvariantCulture);
                Vector2 centro = origen + new Vector2(
                    col * paso + paso * 0.5f - separacionTeclas * 0.5f,
                    -(fila * paso) - paso * 0.5f);
                CrearTecla(partes[0], 1f, centro);
                centros[partes[3]] = centro;
            }

            // Una llamada por acción, apuntando a su primera tecla; el resto de sus
            // teclas también se resaltan (p. ej. Moverse: WASD + flechas).
            var llamadas = new List<(EntradaControl entrada, Vector2 destino, string teclas)>();
            foreach (var e in entradas)
            {
                var accion = BuscarAccion(e);
                if (accion == null) continue;

                var teclas = ControlesDe(accion, "<Keyboard>");
                if (teclas.Count == 0) continue;

                Vector2? destino = null;
                var nombres = new List<string>();
                foreach (var t in teclas)
                {
                    if (!centros.TryGetValue(t, out var c)) continue;
                    Resaltar(c);
                    if (destino == null) destino = c;
                    nombres.Add(EtiquetaDeControl("<Keyboard>", t));
                }
                if (destino == null) continue;

                llamadas.Add((e, destino.Value, string.Join(" ", nombres)));
            }

            ColocarLlamadas(paginaTeclado, llamadas, altoTotal);
        }

        private readonly Dictionary<Vector2, Image> fondosTecla = new();

        private void CrearTecla(string etiqueta, float ancho, Vector2 centro)
        {
            float paso = unidadTecla + separacionTeclas;

            var go = new GameObject("Tecla_" + etiqueta, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(paginaTeclado, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ancho * paso - separacionTeclas, unidadTecla);
            rt.anchoredPosition = centro;

            var img = go.GetComponent<Image>();
            if (spriteTecla != null)
            {
                img.sprite = spriteTecla;
                img.type = Image.Type.Sliced;
            }
            img.color = colorTecla;
            fondosTecla[centro] = img;

            var txtGo = new GameObject("Txt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var trt = (RectTransform)txtGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.text = etiqueta;
            tmp.fontSize = unidadTecla * (etiqueta.Length > 3 ? 0.28f : 0.42f);
            tmp.color = colorTextoTecla;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void Resaltar(Vector2 centro)
        {
            if (!fondosTecla.TryGetValue(centro, out var img)) return;
            img.color = colorTeclaActiva;
            var tmp = img.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.color = colorTextoActiva;
        }

        // ---------- Mando ----------

        private void ConstruirMando()
        {
            if (paginaMando == null) return;

            Vector2 centroSilueta = siluetaMando != null ? siluetaMando.anchoredPosition : desplazamientoDiagrama;
            float alto = siluetaMando != null ? siluetaMando.sizeDelta.y : 360f;

            var centros = DibujarMando();

            var llamadas = new List<(EntradaControl entrada, Vector2 destino, string teclas)>();
            foreach (var e in entradas)
            {
                var accion = BuscarAccion(e);
                if (accion == null) continue;

                var controles = ControlesDe(accion, "<Gamepad>");
                if (controles.Count == 0) continue;

                // La pieza dibujada manda; las anclas serializadas solo cubren
                // los controles que el dibujo no pinta.
                Vector2 local;
                if (!centros.TryGetValue(controles[0], out local))
                {
                    var ancla = anclasMando.Find(a => a.control == controles[0]);
                    if (ancla == null) continue;
                    local = ancla.posicion;
                }

                // El lado lo manda la anatomía del mando, no el orden de la lista.
                var copia = new EntradaControl
                {
                    accion = e.accion,
                    etiqueta = e.etiqueta,
                    detalle = e.detalle,
                    ladoDerecho = local.x >= 0f,
                };
                llamadas.Add((copia, centroSilueta + local, EtiquetaDeControl("<Gamepad>", controles[0])));
            }

            ColocarLlamadas(paginaMando, llamadas, alto);
        }

        /// <summary>
        /// Dibuja el mando con primitivas del propio UI (cajas redondeadas + círculos),
        /// igual que el teclado: sin arte, nítido a cualquier resolución y con cada pieza
        /// como objeto real. Devuelve el centro de cada control, en coordenadas locales a
        /// la silueta, para que las llamadas apunten a la pieza y no a un número a ojo.
        /// </summary>
        private Dictionary<string, Vector2> DibujarMando()
        {
            var centros = new Dictionary<string, Vector2>();
            if (siluetaMando == null) return centros;

            // ---- cuerpo (va primero: queda por detrás de los controles) ----
            // Las "alas" son dos círculos que redondean los extremos del cuerpo: sin
            // ellas el mando se lee como un rectángulo.
            Pieza("Empunadura_Izq", new Vector2(-112f, -78f), new Vector2(92f, 162f), -13f, colorCuerpoMando);
            Pieza("Empunadura_Der", new Vector2(112f, -78f), new Vector2(92f, 162f), 13f, colorCuerpoMando);
            Circulo("Ala_Izq", new Vector2(-118f, 22f), 158f, colorCuerpoMando);
            Circulo("Ala_Der", new Vector2(118f, 22f), 158f, colorCuerpoMando);
            Pieza("Cuerpo", new Vector2(0f, 22f), new Vector2(250f, 158f), 0f, colorCuerpoMando);

            // ---- gatillos y bumpers ----
            centros["leftTrigger"] = Pieza("LT", new Vector2(-112f, 140f), new Vector2(64f, 28f), 0f, colorPiezaMando);
            centros["rightTrigger"] = Pieza("RT", new Vector2(112f, 140f), new Vector2(64f, 28f), 0f, colorPiezaMando);
            centros["leftShoulder"] = Pieza("LB", new Vector2(-112f, 108f), new Vector2(80f, 22f), 0f, colorPiezaMando);
            centros["rightShoulder"] = Pieza("RB", new Vector2(112f, 108f), new Vector2(80f, 22f), 0f, colorPiezaMando);

            // ---- sticks ----
            centros["leftStick"] = Circulo("StickIzq", new Vector2(-100f, 15f), 64f, colorPiezaMando);
            centros["rightStick"] = Circulo("StickDer", new Vector2(52f, -52f), 64f, colorPiezaMando);

            // ---- cruceta ----
            Vector2 cruceta = new(-48f, -52f);
            Pieza("Cruceta_H", cruceta, new Vector2(58f, 20f), 0f, colorPiezaMando);
            Pieza("Cruceta_V", cruceta, new Vector2(20f, 58f), 0f, colorPiezaMando);
            centros["dpad"] = cruceta;

            // ---- botones frontales (nomenclatura Xbox) ----
            centros["buttonNorth"] = Boton("Y", new Vector2(118f, 58f));
            centros["buttonWest"] = Boton("X", new Vector2(82f, 20f));
            centros["buttonEast"] = Boton("B", new Vector2(154f, 20f));
            centros["buttonSouth"] = Boton("A", new Vector2(118f, -18f));

            // ---- botones centrales ----
            centros["select"] = Pieza("Vista", new Vector2(-32f, 58f), new Vector2(24f, 17f), 0f, colorPiezaMando);
            centros["start"] = Pieza("Menu", new Vector2(32f, 58f), new Vector2(24f, 17f), 0f, colorPiezaMando);

            return centros;
        }

        /// <summary>Caja redondeada del mando. Devuelve su centro, por comodidad al registrarlo.</summary>
        private Vector2 Pieza(string nombre, Vector2 centro, Vector2 tamano, float rotacion, Color color)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(siluetaMando, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = tamano;
            rt.anchoredPosition = centro;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotacion);

            var img = go.GetComponent<Image>();
            if (spriteTecla != null)
            {
                img.sprite = spriteTecla;
                img.type = Image.Type.Sliced;
            }
            img.color = color;
            img.raycastTarget = false;
            return centro;
        }

        private Vector2 Circulo(string nombre, Vector2 centro, float diametro, Color color)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(siluetaMando, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(diametro, diametro);
            rt.anchoredPosition = centro;

            var img = go.GetComponent<Image>();
            if (spriteCirculo != null) img.sprite = spriteCirculo;
            img.color = color;
            img.raycastTarget = false;
            return centro;
        }

        /// <summary>Botón frontal: círculo con su letra (A/B/X/Y).</summary>
        private Vector2 Boton(string letra, Vector2 centro)
        {
            Circulo("Boton_" + letra, centro, 42f, colorPiezaMando);

            var go = new GameObject("Txt_" + letra, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(siluetaMando, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(42f, 42f);
            rt.anchoredPosition = centro;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = letra;
            tmp.fontSize = 20f;
            tmp.color = colorTextoActiva;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return centro;
        }

        // ---------- Llamadas (etiqueta + línea guía) ----------

        private void ColocarLlamadas(RectTransform pagina,
                                     List<(EntradaControl entrada, Vector2 destino, string teclas)> llamadas,
                                     float altoDiagrama)
        {
            var izquierda = llamadas.FindAll(l => !l.entrada.ladoDerecho);
            var derecha = llamadas.FindAll(l => l.entrada.ladoDerecho);
            izquierda.Sort((a, b) => b.destino.y.CompareTo(a.destino.y));
            derecha.Sort((a, b) => b.destino.y.CompareTo(a.destino.y));

            ColocarColumna(pagina, izquierda, false, altoDiagrama);
            ColocarColumna(pagina, derecha, true, altoDiagrama);
        }

        private void ColocarColumna(RectTransform pagina,
                                    List<(EntradaControl entrada, Vector2 destino, string teclas)> lista,
                                    bool derecha, float altoDiagrama)
        {
            if (lista.Count == 0) return;

            float paso = unidadTecla + separacionTeclas;
            float anchoDiagrama = pagina == paginaTeclado
                ? 18f * paso
                : (siluetaMando != null ? siluetaMando.sizeDelta.x : 520f);

            float x = desplazamientoDiagrama.x
                    + (anchoDiagrama * 0.5f + margenColumna) * (derecha ? 1f : -1f);

            // Repartidas verticalmente sobre el alto del diagrama, con aire arriba y abajo.
            float alto = altoDiagrama + 80f;
            float arriba = desplazamientoDiagrama.y + alto * 0.5f;
            float separacion = lista.Count > 1 ? alto / (lista.Count - 1) : 0f;

            for (int i = 0; i < lista.Count; i++)
            {
                float y = lista.Count > 1 ? arriba - separacion * i : desplazamientoDiagrama.y;
                var origen = CrearEtiqueta(pagina, lista[i].entrada, lista[i].teclas, new Vector2(x, y), derecha);
                CrearLinea(pagina, origen, lista[i].destino);
            }
        }

        /// <summary>Crea la etiqueta de una llamada y devuelve el punto del que sale su línea.</summary>
        private Vector2 CrearEtiqueta(RectTransform pagina, EntradaControl entrada, string teclas,
                                      Vector2 posicion, bool derecha)
        {
            var go = new GameObject("Llamada_" + entrada.etiqueta, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(pagina, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(derecha ? 0f : 1f, 0.5f);
            rt.sizeDelta = new Vector2(anchoColumna, 60f);
            rt.anchoredPosition = posicion;

            var alineacion = derecha ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;

            CrearTexto(rt, entrada.etiqueta, tamanoEtiqueta, colorEtiqueta, alineacion, new Vector2(0f, 14f));
            string bajo = string.IsNullOrEmpty(entrada.detalle) ? teclas : entrada.detalle + "   " + teclas;
            CrearTexto(rt, bajo, tamanoDetalle, colorDetalle, alineacion, new Vector2(0f, -12f));

            // La línea sale del borde interior de la etiqueta (el que mira al diagrama).
            return posicion + new Vector2(derecha ? -12f : 12f, 0f);
        }

        private void CrearTexto(RectTransform padre, string texto, float tamano, Color color,
                                TextAlignmentOptions alineacion, Vector2 offset)
        {
            var go = new GameObject("Txt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 28f);
            rt.anchoredPosition = offset;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = texto;
            tmp.fontSize = tamano;
            tmp.color = color;
            tmp.alignment = alineacion;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void CrearLinea(RectTransform pagina, Vector2 desde, Vector2 hasta)
        {
            var go = new GameObject("Linea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(pagina, false);
            rt.SetAsFirstSibling(); // las líneas van por detrás de teclas y etiquetas
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = desde;

            Vector2 delta = hasta - desde;
            rt.sizeDelta = new Vector2(delta.magnitude, 1.5f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var img = go.GetComponent<Image>();
            img.color = colorLinea;
            img.raycastTarget = false;
        }

        // ==================== Lectura del asset de Input ====================

        private InputAction BuscarAccion(EntradaControl e)
        {
            if (e == null || string.IsNullOrEmpty(e.accion)) return null;
            return input.asset.FindAction("Player/" + e.accion, false);
        }

        /// <summary>
        /// Nombres de control (sin el dispositivo) de todos los bindings de una
        /// acción para un tipo de dispositivo, incluidas las partes de un compuesto.
        /// Ej.: Move + "&lt;Keyboard&gt;" -> w, s, a, d, upArrow, downArrow, …
        /// </summary>
        private static List<string> ControlesDe(InputAction accion, string prefijoDispositivo)
        {
            var lista = new List<string>();
            if (accion == null) return lista;

            foreach (var b in accion.bindings)
            {
                if (b.isComposite) continue; // la cabecera del compuesto no tiene ruta
                if (string.IsNullOrEmpty(b.effectivePath)) continue;
                if (!b.effectivePath.StartsWith(prefijoDispositivo)) continue;

                string control = b.effectivePath.Substring(b.effectivePath.IndexOf('/') + 1);
                if (!lista.Contains(control)) lista.Add(control);
            }
            return lista;
        }

        /// <summary>Nombre legible y en español de un control ("space" -> "Espacio").</summary>
        private static string EtiquetaDeControl(string prefijoDispositivo, string control)
        {
            string legible = InputControlPath.ToHumanReadableString(
                prefijoDispositivo + "/" + control, InputControlPath.HumanReadableStringOptions.OmitDevice);
            if (string.IsNullOrEmpty(legible)) legible = control;
            return Traduccion.TryGetValue(legible, out var traducido) ? traducido : legible;
        }
    }
}
