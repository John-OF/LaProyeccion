using UnityEngine;

namespace LaProyeccion.Core
{
    /// <summary>
    /// Punto seguro al que el jugador respawnea.
    /// Para Zona 0 basta con un solo checkpoint global = posición de spawn.
    /// Más adelante haremos triggers de checkpoint dinámicos.
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        public static Checkpoint Active { get; private set; }

        [SerializeField] private bool isStartCheckpoint = true;

        private void Awake()
        {
            if (isStartCheckpoint && Active == null)
                Active = this;
        }

        public Vector3 Position => transform.position;

        /// <summary>Permite cambiar el checkpoint activo desde otros sistemas.</summary>
        public static void SetActive(Checkpoint c)
        {
            Active = c;
        }
    }
}