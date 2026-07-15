using UnityEngine;
using UnityEngine.InputSystem;
using LaProyeccion.Core;

namespace LaProyeccion.Player
{
    /// <summary>
    /// Controlador del personaje: movimiento horizontal, salto con coyote time,
    /// y disparador del cambio de mundo. Sin combate. Sin doble salto.
    /// Filosofía del GDD: legibilidad y precisión, no espectacularidad.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpForce = 12f;
        [Tooltip("Velocidad máxima de caída. Evita tunneling y mejora el feel.")]
        [SerializeField] private float maxFallSpeed = 25f;

        [Header("Coyote / Buffer")]
        [Tooltip("Ventana de gracia para saltar después de salir de una plataforma.")]
        [SerializeField] private float coyoteTime = 0.1f;
        [Tooltip("Si pulsas saltar justo antes de aterrizar, se ejecuta al tocar suelo.")]
        [SerializeField] private float jumpBufferTime = 0.1f;

        [Header("Detección de suelo")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.15f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Caída")]
        [Tooltip("Si el jugador cae por debajo de esta altura, reaparece en el último punto seguro.")]
        [SerializeField] private float fallLimit = -50f;

        private Rigidbody2D rb;
        private PlayerInputActions input;
        private Vector2 moveInput;
        private float coyoteCounter;
        private float jumpBufferCounter;
        private bool isGrounded;
        private bool hasJumpedThisAirtime;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.gravityScale = 3f; // sensación más sólida; ajustable
            input = new PlayerInputActions();
        }

        private void OnEnable()
        {
            // Guard: en recompilaciones durante Play, Unity puede llamar OnEnable
            // antes de que Awake haya recreado 'input' (queda null tras el domain reload).
            if (input == null) return;
            input.Player.Enable();
            input.Player.Jump.performed += OnJumpPressed;
            input.Player.SwitchWorld.performed += OnSwitchPressed;
        }

        private void OnDisable()
        {
            if (input == null) return;
            input.Player.Jump.performed -= OnJumpPressed;
            input.Player.SwitchWorld.performed -= OnSwitchPressed;
            input.Player.Disable();
        }

        private void Update()
        {
            if (!InputDelegado) moveInput = input.Player.Move.ReadValue<Vector2>();

            isGrounded = Physics2D.OverlapCircle(
                groundCheck.position, groundCheckRadius, groundLayer);

            // Coyote: tiempo de gracia tras dejar el suelo
            if (isGrounded) coyoteCounter = coyoteTime;
            else coyoteCounter -= Time.deltaTime;

            // Buffer de salto decae con el tiempo
            jumpBufferCounter -= Time.deltaTime;

            // Resolución del salto: solo si NO hemos saltado ya en esta etapa de aire
            if (jumpBufferCounter > 0f && coyoteCounter > 0f && !hasJumpedThisAirtime)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpBufferCounter = 0f;
                coyoteCounter = 0f;
                hasJumpedThisAirtime = true;
                AudioManager.Instance?.PlayJump();
            }

            // Reset de la flag: solo cuando tocamos suelo Y no estamos subiendo
            if (isGrounded && rb.linearVelocity.y <= 0.01f)
            {
                hasJumpedThisAirtime = false;
            }


            // Si caemos fuera del nivel, reaparecemos en el último punto seguro
            // (autoguardado tras puzzle / checkpoint), no al inicio.
            if (transform.position.y < fallLimit)
            {
                if (GameSession.Instance != null)
                {
                    GameSession.Instance.RespawnPlayer();
                }
                else
                {
                    transform.position = new Vector3(0f, 5f, 0f);
                    rb.linearVelocity = Vector2.zero;
                }
            }
        }

        private void FixedUpdate()
        {
            // Velocidad horizontal directa, sin aceleración exagerada (GDD §4.2)
            rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
            // Clamp de velocidad de caída
            if (rb.linearVelocity.y < -maxFallSpeed)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
            }
        }

        private void OnJumpPressed(InputAction.CallbackContext _)
        {
            if (InputDelegado) return;
            jumpBufferCounter = jumpBufferTime;
        }

        /// <summary>
        /// Seam de inyección para el replay de pasadas (Prototipos, idea #8):
        /// con InputDelegado el teclado deja de mover/saltar/cambiar y el
        /// ReproductorDePasada inyecta por los métodos de abajo — que pasan por
        /// los MISMOS caminos internos (buffer de salto incluido), no los puentean.
        /// </summary>
        public bool InputDelegado { get; set; }

        /// <summary>Fija el eje horizontal mientras InputDelegado está activo.</summary>
        public void InyectarMove(float x)
        {
            if (InputDelegado) moveInput = new Vector2(x, 0f);
        }

        /// <summary>Equivale a pulsar saltar: carga el jump buffer, como el teclado.</summary>
        public void InyectarSalto()
        {
            if (InputDelegado) jumpBufferCounter = jumpBufferTime;
        }

        /// <summary>
        /// Si otro componente asume la tecla de cambio (p.ej. el prototipo de
        /// vistazo/peek, que distingue tap = cambiar de hold = mirar), lo anuncia
        /// aquí y este controlador deja de disparar TrySwitchWorld directamente.
        /// </summary>
        public bool CambioDeMundoDelegado { get; set; }

        private void OnSwitchPressed(InputAction.CallbackContext _)
        {
            if (CambioDeMundoDelegado || InputDelegado) return;
            WorldManager.Instance?.TrySwitchWorld();
        }

        // Visualización del ground check en el Editor
        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}