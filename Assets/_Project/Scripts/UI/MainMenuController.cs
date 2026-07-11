using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LaProyeccion.Core;

namespace LaProyeccion.UI
{
    /// <summary>
    /// Controla el menú principal: navegación entre los tres paneles
    /// (principal, jugar, opciones) y las acciones de arranque de partida.
    ///
    /// Los onClick de los botones se cablean por código en Awake (AddListener)
    /// en lugar de UnityEvents serializados: más simple de asignar y menos frágil.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Paneles")]
        [SerializeField] private GameObject panelMain;
        [SerializeField] private GameObject panelPlay;
        [SerializeField] private GameObject panelOptions;

        [Header("Botones - Principal")]
        [SerializeField] private Button buttonPlay;
        [SerializeField] private Button buttonOptions;
        [SerializeField] private Button buttonQuit;

        [Header("Botones - Jugar")]
        [SerializeField] private Button buttonNewGame;
        [SerializeField] private Button buttonContinue;
        [SerializeField] private Button buttonBackFromPlay;

        [Header("Botones - Opciones")]
        [SerializeField] private Button buttonBackFromOptions;

        [Header("Config")]
        [Tooltip("Escena a la que entra 'Nueva partida'.")]
        [SerializeField] private string gameSceneName = "SampleScene";

        private PlayerInputActions input;

        private void Awake()
        {
            input = new PlayerInputActions();

            Wire(buttonPlay, ShowPlay);
            Wire(buttonOptions, ShowOptions);
            Wire(buttonQuit, OnQuit);

            Wire(buttonNewGame, OnNewGame);
            Wire(buttonContinue, OnContinue);
            Wire(buttonBackFromPlay, ShowMain);

            Wire(buttonBackFromOptions, ShowMain);
        }

        private void Start()
        {
            ShowMain();
            // 'Continuar' solo disponible si hay partida guardada.
            if (buttonContinue != null)
                buttonContinue.interactable = SaveSystem.HasSave();
        }

        private void OnEnable()
        {
            // Guard: en recompilaciones durante Play, OnEnable puede llegar antes
            // de que Awake recree 'input' (null tras el domain reload).
            if (input == null) return;
            input.Player.Cancel.performed += OnCancelPressed;
            input.Player.Cancel.Enable();
        }

        private void OnDisable()
        {
            if (input == null) return;
            input.Player.Cancel.performed -= OnCancelPressed;
            input.Player.Cancel.Disable();
        }

        /// <summary>
        /// B (Xbox) / Círculo (PS) = atrás: desde Jugar u Opciones vuelve al
        /// panel principal. En el panel principal no hace nada (evita salir
        /// del juego por accidente).
        /// </summary>
        private void OnCancelPressed(InputAction.CallbackContext _)
        {
            if (panelPlay != null && panelPlay.activeSelf) { ShowMain(); return; }
            if (panelOptions != null && panelOptions.activeSelf) ShowMain();
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b == null) return;
            b.onClick.RemoveListener(action);
            b.onClick.AddListener(action);
        }

        // ==================== Navegación ====================

        public void ShowMain()
        {
            SwitchPanel(panelMain);
            Select(buttonPlay);
        }

        public void ShowPlay()
        {
            SwitchPanel(panelPlay);
            // Empezar sobre 'Continuar' si está disponible; si no, 'Nueva partida'.
            Select(buttonContinue != null && buttonContinue.interactable ? buttonContinue : buttonNewGame);
        }

        public void ShowOptions()
        {
            SwitchPanel(panelOptions);
            SelectFirstIn(panelOptions);
        }

        private void SwitchPanel(GameObject target)
        {
            if (panelMain != null) panelMain.SetActive(target == panelMain);
            if (panelPlay != null) panelPlay.SetActive(target == panelPlay);
            if (panelOptions != null) panelOptions.SetActive(target == panelOptions);
        }

        /// <summary>
        /// Fija el objeto seleccionado del EventSystem para que la navegación con
        /// teclado/gamepad tenga un punto de partida en cada panel.
        /// </summary>
        private static void Select(Button button)
        {
            if (button == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        /// <summary>Selecciona el primer elemento navegable (Selectable) de un panel.</summary>
        private static void SelectFirstIn(GameObject panel)
        {
            if (panel == null || EventSystem.current == null) return;
            var sel = panel.GetComponentInChildren<Selectable>();
            EventSystem.current.SetSelectedGameObject(null);
            if (sel != null) EventSystem.current.SetSelectedGameObject(sel.gameObject);
        }

        // ==================== Acciones ====================

        public void OnNewGame()
        {
            SaveSystem.NewGame(gameSceneName);
            SaveSystem.ContinueRequested = false; // arranca desde el inicio de la escena
            SceneManager.LoadScene(gameSceneName);
        }

        public void OnContinue()
        {
            if (!SaveSystem.HasSave()) return;
            SaveSystem.ContinueRequested = true; // GameSession reposiciona al jugador en el punto guardado
            SceneManager.LoadScene(SaveSystem.GetSaveScene(gameSceneName));
        }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
