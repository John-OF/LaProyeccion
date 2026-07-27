using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LaProyeccion.Narrative;
using LaProyeccion.Puzzles;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/ — fuera de ALCANCE): **interruptor de mantenimiento**
    /// (candidata 1 de "apagar el foco de vigilancia", pedida el 2026-07-11 y sin prototipar
    /// hasta hoy). Apaga una fuerza de Keplin **N segundos** y Keplin la reinicia sola.
    ///
    /// **Qué lo distingue de la pieza-desactivador, que hace el mismo efecto.** Lo que cambia
    /// no es lo que apaga, es **lo que cuesta**:
    /// - La pieza cuesta **movilidad** —tenerla aquí y no allá— y dura lo que dure puesta:
    ///   mientras está, no hay urgencia. Es un puzle de *exclusividad*.
    /// - Esto cuesta **tiempo**: la ventana se cierra sola. Es un puzle de *ritmo* — la ruta y
    ///   el momento. La pieza no puede plantearlo.
    /// Por eso conviven en vez de estorbarse.
    ///
    /// **No inventa su propio apagado**: usa <see cref="ApagadoDeAmenaza"/>, que es donde vive
    /// el contrato de legibilidad (apagar SIEMPRE tiñe de azul hielo, porque un haz rojo que no
    /// mata es la mentira que el Pilar 3 prohíbe). Así el jugador lee igual una amenaza apagada
    /// por la pieza que por el interruptor, que es lo que hace que el vocabulario sirva.
    ///
    /// **El preaviso es la mitad del diseño** (Pilar 3): sin él, la vuelta del foco es una
    /// muerte sin causa visible. Los últimos <see cref="preaviso"/> segundos el tinte apagado
    /// **pulsa cada vez más rápido**. Ojo al detalle: pulsa entre azul hielo y azul hielo
    /// oscurecido, **nunca hacia el color letal** — mostrar el rojo antes de que mate sería
    /// la misma mentira al revés, y el jugador aprendería a desconfiar del color.
    ///
    /// Cableado estándar del proyecto, sin referencias duras:
    ///   Interactable.OnInteract → DualSwitch.Toggle()  →  DualSwitch.OnActivated → Accionar()
    /// Al reiniciarse, este componente devuelve el <see cref="DualSwitch"/> a off para que se
    /// pueda volver a accionar (por eso el switch debe tener `oneWay = false`).
    /// </summary>
    public class InterruptorDeMantenimiento : MonoBehaviour
    {
        [Header("Qué apaga")]
        [Tooltip("Las fuerzas de Keplin que este interruptor corta. Misma idea que el zócalo.")]
        [SerializeField] private List<MonoBehaviour> amenazas = new List<MonoBehaviour>();

        [Header("La ventana — ES EL DIAL DEL PUZLE")]
        [Tooltip("Segundos que permanece apagado. Es el ancho de la ventana: cuánto camino cabe dentro.")]
        [SerializeField, Min(0.5f)] private float duracion = 6f;
        [Tooltip("Últimos segundos, ya dentro de la duración, en los que el apagado parpadea " +
                 "avisando de que vuelve. Sin esto la vuelta es una muerte sin causa visible.")]
        [SerializeField, Min(0f)] private float preaviso = 2f;

        [Header("Vuelta al reposo")]
        [Tooltip("El interruptor que lo acciona. Se devuelve a off al reiniciarse Keplin, para " +
                 "poder volver a usarlo. Debe tener oneWay = false.")]
        [SerializeField] private DualSwitch interruptor;

        [Header("Narrativa")]
        [SerializeField, TextArea] private string mensajeAlAccionar =
            "[TEXTO PENDIENTE: Keplin acusa recibo de un corte de mantenimiento — administrativo, sin alarmarse]";

        private readonly List<ApagadoDeAmenaza.Estado> apagadas = new List<ApagadoDeAmenaza.Estado>();
        private Coroutine ciclo;

        /// <summary>¿Está la ventana abierta ahora mismo?</summary>
        public bool Apagado => ciclo != null;

        /// <summary>
        /// Acciona el corte. Se cablea desde `DualSwitch.OnActivated`. Si ya está apagado no
        /// hace nada: la ventana no se puede alargar volviendo a accionarlo — el reloj es de
        /// Keplin, no tuyo, y esa es justo la diferencia con la pieza.
        /// </summary>
        public void Accionar()
        {
            if (ciclo != null) return;
            ciclo = StartCoroutine(Ciclo());
        }

        private IEnumerator Ciclo()
        {
            foreach (var a in amenazas)
            {
                if (a == null) continue;
                var estado = ApagadoDeAmenaza.Apagar(a);
                if (estado != null) apagadas.Add(estado);
            }
            if (!string.IsNullOrEmpty(mensajeAlAccionar))
                KeplinMessageController.Instance?.ShowMessage(mensajeAlAccionar);

            float estable = Mathf.Max(0f, duracion - preaviso);
            yield return new WaitForSeconds(estable);

            // Preaviso: el tinte apagado pulsa, y cada vez más rápido.
            float t = 0f;
            while (t < preaviso)
            {
                t += Time.deltaTime;
                float restante = Mathf.Clamp01(1f - t / Mathf.Max(0.01f, preaviso));
                float frecuencia = Mathf.Lerp(14f, 4f, restante);   // acelera al acercarse
                float k = (Mathf.Sin(t * frecuencia) + 1f) * 0.5f;
                // Entre el azul hielo y una versión oscurecida de ESE MISMO azul: nunca se
                // asoma el color letal antes de tiempo.
                Color pulso = Color.Lerp(ApagadoDeAmenaza.TintApagado * 0.45f, ApagadoDeAmenaza.TintApagado, k);
                pulso.a = 1f;
                foreach (var e in apagadas)
                    for (int i = 0; i < e.renderers.Length; i++)
                        if (e.renderers[i] != null) e.renderers[i].color = pulso;
                yield return null;
            }

            Restaurar();
        }

        private void Restaurar()
        {
            foreach (var e in apagadas) ApagadoDeAmenaza.Restaurar(e);
            apagadas.Clear();
            // El interruptor vuelve a reposo: Keplin lo ha reiniciado y se puede volver a usar.
            if (interruptor != null) interruptor.SetState(false);
            ciclo = null;
        }

        private void OnDisable()
        {
            // Descarga de escena con la ventana abierta: dejar las amenazas como estaban, o
            // quedarían apagadas y teñidas para siempre en la escena guardada.
            if (ciclo != null) { StopCoroutine(ciclo); Restaurar(); }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.75f, 0.90f, 0.9f);
            foreach (var a in amenazas)
                if (a != null) Gizmos.DrawLine(transform.position, a.transform.position);
        }
    }
}
