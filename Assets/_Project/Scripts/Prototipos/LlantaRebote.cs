using UnityEngine;
using UnityEngine.InputSystem;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/ — fuera de ALCANCE): **la llanta**. Un trasto
    /// de goma tirado en el suelo del Real: caes encima y te dispara hacia arriba.
    /// Referencia declarada por el autor: el neumático de Donkey Kong Country.
    ///
    /// Es la respuesta al feedback sobre <see cref="ImpulsoAscendente"/>: aquello era
    /// una **corriente** (volumen continuo, subes mientras estés dentro, sin momento);
    /// esto es un **golpe** (evento puntual en el contacto, con impacto legible). El
    /// verbo no es "flotar", es "rebotar".
    ///
    /// **Presencia: solo el Real** (se pone con <c>WorldExclusivePresence</c> en el
    /// mismo GameObject, no lo gestiona este script — un componente, un trabajo). La
    /// inversión frente a la corriente solo-Sim es deliberada: en la mentira te sube un
    /// parámetro de Keplin, en la verdad te sube un trasto que alguien dejó ahí.
    ///
    /// **Dos alturas, como en DKC** (<see cref="velocidadRebote"/> /
    /// <see cref="velocidadReboteAlto"/>): rebote normal si llegas sin más, rebote alto
    /// si **mantienes saltar** en el instante del contacto. Dos alturas fijas y
    /// diseñables (Pilar 3), no proporcionales a la caída: la altura la decide el
    /// jugador con una intención, no el azar de desde dónde venía.
    ///
    /// Alturas **medidas** en Play con la física a pasos (no calculadas), desde la cara
    /// superior de la llanta: 27 u/s → **3,79 u** · 38 u/s → **7,66 u**. El cálculo
    /// continuo v²/180 da 4,05 y 8,02: la diferencia es el offset de integración
    /// discreta (v·dt/2 con dt=0,02), el mismo que hace que el salto normal llegue a
    /// **2,25 u** y no a los 2,45 teóricos que cita CLAUDE.md. Escalera real para
    /// trazar niveles: **2,25 / 5,79 / 9,66** de altura de pies sobre el suelo.
    ///
    /// Y la altura **no depende de la velocidad de llegada**: verificado dejándose caer
    /// a 0,5 / 5 / 10 / 18 / 25 u/s — dispersión 0,00 u. Eso es lo que permite diseñar
    /// con ella (Pilar 3), y por eso el rebote ASIGNA la velocidad en vez de sumarla.
    ///
    /// **La llanta NO va en el layer Ground, a propósito.** Si fuera suelo,
    /// <c>PlayerController</c> te contaría como grounded encima y un salto en buffer
    /// pisaría el rebote con su jumpForce=21 — es decir, el rebote alto se convertiría
    /// en un salto normal justo cuando el jugador hace el gesto correcto. Fuera de
    /// Ground no hay coyote ni buffer sobre ella: encima de la llanta solo se rebota.
    /// Collider recomendado: **Box** algo menor que el dibujo (redondo sobre el que
    /// resbalas = altura impredecible; se lo debemos a Pilar 3).
    ///
    /// Empuja por VELOCIDAD, igual que <see cref="ImpulsoAscendente"/> y al revés que
    /// <see cref="ChorroEmpuje"/>: <c>PlayerController</c> sobrescribe velocity.x cada
    /// frame pero **respeta la Y** (solo limita la caída), así que salen gratis el arco,
    /// la gravedad y el control horizontal durante el vuelo.
    ///
    /// Limitación conocida (lab): el "mantener" se lee del dispositivo real, así que un
    /// replay de <see cref="ReproductorDePasada"/> —que inyecta pulsos, no holds— siempre
    /// reproducirá el rebote normal. Aceptable para prototipar el feel.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LlantaRebote : MonoBehaviour
    {
        [Header("Rebote (velocidades en u/s; apex = v²/180 con g=90)")]
        [Tooltip("Rebote normal: llegas y sales. 27 u/s ≈ 4,05 u de altura.")]
        [SerializeField, Min(1f)] private float velocidadRebote = 27f;
        [Tooltip("Rebote alto: manteniendo saltar en el contacto. 38 u/s ≈ 8,02 u.")]
        [SerializeField, Min(1f)] private float velocidadReboteAlto = 38f;
        [Tooltip("Un aterrizaje no debe contar dos veces (Enter + Stay del mismo apoyo).")]
        [SerializeField, Min(0f)] private float tiempoMinimoEntreRebotes = 0.15f;
        [Tooltip("Cuán vertical debe ser el contacto para considerarlo pisada y no un " +
                 "roce lateral. 0.5 ≈ 60° respecto de la cara superior.")]
        [SerializeField, Range(0.1f, 1f)] private float normalMinima = 0.5f;

        [Header("Lectura (Pilar 3: el golpe tiene que VERSE)")]
        [Tooltip("Hijo con el sprite. Se aplasta y recupera en cada rebote — es lo que " +
                 "hace que se lea como goma y no como una piedra que te lanza. Opcional.")]
        [SerializeField] private Transform visual;
        [Tooltip("Escala vertical en el momento del impacto (1 = sin aplastar).")]
        [SerializeField, Range(0.2f, 1f)] private float aplastado = 0.55f;
        [Tooltip("Cuánto tarda en recuperar su forma.")]
        [SerializeField, Min(0.01f)] private float duracionAplastado = 0.18f;

        private PlayerInputActions input;
        private Collider2D miCollider;
        private float proximoReboteValidoEn;
        private Vector3 escalaOriginal = Vector3.one;
        private float aplastadoRestante;

        private void Awake()
        {
            // Instancia propia de las acciones, patrón del proyecto (ver
            // RadarPulseController, WorldPeekController): solo la usamos para
            // PREGUNTAR si saltar está mantenido, nunca para consumir el evento.
            input = new PlayerInputActions();
            miCollider = GetComponent<Collider2D>();
            if (visual != null) escalaOriginal = visual.localScale;
        }

        private void OnEnable() => input?.Player.Enable();

        private void OnDisable() => input?.Player.Disable();

        private void OnCollisionEnter2D(Collision2D colision) => IntentarRebote(colision);

        // También en Stay: si te dejas caer y quedas apoyado sin llegar a disparar el
        // Enter (o vuelves a posarte tras el rebote), la llanta sigue siendo una llanta.
        private void OnCollisionStay2D(Collision2D colision) => IntentarRebote(colision);

        private void IntentarRebote(Collision2D colision)
        {
            if (Time.time < proximoReboteValidoEn) return;

            var jugador = colision.collider.GetComponentInParent<PlayerController>();
            if (jugador == null) return;

            var rb = jugador.GetComponent<Rigidbody2D>();
            if (rb == null) return;

            // Solo se rebota desde arriba, y son DOS comprobaciones a propósito.
            //
            // (1) El contacto tiene que ser vertical, no un roce lateral. Se mira el
            //     VALOR ABSOLUTO de normal.y, no su signo: Unity documenta el sentido de
            //     la normal de forma ambigua y, MEDIDO en este proyecto (Play, física a
            //     pasos), apunta del jugador HACIA la llanta — vale (0,-1) cuando está
            //     encima, no (0,+1). Escrito con el signo, la llanta no rebotaba nunca:
            //     el jugador se quedaba de pie sobre ella. Con el absoluto da igual el
            //     convenio, hoy y en la versión de Unity que venga.
            // (2) Quién está encima de quién lo decide la POSICIÓN, que no admite
            //     interpretaciones. Esto es lo que descarta un golpe desde abajo.
            bool contactoVertical = false;
            for (int i = 0; i < colision.contactCount; i++)
            {
                if (Mathf.Abs(colision.GetContact(i).normal.y) >= normalMinima) { contactoVertical = true; break; }
            }
            if (!contactoVertical) return;
            if (colision.collider.bounds.center.y <= miCollider.bounds.center.y) return;

            // Si ya subía más rápido que el rebote, no le FRENAMOS (mismo criterio que
            // la corriente): la llanta garantiza un suelo de velocidad, no lo impone.
            bool mantieneSalto = input != null && input.Player.Jump.IsPressed();
            float velocidad = mantieneSalto ? velocidadReboteAlto : velocidadRebote;
            if (rb.linearVelocity.y >= velocidad) return;

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, velocidad);
            proximoReboteValidoEn = Time.time + tiempoMinimoEntreRebotes;
            aplastadoRestante = duracionAplastado;
        }

        private void Update()
        {
            if (visual == null || aplastadoRestante <= 0f) return;

            aplastadoRestante -= Time.deltaTime;
            // t: 0 recién golpeada (aplastada del todo) → 1 recuperada.
            float t = Mathf.Clamp01(1f - aplastadoRestante / duracionAplastado);
            // Rebote elástico en la recuperación: la goma se pasa de largo y vuelve.
            // Unclamped a propósito — el sobrepaso (estirarse por encima de 1) es justo
            // lo que la lee como goma; con Lerp normal quedaría recortado a 1 y sería
            // un aplastamiento sin muelle.
            float y = Mathf.LerpUnclamped(aplastado, 1f, 1f - Mathf.Cos(t * Mathf.PI * 1.5f) * (1f - t));
            visual.localScale = new Vector3(
                escalaOriginal.x * (2f - y),   // conserva el volumen: lo que baja, ensancha
                escalaOriginal.y * y,
                escalaOriginal.z);

            if (aplastadoRestante <= 0f) visual.localScale = escalaOriginal;
        }

        private void OnDrawGizmosSelected()
        {
            // Las dos alturas que da, dibujadas con la física real (g=90) para poder
            // trazar el nivel sin hacer cuentas. Ver GizmoDeSalto para el arco normal.
            const float g = 90f;
            var col = GetComponent<Collider2D>();
            float cima = col != null ? col.bounds.max.y : transform.position.y;
            var origen = new Vector3(transform.position.x, cima, 0f);
            float ancho = col != null ? col.bounds.size.x : 1f;

            Gizmos.color = new Color(0.95f, 0.40f, 0.22f, 0.9f);   // óxido: es del Real
            float alto = velocidadRebote * velocidadRebote / (2f * g);
            Gizmos.DrawLine(origen, origen + Vector3.up * alto);
            Gizmos.DrawLine(origen + Vector3.up * alto - Vector3.right * ancho,
                            origen + Vector3.up * alto + Vector3.right * ancho);

            Gizmos.color = new Color(1f, 0.75f, 0.3f, 0.9f);
            float altoMax = velocidadReboteAlto * velocidadReboteAlto / (2f * g);
            Gizmos.DrawLine(origen + Vector3.up * alto, origen + Vector3.up * altoMax);
            Gizmos.DrawLine(origen + Vector3.up * altoMax - Vector3.right * ancho,
                            origen + Vector3.up * altoMax + Vector3.right * ancho);
        }
    }
}
