using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Narrative
{
    /// <summary>
    /// Suscriptor de un solo uso: cuando el switch se desbloquea por primera vez,
    /// Keplin emite su primer mensaje narrativo de la demo.
    /// </summary>
    public class KeplinFirstUnlockReaction : MonoBehaviour
    {
        [SerializeField, TextArea(2, 4)]
        private string firstMessage = "Tu comportamiento ha sido registrado.";

        private void OnEnable() => WorldManager.OnSwitchUnlocked += React;
        private void OnDisable() => WorldManager.OnSwitchUnlocked -= React;

        private void React()
        {
            KeplinMessageController.Instance?.ShowMessage(firstMessage);
        }
    }
}