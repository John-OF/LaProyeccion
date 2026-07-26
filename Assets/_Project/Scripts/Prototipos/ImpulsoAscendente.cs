using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/): **altura extra que solo existe en la
    /// Simulación** — corriente ascendente, respiradero, columna de neón. Da altura
    /// **donde el diseñador la pone**, y NO es un doble salto.
    ///
    /// Por qué así y no como habilidad permanente (razones ya acordadas, `ideas.md`):
    /// un doble salto le cambiaría la identidad al verbo central —se cambiaría de mundo
    /// *para saltar*, no para leer el espacio— e invalidaría la métrica calibrada
    /// (apex 2,45 u), obligando a auditar todos los labs preguntando "¿ahora se puede
    /// saltar esto?". Como volumen colocado, el kit de movimiento no cambia y no
    /// contradice el "sin doble salto" escrito en GDD/CATALOGO/PLAN_REDISENO.
    ///
    /// Lectura diegética (lo más fuerte de la idea): la novela dice que **la física es
    /// un parámetro que Keplin configura**, así que "en la Sim tu cuerpo hace cosas que
    /// un cuerpo no puede" se entiende sin una línea de texto. Y la contraparte sale
    /// gratis: en la Cueva, que es lo Real, eres solo un cuerpo pesado y limitado.
    ///
    /// **Empuja por VELOCIDAD, no por posición** — al revés que su pariente
    /// <see cref="ChorroEmpuje"/> (T6). Aquel tuvo que mover `rb.position` porque
    /// `PlayerController` sobrescribe `velocity.x` cada frame; pero **la componente Y la
    /// respeta** (solo limita la caída), así que aquí se puede fijar la velocidad de
    /// subida y salen gratis el arco correcto al salir, la gravedad y el control
    /// horizontal mientras subes.
    ///
    /// Velocidad CONSTANTE, no aceleración: la altura que da es siempre la misma y se
    /// puede diseñar el nivel con ella (Pilar 3). Con aceleración, cuánto subes
    /// dependería de cuánto rato lleves dentro.
    /// </summary>
    public class ImpulsoAscendente : MonoBehaviour
    {
        [Header("Presencia")]
        [Tooltip("Mundo en el que la corriente existe. La gracia es que sea la Simulación.")]
        [SerializeField] private WorldState mundoActivo = WorldState.Simulation;

        [Header("Empuje")]
        [Tooltip("Velocidad de subida mientras estás dentro (u/s). Al salir conservas esta " +
                 "velocidad, así que la altura extra tras la boca es v²/(2·g) con g=90.")]
        [SerializeField, Min(1f)] private float velocidadAscenso = 15f;
        [Tooltip("Tamaño del volumen de corriente, en unidades de mundo.")]
        [SerializeField] private Vector2 tamano = new Vector2(2f, 6f);

        [Header("Lectura")]
        [Tooltip("Cyan de Simulación (GDD §7). No usar otro color: dice de qué mundo es.")]
        [SerializeField] private Color color = new Color(0.25f, 0.85f, 1f, 0.30f);
        [SerializeField, Min(0f)] private float velocidadPulso = 3f;

        private SpriteRenderer sr;
        private bool activo;
        private readonly Collider2D[] resultados = new Collider2D[8];
        private ContactFilter2D filtro;

        private Vector2 Centro => (Vector2)transform.position;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            filtro = new ContactFilter2D { useTriggers = false };
            filtro.NoFilter();
        }

        private void OnEnable()
        {
            WorldManager.OnWorldChanged += AlCambiarMundo;
            if (WorldManager.Instance != null) AlCambiarMundo(WorldManager.Instance.CurrentWorld);
        }

        private void OnDisable() => WorldManager.OnWorldChanged -= AlCambiarMundo;

        private void AlCambiarMundo(WorldState mundo)
        {
            activo = mundo == mundoActivo;
            if (sr != null) sr.enabled = activo;
        }

        private void Update()
        {
            // Latido suave: una corriente quieta se lee como decorado.
            if (!activo || sr == null) return;
            float t = (Mathf.Sin(Time.time * velocidadPulso) + 1f) * 0.5f;
            var c = color;
            c.a = Mathf.Lerp(color.a * 0.6f, color.a, t);
            sr.color = c;
        }

        private void FixedUpdate()
        {
            if (!activo) return;

            int n = Physics2D.OverlapBox(Centro, tamano, 0f, filtro, resultados);
            for (int i = 0; i < n; i++)
            {
                var col = resultados[i];
                if (col == null) continue;
                var jugador = col.GetComponentInParent<LaProyeccion.Player.PlayerController>();
                if (jugador == null) continue;

                var rb = jugador.GetComponent<Rigidbody2D>();
                if (rb == null) continue;

                // Max, no asignación: si ya subes más rápido (un salto reciente), la
                // corriente no te FRENA. Solo garantiza un suelo de velocidad.
                if (rb.linearVelocity.y < velocidadAscenso)
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, velocidadAscenso);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, tamano);
        }
    }
}
