using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace LaProyeccion.UI
{
    /// <summary>
    /// Pantalla de **LABORATORIOS**: saltar de un lab a otro sin salir de Play, en vez
    /// del ritual de parar → abrir escena → volver a darle. Con ~30 labs, ese ritual es
    /// el mayor coste de iterar.
    ///
    /// Vive **dentro de los menús** (pausa y principal) y no en una tecla suelta: una
    /// tecla de función no tiene equivalente en mando, y aquí todo tiene que poder
    /// jugarse con mando (decisión del autor, 2026-07-25). Al ser uGUI como el resto de
    /// menús, la navegación con stick/cruceta sale del EventSystem sin código extra.
    ///
    /// **ANDAMIAJE DE DESARROLLO — no llega al juego.** `LabsPanel.Disponible` es false
    /// fuera del editor y los controladores de menú **destruyen su botón** en ese caso;
    /// además, listar y cargar escenas usa APIs de `UnityEditor` que solo compilan aquí.
    /// La clase entera NO va dentro de `#if UNITY_EDITOR` a propósito: los prefabs de
    /// menú la referencian, y si el tipo desapareciera en build quedarían con un script
    /// perdido.
    ///
    /// Por qué no `SceneManager.LoadScene`: los labs **nunca están en Build Settings**
    /// (convención de CATALOGO §7), así que no los encontraría. `LoadSceneInPlayMode`
    /// carga por RUTA y se salta esa lista.
    ///
    /// Se construye por código (sin arte, como `ControlsPanel`); el botón "Volver" lo
    /// cablea el controlador contenedor vía <see cref="BotonVolver"/>.
    /// </summary>
    public class LabsPanel : MonoBehaviour
    {
        private const string CarpetaLabs = "Assets/Scenes/Pruebas";
        private const string CarpetaEscenas = "Assets/Scenes";

        /// <summary>Fuera del editor no hay labs que cargar: el botón ni se muestra.</summary>
        public static bool Disponible => Application.isEditor;

        [Header("Aspecto")]
        [SerializeField] private Color colorFondo = new Color(0.04f, 0.05f, 0.07f, 0.96f);
        [SerializeField] private Color colorTitulo = new Color(0.25f, 0.85f, 1f);
        [SerializeField] private Color colorCabecera = new Color(0.55f, 0.8f, 1f, 0.8f);
        [SerializeField] private Color colorTexto = new Color(0.92f, 0.96f, 1f);

        private Button botonVolver;
        private Button primerLab;
        private bool construido;
        private ScrollRect scroll;
        private RectTransform viewport, contenido;
        private GameObject ultimoSeleccionado;

        /// <summary>El contenedor cablea aquí su "volver" (mismo patrón que ControlsPanel).</summary>
        public Button BotonVolver => botonVolver;

        /// <summary>Qué seleccionar al abrir, para que el mando tenga foco desde el primer frame.</summary>
        public Selectable PrimerSeleccionable => primerLab != null ? (Selectable)primerLab : botonVolver;

        private void Awake() => Construir();

        // ==================== Construcción ====================

        private void Construir()
        {
            if (construido) return;
            construido = true;

            var fondo = gameObject.GetComponent<Image>();
            if (fondo == null) fondo = gameObject.AddComponent<Image>();
            fondo.color = colorFondo;

            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Titulo("LABORATORIOS", 34f, colorTitulo, new Vector2(0f, -46f));
            Titulo("Herramienta de desarrollo · no forma parte del juego", 15f,
                   new Color(1f, 1f, 1f, 0.45f), new Vector2(0f, -84f));

            viewport = CrearViewport();
            contenido = CrearContenido(viewport);
            scroll = viewport.GetComponent<ScrollRect>();
            Rellenar(contenido);
            botonVolver = CrearBotonVolver();
        }

        private void Titulo(string texto, float tamano, Color color, Vector2 pos)
        {
            var go = new GameObject("Titulo", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = texto;
            t.fontSize = tamano;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;

            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(-80f, tamano * 1.6f);
        }

        private RectTransform CrearViewport()
        {
            var go = new GameObject("Lista", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(520f, -190f);
            r.anchoredPosition = new Vector2(0f, 12f);

            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);
            go.AddComponent<Mask>().showMaskGraphic = true;

            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            return r;
        }

        private RectTransform CrearContenido(RectTransform viewport)
        {
            var go = new GameObject("Contenido", typeof(RectTransform));
            go.transform.SetParent(viewport, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = Vector2.zero;
            // OJO: un RectTransform nuevo nace con sizeDelta (100,100). Con anclas
            // estiradas eso significa "viewport + 100 de ancho", así que el contenido
            // sobresalía 50 px por cada lado y la máscara cortaba los nombres por la
            // izquierda. La altura la gobierna el ContentSizeFitter.
            r.sizeDelta = Vector2.zero;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = r;
            return r;
        }

        private void Rellenar(RectTransform contenido)
        {
            var escenas = DescubrirEscenas();
            if (escenas.Count == 0)
            {
                Cabecera(contenido, "No hay escenas que listar (solo funciona en el editor).");
                return;
            }

            string activa = SceneManager.GetActiveScene().path;
            bool cabLabs = false, cabJuego = false;

            foreach (var (nombre, ruta, esLab) in escenas)
            {
                if (esLab && !cabLabs) { Cabecera(contenido, "LABORATORIOS"); cabLabs = true; }
                if (!esLab && !cabJuego) { Cabecera(contenido, "ESCENAS DEL JUEGO"); cabJuego = true; }

                bool actual = ruta == activa;
                var b = CrearBotonLab(contenido, (actual ? "▸  " : "") + nombre, ruta, actual);
                if (primerLab == null) primerLab = b;
            }
        }

        private List<(string nombre, string ruta, bool esLab)> DescubrirEscenas()
        {
            var lista = new List<(string, string, bool)>();
#if UNITY_EDITOR
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { CarpetaEscenas }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                lista.Add((System.IO.Path.GetFileNameWithoutExtension(ruta),
                           ruta,
                           ruta.StartsWith(CarpetaLabs)));
            }
            lista.Sort((a, b) => a.Item3 != b.Item3
                ? (a.Item3 ? -1 : 1)
                : string.Compare(a.Item1, b.Item1, System.StringComparison.OrdinalIgnoreCase));
#endif
            return lista;
        }

        private void Cabecera(RectTransform padre, string texto)
        {
            var go = new GameObject("Cabecera", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = texto;
            t.fontSize = 14f;
            t.color = colorCabecera;
            t.alignment = TextAlignmentOptions.Left;
            t.margin = new Vector4(4f, 8f, 0f, 2f);
            go.AddComponent<LayoutElement>().preferredHeight = 30f;
        }

        private Button CrearBotonLab(RectTransform padre, string etiqueta, string ruta, bool actual)
        {
            var go = new GameObject("Lab_" + etiqueta, typeof(RectTransform));
            go.transform.SetParent(padre, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, actual ? 0.14f : 0.06f);

            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var colores = b.colors;
            colores.highlightedColor = new Color(0.25f, 0.85f, 1f, 0.55f);
            colores.selectedColor = new Color(0.25f, 0.85f, 1f, 0.55f);
            colores.pressedColor = new Color(0.25f, 0.85f, 1f, 0.85f);
            b.colors = colores;

            go.AddComponent<LayoutElement>().preferredHeight = 30f;

            var txt = new GameObject("Texto", typeof(RectTransform));
            txt.transform.SetParent(go.transform, false);
            var t = txt.AddComponent<TextMeshProUGUI>();
            t.text = etiqueta;
            t.fontSize = 16f;
            t.color = colorTexto;
            t.alignment = TextAlignmentOptions.Left;
            t.margin = new Vector4(10f, 0f, 6f, 0f);
            var rt = (RectTransform)txt.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            b.onClick.AddListener(() => Cargar(ruta));
            return b;
        }

        private Button CrearBotonVolver()
        {
            var go = new GameObject("BotonVolver", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.10f);

            var b = go.AddComponent<Button>();
            b.targetGraphic = img;

            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.sizeDelta = new Vector2(220f, 44f);
            r.anchoredPosition = new Vector2(0f, 30f);

            var txt = new GameObject("Texto", typeof(RectTransform));
            txt.transform.SetParent(go.transform, false);
            var t = txt.AddComponent<TextMeshProUGUI>();
            t.text = "Volver";
            t.fontSize = 20f;
            t.color = colorTexto;
            t.alignment = TextAlignmentOptions.Center;
            var rt = (RectTransform)txt.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return b;
        }

        /// <summary>
        /// Mantiene a la vista el elemento seleccionado: un `ScrollRect` no sigue al
        /// foco por su cuenta, y navegando con mando la selección se iba fuera de la
        /// ventana — justo lo que este panel viene a resolver.
        ///
        /// ⚠️ Solo actúa **cuando la selección CAMBIA**, y solo si el elemento se salió
        /// de la ventana. La primera versión corregía el scroll CADA frame con un lerp,
        /// y eso se peleaba con la rueda del ratón: la rueda bajaba la lista y el lerp
        /// la devolvía, así que **temblaba en vez de desplazarse** (lo cazó el autor).
        /// Ajustando solo al cambiar el foco, y lo mínimo para que se vea, la rueda
        /// queda libre y las dos formas de navegar conviven.
        /// </summary>
        private void Update()
        {
            if (scroll == null || contenido == null || viewport == null) return;
            var evt = UnityEngine.EventSystems.EventSystem.current;
            var actual = evt != null ? evt.currentSelectedGameObject : null;

            if (actual == ultimoSeleccionado) return;   // sin cambio: no tocar el scroll
            ultimoSeleccionado = actual;
            if (actual == null) return;

            var sel = actual.transform as RectTransform;
            if (sel == null || !sel.IsChildOf(contenido)) return;

            float alturaContenido = contenido.rect.height;
            float alturaVentana = viewport.rect.height;
            float recorrido = alturaContenido - alturaVentana;
            if (recorrido <= 0f) return;

            const float margen = 6f;
            float centro = -sel.localPosition.y;             // distancia desde el borde superior
            float arribaItem = centro - sel.rect.height * 0.5f - margen;
            float abajoItem = centro + sel.rect.height * 0.5f + margen;

            float arribaVentana = (1f - scroll.verticalNormalizedPosition) * recorrido;
            float abajoVentana = arribaVentana + alturaVentana;

            float nuevoArriba = arribaVentana;
            if (arribaItem < arribaVentana) nuevoArriba = arribaItem;              // subir
            else if (abajoItem > abajoVentana) nuevoArriba = abajoItem - alturaVentana;  // bajar
            else return;                                                            // ya se ve

            scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - nuevoArriba / recorrido);
        }

        // ==================== Carga ====================

        private void Cargar(string ruta)
        {
#if UNITY_EDITOR
            // SIEMPRE 1: se llega aquí desde el menú de PAUSA, que dejó el tiempo en 0.
            // Sin esto, la escena nueva arrancaría congelada y sin nada que la
            // descongele, porque el menú de pausa se quedó en la escena anterior.
            Time.timeScale = 1f;
            EditorSceneManager.LoadSceneInPlayMode(ruta, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Debug.LogWarning("[LabsPanel] Cargar labs solo funciona en el editor.");
#endif
        }
    }
}
