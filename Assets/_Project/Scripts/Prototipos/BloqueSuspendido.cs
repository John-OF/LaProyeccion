using System.Collections;
using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/ — fuera de ALCANCE): **bloque suspendido /
    /// contrapeso** (idea del 2026-07-21, la única de la ronda de trampas de la cueva que
    /// nunca se construyó; el autor decidió entonces que **no es una trampa**, sino
    /// hermana de la caja empujable y la placa de presión).
    ///
    /// Peñasco retenido en alto por un lastre: **quitar o poner lastre lo suelta**. La
    /// gracia es la doble lectura con un solo objeto — es **trampa** si cuelga sobre tu
    /// cabeza y **herramienta** si lo sueltas donde quieres (pisar una placa que no
    /// alcanzas, dejar un peso donde hace falta).
    ///
    /// **No trae detección propia a propósito.** Quién es el lastre ya lo resuelve
    /// <see cref="PlacaDePresion"/> (jugador, caja, corrector congelado, eco, plataforma
    /// congelable…), y ella conduce un <see cref="LaProyeccion.Puzzles.DualSwitch"/>, que
    /// ya emite eventos. Así que las dos lecturas son **puro cableado de Inspector**,
    /// sin un solo campo de modo en este script:
    ///
    ///   quitar lastre lo suelta →  DualSwitch.OnDeactivated → <see cref="Soltar"/>
    ///   poner  lastre lo suelta →  DualSwitch.OnActivated   → <see cref="Soltar"/>
    ///   presión CONSTANTE       →  las dos a la vez, en la misma placa:
    ///                              OnActivated → <see cref="Retener"/> · OnDeactivated → Soltar
    ///   …y su ESPEJO            →  OnActivated → Soltar · OnDeactivated → Retener
    ///                              (el lastre lo BAJA, y hay que mantenerlo abajo)
    ///
    /// La tercera es la que el autor pidió probar (2026-07-26) y la única en la que la caja
    /// es **obligatoria** y no una demostración: el puzle te obliga a irte de la placa, así
    /// que hay que sustituir el propio peso por algo que se quede.
    ///
    /// Ir montado encima es seguro en los dos sentidos: al caer no hay colisión NUEVA, así
    /// que no mata (bajas con él), y al subir el kinematic te levanta con su propio collider
    /// porque se mueve con **MovePosition en FixedUpdate** y no por transform.
    ///
    /// Y sale gratis que **no se dispare solo al arrancar la escena**: `DualSwitch.SetState`
    /// no emite si el valor no cambia, así que una placa que nace vacía no grita
    /// "OnDeactivated" en el frame 1. El bloque solo cae si el lastre estuvo y se fue —
    /// que es justo lo que dice la idea.
    ///
    /// Telegrafía (Pilar 3): el **cable** se ve tenso mientras sostiene y desaparece al
    /// soltarse, más un temblor corto antes de caer. Nunca color de estado: esto es un
    /// peligro físico, no del sistema (mismo criterio que <see cref="Estalactita"/>).
    ///
    /// Mientras cuelga es **estático y sólido** — se puede pisar. Al aterrizar vuelve a
    /// estático en el sitio: un peso que se queda quieto pesa mejor que uno que patina
    /// (mismo criterio que <see cref="CajaEmpujable"/>, determinismo sobre inercia).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class BloqueSuspendido : MonoBehaviour
    {
        [Header("Caída")]
        [Tooltip("Aviso (temblor) antes de soltarse. NO bajarlo de ~0.6: a 8 u/s el jugador " +
                 "recorre 5.6 u en 0.7 s, así que quien viene andando SIEMPRE sale de debajo. " +
                 "Es lo que hace que el bloque castigue quedarse parado debajo y no pueda " +
                 "pillarte de paso — con 0.25 sí pillaba, y eso era injusto, no difícil.")]
        [SerializeField, Min(0f)] private float retardoAviso = 0.7f;
        [SerializeField, Min(0.1f)] private float gravedadCaida = 4f;
        [Tooltip("Amplitud del temblor de aviso.")]
        [SerializeField, Min(0f)] private float temblor = 0.05f;
        [Tooltip("Letal para el jugador mientras cae. Apagarlo lo vuelve solo herramienta.")]
        [SerializeField] private bool letalAlCaer = true;

        [Header("Izado (para placas de presión constante)")]
        [Tooltip("A qué velocidad vuelve arriba con Retener(). Sube ANIMADO y no de golpe: si " +
                 "una placa lo sube y lo baja a la vista, un teletransporte se lee como un fallo.")]
        [SerializeField, Min(0.5f)] private float velocidadIzado = 6f;

        [Header("Después")]
        [Tooltip("Segundos hasta volver a colgar. **0 = se queda donde cayó** — es lo que " +
                 "quieres cuando el bloque es la herramienta que mantiene pisada una placa; " +
                 "si vuelve arriba, la puerta que abrió se cierra sola.")]
        [SerializeField, Min(0f)] private float retardoReaparece = 5f;

        [Header("Lectura")]
        [Tooltip("El cable/cadena que lo sostiene. Se apaga al soltarse: es lo que hace " +
                 "visible la relación entre el lastre y el bloque.")]
        [SerializeField] private GameObject cable;

        private Rigidbody2D rb;
        private Collider2D col;
        private Collider2D colliderIgnorado;
        private Vector3 posOriginal;
        private bool colgando = true;
        private bool cayendo;
        private bool izando;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            rb.freezeRotation = true;   // un bloque que vuelca no se lee
            // Física a 50 Hz vs render a 60+: sin interpolar, el izado (y quien vaya
            // encima) se ve a saltitos. Mismo motivo que en PlataformaCongelable.
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.bodyType = RigidbodyType2D.Static;
            posOriginal = transform.position;
        }

        /// <summary>
        /// Suéltalo. Se cablea desde el DualSwitch de la placa: OnDeactivated para
        /// "quitar lastre lo suelta", OnActivated para "ponerlo lo suelta".
        /// </summary>
        public void Soltar()
        {
            if (!colgando) return;
            colgando = false;
            izando = false;
            StopAllCoroutines();          // puede estar a mitad de izado: manda el último gesto
            StartCoroutine(AvisarYCaer());
        }

        /// <summary>
        /// Vuelve a izarlo. Cableado junto con <see cref="Soltar"/> en la MISMA placa
        /// (OnActivated → Retener, OnDeactivated → Soltar) da el bloque atado a **presión
        /// constante**: cuelga mientras haya lastre y cae en cuanto se va. Es la variante
        /// donde la caja deja de ser demostración y es obligatoria — el puzle te obliga a
        /// irte de la placa, así que tienes que sustituir tu peso por algo que se quede.
        /// </summary>
        public void Retener()
        {
            if (izando) return;
            StopAllCoroutines();
            StartCoroutine(Izar());
        }

        private IEnumerator Izar()
        {
            izando = true;
            cayendo = false;
            RestaurarColisionJugador();
            // El tipo PRIMERO: si el bloque ya estaba colgado sigue siendo estático, y tocarle
            // la velocidad a un cuerpo estático llena la consola de avisos.
            rb.bodyType = RigidbodyType2D.Kinematic;   // sube tirado por el cable, sin gravedad
            rb.linearVelocity = Vector2.zero;
            if (cable != null) cable.SetActive(true);
            // colgando ya en true a mitad de subida: si le quitan el lastre mientras sube,
            // Soltar() tiene que poder interrumpirlo.
            colgando = true;

            // MovePosition en el paso de FÍSICA, no transform.position en el de render.
            // Es el patrón ya pagado en PlataformaCongelable: un kinematic movido por
            // transform atraviesa al jugador sin empujarlo, y movido por MovePosition lo
            // levanta con su propio collider. Aquí importa de verdad — en la estación del
            // bloque que hay que BAJAR, estar encima cuando sube es el caso normal, no un
            // caso raro. Solo hace falta para el eje Y: la horizontal sí exigiría sumarle
            // velocidad al jugador (PlayerController le pisa la X cada FixedUpdate), pero
            // este bloque solo sube y baja.
            while ((rb.position - (Vector2)posOriginal).sqrMagnitude > 0.0001f)
            {
                rb.MovePosition(Vector2.MoveTowards(rb.position, posOriginal, velocidadIzado * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
            }
            rb.MovePosition(posOriginal);
            yield return new WaitForFixedUpdate();
            rb.bodyType = RigidbodyType2D.Static;
            izando = false;
        }

        private IEnumerator AvisarYCaer()
        {
            if (cable != null) cable.SetActive(false);

            float t = 0f;
            while (t < retardoAviso)
            {
                t += Time.deltaTime;
                Vector2 j = Random.insideUnitCircle * temblor;
                transform.position = posOriginal + new Vector3(j.x, j.y, 0f);
                yield return null;
            }
            transform.position = posOriginal;

            cayendo = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = gravedadCaida;
        }

        private void OnCollisionEnter2D(Collision2D colision)
        {
            if (!cayendo) return;

            // Un CUERPO no es suelo. Aterrizar aquí es el bug que encontró el autor: el
            // bloque te aplastaba, tú desaparecías por el respawn, y él se quedaba
            // congelado a media altura sobre nada. Aplasta y SIGUE cayendo hasta el suelo
            // de verdad; además se desactiva el par de colisión para que el cuerpo del
            // jugador no le frene la caída durante el frame del impacto.
            if (colision.collider.GetComponentInParent<PlayerController>() != null)
            {
                if (letalAlCaer)
                {
                    AudioManager.Instance?.PlayDeath();
                    GameSession.Instance?.RespawnPlayer();
                }
                colliderIgnorado = colision.collider;
                Physics2D.IgnoreCollision(col, colliderIgnorado, true);
                return;
            }

            // Y solo es suelo lo que está DEBAJO: rozar una pared al caer tampoco es
            // aterrizar (misma causa, mismo síntoma — quedarse clavado en el aire).
            for (int i = 0; i < colision.contactCount; i++)
            {
                if (colision.GetContact(i).point.y < col.bounds.center.y) { Aterrizar(); return; }
            }
        }

        private void Aterrizar()
        {
            if (!cayendo) return;
            cayendo = false;
            // Al posarse vuelve a ser sólido para el jugador. Imprescindible: con
            // retardoReaparece = 0 el bloque no vuelve a colgar nunca, así que si el par
            // ignorado no se restaurase aquí, el bloque que te aplastó dejaría de ser el
            // escalón al que hay que subirse — se podría atravesar.
            RestaurarColisionJugador();
            AudioManager.Instance?.PlayPiedraGolpe();   // impacto pétreo (provisional)

            // Estático donde cayó: así pesa de verdad sobre una placa y no lo empuja nadie
            // sin querer. Un frame de espera para que el motor resuelva el contacto antes
            // de congelarlo, o se queda incrustado en el suelo.
            StartCoroutine(PosarseYQuedarse());
        }

        private IEnumerator PosarseYQuedarse()
        {
            yield return new WaitForFixedUpdate();
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;

            if (retardoReaparece <= 0f) yield break;   // se queda: es la herramienta
            yield return new WaitForSeconds(retardoReaparece);
            Recolocar();
        }

        private void RestaurarColisionJugador()
        {
            if (colliderIgnorado == null) return;
            Physics2D.IgnoreCollision(col, colliderIgnorado, false);
            colliderIgnorado = null;
        }

        private void Recolocar()
        {
            cayendo = false;
            RestaurarColisionJugador();
            rb.bodyType = RigidbodyType2D.Static;   // pasar a estático ya anula la velocidad
            transform.position = posOriginal;
            if (cable != null) cable.SetActive(true);
            colgando = true;
        }

        private void OnDrawGizmosSelected()
        {
            // Por dónde va a caer: lo que hay que mirar para colocarlo.
            var c = GetComponent<Collider2D>();
            Vector3 tam = c != null ? c.bounds.size : Vector3.one;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.5f);
            for (float y = 0f; y > -20f; y -= tam.y)
                Gizmos.DrawWireCube(transform.position + Vector3.up * y, tam);
        }
    }
}
