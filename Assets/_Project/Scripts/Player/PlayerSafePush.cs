using System.Collections;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Player
{
    /// <summary>
    /// Cuando cambia el mundo, comprueba si el Player quedó atrapado dentro
    /// de geometría sólida. Empuja en la dirección más corta. Si no logra
    /// liberarse, respawnea en el checkpoint activo.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerSafePush : MonoBehaviour
    {
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float maxPushDistance = 4f;
        [SerializeField] private float pushStep = 0.05f;

        private BoxCollider2D playerCollider;
        private Rigidbody2D rb;

        private static readonly Vector2[] PushDirections =
        {
            Vector2.up,
            Vector2.left,
            Vector2.right,
            Vector2.down
        };

        private void Awake()
        {
            playerCollider = GetComponent<BoxCollider2D>();
            rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable() => WorldManager.OnWorldChanged += HandleWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= HandleWorldChanged;

        private void HandleWorldChanged(WorldState _)
        {
            StartCoroutine(ResolveOverlapNextFrame());
        }

        private IEnumerator ResolveOverlapNextFrame()
        {
            yield return new WaitForFixedUpdate();

            if (!IsOverlapping(transform.position)) yield break;

            Vector2 originalPos = transform.position;
            Vector2? bestEscape = null;
            float bestDistance = float.MaxValue;

            foreach (var dir in PushDirections)
            {
                if (TryFindEscape(originalPos, dir, out Vector2 escape, out float distance))
                {
                    if (distance < bestDistance)
                    {
                        bestEscape = escape;
                        bestDistance = distance;
                    }
                }
            }

            if (bestEscape.HasValue)
            {
                transform.position = bestEscape.Value;
                if (rb != null) rb.linearVelocity = Vector2.zero;

                // Doble check de seguridad: si tras empujar sigue overlapeado, respawn.
                yield return new WaitForFixedUpdate();
                if (IsOverlapping(transform.position))
                {
                    Debug.Log("[SafePush] Empuje completado pero sigue atrapado. Forzando respawn.");
                    Respawn();
                }
            }
            else
            {
                Respawn();
            }
        }

        private bool TryFindEscape(Vector2 origin, Vector2 direction, out Vector2 freePos, out float distance)
        {
            float traveled = 0f;
            Vector2 testPos = origin;

            while (traveled < maxPushDistance)
            {
                testPos += direction * pushStep;
                traveled += pushStep;

                if (!IsOverlapping(testPos))
                {
                    freePos = testPos;
                    distance = traveled;
                    return true;
                }
            }

            freePos = Vector2.zero;
            distance = float.MaxValue;
            return false;
        }

        private bool IsOverlapping(Vector2 atPosition)
        {
            Vector2 center = atPosition + playerCollider.offset;
            Vector2 size = playerCollider.size * (Vector2)transform.lossyScale * 0.95f;
            return Physics2D.OverlapBox(center, size, 0f, groundLayer) != null;
        }

        private void Respawn()
        {
            if (Checkpoint.Active == null)
            {
                Debug.LogWarning("[SafePush] No hay checkpoint activo. Respawn cancelado.");
                return;
            }

            transform.position = Checkpoint.Active.Position;
            if (rb != null) rb.linearVelocity = Vector2.zero;

            var keplin = LaProyeccion.Narrative.KeplinMessageController.Instance;
            keplin?.ShowMessage("Sector reorganizado. Continúe.");
        }
    }
}