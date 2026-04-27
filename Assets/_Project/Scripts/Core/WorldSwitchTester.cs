using UnityEngine;
using LaProyeccion.Core;

/// <summary>
/// Script temporal del Día 1: hookea la acción SwitchWorld al WorldManager
/// y loguea el resultado. Borrar cuando exista PlayerController.
/// </summary>
public class WorldSwitchTester : MonoBehaviour
{
    private PlayerInputActions input;
    private bool subscribedToManager;

    private void Awake()
    {
        input = new PlayerInputActions();
    }

    private void OnEnable()
    {
        input.Player.Enable();
        input.Player.SwitchWorld.performed += OnSwitchPressed;

        WorldManager.OnWorldChanged += LogWorldChange;
        subscribedToManager = true;
    }

    private void OnDisable()
    {
        input.Player.SwitchWorld.performed -= OnSwitchPressed;
        input.Player.Disable();

        if (subscribedToManager)
        {
            WorldManager.OnWorldChanged -= LogWorldChange;
            subscribedToManager = false;
        }
    }

    private void OnSwitchPressed(UnityEngine.InputSystem.InputAction.CallbackContext _)
    {
        if (WorldManager.Instance == null)
        {
            Debug.LogError("[Tester] No hay WorldManager en la escena.");
            return;
        }

        bool ok = WorldManager.Instance.TrySwitchWorld();
        if (!ok) Debug.Log("[Tester] Cambio bloqueado por cooldown.");
    }

    private void LogWorldChange(WorldState newWorld)
    {
        Debug.Log($"<color=cyan>[WorldManager]</color> Mundo cambiado a: <b>{newWorld}</b>");
    }
}