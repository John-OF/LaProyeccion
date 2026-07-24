using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): volumen invisible bajo el
    /// nivel. Al caer el jugador dentro, lo reaparece en el último punto seguro
    /// (GameSession.RespawnPlayer). Evita la caída larguísima hasta el
    /// fallLimit=-50 del PlayerController cuando la cueva tiene fosas propias.
    ///
    /// Colócalo como una banda ancha y plana justo por debajo de la geometría
    /// más baja del nivel. BoxCollider2D en modo trigger.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ZonaMuerteCaida : MonoBehaviour
    {
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
            if (other.GetComponentInParent<PlayerController>() == null) return;
            GameSession.Instance?.RespawnPlayer();
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.15f);
            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.7f);
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
