using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using LaProyeccion.Player;
using LaProyeccion.Puzzles;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Coordina la sesión de juego dentro de una escena jugable:
    /// - Autoguardado tras cada puzzle (escena, posición, poder de cambio de mundo,
    ///   estado de los switches). Ver <see cref="Gate"/>.
    /// - Al continuar partida, reposiciona al jugador y restaura ese estado.
    /// - Respawn dentro de la partida: si el jugador cae, reaparece en el último
    ///   punto seguro (el del último autoguardado), no al inicio.
    ///
    /// Colocar un GameObject con este componente en cada escena jugable.
    /// El jugador se autodetecta si no se asigna manualmente.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        /// <summary>True mientras se restaura un guardado (evita autoguardados en cadena).</summary>
        public static bool IsRestoring { get; private set; }

        [Tooltip("Transform del jugador. Si se deja vacío, se busca un PlayerController en la escena.")]
        [SerializeField] private Transform player;

        /// <summary>Último punto seguro de reaparición dentro de la partida.</summary>
        public Vector3 CurrentRespawn { get; private set; }

        /// <summary>
        /// True si esta escena arrancó restaurando un guardado ("Continuar").
        /// Lo consultan sistemas que deben comportarse distinto en la primera
        /// entrada a una zona (p. ej. ZoneEntry no pisa el guardado restaurado).
        /// </summary>
        public bool RestoredFromContinue { get; private set; }

        private Rigidbody2D playerBody;

        private void Awake()
        {
            Instance = this;
            if (player == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) player = pc.transform;
            }
            if (player != null) playerBody = player.GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            // Al continuar: reposicionar en el punto guardado y restaurar el estado del mundo.
            if (SaveSystem.ContinueRequested && SaveSystem.HasSave() && player != null)
            {
                Vector2 spawn = SaveSystem.GetSpawn();
                player.position = new Vector3(spawn.x, spawn.y, player.position.z);
                RestoreWorldState();
                RestoredFromContinue = true;
            }
            SaveSystem.ContinueRequested = false;

            // Punto de respawn inicial = donde arranca el jugador (start o punto continuado).
            if (player != null) CurrentRespawn = player.position;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ==================== Guardado ====================

        /// <summary>
        /// Guarda el progreso actual: escena + posición del jugador (que pasa a ser el
        /// nuevo punto de respawn), poder de cambio de mundo y estado de los switches.
        /// </summary>
        public void SaveNow()
        {
            if (player == null) return;

            CurrentRespawn = player.position;
            SaveSystem.SaveProgress(SceneManager.GetActiveScene().name, player.position);
            SaveSystem.SetWorldUnlocked(WorldManager.Instance != null && WorldManager.Instance.IsSwitchEnabled);
            SaveSystem.SetSwitchStates(SerializeSwitches());
        }

        /// <summary>Autoguardado conveniente para llamar desde cualquier sistema (p. ej. Gate).</summary>
        public static void AutoSave()
        {
            if (Instance != null) Instance.SaveNow();
        }

        // ==================== Respawn en sesión ====================

        /// <summary>Reaparece al jugador en el último punto seguro y frena su velocidad.</summary>
        public void RespawnPlayer()
        {
            if (player == null) return;
            player.position = CurrentRespawn;
            if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
        }

        // ==================== Estado del mundo ====================

        private void RestoreWorldState()
        {
            IsRestoring = true;

            if (SaveSystem.GetWorldUnlocked() && WorldManager.Instance != null)
                WorldManager.Instance.RestoreSwitchEnabled(true);

            ApplySwitchStates(SaveSystem.GetSwitchStates());

            IsRestoring = false;
        }

        private static string SerializeSwitches()
        {
            var switches = FindObjectsByType<DualSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sb = new StringBuilder();
            foreach (var s in switches)
                sb.Append(s.name).Append(':').Append(s.IsOn ? '1' : '0').Append(';');
            return sb.ToString();
        }

        private static void ApplySwitchStates(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            var switches = FindObjectsByType<DualSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var entry in data.Split(';'))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                int idx = entry.LastIndexOf(':');
                if (idx <= 0) continue;

                string name = entry.Substring(0, idx);
                bool on = entry.Substring(idx + 1) == "1";
                foreach (var s in switches)
                    if (s.name == name) s.SetState(on);
            }
        }
    }
}
