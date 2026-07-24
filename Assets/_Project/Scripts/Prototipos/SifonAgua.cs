using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): sifón con reloj (T7, ideas.md §Trampas).
    /// SIN natación ni aguantar la respiración: el agua es LETAL al contacto y lo único que
    /// hace es SUBIR y BAJAR con el reloj compartido (mismo patrón que MareaDeMundos /
    /// SueloGlicheadoParpadeante: fase desde Time.time contra un origen; mismas duraciones =
    /// sincronizado). Sobrevives estando por ENCIMA de la línea de agua: marea baja abre el
    /// paso de abajo, marea alta obliga a las cornisas altas. Es la única trampa que fuerza
    /// plataformeo HACIA ARRIBA (la aspereza anotada de la cueva: "casi todo es descenso").
    ///
    /// Se lee ANTES de entrar (Pilar 3): la subida es SUAVE (nunca de golpe) — la propia agua
    /// que trepa es el aviso — y una MARCA DE NIVEL en la pared (objeto aparte) señala hasta
    /// dónde llega; todo lo que esté por encima es seguro. Opcional: tick creciente durante
    /// la subida (AudioSource local, misma excepción de laboratorio que la marea).
    ///
    /// El agua es un único sprite+trigger cuya superficie sube/baja y cuyo fondo es fijo.
    ///
    /// [PENDIENTE]: arte/shader del agua (hoy un rect unlit semitransparente); SFX de subida.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class SifonAgua : MonoBehaviour
    {
        [Header("Reloj compartido (Time.time; mismas duraciones = sincronizado)")]
        [Tooltip("Marea baja sostenida (paso de abajo abierto).")]
        [SerializeField, Min(0.2f)] private float duracionBaja = 2.5f;
        [Tooltip("Subida (anunciada por la propia agua que trepa; nunca de golpe).")]
        [SerializeField, Min(0.2f)] private float duracionSubida = 6f;
        [Tooltip("Marea alta sostenida (obliga a las cornisas altas).")]
        [SerializeField, Min(0.2f)] private float duracionAlta = 2.5f;
        [SerializeField, Min(0.2f)] private float duracionBajada = 4f;
        [Tooltip("Desplazamiento del ciclo (sifones a contratiempo).")]
        [SerializeField, Min(0f)] private float desfase = 0f;

        [Header("Niveles (Y de la superficie del agua)")]
        [SerializeField] private float nivelBajo = -0.5f;
        [SerializeField] private float nivelAlto = 6.5f;
        [Tooltip("Y del fondo del pozo: el agua rellena de aquí a la superficie.")]
        [SerializeField] private float fondo = -6f;
        [Tooltip("Ancho del pozo (x).")]
        [SerializeField, Min(0.5f)] private float ancho = 10f;

        [Header("Visual")]
        [SerializeField] private Color colorAgua = new Color(0.18f, 0.4f, 0.55f, 0.85f);

        [Header("Audio de la subida (opcional; sonido creciente)")]
        [Tooltip("Clip corto de tick. Vacío = subida muda (el aviso es el agua que trepa).")]
        [SerializeField] private AudioClip tickClip;
        [SerializeField, Range(0f, 1f)] private float tickVolumen = 0.35f;

        private SpriteRenderer sr;
        private BoxCollider2D trigger;
        private AudioSource src;
        private float origen;
        private float acumTick;
        private bool tickEncendido;
        private float xFijo;

        private float Periodo => duracionBaja + duracionSubida + duracionAlta + duracionBajada;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            trigger = GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            sr.color = colorAgua;
            xFijo = transform.position.x;

            if (tickClip != null)
            {
                src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.volume = tickVolumen;
            }
            origen = Time.time;
        }

        private void Update()
        {
            bool subiendo;
            float k;
            float nivel = NivelActual(out subiendo, out k);
            AplicarNivel(nivel);

            if (subiendo && src != null) AudioSubida(k);
            else { acumTick = 0f; tickEncendido = false; }
        }

        /// <summary>Y de la superficie ahora; 'subiendo' + 'k' (0..1) durante la fase de subida.</summary>
        private float NivelActual(out bool subiendo, out float k)
        {
            subiendo = false; k = 0f;
            float t = Mathf.Repeat(Time.time - origen - desfase, Periodo);
            if (t < duracionBaja) return nivelBajo;
            t -= duracionBaja;
            if (t < duracionSubida) { subiendo = true; k = t / duracionSubida; return Mathf.SmoothStep(nivelBajo, nivelAlto, k); }
            t -= duracionSubida;
            if (t < duracionAlta) return nivelAlto;
            t -= duracionAlta;
            return Mathf.SmoothStep(nivelAlto, nivelBajo, t / duracionBajada); // bajando
        }

        private void AplicarNivel(float nivelY)
        {
            float alto = Mathf.Max(0.02f, nivelY - fondo);
            float centroY = (nivelY + fondo) * 0.5f;
            transform.position = new Vector3(xFijo, centroY, 0f);
            transform.localScale = new Vector3(ancho, alto, 1f); // sprite 1u ⇒ escala = tamaño
        }

        private void AudioSubida(float k)
        {
            float hz = Mathf.Lerp(2f, 8f, k);
            acumTick += Time.deltaTime * hz;
            bool encendido = Mathf.Repeat(acumTick, 1f) < 0.5f;
            if (encendido && !tickEncendido)
            {
                src.pitch = 1f + 0.5f * k; // sube con el nivel: "sonido creciente"
                src.PlayOneShot(tickClip, tickVolumen);
            }
            tickEncendido = encendido;
        }

        private void OnTriggerEnter2D(Collider2D other) => Ahogar(other);
        private void OnTriggerStay2D(Collider2D other) => Ahogar(other);

        private void Ahogar(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            AudioManager.Instance?.PlayDeath();
            GameSession.Instance?.RespawnPlayer();
        }
    }
}
