using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.World
{
    /// <summary>
    /// Salida de zona (F1.P3): al tocarla el jugador (trigger 2D) o al activarla
    /// desde un Interactable (cablear OnInteract → Activate en el Inspector),
    /// autoguarda y funde a la escena destino.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ZoneExit : MonoBehaviour
    {
        [Tooltip("Nombre exacto de la escena destino (debe estar en Build Settings).")]
        [SerializeField] private string escenaDestino;

        [Tooltip("Si está activo, el jugador la dispara al tocar el trigger. " +
                 "Desactivar para usarla solo vía Interactable → Activate().")]
        [SerializeField] private bool activarPorTrigger = true;

        private bool used;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!activarPorTrigger) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            Activate();
        }

        /// <summary>Dispara la salida: autoguardado + fundido a la escena destino.</summary>
        public void Activate()
        {
            if (used) return;
            if (string.IsNullOrEmpty(escenaDestino))
            {
                Debug.LogWarning($"ZoneExit '{name}' sin escena destino asignada.", this);
                return;
            }
            used = true;
            GameSession.AutoSave();
            ScreenFader.FadeOutAndLoad(escenaDestino);
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider2D>();
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.offset, box.size);
                Gizmos.DrawWireCube(box.offset, box.size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }
    }
}
