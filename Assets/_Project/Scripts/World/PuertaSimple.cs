using UnityEngine;
using LaProyeccion.Puzzles;

namespace LaProyeccion.World
{
    /// <summary>
    /// SISTEMA DEL JUEGO: puerta que se abre mientras un DualSwitch este activo, SIN autoguardado. puerta que se abre con un DualSwitch,
    /// como Gate pero SIN autoguardado — las escenas de laboratorio no deben
    /// escribir save.*. Si el nivel se promueve al juego, se sustituye por Gate.
    /// </summary>
    public class PuertaSimple : MonoBehaviour
    {
        [SerializeField] private DualSwitch interruptor;
        [SerializeField] private SpriteRenderer cuerpo;
        [SerializeField] private Collider2D bloqueo;

        private void Start()
        {
            if (interruptor == null) return;
            interruptor.OnStateChanged.AddListener(Apply);
            Apply(interruptor.IsOn);
        }

        private void Apply(bool abierta)
        {
            if (cuerpo != null) cuerpo.enabled = !abierta;
            if (bloqueo != null) bloqueo.enabled = !abierta;
        }
    }
}
