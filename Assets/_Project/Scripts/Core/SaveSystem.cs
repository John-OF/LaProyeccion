using UnityEngine;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Sistema de guardado mínimo sobre PlayerPrefs.
    /// Por ahora solo registra que existe una partida y en qué escena/spawn
    /// retomarla. Es la base para enlazar más adelante con <see cref="Checkpoint"/>
    /// y un guardado de progreso real.
    /// </summary>
    public static class SaveSystem
    {
        const string KeyExists = "save.exists";
        const string KeyScene = "save.scene";
        const string KeySpawnX = "save.spawnX";
        const string KeySpawnY = "save.spawnY";
        const string KeyWorldUnlocked = "save.worldUnlocked";
        const string KeySwitches = "save.switches";

        /// <summary>
        /// No se persiste: indica que el jugador entró por "Continuar" y que la escena
        /// de juego debe reposicionarlo en el punto guardado (lo consume GameSession).
        /// </summary>
        public static bool ContinueRequested { get; set; }

        /// <summary>True si hay una partida guardada que se pueda continuar.</summary>
        public static bool HasSave() => PlayerPrefs.GetInt(KeyExists, 0) == 1;

        /// <summary>
        /// Empieza una partida nueva: borra el progreso previo (así "Continuar" solo
        /// se habilita tras el primer autoguardado) y registra la escena de arranque.
        /// </summary>
        public static void NewGame(string sceneName)
        {
            Clear();
            PlayerPrefs.SetString(KeyScene, sceneName);
            PlayerPrefs.Save();
        }

        /// <summary>Escena a cargar al continuar. Fallback al nombre dado si no hay guardado.</summary>
        public static string GetSaveScene(string fallback = "SampleScene")
            => PlayerPrefs.GetString(KeyScene, fallback);

        /// <summary>Punto de aparición guardado (para uso futuro con checkpoints).</summary>
        public static Vector2 GetSpawn()
            => new Vector2(PlayerPrefs.GetFloat(KeySpawnX, 0f), PlayerPrefs.GetFloat(KeySpawnY, 0f));

        /// <summary>Actualiza el punto de continuación (escena + posición).</summary>
        public static void SaveProgress(string sceneName, Vector2 spawn)
        {
            PlayerPrefs.SetInt(KeyExists, 1);
            PlayerPrefs.SetString(KeyScene, sceneName);
            PlayerPrefs.SetFloat(KeySpawnX, spawn.x);
            PlayerPrefs.SetFloat(KeySpawnY, spawn.y);
            PlayerPrefs.Save();
        }

        // ==================== Estado del mundo ====================

        /// <summary>Guarda si el poder de cambio de mundo está desbloqueado.</summary>
        public static void SetWorldUnlocked(bool unlocked)
        {
            PlayerPrefs.SetInt(KeyWorldUnlocked, unlocked ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool GetWorldUnlocked() => PlayerPrefs.GetInt(KeyWorldUnlocked, 0) == 1;

        /// <summary>
        /// Guarda el estado on/off de los switches, serializado como
        /// "nombre:1;nombre2:0;...".
        /// </summary>
        public static void SetSwitchStates(string serialized)
        {
            PlayerPrefs.SetString(KeySwitches, serialized ?? "");
            PlayerPrefs.Save();
        }

        public static string GetSwitchStates() => PlayerPrefs.GetString(KeySwitches, "");

        // ==================== Utilidad ====================

        /// <summary>Borra la partida guardada por completo.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(KeyExists);
            PlayerPrefs.DeleteKey(KeyScene);
            PlayerPrefs.DeleteKey(KeySpawnX);
            PlayerPrefs.DeleteKey(KeySpawnY);
            PlayerPrefs.DeleteKey(KeyWorldUnlocked);
            PlayerPrefs.DeleteKey(KeySwitches);
            PlayerPrefs.Save();
        }
    }
}
