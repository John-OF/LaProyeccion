using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LaProyeccion.Player;

namespace LaProyeccion.UI
{
    /// <summary>
    /// Menú de pausa in-game (GDD §9, prioridad 1). Alterna con la acción Pause
    /// (Esc / Start). En pausa: Time.timeScale = 0, el input del jugador queda
    /// sin efecto (se desactivan PlayerController/PlayerInteractor, así el cambio
    /// de mundo y la interacción no se disparan) y la navegación es por
    /// teclado/gamepad con selección inicial por panel (patrón MainMenuController).
    /// Todo el feedback visual del menú usa tiempo NO escalado (MenuButtonFX ya
    /// usa unscaledDeltaTime).
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        /// <summary>Estado global de pausa, consultable por otros sistemas.</summary>
        public static bool IsPaused { get; private set; }

        [Header("Paneles")]
        [Tooltip("Raíz visual del menú (fondo oscuro + subpaneles). Se activa solo en pausa.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject panelPause;
        [SerializeField] private GameObject panelOptions;

        [Header("Botones - Pausa")]
        [SerializeField] private Button buttonContinue;
        [SerializeField] private Button buttonOptions;
        [SerializeField] private Button buttonQuitToMenu;

        [Header("Botones - Opciones")]
        [SerializeField] private Button buttonBackFromOptions;

        [Header("Config")]
        [Tooltip("Escena que carga 'Salir al menú'. El progreso ya está autoguardado por los Gates.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private PlayerInputActions input;
        private PlayerController playerController;
        private PlayerInteractor playerInteractor;

        private void Awake()
        {
            input = new PlayerInputActions();

            Wire(buttonContinue, Resume);
            Wire(buttonOptions, ShowOptions);
            Wire(buttonQuitToMenu, QuitToMenu);
            Wire(buttonBackFromOptions, ShowPause);
        }

        private void Start()
        {
            // Referencias al jugador de la escena (cada zona instancia su PF_Player).
            playerController = FindFirstObjectByType<PlayerController>();
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            // Guard: en recompilaciones durante Play, OnEnable puede llegar antes
            // de que Awake recree 'input' (null tras el domain reload).
            if (input == null) return;
            input.Player.Pause.performed += OnPausePressed;
            input.Player.Pause.Enable();
            input.Player.Cancel.performed += OnCancelPressed;
            input.Player.Cancel.Enable();
        }

        private void OnDisable()
        {
            if (input == null) return;
            input.Player.Pause.performed -= OnPausePressed;
            input.Player.Pause.Disable();
            input.Player.Cancel.performed -= OnCancelPressed;
            input.Player.Cancel.Disable();
        }

        private void OnDestroy()
        {
            // Red de seguridad: si la escena se descarga en pausa, el tiempo
            // no puede quedarse congelado.
            if (IsPaused)
            {
                Time.timeScale = 1f;
                IsPaused = false;
            }
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b == null) return;
            b.onClick.RemoveListener(action);
            b.onClick.AddListener(action);
        }

        // ==================== Pausa ====================

        private void OnPausePressed(InputAction.CallbackContext _)
        {
            if (!IsPaused)
            {
                Pause();
            }
            else if (panelOptions != null && panelOptions.activeSelf)
            {
                // Esc dentro de Opciones vuelve al panel de pausa, no reanuda.
                ShowPause();
            }
            else
            {
                Resume();
            }
        }

        /// <summary>
        /// B (Xbox) / Círculo (PS) = atrás. Solo actúa en pausa: en Opciones
        /// vuelve al panel de pausa; en el panel de pausa reanuda. Durante el
        /// juego no hace nada (no abre el menú).
        /// </summary>
        private void OnCancelPressed(InputAction.CallbackContext _)
        {
            if (!IsPaused) return;
            if (panelOptions != null && panelOptions.activeSelf) ShowPause();
            else Resume();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
            SetPlayerInputEnabled(false);

            if (panelRoot != null) panelRoot.SetActive(true);
            ShowPause();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            SetPlayerInputEnabled(true);

            if (panelRoot != null) panelRoot.SetActive(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void QuitToMenu()
        {
            // Restaurar SIEMPRE el tiempo antes de cambiar de escena.
            IsPaused = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>
        /// En pausa el input del jugador no debe tener efecto (cambio de mundo,
        /// interacción, salto). Se desactivan los componentes: cada uno cierra su
        /// propio mapa de acciones en OnDisable y lo reabre en OnEnable.
        /// </summary>
        private void SetPlayerInputEnabled(bool enabled)
        {
            if (playerController != null) playerController.enabled = enabled;
            if (playerInteractor != null) playerInteractor.enabled = enabled;
        }

        // ==================== Navegación ====================

        public void ShowPause()
        {
            SwitchPanel(panelPause);
            Select(buttonContinue);
        }

        public void ShowOptions()
        {
            SwitchPanel(panelOptions);
            SelectFirstIn(panelOptions);
        }

        private void SwitchPanel(GameObject target)
        {
            if (panelPause != null) panelPause.SetActive(target == panelPause);
            if (panelOptions != null) panelOptions.SetActive(target == panelOptions);
        }

        private static void Select(Button button)
        {
            if (button == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private static void SelectFirstIn(GameObject panel)
        {
            if (panel == null || EventSystem.current == null) return;
            var sel = panel.GetComponentInChildren<Selectable>();
            EventSystem.current.SetSelectedGameObject(null);
            if (sel != null) EventSystem.current.SetSelectedGameObject(sel.gameObject);
        }
    }
}
