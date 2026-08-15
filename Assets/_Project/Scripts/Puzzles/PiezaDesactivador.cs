using UnityEngine;

namespace LaProyeccion.Puzzles
{
    /// <summary>
    /// SISTEMA DEL JUEGO en su uso de PIEZA-LLAVE (`Zona1`: la llave del ascensor).
    /// ⚠️ Su uso como DESACTIVADOR de amenazas sigue siendo de laboratorio, y promoverlo
    /// exige enmienda consciente de `ALCANCE.md` (ver `pendientes.md`). la PIEZA-DESACTIVADOR de PLAN_REDISENO §2.
    /// Objeto físico y escaso que se recoge, se TRANSPORTA y se coloca en un
    /// <see cref="ZocaloDesactivador"/> para apagar UNA fuerza de Keplin.
    ///
    /// La pieza en sí es tonta: solo sabe si está SUELTA, CARGADA o COLOCADA. Toda la
    /// decisión vive en <see cref="PortadorDePieza"/> (el coste de llevarla) y en el
    /// zócalo (el efecto). Se separa así porque el diseño dice que "usarla ES un
    /// puzzle": el puzzle está en el trayecto y en la elección de dónde gastarla, no
    /// en el objeto.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PiezaDesactivador : MonoBehaviour
    {
        public enum Estado { Suelta, Cargada, Colocada }

        [Header("Lectura (GDD §7: lo que late, se toca)")]
        [SerializeField] private float velocidadPulso = 2.0f;
        [SerializeField, Range(0f, 0.5f)] private float amplitudPulso = 0.10f;
        [Tooltip("Verde menta = recurso vivo, la misma familia que la Semilla.")]
        [SerializeField] private Color colorSuelta = new Color(0.55f, 1f, 0.75f);

        private Vector3 escalaBase;
        private float fase;
        private SpriteRenderer sr;

        public Estado EstadoActual { get; private set; } = Estado.Suelta;

        private void Awake()
        {
            escalaBase = transform.localScale;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = colorSuelta;
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void Update()
        {
            // Solo late cuando está disponible: colocada o cargada, se queda quieta.
            if (EstadoActual != Estado.Suelta) return;
            fase += Time.deltaTime * velocidadPulso;
            transform.localScale = escalaBase * (1f + Mathf.Sin(fase) * amplitudPulso);
        }

        public void MarcarCargada(Transform portador, Vector3 offsetLocal)
        {
            EstadoActual = Estado.Cargada;
            transform.localScale = escalaBase;
            transform.SetParent(portador, false);
            transform.localPosition = offsetLocal;
        }

        public void MarcarSuelta(Vector3 posicionMundo)
        {
            EstadoActual = Estado.Suelta;
            transform.SetParent(null);
            transform.position = posicionMundo;
            transform.localScale = escalaBase;
        }

        public void MarcarColocada(Transform zocalo)
        {
            EstadoActual = Estado.Colocada;
            transform.SetParent(zocalo, false);
            transform.localPosition = new Vector3(0f, 0.55f, 0f);
            transform.localScale = escalaBase;
        }
    }
}
