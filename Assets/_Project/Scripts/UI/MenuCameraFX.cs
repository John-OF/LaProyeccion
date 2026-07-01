using UnityEngine;

namespace LaProyeccion.UI
{
    /// <summary>
    /// Da vida al fondo del menú haciendo un pulso lento del color de fondo de la
    /// cámara. Se combina con el post-procesado del "mundo real" (grano + viñeta)
    /// para que el fondo no se vea estático. Colocar en la Main Camera del menú.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MenuCameraFX : MonoBehaviour
    {
        [SerializeField] private Color colorA = new Color(0.02f, 0.03f, 0.05f);
        [SerializeField] private Color colorB = new Color(0.06f, 0.05f, 0.09f);
        [Tooltip("Ciclos por segundo del pulso de color.")]
        [SerializeField] private float speed = 0.15f;

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Update()
        {
            float t = (Mathf.Sin(Time.unscaledTime * speed * Mathf.PI * 2f) + 1f) * 0.5f;
            cam.backgroundColor = Color.Lerp(colorA, colorB, t);
        }
    }
}
