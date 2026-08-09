using UnityEngine;
using UnityEngine.Events;
using LaProyeccion.Prototipos;

namespace LaProyeccion.World
{
    /// <summary>
    /// Devuelve la caja empujable a su sitio cuando cae donde ya no puede salir.
    ///
    /// POR QUÉ EXISTE: el hueco del ascensor está abierto en cada rellano (decisión
    /// del autor 2026-08-08: sin puertas por ahora). Con el coche arriba, empujar la
    /// caja hacia el ascensor la tira al foso, que está 0,40 u por debajo del suelo
    /// de la planta baja — y un cuerpo dinámico no sube un escalón. Sin esta red, el
    /// jugador puede dejar el nivel IRRESOLUBLE sin hacer nada raro: solo empujando
    /// la caja hacia donde el nivel le está pidiendo que la empuje.
    ///
    /// No es una tirita al foso, es la regla general de cualquier objeto empujable
    /// junto a un vacío. Si algún día el hueco lleva puertas, esta red sobra.
    ///
    /// Detección por OverlapBox en FixedUpdate y no por triggers: un objeto que
    /// aparece por teleport dentro del volumen no dispara OnTriggerEnter (misma
    /// razón por la que PlacaDePresion mira cada frame).
    /// </summary>
    public class RedDeSeguridad : MonoBehaviour
    {
        [Header("Volumen que recupera (relativo al objeto)")]
        [SerializeField] private Vector2 tamano = new Vector2(3.6f, 0.32f);
        [SerializeField] private Vector2 desplazamiento = Vector2.zero;

        [Tooltip("Se dispara al recuperar algo: sitio para un sonido o un aviso.")]
        public UnityEvent OnRecupera;

        private void FixedUpdate()
        {
            Vector2 centro = (Vector2)transform.position + desplazamiento;
            foreach (var hit in Physics2D.OverlapBoxAll(centro, tamano, 0f))
            {
                var caja = hit.GetComponentInParent<CajaEmpujable>();
                if (caja == null) continue;
                caja.ReiniciarFase();
                OnRecupera?.Invoke();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.55f);
            Gizmos.DrawWireCube((Vector2)transform.position + desplazamiento, tamano);
        }
    }
}
