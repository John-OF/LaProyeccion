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

        [Header("Agacharse (2026-07-27) — moverse despacio y en silencio")]
        // Añadido para el GuardianCiego, que oye moverse al jugador. Se eligió agacharse
        // frente a un modificador de andar/correr por LEGIBILIDAD (Pilar 3): en la oscuridad
        // de la cueva, "voy en silencio" tiene que verse en la SILUETA, no vivir en tus dedos.
        // NO toca el collider a propósito (decisión del autor): es pose + velocidad.
        // Restricción de diseño que esto impone: JAMÁS construir un paso que exija ir
        // agachado, porque el cuerpo físico sigue midiendo 1,20 — el visual no puede mentir.
        [Tooltip("Multiplica moveSpeed al ir agachado. Con moveSpeed=8 y 0.35 -> 2.8, por debajo " +
                 "del umbral de ruido del GuardianCiego (5): agachado es silencioso.")]
        [SerializeField, Range(0.1f, 1f)] private float factorAgachado = 0.35f;
        [Tooltip("Nodo VISUAL que se encoge al agacharse. Debe ser un HIJO, nunca la raíz: " +
                 "la raíz lleva la escala que dimensiona el BoxCollider2D. Si se deja vacío, " +
                 "el agachado sigue funcionando pero SIN pose (degradación consciente).")]
        [SerializeField] private Transform visual;
        [Tooltip("Escala vertical del visual al agacharse (1 = de pie). SOLO se usa si NO hay " +
                 "Animator: es el apaño de cuando no existía pose de agachado. Con Animator, la " +
                 "pose la da el sprite y aplastar además sería aplastar dos veces.")]
        [SerializeField, Range(0.4f, 1f)] private float escalaAgachado = 0.7f;

        [Header("Animación")]
        [Tooltip("Animator del nodo visual. Si está vacío, todo sigue funcionando sin animación " +
                 "(SampleScene, cuyo jugador no es instancia del prefab, entra por aquí).")]
        [SerializeField] private Animator animator;
        [Tooltip("Renderer que se voltea al mirar a la izquierda. Vacío = se busca en el visual.")]
        [SerializeField] private SpriteRenderer visualRenderer;
        [Tooltip("Velocidad horizontal mínima para considerar que el jugador se está girando. " +
                 "Evita que el sprite parpadee de lado con el stick en reposo.")]
        [SerializeField, Min(0.01f)] private float umbralGiro = 0.05f;

        private static readonly int PEnSuelo    = Animator.StringToHash("enSuelo");
        private static readonly int PVelocidadX = Animator.StringToHash("velocidadX");
        private static readonly int PVelocidadY = Animator.StringToHash("velocidadY");
        private static readonly int PAgachado   = Animator.StringToHash("agachado");

        /// <summary>Va agachado: despacio y en silencio. Solo en el suelo.</summary>
        public bool Agachado { get; private set; }

        private Rigidbody2D rb;
        private PlayerInputActions input;
        private Vector2 moveInput;
        private Vector3 visualEscalaBase;
        private Vector3 visualPosBase;
        private float alturaSpriteLocal = 1f;
        private float coyoteCounter;
        private float jumpBufferCounter;
        private bool isGrounded;
        private bool hasJumpedThisAirtime;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            // gravityScale NO se toca aquí: manda el valor del Rigidbody2D (3 en
            // PF_Player). Hasta el 2026-07-16 este Awake lo forzaba a 3 y pisaba
            // al Inspector, que serializaba 2 — el dial mentía y quien calculaba
            // la física leyendo el Inspector se equivocaba en un 50% (apex real
            // 2.45 u con g=90, no 3.68 con g=60). Fuente de verdad única: el prefab.
            input = new PlayerInputActions();

            if (visual != null)
            {
                visualEscalaBase = visual.localScale;
                visualPosBase = visual.localPosition;
                // Altura REAL del sprite en unidades locales, no la unidad de escala: el
                // sprite del jugador no mide 1 (mide ~1.16), así que compensar con 1 dejaba
                // los pies flotando ~1 px al agacharse. Se lee una vez y no cada frame porque
                // basta con el sprite base; las animaciones futuras conservan la misma altura.
                var srVisual = visual.GetComponent<SpriteRenderer>();
                if (srVisual != null && srVisual.sprite != null)
                    alturaSpriteLocal = srVisual.sprite.bounds.size.y;
                if (animator == null) animator = visual.GetComponent<Animator>();
                if (visualRenderer == null) visualRenderer = srVisual;
            }
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

            // Agacharse: solo en el suelo, y con el eje vertical del Move que ya existía
            // sin usar — cero bindings nuevos (Pilar 4), y en mando cae en el stick abajo.
            bool agacharse = isGrounded && moveInput.y <= -0.5f;
            if (agacharse != Agachado)
            {
                Agachado = agacharse;
                AplicarPose();
            }

            ActualizarVisual();

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
            // Velocidad horizontal directa, sin aceleración exagerada (GDD §4.2).
            // Agachado solo ESCALA esta velocidad: el salto, la gravedad y el resto de
            // la física calibrada quedan intactos.
            float velocidad = Agachado ? moveSpeed * factorAgachado : moveSpeed;
            rb.linearVelocity = new Vector2(moveInput.x * velocidad, rb.linearVelocity.y);
            // Clamp de velocidad de caída
            if (rb.linearVelocity.y < -maxFallSpeed)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
            }
        }

        /// <summary>
        /// Pose de agachado: encoge el nodo VISUAL y lo baja la mitad de lo encogido, para
        /// que los pies queden clavados en el suelo en vez de flotar. Nunca toca la raíz
        /// (que dimensiona el collider) ni el Rigidbody2D.
        /// </summary>
        /// <summary>
        /// Giro y parámetros del Animator. Se hace en Update (no en FixedUpdate) porque es
        /// presentación: debe seguir al render, no a la física.
        /// </summary>
        private void ActualizarVisual()
        {
            // GIRO: espejo del renderer, no un estado de animación. Se usa el INPUT y no la
            // velocidad para que el personaje se gire aunque esté empujando contra una pared.
            if (visualRenderer != null && Mathf.Abs(moveInput.x) > umbralGiro)
                visualRenderer.flipX = moveInput.x < 0f;

            if (animator == null) return;
            animator.SetBool(PEnSuelo, isGrounded);
            animator.SetFloat(PVelocidadX, Mathf.Abs(rb.linearVelocity.x));
            animator.SetFloat(PVelocidadY, rb.linearVelocity.y);
            animator.SetBool(PAgachado, Agachado);
        }

        private void AplicarPose()
        {
            if (visual == null) return;   // sin nodo visual: mecánica sí, pose no
            // Con Animator hay sprite de agachado, así que aplastar la escala sería aplastar dos
            // veces. El apaño de la escala solo sigue vivo donde no hay animación.
            if (animator != null) return;
            if (Agachado)
            {
                visual.localScale = new Vector3(
                    visualEscalaBase.x, visualEscalaBase.y * escalaAgachado, visualEscalaBase.z);
                // Baja la MITAD de lo que ha encogido, para que los pies queden clavados.
                float encogido = visualEscalaBase.y * (1f - escalaAgachado) * alturaSpriteLocal;
                visual.localPosition = visualPosBase + new Vector3(0f, -encogido * 0.5f, 0f);
            }
            else
            {
                visual.localScale = visualEscalaBase;
                visual.localPosition = visualPosBase;
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