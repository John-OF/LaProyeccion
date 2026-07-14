using UnityEngine;
using UnityEngine.Tilemaps;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE v1.1;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Variante TILEMAP de <see cref="SueloGlicheado"/>: un solo componente en el
    /// GameObject del tilemap cubre TODOS los tiles glicheados pintados en él.
    /// Binomio masivo/puntual, igual que TilemapDualLayer vs WorldExclusivePresence:
    /// - Tilemap (este script): suelos glicheados alineados a la cuadrícula, se
    ///   pintan con el pincel de tiles.
    /// - Caja (SueloGlicheado): parches fuera de cuadrícula, rotados o móviles.
    ///
    /// A diferencia de la caja (trigger + margen superior), aquí el collider es
    /// SÓLIDO y la muerte llega por contacto físico (OnCollisionEnter2D/Stay2D):
    /// al posarse encima hay contacto garantizado, sin necesidad de margen. La
    /// superficie sigue siendo piso real, lo que deja abierta la evolución prevista
    /// (transitable en un mundo, letal en el otro) vía WorldManager.OnWorldChanged.
    ///
    /// Presencia por mundo: sin componente extra vive en ambos mundos; con
    /// TilemapDualLayer al lado, solo en el mundo que le toque.
    ///
    /// Montaje esperado (mismo que los tilemaps de suelo normales):
    /// Tilemap + TilemapRenderer + TilemapCollider2D (Composite Operation = Merge)
    /// + Rigidbody2D (Static) + CompositeCollider2D (Geometry Type = POLYGONS).
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class SueloGlicheadoTilemap : MonoBehaviour
    {
        [Header("Legibilidad (Pilar 3)")]
        [Tooltip("Tinte aplicado al tilemap entero para que el peligro se lea, " +
                 "sin necesitar tiles dedicados: se pinta con cualquier tile y " +
                 "este color lo marca como corrupto.")]
        [SerializeField] private Color tinte = new Color(1f, 0.15f, 0.6f, 1f); // magenta corrupto

        private void OnCollisionEnter2D(Collision2D collision) => TryKill(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => TryKill(collision.collider);

        private void TryKill(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            GameSession.Instance?.RespawnPlayer();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            // Tocar el Tilemap directamente en OnValidate dispara un SendMessage
            // prohibido (warning del editor); se difiere un frame.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return; // el objeto pudo destruirse entre tanto
                var tilemap = GetComponent<Tilemap>();
                if (tilemap != null && tilemap.color != tinte) tilemap.color = tinte;
            };
#endif
        }
    }
}
