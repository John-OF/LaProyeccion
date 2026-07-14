using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE v1.1;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Piso/suelo glicheado (idea del autor, 2026-07-13): plataforma corrompida
    /// que es instakill al contacto. Tercer sabor de peligro tras el contacto con
    /// el guardia (<see cref="Corrector"/>) y la vigilancia letal
    /// (FocoVigilancia / ZonaVigilada).
    ///
    /// VERSIÓN 1 — LETAL PERMANENTE: nunca es seguro pisarla, en ningún mundo.
    /// El objeto es a la vez SUPERFICIE SÓLIDA (para que se lea como un piso y para
    /// que versiones futuras puedan hacerla transitable en un mundo) y VOLUMEN LETAL
    /// (un trigger que cubre la superficie más un margen por encima, para atrapar al
    /// jugador en el instante en que se posa encima). Al tocar → respawn en el último
    /// punto seguro (<see cref="GameSession.RespawnPlayer"/>).
    ///
    /// Evolución prevista (versiones siguientes, NO en v1): condicionar la letalidad
    /// al mundo (segura en uno, letal en el otro) suscribiéndose a
    /// WorldManager.OnWorldChanged.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class SueloGlicheado : MonoBehaviour
    {
        [Header("Superficie")]
        [Tooltip("Si es sólida, el jugador puede posarse encima (piso real). En v1 " +
                 "morirá igual al tocarla; importa para las versiones futuras.")]
        [SerializeField] private bool solida = true;

        [Tooltip("Cuánto sobresale el volumen letal por encima de la superficie, para " +
                 "atrapar al jugador que se posa encima (Pilar 3: el peligro es legible).")]
        [SerializeField, Min(0f)] private float margenLetalArriba = 0.6f;

        private BoxCollider2D letal;   // trigger que mata
        private BoxCollider2D solido;  // collider físico opcional (la superficie)

        private void Awake()
        {
            // El BoxCollider2D propio es el VOLUMEN LETAL (trigger).
            letal = GetComponent<BoxCollider2D>();
            letal.isTrigger = true;

            Vector2 baseSize = letal.size;
            Vector2 baseOffset = letal.offset;

            // Extiende el trigger hacia arriba para cubrir al jugador posado encima.
            letal.size = new Vector2(baseSize.x, baseSize.y + margenLetalArriba);
            letal.offset = new Vector2(baseOffset.x, baseOffset.y + margenLetalArriba * 0.5f);

            if (solida)
            {
                // Superficie física: la caja visible original, sin el margen letal.
                solido = gameObject.AddComponent<BoxCollider2D>();
                solido.isTrigger = false;
                solido.size = baseSize;
                solido.offset = baseOffset;
            }
        }

        private void OnTriggerEnter2D(Collider2D other) => TryKill(other);
        private void OnTriggerStay2D(Collider2D other) => TryKill(other);

        private void TryKill(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            GameSession.Instance?.RespawnPlayer();
        }

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.15f, 0.6f, 0.20f);   // magenta corrupto
            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = new Color(1f, 0.15f, 0.6f, 0.9f);
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
