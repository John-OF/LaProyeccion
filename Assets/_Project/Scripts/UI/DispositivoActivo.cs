using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XInput;

namespace LaProyeccion.UI
{
    /// <summary>Con qué está jugando el jugador AHORA MISMO.</summary>
    public enum TipoDispositivo
    {
        Teclado = 0,
        MandoXbox = 1,
        MandoPlayStation = 2,
    }

    /// <summary>
    /// Detecta con qué se está jugando para que los prompts en pantalla enseñen la
    /// tecla correcta. No basta "teclado o mando": en un DualShock/DualSense el
    /// `buttonWest` es **□**, no **X**, y enseñar "X" ahí sería mentirle al jugador
    /// (Pilar 3, y el mismo criterio por el que `ControlsPanel` dibuja los controles
    /// desde los bindings reales en vez de pintarlos a mano).
    ///
    /// Se guía por el ÚLTIMO dispositivo que produjo entrada real, no por lo que haya
    /// conectado: con mando enchufado y jugando a teclado, deben salir teclas.
    ///
    /// ⚠️ No vale filtrar por `HasButtonPress()` (primer intento, 2026-07-25). Caminar
    /// con el teclado son TECLAS —botones— pero caminar con el mando es el **stick**,
    /// que es un eje. El resultado era asimétrico y el autor lo cazó enseguida: pasar
    /// de mando a teclado funcionaba, y de teclado a mando no, porque mover el stick
    /// no cuenta como "pulsación". Ahora se miran los controles que CAMBIARON con
    /// magnitud por encima de un umbral: eso incluye el stick al caminar y sigue
    /// descartando su deriva en reposo, que es lo que hacía falta filtrar.
    /// </summary>
    public static class DispositivoActivo
    {
        public static TipoDispositivo Actual { get; private set; } = TipoDispositivo.Teclado;

        /// <summary>Se dispara solo cuando CAMBIA (los prompts se redibujan aquí).</summary>
        public static event System.Action<TipoDispositivo> OnCambio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Inicializar()
        {
            // Estado estático: hay que reponerlo en cada Play (dominio no recargado).
            Actual = TipoDispositivo.Teclado;
            InputSystem.onEvent -= AlEvento;
            InputSystem.onEvent += AlEvento;
        }

        /// <summary>
        /// Deadzone para aceptar un eje como intención. 0.5 deja fuera la deriva de un
        /// stick en reposo (típicamente &lt;0.2) y entra de sobra al caminar (llega a 1).
        /// </summary>
        private const float UmbralEje = 0.5f;

        private static void AlEvento(InputEventPtr ptr, InputDevice dispositivo)
        {
            if (!ptr.IsA<StateEvent>() && !ptr.IsA<DeltaStateEvent>()) return;
            if (!HuboEntradaReal(ptr, dispositivo)) return;

            var nuevo = Clasificar(dispositivo);
            if (!nuevo.HasValue || nuevo.Value == Actual) return;

            Actual = nuevo.Value;
            OnCambio?.Invoke(Actual);
        }

        /// <summary>
        /// ¿Este evento es el jugador haciendo algo, o ruido? Cuenta cualquier control
        /// que haya cambiado por encima de <see cref="UmbralEje"/> — teclas, botones y
        /// **sticks al caminar**.
        ///
        /// El RATÓN es la excepción: solo cuenta si se pulsa. Moverlo sin querer no
        /// debería sacar los prompts del mando, y en este juego no se apunta con él.
        /// </summary>
        private static bool HuboEntradaReal(InputEventPtr ptr, InputDevice d)
        {
            if (d is Mouse) return ptr.HasButtonPress();

            foreach (var _ in ptr.EnumerateChangedControls(d, UmbralEje)) return true;
            return false;
        }

        private static TipoDispositivo? Clasificar(InputDevice d)
        {
            if (d is Keyboard || d is Mouse) return TipoDispositivo.Teclado;
            if (d is DualShockGamepad) return TipoDispositivo.MandoPlayStation;
            if (d is XInputController) return TipoDispositivo.MandoXbox;
            // Mando genérico (o layout no reconocido): la nomenclatura Xbox es la
            // más extendida en PC, así que es el fallback menos sorprendente.
            if (d is Gamepad) return TipoDispositivo.MandoXbox;
            return null;   // otros dispositivos no cambian el prompt
        }

        public static bool EsMando =>
            Actual == TipoDispositivo.MandoXbox || Actual == TipoDispositivo.MandoPlayStation;
    }
}
