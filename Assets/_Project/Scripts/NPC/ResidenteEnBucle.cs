using UnityEngine;
using UnityEngine.Events;
using LaProyeccion.World;
using LaProyeccion.Player;

namespace LaProyeccion.NPC
{
    /// <summary>
    /// El vecino del edificio (Zona 1, nivel 1). Primer NPC del juego.
    ///
    /// POR QUÉ EXISTE ASÍ: la llave del ascensor no se pide ni se negocia — se
    /// cae. Él lleva su bucle entero esperando un ascensor que nadie llama; en
    /// cuanto el coche para en su planta, su rutina POR FIN AVANZA, entra, y la
    /// llave se le queda en el suelo. La diferencia con "hablas y te la da" es
    /// que aquí el jugador resuelve algo (llamar al ascensor) en vez de activar
    /// un flag, y de paso aprende para qué sirve el botón de rellano antes de
    /// necesitarlo.
    ///
    /// Y dice una cosa sin una sola línea de diálogo: **él no necesita llave y
    /// tú sí**. La simulación funciona para sus habitantes.
    ///
    /// SIN DIÁLOGO A PROPÓSITO: no hay sistema de conversación en el proyecto y
    /// este NPC no lo necesita. Si algún día habla, el texto lo escribe el autor.
    ///
    /// Determinista y sin física: se mueve por transform sobre un raíl 1D con
    /// pausas (mismo lenguaje que el Corrector), y mientras viaja va colgado del
    /// coche, así que sube exactamente con él. Sin collider: un vecino que te
    /// bloquea el paso sería un obstáculo, y no es eso lo que es.
    /// </summary>
    public class ResidenteEnBucle : MonoBehaviour
    {
        public enum Fase { EnBucle, Entrando, Cerrando, Viajando, Saliendo, Ido }

        [Header("Ascensor")]
        [SerializeField] private Ascensor ascensor;
        [Tooltip("Índice de parada en la que espera. Su bucle avanza cuando el coche PARA aquí.")]
        [SerializeField] private int pisoDondeEspera = 1;
        [Tooltip("Índice de parada a la que se va (su casa).")]
        [SerializeField] private int pisoDestino = 2;

        [Header("El bucle (paseo 1D con pausas)")]
        [SerializeField] private float paseoXIzquierda = -1f;
        [SerializeField] private float paseoXDerecha = 3f;
        [SerializeField, Min(0.1f)] private float velocidad = 1.6f;
        [Tooltip("Pausa en cada extremo: es lo que hace que se lea como un bucle y no como un paseo.")]
        [SerializeField, Min(0f)] private float pausaEnExtremos = 1.1f;

        [Header("Entrada y salida")]
        [Tooltip("X del centro del coche: adonde camina para entrar.")]
        [SerializeField] private float xDentroDelCoche = 7f;
        [Tooltip("X a la que camina al salir en su planta, antes de irse del todo.")]
        [SerializeField] private float xSalida = 0.5f;

        [Header("Lo que deja al entrar")]
        [Tooltip("La llave. Empieza desactivada y CAE DENTRO DEL COCHE, colgada de él: así viaja " +
                 "con el ascensor y nunca puede quedarse en una planta a la que no puedes volver.")]
        [SerializeField] private GameObject llave;
        [Tooltip("Dónde cae la llave dentro del coche, relativo a su centro.")]
        [SerializeField] private Vector2 offsetLlaveEnCoche = new Vector2(-1.2f, 0.45f);
        [Tooltip("Beat entre que se le cae la llave y el ascensor arranca: es el tiempo de VERLO.")]
        [SerializeField, Min(0f)] private float esperaAntesDeIrse = 0.9f;

        [Header("Eventos")]
        public UnityEvent OnEntraAlAscensor;
        public UnityEvent OnSeVa;

        public Fase FaseActual { get; private set; } = Fase.EnBucle;

        private float objetivoX;
        private float pausaRestante;
        private SpriteRenderer sprite;
        private bool llaveDejada;

        private void Awake()
        {
            sprite = GetComponentInChildren<SpriteRenderer>();
            objetivoX = paseoXDerecha;
            if (llave != null) llave.SetActive(false);
        }

        private void Update()
        {
            switch (FaseActual)
            {
                case Fase.EnBucle:   Bucle();     break;
                case Fase.Entrando:  Entrar();    break;
                case Fase.Cerrando:  Cerrar();    break;
                case Fase.Viajando:  Viajar();    break;
                case Fase.Saliendo:  Salir();     break;
            }
        }

        private void Bucle()
        {
            // El disparo: el coche PARADO en su planta. No basta con que pase.
            if (ascensor != null && !ascensor.EnMovimiento && ascensor.ParadaActual == pisoDondeEspera)
            {
                FaseActual = Fase.Entrando;
                return;
            }

            if (pausaRestante > 0f) { pausaRestante -= Time.deltaTime; return; }

            if (CaminarHacia(objetivoX))
            {
                objetivoX = Mathf.Approximately(objetivoX, paseoXDerecha) ? paseoXIzquierda : paseoXDerecha;
                pausaRestante = pausaEnExtremos;
            }
        }

