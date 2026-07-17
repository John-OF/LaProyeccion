using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Suelo glicheado que parpadea (idea #7 de Claude, 2026-07-13, ideas.md):
    /// variante intermitente de <see cref="SueloGlicheado"/> para plataformeo
    /// rítmico. Ciclo de tres fases, siempre en el mismo orden:
    ///   SEGURO (piso normal) → PREAVISO (flicker + ticks de audio, ambos con
    ///   frecuencia CRECIENTE — el ritmo se telegrafía, Pilar 3) → LETAL
    ///   (magenta corrupto; tocarlo = respawn) → SEGURO...
    ///
    /// La superficie es SIEMPRE sólida (piso real en todas las fases): el peligro
    /// no es caerse, es estar parado encima cuando letaliza. El volumen letal es
    /// un trigger aparte (base + margen superior, como la caja original) que solo
    /// se habilita en fase letal.
    ///
    /// DETERMINISMO: la fase se calcula desde un reloj compartido (Time.time
    /// respecto a un origen), no de una corrutina por instancia — todas las
    /// losas con el mismo período van sincronizadas, y <see cref="desfase"/>
    /// permite grupos alternos (A/B a medio período: cuando A letaliza, B está
    /// segura). <see cref="ReiniciarFase"/> cumple la convención de FASE CERO
    /// del grabador/replay de pasadas.
    ///
    /// Audio: excepción consciente a la regla "todo suena por AudioManager" —
    /// el tick necesita pitch por-losa y frecuencia variable, así que usa un
    /// AudioSource local (sin grupo de mixer; solo laboratorio). Si el prototipo
    /// entra al juego, el tick migra a un verbo del AudioManager.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SueloGlicheadoParpadeante : MonoBehaviour
    {
        [Header("Ritmo (reloj compartido: mismas duraciones = losas sincronizadas)")]
        [SerializeField, Min(0.2f)] private float duracionSeguro = 1.0f;
        [SerializeField, Min(0.2f)] private float duracionPreaviso = 0.8f;
        [SerializeField, Min(0.2f)] private float duracionLetal = 0.8f;
        [Tooltip("Desplazamiento del ciclo en segundos. Medio período en losas " +
                 "alternas crea el patrón A/B: cuando A letaliza, B está segura.")]
        [SerializeField, Min(0f)] private float desfase = 0f;

        [Header("Visual (Pilar 3: el estado se lee de un vistazo)")]
        [SerializeField] private Color colorSeguro = new Color(0.40f, 0.45f, 0.55f);
        [SerializeField] private Color colorLetal = new Color(1f, 0.15f, 0.6f); // magenta corrupto
        [Tooltip("Flicker del preaviso: de esta frecuencia inicial...")]
        [SerializeField, Min(1f)] private float flickerHzInicial = 4f;
        [Tooltip("...a esta al final del preaviso (acelera = urgencia creciente).")]
        [SerializeField, Min(1f)] private float flickerHzFinal = 14f;

        [Header("Audio del preaviso (tick por flanco de flicker; opcional)")]
        [Tooltip("Clip corto para el tick. Vacío = preaviso mudo.")]
        [SerializeField] private AudioClip tickClip;
        [SerializeField, Range(0f, 1f)] private float tickVolumen = 0.3f;
        [SerializeField, Range(0.5f, 3f)] private float tickPitch = 1.8f;

        [Tooltip("Cuánto sobresale el volumen letal por encima de la superficie " +
                 "(atrapa al jugador posado encima; igual que SueloGlicheado).")]
        [SerializeField, Min(0f)] private float margenLetalArriba = 0.6f;

        private BoxCollider2D solido;   // el collider propio: superficie, siempre activa
        private BoxCollider2D letal;    // trigger creado en Awake, solo activo en fase letal
        private SpriteRenderer sprite;
        private AudioSource tickSource;

        private float origen;           // t=0 del reloj compartido (ReiniciarFase lo re-ancla)
        private float acumuladorFlicker;
        private bool flickerEncendido;

        private float Periodo => duracionSeguro + duracionPreaviso + duracionLetal;

        private void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            solido = GetComponent<BoxCollider2D>();
            solido.isTrigger = false;

            // Volumen letal: la caja de la superficie extendida hacia arriba.
            letal = gameObject.AddComponent<BoxCollider2D>();
            letal.isTrigger = true;
            letal.size = new Vector2(solido.size.x, solido.size.y + margenLetalArriba);
            letal.offset = new Vector2(solido.offset.x, solido.offset.y + margenLetalArriba * 0.5f);
            letal.enabled = false;

            if (tickClip != null)
            {
                tickSource = gameObject.AddComponent<AudioSource>();
                tickSource.playOnAwake = false;
                tickSource.spatialBlend = 0f;
                tickSource.volume = tickVolumen;
            }

            origen = Time.time;
            sprite.color = colorSeguro;
        }

        /// <summary>
        /// Re-ancla el reloj: el ciclo vuelve a empezar en SEGURO (más su desfase).
        /// Convención de FASE CERO del grabador/replay de pasadas.
        /// </summary>
        public void ReiniciarFase()
        {
            origen = Time.time;
            acumuladorFlicker = 0f;
            flickerEncendido = false;
        }

        private void Update()
        {
            // Posición dentro del ciclo (el desfase ATRASA el arranque del ciclo).
            float t = Mathf.Repeat(Time.time - origen - desfase, Periodo);

            if (t < duracionSeguro)
            {
                AplicarSeguro();
            }
            else if (t < duracionSeguro + duracionPreaviso)
            {
                AplicarPreaviso((t - duracionSeguro) / duracionPreaviso);
            }
            else
            {
                AplicarLetal();
            }
        }

        private void AplicarSeguro()
        {
            letal.enabled = false;
            sprite.color = colorSeguro;
            acumuladorFlicker = 0f;
            flickerEncendido = false;
        }

        /// <summary>
        /// k va de 0 a 1 a lo largo del preaviso. El flicker es una onda cuadrada
        /// cuya frecuencia se integra frame a frame (acumulador): sube limpia de
        /// inicial a final sin artefactos de barrido. Cada flanco de ENCENDIDO
        /// dispara un tick de audio — se OYE acelerar, no solo se ve.
        /// </summary>
        private void AplicarPreaviso(float k)
        {
            letal.enabled = false;

            float hz = Mathf.Lerp(flickerHzInicial, flickerHzFinal, k);
            acumuladorFlicker += Time.deltaTime * hz;
            bool encendido = Mathf.Repeat(acumuladorFlicker, 1f) < 0.5f;

            if (encendido && !flickerEncendido && tickSource != null)
            {
                // El pitch sube levemente con la urgencia (k), sobre el pitch base.
                tickSource.pitch = tickPitch * (1f + 0.25f * k);
                tickSource.PlayOneShot(tickClip, tickVolumen);
            }
            flickerEncendido = encendido;

            sprite.color = encendido ? colorLetal : colorSeguro;
        }

        private void AplicarLetal()
        {
            letal.enabled = true;
            sprite.color = colorLetal;
            acumuladorFlicker = 0f;
            flickerEncendido = false;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryKill(other);
        private void OnTriggerStay2D(Collider2D other) => TryKill(other);

        private void TryKill(Collider2D other)
        {
            if (!letal.enabled) return; // el trigger apagado no llama, pero por si acaso
            if (other.GetComponentInParent<PlayerController>() == null) return;
            GameSession.Instance?.RespawnPlayer();
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.15f, 0.6f, 0.20f);
            Gizmos.DrawCube(box.offset + Vector2.up * (margenLetalArriba * 0.5f),
                            box.size + Vector2.up * margenLetalArriba);
            Gizmos.color = new Color(1f, 0.15f, 0.6f, 0.9f);
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
