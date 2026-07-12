using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Narrative
{
    /// <summary>
    /// Reacción one-shot de Keplin a la PRIMERA muerte del jugador en la zona
    /// (F2.P1.7). Se suscribe a GameSession.OnPlayerRespawned; tras disparar una
    /// vez, calla para siempre (una instancia por escena → una vez por zona).
    /// El texto es placeholder de diseño (regla narrativa: el agente no inventa lore).
    /// </summary>
    public class KeplinFirstDeathReaction : MonoBehaviour
    {
        [SerializeField, TextArea] private string message =
            "[TEXTO PENDIENTE: primera reacción de Keplin a una muerte del jugador — tono administrativo, registra el incidente]";

        private bool fired;

        private void OnEnable() => GameSession.OnPlayerRespawned += OnRespawn;
        private void OnDisable() => GameSession.OnPlayerRespawned -= OnRespawn;

        private void OnRespawn()
        {
            if (fired) return;
            fired = true;
            KeplinMessageController.Instance?.ShowMessage(message);
        }
    }
}
