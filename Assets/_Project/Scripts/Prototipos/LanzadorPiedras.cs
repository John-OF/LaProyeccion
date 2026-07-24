using UnityEngine;
using UnityEngine.InputSystem;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): el verbo LANZAR piedra. Vive en
    /// PF_Player (como override de la instancia del lab, NO en el prefab: el verbo aún
    /// no entra al juego). Gasta una piedra del <see cref="InventarioPiedras"/> y suelta
    /// una <see cref="PiedraLanzada"/> en arco hacia el lado que mira el jugador.
    ///
    /// El "lado que mira" se deduce de la velocidad horizontal (no hay facing expuesto en
    /// PlayerController y las animaciones aún no lo fijan): se cachea el último signo con
    /// movimiento real; parado, conserva el último. No toca la física del jugador.
    ///
    /// Input propio en código (una acción "Lanzar" con tecla F / hombro derecho), a
    /// propósito FUERA de PlayerInputActions: es un verbo de laboratorio y editar el
    /// .inputactions obligaría a regenerar la clase compartida. Al graduar el verbo al
    /// juego, se añade la acción al asset como es debido.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class LanzadorPiedras : MonoBehaviour
    {
        [Tooltip("Prefab de la piedra (Rigidbody2D + Collider2D + PiedraLanzada).")]
        [SerializeField] private GameObject prefabPiedra;

        [Header("Lanzamiento")]
        [SerializeField] private float velHorizontal = 8f;
        [SerializeField] private float velVertical = 6f;
        [Tooltip("Punto de salida relativo al jugador (x se voltea según el lado que mira).")]
        [SerializeField] private Vector2 offsetSalida = new Vector2(0.6f, 0.5f);

        private Rigidbody2D rb;
        private InventarioPiedras inv;
        private InputAction lanzar;
        private int facing = 1;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            inv = GetComponent<InventarioPiedras>();
            lanzar = new InputAction("Lanzar", InputActionType.Button);
            lanzar.AddBinding("<Keyboard>/f");
            lanzar.AddBinding("<Gamepad>/rightShoulder");
        }

        private void OnEnable()
        {
            if (lanzar == null) return; // guard del domain reload en Play (patrón del proyecto)
            lanzar.performed += OnLanzar;
            lanzar.Enable();
        }

        private void OnDisable()
        {
            if (lanzar == null) return;
            lanzar.performed -= OnLanzar;
            lanzar.Disable();
        }

        private void Update()
        {
            float vx = rb.linearVelocity.x;
            if (Mathf.Abs(vx) > 0.5f) facing = vx > 0f ? 1 : -1;
        }

        private void OnLanzar(InputAction.CallbackContext _)
        {
            if (Time.timeScale == 0f) return;         // no en pausa
            if (prefabPiedra == null) return;
            if (inv != null && !inv.TryConsume()) return; // sin piedras: no hace nada

            Vector3 origen = transform.position +
                             new Vector3(offsetSalida.x * facing, offsetSalida.y, 0f);
            var go = Instantiate(prefabPiedra, origen, Quaternion.identity);

            var prb = go.GetComponent<Rigidbody2D>();
            if (prb != null)
                prb.linearVelocity = new Vector2(velHorizontal * facing, velVertical);

            // Que no rebote en el propio jugador al salir.
            var pcol = GetComponent<Collider2D>();
            var rcol = go.GetComponent<Collider2D>();
            if (pcol != null && rcol != null) Physics2D.IgnoreCollision(rcol, pcol);
        }
    }
}