        /// <summary>
        /// La llave se queda DONDE ESTABA ESPERANDO, no donde se sube: el jugador
        /// la ve caer en el sitio que lleva mirando todo el rato.
        /// </summary>
        /// <summary>
        /// La llave cae DENTRO del coche y colgada de él.
        ///
        /// No es un detalle: si cayera en el rellano, un jugador que se suba con él
        /// se queda en la planta de arriba con la llave abajo, el panel apagado y
        /// ninguna forma de bajar — softlock sin haber hecho nada raro. Dentro del
        /// coche la llave viaja siempre con el ascensor, así que esté donde esté el
        /// jugador, llamar al coche se la trae.
        ///
        /// Una sola vez: si volviera a dispararse con el jugador llevándola encima,
        /// la reposicionaría y se la arrancaría de las manos.
        /// </summary>
        private void DejarLlave()
        {
            if (llave == null || llaveDejada || ascensor == null) return;
            llaveDejada = true;

            llave.transform.position = (Vector2)ascensor.transform.position + offsetLlaveEnCoche;
            llave.transform.SetParent(ascensor.transform, true);
            llave.SetActive(true);

            // El cartel de tecla se configura en el Awake del portador recorriendo los
            // que ya existen. La llave nace desactivada, así que no estaba: hay que
            // decirle sus rutas aquí o saldría sin glifo.
            var prompt = llave.GetComponent<LaProyeccion.UI.PromptDeTecla>();
            if (prompt != null)
                prompt.Configurar(PortadorDePieza.RutaTeclado,
                                  PortadorDePieza.RutaMando);
        }

        private void Entrar()
        {
            // Si le vuelven a robar el ascensor a mitad de camino, se rinde y
            // vuelve al bucle — que es exactamente lo que lleva haciendo siempre.
            // Sin esto se colgaría de un coche que ya no está en su planta y
            // aparecería flotando en otra.
            if (ascensor == null || ascensor.EnMovimiento || ascensor.ParadaActual != pisoDondeEspera)
            {
                FaseActual = Fase.EnBucle;
                pausaRestante = pausaEnExtremos;
                return;
            }

            if (!CaminarHacia(xDentroDelCoche)) return;

            // Colgarse del coche: a partir de aquí sube con él sin física.
            transform.SetParent(ascensor.transform, true);
            DejarLlave();
            OnEntraAlAscensor?.Invoke();
            pausaRestante = esperaAntesDeIrse;
            FaseActual = Fase.Cerrando;
        }

        /// <summary>Beat entre que la llave cae y el coche arranca: sin esto, se ve un parpadeo.</summary>
        private void Cerrar()
        {
            pausaRestante -= Time.deltaTime;
            if (pausaRestante > 0f) return;
            FaseActual = Fase.Viajando;
        }

        private void Viajar()
        {
            if (ascensor == null) { FaseActual = Fase.Saliendo; return; }
            if (ascensor.EnMovimiento) return;

            // Insiste hasta llegar. Si el jugador le roba el coche a otra planta a
            // mitad de viaje, vuelve a pedir la suya en vez de quedarse colgado para
            // siempre esperando una llegada que ya no va a pasar.
            if (ascensor.ParadaActual != pisoDestino) { ascensor.IrA(pisoDestino); return; }

            transform.SetParent(null, true);
            FaseActual = Fase.Saliendo;
        }

        private void Salir()
        {
            if (!CaminarHacia(xSalida)) return;
            OnSeVa?.Invoke();
            FaseActual = Fase.Ido;
            gameObject.SetActive(false);
        }

        /// <summary>Camina hacia una X. Devuelve true al llegar. Se gira hacia donde va.</summary>
        private bool CaminarHacia(float destinoX)
        {
            Vector3 p = transform.position;
            float nuevaX = Mathf.MoveTowards(p.x, destinoX, velocidad * Time.deltaTime);
            if (sprite != null && !Mathf.Approximately(nuevaX, p.x))
                sprite.flipX = nuevaX < p.x;
            transform.position = new Vector3(nuevaX, p.y, p.z);
            return Mathf.Abs(nuevaX - destinoX) < 0.01f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.9f);
            float y = transform.position.y;
            Gizmos.DrawLine(new Vector3(paseoXIzquierda, y, 0f), new Vector3(paseoXDerecha, y, 0f));
            Gizmos.DrawWireSphere(new Vector3(paseoXIzquierda, y, 0f), 0.2f);
            Gizmos.DrawWireSphere(new Vector3(paseoXDerecha, y, 0f), 0.2f);
        }
    }
}
