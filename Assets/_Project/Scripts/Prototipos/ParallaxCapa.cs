using UnityEngine;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, pase de arte): capa de fondo en parallax.
    /// Desplaza su Transform una fracción de lo que se ha movido la cámara, para que
    /// los fondos lejanos se muevan menos que el plano de juego y den profundidad.
    ///
    /// La referencia es una posición de cámara FIJADA AL COLOCAR la capa, no la que
    /// tenga la cámara al arrancar: así lo que se ve en el editor con la cámara en esa
    /// posición es exactamente lo que se ve en Play, y no depende de dónde esté
    /// Cinemachine en el primer frame.
    ///
    /// Solo mueve su Transform. No toca colliders ni jugabilidad: es decorado.
    /// </summary>
    [DefaultExecutionOrder(10000)] // después de Cinemachine, que mueve la cámara en LateUpdate
    public class ParallaxCapa : MonoBehaviour
    {
        [Tooltip("0 = la capa está clavada al mundo (como el nivel). 1 = viaja con la cámara (cielo).")]
        [SerializeField, Range(0f, 1f)] private float factorX = 0.5f;
        [SerializeField, Range(0f, 1f)] private float factorY = 0f;

        [Tooltip("Posición de cámara con la que se colocó la capa en el editor.")]
        [SerializeField] private Vector2 camaraDeReferencia;

        [SerializeField] private Transform camara;

        private Vector3 origen;

        private void Start()
        {
            if (camara == null && Camera.main != null) camara = Camera.main.transform;
            origen = transform.position;
        }

        private void LateUpdate()
        {
            if (camara == null) return;
            float dx = camara.position.x - camaraDeReferencia.x;
            float dy = camara.position.y - camaraDeReferencia.y;
            transform.position = new Vector3(origen.x + dx * factorX, origen.y + dy * factorY, origen.z);
        }
    }
}
