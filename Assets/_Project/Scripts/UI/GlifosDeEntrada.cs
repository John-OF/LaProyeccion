using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace LaProyeccion.UI
{
    /// <summary>
    /// Traduce una ruta de binding del Input System ("&lt;Gamepad&gt;/buttonWest") a la
    /// etiqueta que hay que ENSEÑARLE a este jugador, según el mando que tenga en la
    /// mano. Un único sitio con las tres nomenclaturas, para que no vuelva a haber
    /// dos tablas que se contradigan.
    ///
    /// Regla que justifica que exista: el MISMO binding se llama distinto en cada
    /// mando. `buttonWest` es **X** en Xbox y **□** en PlayStation. Enseñar "X" a
    /// quien juega con un DualSense le manda al botón equivocado — es exactamente
    /// la clase de mentira que el Pilar 3 prohíbe.
    ///
    /// ⚠️ Deuda conocida: `ControlsPanel` tiene su propia tabla, solo Xbox, con una
    /// nota al pie aclarándolo. Debería migrar aquí (anotado en `pendientes.md`);
    /// no se toca de momento porque es una pantalla ya validada.
    /// </summary>
    public static class GlifosDeEntrada
    {
        // Nomenclatura Xbox (y fallback de mandos genéricos en PC).
        private static readonly Dictionary<string, string> Xbox = new()
        {
            { "buttonSouth", "A" },  { "buttonEast", "B" },
            { "buttonWest",  "X" },  { "buttonNorth", "Y" },
            { "leftShoulder", "LB" }, { "rightShoulder", "RB" },
            { "leftTrigger",  "LT" }, { "rightTrigger",  "RT" },
            { "start", "Menú" },      { "select", "Vista" },
        };

        // Nomenclatura PlayStation. Los símbolos van por NOMBRE y no por carácter
        // (□ △ ○ ✕) a propósito: no todas las fuentes del proyecto los tienen y un
        // glifo ausente se dibuja como un cuadrado vacío — peor que no poner nada.
        private static readonly Dictionary<string, string> PlayStation = new()
        {
            { "buttonSouth", "Equis" },    { "buttonEast", "Circulo" },
            { "buttonWest",  "Cuadrado" }, { "buttonNorth", "Triangulo" },
            { "leftShoulder", "L1" },      { "rightShoulder", "R1" },
            { "leftTrigger",  "L2" },      { "rightTrigger",  "R2" },
            { "start", "Options" },        { "select", "Share" },
        };

        // Teclado: se apoya en el Input System y solo se corrige lo que suena mal
        // en español o queda demasiado largo dentro de un prompt.
        private static readonly Dictionary<string, string> Teclado = new()
        {
            { "Space", "Espacio" }, { "Escape", "Esc" },   { "Enter", "Intro" },
            { "Left Shift", "Shift" }, { "Right Shift", "Shift" },
            { "Left Ctrl", "Ctrl" },   { "Right Ctrl", "Ctrl" },
            { "Left Alt", "Alt" },
            { "Up Arrow", "↑" }, { "Down Arrow", "↓" },
            { "Left Arrow", "←" }, { "Right Arrow", "→" },
        };

        /// <summary>
        /// Etiqueta para el dispositivo ACTIVO. Se le pasan las dos rutas del verbo
        /// (la de teclado y la de mando) y elige según con qué se esté jugando.
        /// </summary>
        public static string Etiqueta(string rutaTeclado, string rutaMando)
        {
            return DispositivoActivo.EsMando
                ? EtiquetaMando(rutaMando)
                : EtiquetaTeclado(rutaTeclado);
        }

        public static string EtiquetaMando(string rutaMando)
        {
            string control = Control(rutaMando);
            if (string.IsNullOrEmpty(control)) return "?";

            var tabla = DispositivoActivo.Actual == TipoDispositivo.MandoPlayStation
                ? PlayStation : Xbox;
            return tabla.TryGetValue(control, out var etiqueta) ? etiqueta : Legible(rutaMando);
        }

        public static string EtiquetaTeclado(string rutaTeclado)
        {
            string legible = Legible(rutaTeclado);
            if (string.IsNullOrEmpty(legible)) return "?";
            if (Teclado.TryGetValue(legible, out var traducido)) return traducido;
            // Una letra suelta se lee mejor en mayúscula: "t" -> "T".
            return legible.Length == 1 ? legible.ToUpperInvariant() : legible;
        }

        /// <summary>"&lt;Gamepad&gt;/buttonWest" -> "buttonWest".</summary>
        private static string Control(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return null;
            int barra = ruta.IndexOf('/');
            return barra < 0 ? ruta : ruta.Substring(barra + 1);
        }

        private static string Legible(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return null;
            return InputControlPath.ToHumanReadableString(
                ruta, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
    }
}
