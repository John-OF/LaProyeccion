using UnityEngine;
using UnityEngine.InputSystem;
using LaProyeccion.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (herramienta de laboratorio, idea #8 de Claude, ideas.md).
    ///
    /// Grabador de pasadas: F9 empieza/detiene la grabación mientras el autor
    /// juega. Muestrea por tick de FixedUpdate el eje de movimiento, los botones
    /// pulsados desde el tick anterior (salto, cambio de mundo, interactuar,
    /// sondear) y la posición del jugador, y al detener guarda todo como asset
    /// en Assets/_Project/Pasadas/ (solo en el Editor — los labs nunca van a
    /// build). Componente hermano de PlayerController en el jugador del lab.
    ///
    /// Escucha las ACCIONES del Input System (instancia propia de
    /// PlayerInputActions, patrón del proyecto), no las teclas: una pasada
    /// grabada con teclado se reproduce igual aunque el binding cambie.
    ///
    /// LIMITACIÓN aceptada en lab: en escenas con peek (P_Peek) la semántica
    /// tap/hold de SwitchWorld no se captura — se graba el `performed` crudo.
    /// </summary>
    [RequireComponent(typeof(LaProyeccion.Player.PlayerController))]
    public class GrabadorDePasada : MonoBehaviour
    {
        private PlayerInputActions input;
        private bool grabando;
        private PasadaGrabada pasada;
        private byte flags;

        public bool Grabando => grabando;

        private void Awake() => input = new PlayerInputActions();

        private void OnEnable()
        {
            // Guard del domain reload durante Play (patrón del proyecto).
            if (input == null) return;
            input.Player.Enable();
            input.Player.Jump.performed += OnJump;
            input.Player.SwitchWorld.performed += OnSwitch;
            input.Player.Interact.performed += OnInteract;
            input.Player.Sondear.performed += OnSondear;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Player.Jump.performed -= OnJump;
                input.Player.SwitchWorld.performed -= OnSwitch;
                input.Player.Interact.performed -= OnInteract;
                input.Player.Sondear.performed -= OnSondear;
                input.Player.Disable();
            }
            grabando = false; // descarga de escena en plena grabación: descartar
            pasada = null;
        }

        private void OnJump(InputAction.CallbackContext _) => flags |= (byte)PasadaGrabada.Botones.Salto;
        private void OnSwitch(InputAction.CallbackContext _) => flags |= (byte)PasadaGrabada.Botones.CambioMundo;
        private void OnInteract(InputAction.CallbackContext _) => flags |= (byte)PasadaGrabada.Botones.Interactuar;
        private void OnSondear(InputAction.CallbackContext _) => flags |= (byte)PasadaGrabada.Botones.Sondear;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f9Key.wasPressedThisFrame)
            {
                if (grabando) Detener();
                else Iniciar();
            }
        }

        private void FixedUpdate()
        {
            if (!grabando) return;
            pasada.muestras.Add(new PasadaGrabada.Muestra
            {
                moveX = input.Player.Move.ReadValue<Vector2>().x,
                botones = flags,
                pos = transform.position
            });
            flags = 0;
        }

        public void Iniciar()
        {
            if (grabando) return;
            pasada = ScriptableObject.CreateInstance<PasadaGrabada>();
            pasada.escena = gameObject.scene.name;
            pasada.fecha = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            pasada.spawn = transform.position;
            pasada.mundoInicial = WorldManager.Instance != null
                ? WorldManager.Instance.CurrentWorld : WorldState.Simulation;
            pasada.fixedDeltaTime = Time.fixedDeltaTime;
            flags = 0;
            ReproductorDePasada.SincronizarFasesDeGuardias(); // fase cero: ver docstring del método
            grabando = true;
            Debug.Log("[GrabadorDePasada] GRABANDO desde " + pasada.spawn
                + " (guardias reseteados a fase cero) — F9 para detener y guardar.");
        }

        public void Detener()
        {
            if (!grabando) return;
            grabando = false;
#if UNITY_EDITOR
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Pasadas"))
                AssetDatabase.CreateFolder("Assets/_Project", "Pasadas");
            string path = string.Format("Assets/_Project/Pasadas/Pasada_{0}_{1:yyyyMMdd_HHmmss}.asset",
                pasada.escena, System.DateTime.Now);
            AssetDatabase.CreateAsset(pasada, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GrabadorDePasada] Guardada: {path} ({pasada.muestras.Count} ticks, {pasada.Duracion:F1} s)");
#else
            Debug.LogWarning("[GrabadorDePasada] El guardado de pasadas solo existe en el Editor.");
#endif
            pasada = null;
        }
    }
}
