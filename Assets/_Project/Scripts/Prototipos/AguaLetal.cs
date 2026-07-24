using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): lámina de AGUA NEGRA (T4). En la
    /// oscuridad lee como roca sólida; la trampa es que NO lo es. REGLA DEL AGUA de la
    /// cueva (ideas.md): el agua SIEMPRE es letal, y una piedra SIEMPRE suena distinto al
    /// caer en ella (el chapoteo lo dispara la propia <see cref="PiedraLanzada"/>).
    ///
    /// Aquí solo se resuelve al JUGADOR: al tocarla, corrección (respawn en el último
    /// punto seguro), como el resto de peligros de la cueva — peligro físico indiferente,
    /// no del sistema. Trigger, para que el jugador "entre" en ella y no rebote.
    ///
    /// Sin filtro por mundo (a propósito): el agua de la cueva es Real y física. Si el
    /// juego real la quiere condicionada por mundo, se filtra entonces.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AguaLetal : MonoBehaviour
    {
        private void Reset() => GetComponent<Collider2D>().isTrigger = true;

        private void Awake() => GetComponent<Collider2D>().isTrigger = true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            AudioManager.Instance?.PlayDeath();
            GameSession.Instance?.RespawnPlayer();
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.color = new Color(0.2f, 0.4f, 0.9f, 0.15f);
                Gizmos.DrawCube(box.offset, box.size);
                Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.7f);
                Gizmos.DrawWireCube(box.offset, box.size);
            }
        }
    }
}
