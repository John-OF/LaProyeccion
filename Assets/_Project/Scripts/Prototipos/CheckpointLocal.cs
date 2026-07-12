using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/): checkpoint que actualiza SOLO el punto
    /// de respawn en memoria (GameSession.SetRespawnPoint), sin escribir el
    /// guardado — las escenas de laboratorio jamás deben pisar la partida real
    /// del jugador (save.*). Si el concepto de "checkpoint dinámico" se valida,
    /// puede promoverse al juego (Apéndice D del PLAN).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CheckpointLocal : MonoBehaviour
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
            GameSession.Instance?.SetRespawnPoint(transform.position);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
