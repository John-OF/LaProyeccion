using UnityEngine;
using UnityEngine.InputSystem;
using LaProyeccion.Prototipos;
using LaProyeccion.World;

namespace LaProyeccion.Player
{
    /// <summary>
    /// Agarrar y JALAR la caja empujable, además de empujarla.
    ///
    /// POR QUÉ EXISTE: empujar solo es un verbo que el jugador puede usar para
    /// arruinar el nivel sin hacer nada raro. Basta con meter la caja en una
    /// esquina —contra una pared, contra el marco de una puerta— para que deje
    /// de ser recuperable, y si esa caja es la llave de un puzle, la partida
    /// queda muerta sin un solo error en pantalla. Con jalar, **todo empujón es
    /// reversible**: no hay estado del que no se pueda volver. Es la misma regla
    /// que la red del foso, aplicada al plano horizontal.
    ///
    /// MANTENER PULSADO, no alternar: soltar la tecla suelta la caja, así que no
    /// existe el estado invisible "creía que la llevaba agarrada".
    ///
    /// Empujar SIN agarrar sigue funcionando exactamente igual que antes — la
    /// física de la caja no se toca. Esto es un modo extra que se pide, no un
    /// cambio del comportamiento validado en el laboratorio.
    ///
    /// La física del jugador tampoco se toca (está calibrada): mientras arrastra,
    /// la caja simplemente COPIA la velocidad horizontal del jugador, y la
    /// vertical se la deja a la gravedad — así sigue cayendo, y viaja bien encima
    /// del ascensor.
    /// </summary>
    // Después de PlayerController (orden 0): él escribe la velocidad del jugador
    // cada FixedUpdate y nosotros necesitamos leer la definitiva, no la anterior.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody2D))]
    public class AgarreDeCaja : MonoBehaviour
    {
        public const string RutaTeclado = "<Keyboard>/g";
        public const string RutaMando = "<Gamepad>/leftShoulder";

        [Tooltip("A qué distancia del BORDE de la caja se puede agarrar.")]
        [SerializeField, Min(0.2f)] private float alcance = 1.1f;
        [Tooltip("Si te separas más de esto, se suelta sola. Debe ser mayor que el alcance " +
                 "o el agarre se rompería en cuanto empieces a jalar.")]
        [SerializeField, Min(0.3f)] private float alcanceSuelta = 1.9f;

        private InputAction accion;
        private Rigidbody2D rb;
        private CajaEmpujable agarrada;
        private Rigidbody2D rbCaja;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            accion = new InputAction("AgarreDeCaja", InputActionType.Button);
            accion.AddBinding(RutaTeclado);
            accion.AddBinding(RutaMando);

            // Misma fuente de verdad que el resto de verbos: los carteles se
            // configuran desde LAS RUTAS de aquí, no escribiendo la letra a mano.
            foreach (var p in FindObjectsByType<LaProyeccion.UI.PromptDeTecla>(FindObjectsSortMode.None))
                if (p.Verbo == LaProyeccion.UI.PromptDeTecla.VerboLab.Agarre)
                    p.Configurar(RutaTeclado, RutaMando);
        }

        private void OnEnable() => accion.Enable();
        private void OnDisable() { accion.Disable(); Soltar(); }

        private void Update() => ActualizarPrompts();

        private void FixedUpdate()
        {
            bool pulsando = accion != null && accion.IsPressed();

            if (agarrada != null)
            {
                if (!pulsando || !SigueAgarrable(agarrada)) { Soltar(); return; }
                // Solo la X: la vertical es de la gravedad (y del ascensor).
                rbCaja.linearVelocity = new Vector2(rb.linearVelocity.x, rbCaja.linearVelocity.y);
                return;
            }

            if (pulsando) Agarrar(CajaMasCercana());
        }

        private void Agarrar(CajaEmpujable caja)
        {
            if (caja == null) return;
            var cuerpo = caja.GetComponent<Rigidbody2D>();
            // Clavada (existe en el mundo en el que no se empuja): es un apoyo, no
            // un objeto. Jalarla contradiría lo que su propio color está diciendo.
            if (cuerpo == null || cuerpo.bodyType == RigidbodyType2D.Static) return;

            agarrada = caja;
            rbCaja = cuerpo;
        }

        private void Soltar()
        {
            agarrada = null;
            rbCaja = null;
        }

        private CajaEmpujable CajaMasCercana()
        {
            CajaEmpujable mejor = null;
            float mejorD = float.MaxValue;
            foreach (var c in FindObjectsByType<CajaEmpujable>(FindObjectsSortMode.None))
            {
                float d = Distancia(c);
                if (d > alcance || d >= mejorD) continue;
                mejor = c; mejorD = d;
            }
            return mejor;
        }

        private bool SigueAgarrable(CajaEmpujable caja)
        {
            if (caja == null || rbCaja == null) return false;
            if (rbCaja.bodyType == RigidbodyType2D.Static) return false;
            return Distancia(caja) <= alcanceSuelta;
        }

        /// <summary>
        /// Al BORDE de la caja, no a su centro: medir al centro haría que una caja
        /// grande fuera inagarrable y una pequeña se agarrase desde lejos.
        /// </summary>
        private float Distancia(CajaEmpujable caja)
        {
            var col = caja.GetComponent<Collider2D>();
            if (col == null) return float.MaxValue;
            return Vector2.Distance(transform.position, col.ClosestPoint(transform.position));
        }

        /// <summary>El cartel dice lo que hará la tecla AHORA: agarrar o soltar.</summary>
        private void ActualizarPrompts()
        {
            foreach (var c in FindObjectsByType<CajaEmpujable>(FindObjectsSortMode.None))
            {
                var prompt = c.GetComponent<LaProyeccion.UI.PromptDeTecla>();
                if (prompt == null) continue;

                bool esLaQueLlevo = agarrada == c;
                var cuerpo = c.GetComponent<Rigidbody2D>();
                bool clavada = cuerpo == null || cuerpo.bodyType == RigidbodyType2D.Static;

                prompt.Ocultar(clavada || (agarrada != null && !esLaQueLlevo));
                prompt.Accion = esLaQueLlevo ? "soltar" : "agarrar";
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.7f, 0.35f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, alcance);
        }
    }
}
