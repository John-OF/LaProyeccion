using System.Collections;
using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE).
    ///
    /// Feedback de pulsar el cambio de mundo DONDE NO HAY OTRO MUNDO (la Cueva;
    /// `PLAN_REDISENO.md` §4, decidido el 2026-07-27). El bloqueo en sí es gratis —
    /// `WorldManager.DisableSwitch()` + el evento `OnSwitchDenied` ya existían — pero
    /// no puede fallar en silencio (Pilar 3).
    ///
    /// DOS FAMILIAS DE "PULSASTE Y NO PASÓ", y esta es la segunda:
    /// - **Flash rojo** (`ZonaDeCambio`, marea): *el sistema te lo PROHÍBE*. Regla de Keplin.
    /// - **Aviso breve y apagado** (esto, y el fizzle del radar sin Semilla): *NO HAY NADA AHÍ*.
    /// Nunca teñir esto de rojo: diría "prohibido" cuando lo que pasa es que no hay a dónde ir.
    ///
    /// EL ANILLO SE CONTRAE, no se expande, y ahí está toda la idea (decisión del autor
    /// 2026-07-27): el radar lanza un pulso hacia fuera y algo vuelve; aquí lanzas y **no
    /// vuelve nada**. En la cueva, donde el radar ya es sonar, la lectura es casi literal.
    /// Efecto PROPIO, no el fizzle de la Semilla reciclado: son mensajes distintos —
    /// "no tienes el recurso" contra "no hay otro lado".
    /// </summary>
    public class CambioAusenteFeedback : MonoBehaviour
    {
        [Header("Anillo que se contrae")]
        [Tooltip("Corona. Reutiliza el sprite del radar: lo que cambia es la animación, no el asset.")]
        [SerializeField] private Sprite ringSprite;
        [SerializeField] private Color color = new Color(0.62f, 0.66f, 0.70f, 0.55f);
        [Tooltip("Radio inicial (fuera) — el anillo nace ancho y se cierra sobre el jugador.")]
        [SerializeField, Min(0.1f)] private float radioInicial = 2.2f;
        [Tooltip("Radio final (dentro). No llega a cero: se apaga antes, para que no parezca un punto.")]
        [SerializeField, Min(0f)] private float radioFinal = 0.25f;
        [SerializeField, Min(0.05f)] private float duracion = 0.32f;
        [SerializeField] private int sortingOrder = 40;

        [Header("Ritmo")]
        [Tooltip("Antirebote: pulsar en ráfaga no encadena diez anillos.")]
        [SerializeField, Min(0f)] private float cooldown = 0.25f;

        [Header("Texto (pensamiento del personaje)")]
        [Tooltip("Solo la PRIMERA vez. La primera pulsación es el hito narrativo — pierdes el verbo " +
                 "central del juego —; la decimocuarta ya es vocabulario y el mensaje estorbaría.")]
        [SerializeField] private bool soloUnaVezElMensaje = true;
        [TextArea]
        [SerializeField] private string mensaje =
            "[TEXTO PENDIENTE: primer intento de cambiar de mundo en la Cueva. Es DESCONCIERTO, no " +
            "conclusión: aquí todavía no sabe que hay una simulación — eso lo descubre en esta misma " +
            "zona. La frase que lo explica va DESPUÉS del hallazgo, no aquí.]";
        [Tooltip("A dónde va el texto. Hoy no hay canal de pensamiento del personaje (es su propio " +
                 "paso); mientras no exista, esto queda sin cablear y el mensaje solo se registra.")]
        [SerializeField] private UnityEngine.Events.UnityEvent<string> alMostrarMensaje;

        private Transform player;
        private float ultimo = -999f;
        private bool yaMostrado;

        private void Awake()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        private void OnEnable() { WorldManager.OnSwitchDenied += AlDenegar; }
        private void OnDisable() { WorldManager.OnSwitchDenied -= AlDenegar; }

        private void AlDenegar()
        {
            if (Time.time - ultimo < cooldown) return;
            ultimo = Time.time;

            LanzarAnillo();
            AudioManager.Instance?.PlayCambioAusente();

            if (!string.IsNullOrEmpty(mensaje) && !(soloUnaVezElMensaje && yaMostrado))
            {
                yaMostrado = true;
                if (alMostrarMensaje != null) alMostrarMensaje.Invoke(mensaje);
                else Debug.Log("[CambioAusente] " + mensaje);
            }
        }

        private void LanzarAnillo()
        {
            if (ringSprite == null || player == null) return;

            var go = new GameObject("AnilloCambioAusente");
            go.transform.position = player.position;   // anclado al jugador, no al origen
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ringSprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            StartCoroutine(Contraer(go.transform, sr));
        }

        private IEnumerator Contraer(Transform anillo, SpriteRenderer sr)
        {
            float t = 0f;
            while (t < duracion && anillo != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duracion);
                // Acelerando hacia dentro (al revés que el radar, que desacelera hacia fuera):
                // se cierra de golpe, como algo que se traga a sí mismo.
                float eased = k * k;
                anillo.localScale = Vector3.one * Mathf.Lerp(radioInicial, radioFinal, eased);
                var c = color;
                c.a = color.a * (1f - k);
                sr.color = c;
                yield return null;
            }
            if (anillo != null) Destroy(anillo.gameObject);
        }
    }
}
