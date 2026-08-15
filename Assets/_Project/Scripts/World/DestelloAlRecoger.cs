using UnityEngine;
using UnityEngine.Events;
using LaProyeccion.Puzzles;

namespace LaProyeccion.World
{
    /// <summary>
    /// SISTEMA DEL JUEGO (`Zona1` desde 2026-08-14; nació como IDEA 1 de `_docs/ideas.md`). **IDEA 1** de `_docs/ideas.md`:
    /// el mundo se deshilacha UNA vez, justo al recoger la pieza del aparato.
    ///
    /// Qué se está probando: si el último segundo del nivel puede dejar de ser "coges un
    /// objeto" y pasar a ser **la pregunta que abre el nivel siguiente**, sin una línea de
    /// texto. La pieza ES la Etapa 1 (`ALCANCE.md` §4 v1.5) y la Etapa 1 es exactamente
    /// esto: información involuntaria, no un verbo.
    ///
    /// Vigila el estado de la pieza en vez de engancharse a la recogida: `PortadorDePieza`
    /// no expone ningún evento, y **no se toca código compartido para probar una idea**. El
    /// coste es un `Update` trivial que se apaga solo en cuanto dispara.
    ///
    /// ⚠️ Para que el destello SIGNIFIQUE algo tiene que haber algo del otro lado que
    /// asomar: sin objetos `WorldExclusivePresence` del Real cerca del punto, la pantalla
    /// parpadea y no revela nada. Ese es justo uno de los motivos por los que la idea puede
    /// terminar descartada.
    /// </summary>
    public class DestelloAlRecoger : MonoBehaviour
    {
        [Header("Qué vigilar")]
        [Tooltip("La pieza cuya recogida dispara la grieta.")]
        [SerializeField] private PiezaDesactivador pieza;

        [Header("Qué disparar")]
        [SerializeField] private ParpadeoDeSimulacion parpadeo;
        [Tooltip("Dónde se abre la grieta. Vacío = el parpadeo elige punto él solo.")]
        [SerializeField] private Transform punto;
        [Tooltip("Espera desde la recogida hasta la grieta. Un pelín de retardo se lee " +
                 "como consecuencia; cero se lee como parte del propio pickup.")]
        [SerializeField, Min(0f)] private float retardo = 0.35f;

        [Tooltip("Se dispara a la vez que la grieta: sitio para sonido o pensamiento.")]
        public UnityEvent OnGrieta;

        [Header("Qué DESPIERTA la grieta (decisión del autor, 2026-08-12)")]
        [Tooltip("Objetos apagados que se encienden con la grieta. La planta vacía está " +
                 "vacía de VERDAD hasta este momento: si los rastros del predecesor ya " +
                 "estuvieran ahí antes, coger la pieza no habría abierto nada.\n\n" +
                 "Va aquí y no en OnGrieta porque un UnityEvent a medio cablear no da error: " +
                 "el nivel simplemente se quedaría sin su parte opcional y en silencio.")]
        [SerializeField] private GameObject[] despiertan;

        private bool armado = true;
        private float dispararEn = -1f;

        private void Update()
        {
            if (!armado) return;

            if (dispararEn < 0f)
            {
                if (pieza == null || pieza.EstadoActual == PiezaDesactivador.Estado.Suelta) return;
                dispararEn = Time.time + retardo;
                return;
            }

            if (Time.time < dispararEn) return;

            // Si el registro GhostReveal está ocupado, DestellarAhora devuelve false:
            // se reintenta al frame siguiente en vez de perder el momento en silencio.
            if (parpadeo != null && !parpadeo.DestellarAhora(punto)) return;

            if (despiertan != null)
            {
                foreach (var go in despiertan)
                    if (go != null) go.SetActive(true);
            }

            OnGrieta?.Invoke();
            armado = false;
            enabled = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (punto == null) return;
            Gizmos.color = new Color(0.95f, 0.4f, 0.22f, 0.9f);
            Gizmos.DrawWireSphere(punto.position, 1.2f);
        }
    }
}
