using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Narrative;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/). Correctores y vigilancia
    /// ya están en ALCANCE v1.2; este SUBTIPO COMBINADO (guardia + cono) se
    /// valida en laboratorio antes de entrar al juego.
    ///
    /// Corrector vigilante: el segundo tipo de guardia, espejo de los dos tipos
    /// de vigilancia. Patrulla determinista A↔B como <see cref="Corrector"/>
    /// (tocarlo → respawn) y además lleva INCORPORADO un cono de vigilancia
    /// ESTÁTICO hacia el lado que mira: no barre (a diferencia de
    /// <see cref="FocoVigilancia"/>), pero viaja y SE VOLTEA INSTANTÁNEAMENTE
    /// con el guardia al girar en los extremos (decisión del autor 2026-07-12:
    /// esa brusquedad es la dificultad buscada).
    /// Regla del cono, la de siempre: estar iluminado no castiga; CAMBIAR DE
    /// MUNDO dentro del cono = anomalía → respawn. Sondear no es anomalía.
    /// El cono NO atraviesa paredes (línea de visión contra el layer de muros)
    /// y es más corto que el del foco (visión del guardia, no un reflector).
    /// El visual reusa FocoVigilanciaHaz con ángulo fijo; el largo visible se
    /// recorta contra el muro central — el visual nunca miente (Pilar 3) y en
    /// los bordes la aproximación siempre favorece al jugador.
    ///
    /// Presencia: NO usar WorldExclusivePresence (apagaría el haz hijo y el
    /// cono seguiría detectando — el visual mentiría). El CUERPO puede ser
    /// exclusivo de un mundo (contacto solo donde existe), pero el OJO y el
    /// CONO se ven y detectan SIEMPRE: la mirada de Keplin trasciende el
    /// cambio, igual que la caja y el foco.
    ///
    /// Variante LETAL del cono (prototipo 2026-07-13): el CUERPO del guardia
    /// ya mataba por contacto desde el Corrector original (sin flag). Con
    /// <see cref="letal"/> activo, TOCAR EL CONO también mata por contacto —
    /// no hace falta cambiar de mundo, ya no es solo "zona de no-cambio" sino
    /// pared móvil letal, espejo de <see cref="FocoVigilancia"/>. Se reutiliza
    /// la misma comprobación de <see cref="PlayerEnCono"/> (cuerpo con radio +
    /// oclusión) para la detección de anomalía Y para el contacto letal. Usar
    /// M_FocoLetal en el haz para que se lea a primera vista.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CorrectorVigilante : MonoBehaviour
    {
        public enum Presencia { Ambos, SoloSimulacion, SoloReal }

        [Header("Presencia del cuerpo (el cono vigila SIEMPRE)")]
        [SerializeField] private Presencia cuerpoPresenteEn = Presencia.Ambos;

        [Header("Patrulla (posiciones de mundo)")]
        [SerializeField] private Vector2 puntoA;
        [SerializeField] private Vector2 puntoB;
        [SerializeField, Min(0.1f)] private float velocidad = 2.5f;
        [Tooltip("Pausa en cada extremo: hace el patrón legible (Pilar 3).")]
        [SerializeField, Min(0f)] private float pausaEnExtremos = 0.4f;

        [Header("Cono de vigilancia (fijo hacia donde mira)")]
        [Tooltip("Más corto que el foco (8): es la visión del guardia.")]
        [SerializeField, Min(1f)] private float largo = 4.5f;
        [Tooltip("Semi-apertura del cono, en grados.")]
        [SerializeField, Range(3f, 40f)] private float aperturaMediaDeg = 12f;
        [Tooltip("Origen del cono (ojo) relativo al guardia; X se voltea con la mirada.")]
        [SerializeField] private Vector2 ojoOffset = new Vector2(0.2f, 0.25f);
        [Tooltip("El jugador cuenta como un cuerpo de este radio, no como un punto: si el haz toca el cuerpo, vigila. Sin esto, la parte fina del cono (junto al ojo) era un punto ciego.")]
        [SerializeField, Min(0f)] private float radioJugador = 0.5f;
        [Tooltip("Muros que bloquean la línea de visión del cono.")]
        [SerializeField] private LayerMask paredes;

        [Header("Letalidad del cono (el cuerpo del guardia ya mata al tocarlo)")]
        [Tooltip("Si está activo, TOCAR EL CONO mata por contacto (no hace falta cambiar de mundo). Usar M_FocoLetal en el haz.")]
        [SerializeField] private bool letal = false;

        [Header("Visual (hijos; se cablean al construir)")]
        [Tooltip("Sprite del cuerpo, hijo aparte: el root queda a escala 1 para no deformar el quad del haz.")]
        [SerializeField] private SpriteRenderer cuerpo;
        [SerializeField] private SpriteRenderer haz;
        [SerializeField] private SpriteRenderer ojo;

        [Header("Narrativa")]
        [SerializeField, TextArea] private string mensajeDeteccion =
            "[TEXTO PENDIENTE: Keplin registra una anomalía frente a un Corrector vigilante — tono administrativo, sin amenazar]";

        private static readonly int AnguloID = Shader.PropertyToID("_Angulo");
        private static readonly int AperturaID = Shader.PropertyToID("_Apertura");
        private static readonly int QuadSizeID = Shader.PropertyToID("_QuadSize");

        private MaterialPropertyBlock mpb;
        private BoxCollider2D cuerpoCol;
        private Transform player;
        private Vector2 objetivo;
        private float pausaRestante;
        private int dir = 1;            // +1 mira a la derecha, -1 a la izquierda
        private float largoVisible;     // largo recortado por el muro central
        private bool avisoDado;

        private void Awake()
        {
            cuerpoCol = GetComponent<BoxCollider2D>();
            cuerpoCol.isTrigger = true;
            // Collider en movimiento: kinematic para que los triggers disparen bien.
            var rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        private void OnEnable() => WorldManager.OnWorldChanged += OnWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= OnWorldChanged;

        private void Start()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;

            transform.position = puntoA;
            objetivo = puntoB;
            dir = puntoB.x >= puntoA.x ? 1 : -1;
            largoVisible = largo;

            if (ojo != null) ojo.color = new Color(1f, 0.25f, 0.2f, 1f); // siempre vigila
            AplicarPresenciaCuerpo();

            if (haz != null)
            {
                mpb = new MaterialPropertyBlock();
                haz.GetPropertyBlock(mpb);
                mpb.SetFloat(AnguloID, 0f); // cono fijo: sin péndulo
                mpb.SetFloat(AperturaID, aperturaMediaDeg * Mathf.Deg2Rad);
                haz.SetPropertyBlock(mpb);
                AplicarVisualHaz();
            }
        }

        private void Update()
        {
            // Tiempo escalado: se congela en pausa, como todo el mundo de juego.
            if (pausaRestante > 0f)
            {
                pausaRestante -= Time.deltaTime;
            }
            else
            {
                Vector2 nueva = Vector2.MoveTowards(
                    transform.position, objetivo, velocidad * Time.deltaTime);
                transform.position = nueva;

                if (Vector2.Distance(nueva, objetivo) < 0.01f)
                {
                    objetivo = objetivo == puntoB ? puntoA : puntoB;
                    pausaRestante = pausaEnExtremos;
                    // Volteo INSTANTÁNEO del guardia y su cono (decisión del autor):
                    // durante la pausa ya vigila el camino de vuelta.
                    dir = objetivo.x >= transform.position.x ? 1 : -1;
                }
            }

            // El muro central recorta el haz visible; el guardia se mueve, así
            // que se re-evalúa cada frame.
            RaycastHit2D hit = Physics2D.Raycast(PosOjo(), new Vector2(dir, 0f), largo, paredes);
            largoVisible = hit.collider != null ? hit.distance : largo;

            AplicarVisualHaz();

            // Variante letal: tocar el cono mata por contacto, en cualquier momento.
            if (letal && PlayerEnCono())
                GameSession.Instance?.RespawnPlayer();
        }

        private Vector2 PosOjo() =>
            (Vector2)transform.position + new Vector2(ojoOffset.x * dir, ojoOffset.y);

        /// <summary>
        /// Orienta y dimensiona el quad del haz: el shader emite "hacia abajo"
        /// en su espacio local, así que rotarlo ±90° lo tumba hacia la mirada.
        /// </summary>
        private void AplicarVisualHaz()
        {
            if (haz == null || mpb == null) return;

            float aperturaRad = aperturaMediaDeg * Mathf.Deg2Rad;
            float ancho = 2f * largoVisible * Mathf.Sin(aperturaRad) + 0.6f;

            haz.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * dir);
            haz.transform.localScale = new Vector3(ancho, largoVisible, 1f);
            haz.transform.localPosition = new Vector3(
                ojoOffset.x * dir + dir * largoVisible * 0.5f, ojoOffset.y, 0f);

            mpb.SetVector(QuadSizeID, new Vector4(ancho, largoVisible, 0f, 0f));
            haz.SetPropertyBlock(mpb);

            // El ojo acompaña la mirada (se voltea con el guardia).
            if (ojo != null)
                ojo.transform.localPosition = new Vector3(ojoOffset.x * dir, ojoOffset.y, 0f);
        }

        /// <summary>
        /// Misma geometría que el visual, con el jugador como CUERPO de radio
        /// <see cref="radioJugador"/> (no como punto): si el haz le toca el
        /// cuerpo, está vigilado. Con la comprobación puntual, la parte fina
        /// del cono (junto al ojo) era más estrecha que el jugador y quedaba
        /// como punto ciego — frente a la cara del guardia debe ser el sitio
        /// MÁS peligroso, no el más seguro.
        /// Oclusión: sin línea de visión no hay detección, y la distancia se
        /// limita al largo visible (el haz recortado nunca detecta más de lo
        /// que muestra).
        /// </summary>
        private bool PlayerEnCono()
        {
            if (player == null) return false;
            Vector2 eye = PosOjo();
            Vector2 delta = (Vector2)player.position - eye;

            float alFrente = delta.x * dir;
            if (alFrente <= 0f) return false; // detrás del guardia no hay cono

            if (delta.magnitude > largoVisible) return false;

            // Semi-alto del cono a esta distancia + el radio del cuerpo:
            // "el borde del haz toca el borde del jugador" = vigilado.
            float semiAlto = Mathf.Tan(aperturaMediaDeg * Mathf.Deg2Rad) * alFrente + radioJugador;
            if (Mathf.Abs(delta.y) > semiAlto) return false;

            // Línea de visión: una pared entre el ojo y el jugador lo cubre.
            return Physics2D.Linecast(eye, player.position, paredes).collider == null;
        }

        /// <summary>El cuerpo existe (contacto letal + sprite) solo en su mundo; el cono, siempre.</summary>
        private void AplicarPresenciaCuerpo()
        {
            bool presente = cuerpoPresenteEn == Presencia.Ambos ||
                (WorldManager.Instance != null &&
                 ((cuerpoPresenteEn == Presencia.SoloSimulacion && WorldManager.Instance.CurrentWorld == WorldState.Simulation) ||
                  (cuerpoPresenteEn == Presencia.SoloReal && WorldManager.Instance.CurrentWorld == WorldState.Real)));
            if (cuerpo != null) cuerpo.enabled = presente;
            if (cuerpoCol != null) cuerpoCol.enabled = presente;
        }

        private void OnWorldChanged(WorldState _)
        {
            // La detección va ANTES de aplicar presencia: se castiga el acto de
            // cambiar bajo el cono, exista o no el cuerpo en el mundo destino.
            bool detectado = Time.timeSinceLevelLoad >= 0.5f && PlayerEnCono();
            AplicarPresenciaCuerpo();
            if (!detectado) return;

            if (!avisoDado)
            {
                avisoDado = true;
                KeplinMessageController.Instance?.ShowMessage(mensajeDeteccion);
            }
            GameSession.Instance?.RespawnPlayer();
        }

        private void OnTriggerEnter2D(Collider2D other) => TryKill(other);
        private void OnTriggerStay2D(Collider2D other) => TryKill(other);

        private void TryKill(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            GameSession.Instance?.RespawnPlayer();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawLine(puntoA, puntoB);
            Gizmos.DrawWireSphere(puntoA, 0.25f);
            Gizmos.DrawWireSphere(puntoB, 0.25f);

            // Cono en la posición/mirada actual (en editor, desde A mirando a B).
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.7f);
            Vector2 eye = Application.isPlaying
                ? PosOjo()
                : puntoA + new Vector2(ojoOffset.x * (puntoB.x >= puntoA.x ? 1 : -1), ojoOffset.y);
            int d = Application.isPlaying ? dir : (puntoB.x >= puntoA.x ? 1 : -1);
            float l = Application.isPlaying ? largoVisible : largo;
            float ap = aperturaMediaDeg * Mathf.Deg2Rad;
            Vector3 o = eye;
            Gizmos.DrawLine(o, o + new Vector3(Mathf.Cos(ap) * d, Mathf.Sin(ap), 0f) * l);
            Gizmos.DrawLine(o, o + new Vector3(Mathf.Cos(ap) * d, -Mathf.Sin(ap), 0f) * l);
            Gizmos.DrawLine(o, o + new Vector3(d, 0f, 0f) * l);
        }
    }
}
