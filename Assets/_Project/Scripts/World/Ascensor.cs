using UnityEngine;
using UnityEngine.Events;

namespace LaProyeccion.World
{
    /// <summary>
    /// Ascensor del edificio (Zona 1, nivel 1). Es el ÚNICO enlace vertical del
    /// nivel: la altura libre entre plantas (4 u) supera el ápice del salto
    /// (2,45 u medidos), así que la física ya prohíbe subir a pie — no hace falta
    /// una regla que lo diga ni una pared que lo impida.
    ///
    /// REGLA DEL NIVEL: LLAMAR ES GRATIS, VIAJAR NO.
    /// <see cref="IrA"/> (botón de rellano) trae el coche aunque esté bloqueado;
    /// <see cref="Subir"/> / <see cref="Bajar"/> (panel de dentro) no responden sin
    /// la llave. De ahí sale el puzzle del NPC: lleva su bucle entero esperando un
    /// ascensor que nadie llama; tú lo llamas, él entra y la llave se le cae. Él no
    /// necesita llave y tú sí — la simulación funciona para sus habitantes.
    /// El bloqueo no puede saltarse desde dentro porque los botones de rellano
    /// quedan fuera del radio de interacción (1,2 u) del interior del coche, y
    /// llamar a la planta en la que ya estás no hace nada.
    ///
    /// Un intento bloqueado dispara <see cref="OnDenegado"/>: la tecla nunca falla
    /// en silencio (misma regla que el flasheo de ZonaDeCambio, Pilar 3).
    ///
    /// Movimiento kinematic por MovePosition en FixedUpdate (patrón de
    /// PlataformaCongelable). En vertical NO hace falta arrastrar a quien va encima:
    /// lo lleva el empuje físico del collider, y PlayerController preserva la
    /// velocidad Y cuando escribe la X — jugador y caja empujable suben igual.
    ///
    /// APLASTAMIENTO: un cuerpo kinematic movido por MovePosition NO resuelve
    /// contra un dinámico al que no puede empujar a ningún sitio — atraviesa al
    /// jugador que está contra el suelo en vez de aplastarlo. Así que el aplaste
    /// se decide por código y no por colisión: mientras BAJA, barremos el tramo
    /// que recorre el bajo del coche y quien quede ahí es corregido. Solo al
    /// bajar — subiendo, quien está encima viaja, que es justo lo contrario.
    /// </summary>
    // Después de PlayerController (orden 0), por el mismo motivo que la plataforma
    // congelable: él escribe la velocidad del jugador cada FixedUpdate.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Ascensor : MonoBehaviour
    {
        [Header("Paradas (Y de mundo del transform del coche, de ABAJO a ARRIBA)")]
        [SerializeField] private float[] paradasY = { 0f, 4.5f, 9f };
        [SerializeField, Min(0)] private int paradaInicial = 1;
        [Tooltip("u/s. Muy por debajo de la caída libre (g=90), así que quien va encima nunca se despega.")]
        [SerializeField, Min(0.1f)] private float velocidad = 3f;

        [Header("Llave")]
        [Tooltip("Sin llave el panel de dentro no responde. Los botones de rellano SÍ (llamar es gratis).")]
        [SerializeField] private bool bloqueado = true;

        [Header("Aplastamiento")]
        [Tooltip("Quedarse bajo el coche mientras baja = corrección (respawn).")]
        [SerializeField] private bool aplastaAlBajar = true;
        [Tooltip("Cuánto se adelanta la zona letal al bajo del coche. Es el margen que hace que te aplaste al TOCARTE y no medio cuerpo después.")]
        [SerializeField, Min(0f)] private float margenAplastamiento = 0.15f;

        [Header("Eventos (cableado de Inspector)")]
        public UnityEvent OnArranque;
        [Tooltip("Al detenerse en una parada. Lo escucha el NPC para subirse.")]
        public UnityEvent OnLlegada;
        [Tooltip("Intento de viajar sin llave. Obligatorio cablear algo: nunca fallar en silencio.")]
        public UnityEvent OnDenegado;
        public UnityEvent OnDesbloqueado;

        private Rigidbody2D rb;
        private BoxCollider2D col;
        private int paradaActual;
        private int paradaObjetivo;
        private bool enMovimiento;

        /// <summary>Índice de la parada donde está parado (o de la que salió si va en marcha).</summary>
        public int ParadaActual => paradaActual;
        /// <summary>Adónde va. Igual a <see cref="ParadaActual"/> cuando está quieto.</summary>
        public int ParadaObjetivo => paradaObjetivo;
        public bool EnMovimiento => enMovimiento;
        public bool Bloqueado => bloqueado;
        public int NumeroDeParadas => paradasY != null ? paradasY.Length : 0;

        private void Awake()
        {
            col = GetComponent<BoxCollider2D>();
            // Kinematic para empujar bien al jugador dinámico, sin trigger:
            // el coche es suelo sólido (mismo motivo que PlataformaCongelable).
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            // Física a 50 Hz vs render a 60+: sin interpolar se ve a saltitos.
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Start()
        {
            if (paradasY == null || paradasY.Length == 0) return;

            paradaActual = Mathf.Clamp(paradaInicial, 0, paradasY.Length - 1);
            paradaObjetivo = paradaActual;
            enMovimiento = false;

            Vector2 p = new Vector2(transform.position.x, paradasY[paradaActual]);
            transform.position = p;
            rb.position = p;
        }

        /// <summary>
        /// Botón de RELLANO: trae el coche a esa parada. Ignora el bloqueo a
        /// propósito — llamar es gratis, y es lo que destraba el bucle del NPC.
        /// </summary>
        public void IrA(int indice)
        {
            if (paradasY == null || paradasY.Length == 0) return;
            if (enMovimiento) return;                       // el coche en marcha ya ES el feedback

            indice = Mathf.Clamp(indice, 0, paradasY.Length - 1);
            if (indice == paradaActual) return;             // ya estás aquí: no hay nada que anunciar

            paradaObjetivo = indice;
            enMovimiento = true;
            OnArranque?.Invoke();
        }

        /// <summary>Panel de DENTRO: una parada hacia arriba. Necesita llave.</summary>
        public void Subir() => Viajar(paradaActual + 1);

        /// <summary>Panel de DENTRO: una parada hacia abajo. Necesita llave.</summary>
        public void Bajar() => Viajar(paradaActual - 1);

        private void Viajar(int destino)
        {
            if (paradasY == null || paradasY.Length == 0) return;
            if (enMovimiento) return;

            if (bloqueado)
            {
                OnDenegado?.Invoke();
                return;
            }

            if (destino < 0 || destino >= paradasY.Length) return;  // tope: no hay más edificio

            paradaObjetivo = destino;
            enMovimiento = true;
            OnArranque?.Invoke();
        }

        /// <summary>La llave encajada en la ranura. Irreversible: el aparato se instala.</summary>
        public void Desbloquear()
        {
            if (!bloqueado) return;
            bloqueado = false;
            OnDesbloqueado?.Invoke();
        }

        private void FixedUpdate()
        {
            if (!enMovimiento) return;

            float objetivoY = paradasY[paradaObjetivo];
            Vector2 antes = rb.position;
            float nuevaY = Mathf.MoveTowards(antes.y, objetivoY, velocidad * Time.fixedDeltaTime);

            if (aplastaAlBajar && nuevaY < antes.y) ComprobarAplastamiento(antes.y, nuevaY);

            rb.MovePosition(new Vector2(antes.x, nuevaY));

            if (Mathf.Abs(nuevaY - objetivoY) < 0.001f)
            {
                paradaActual = paradaObjetivo;
                enMovimiento = false;
                OnLlegada?.Invoke();
            }
        }

        /// <summary>
        /// Barre el tramo que el BAJO del coche recorre en este paso de física, no
        /// solo su posición final: a 3 u/s son 0,06 u por FixedUpdate, pero subir
        /// la velocidad con un chequeo puntual dejaría al coche pasar a través del
        /// jugador sin verlo (túnel). El ancho se recorta al 90% para que estar de
        /// pie en el borde del rellano, fuera del hueco, no cuente como estar debajo.
        /// </summary>
        private void ComprobarAplastamiento(float yAntes, float yDespues)
        {
            if (col == null) return;

            float mitadAlto = col.size.y * 0.5f + col.edgeRadius;
            float bajoAntes = yAntes + col.offset.y - mitadAlto;
            float bajoDespues = yDespues + col.offset.y - mitadAlto;

            float yMin = bajoDespues - margenAplastamiento;
            float yMax = bajoAntes;
            float alto = Mathf.Max(0.02f, yMax - yMin);
            var centro = new Vector2(rb.position.x + col.offset.x, (yMin + yMax) * 0.5f);
            var tamano = new Vector2((col.size.x + col.edgeRadius * 2f) * 0.9f, alto);

            foreach (var hit in Physics2D.OverlapBoxAll(centro, tamano, 0f))
            {
                if (hit.GetComponentInParent<LaProyeccion.Player.PlayerController>() == null) continue;
                LaProyeccion.Core.GameSession.Instance?.RespawnPlayer();
                return;
            }
        }

        private void OnDrawGizmos()
        {
            if (paradasY == null) return;

            Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.9f);
            float x = transform.position.x;
            for (int i = 0; i < paradasY.Length; i++)
            {
                Gizmos.DrawWireCube(new Vector3(x, paradasY[i], 0f), new Vector3(3.6f, 0.3f, 0f));
                if (i > 0)
                    Gizmos.DrawLine(new Vector3(x, paradasY[i - 1], 0f), new Vector3(x, paradasY[i], 0f));
            }
        }
    }
}
