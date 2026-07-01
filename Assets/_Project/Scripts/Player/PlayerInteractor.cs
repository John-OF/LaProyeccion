using UnityEngine;
using UnityEngine.InputSystem;
using LaProyeccion.Puzzles;

namespace LaProyeccion.Player
{
    /// <summary>
    /// Detecta el Interactable más cercano dentro de un radio
    /// y lo activa con la acción Interact (E / Button West).
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private float interactionRadius = 1.2f;
        [SerializeField] private LayerMask interactableLayer;

        private PlayerInputActions input;
        private Interactable currentTarget;

        private void Awake()
        {
            input = new PlayerInputActions();
        }

        private void OnEnable()
        {
            if (input == null) return;
            input.Player.Enable();
            input.Player.Interact.performed += OnInteract;
        }

        private void OnDisable()
        {
            if (input == null) return;
            input.Player.Interact.performed -= OnInteract;
            input.Player.Disable();
        }

        private void Update()
        {
            // Buscamos el Interactable más cercano cada frame.
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position, interactionRadius, interactableLayer);

            Interactable closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var interactable = hit.GetComponent<Interactable>();
                if (interactable == null || !interactable.IsAvailable) continue;

                float d = Vector2.Distance(transform.position, hit.transform.position);
                if (d < closestDist)
                {
                    closest = interactable;
                    closestDist = d;
                }
            }

            // Notificar cambios de target (entrada/salida del rango).
            if (closest != currentTarget)
            {
                if (currentTarget != null) currentTarget.OnPlayerExitRange();
                currentTarget = closest;
                if (currentTarget != null) currentTarget.OnPlayerEnterRange();
            }
        }

        private void OnInteract(InputAction.CallbackContext _)
        {
            if (currentTarget != null && currentTarget.IsAvailable)
            {
                currentTarget.Interact();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}