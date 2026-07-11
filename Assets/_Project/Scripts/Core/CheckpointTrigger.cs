using UnityEngine;
using LaProyeccion.Player;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Punto de guardado invisible para tramos de plataformeo sin Gates (F1.P3):
    /// BoxCollider2D en modo trigger; al pasar el jugador, autoguarda (la posición
    /// guardada es la del jugador en ese momento, que pasa a ser su respawn).
    /// One-shot por defecto. Gizmo verde visible en el editor para diseñar.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CheckpointTrigger : MonoBehaviour
    {
        [Tooltip("Si está activo, solo guarda la primera vez que el jugador pasa.")]
        [SerializeField] private bool oneShot = true;

        private bool used;

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (used && oneShot) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;

            used = true;
            GameSession.AutoSave();
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
