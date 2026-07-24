using System.Collections;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): la piedra en vuelo. Es el corazón
    /// del "ver con el oído" de la cueva (ideas.md §Trampas, T3/T4):
    ///
    /// - Golpea algo SÓLIDO  → GOLPE SECO  (hay suelo).      [T3]
    /// - Cae al vacío        → nunca golpea → se desvanece en SILENCIO (no hay suelo). [T3]
    /// - Entra en <see cref="AguaLetal"/> → CHAPOTEO (es agua; REGLA DEL AGUA).       [T4]
    ///
    /// El silencio del vacío es intencionado: la trampa T3 es la AUSENCIA de información,
    /// no un objeto. Por eso, si no impacta nada, la piedra se destruye SIN sonido al
    /// agotar <see cref="vidaMax"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class PiedraLanzada : MonoBehaviour
    {
        [Tooltip("Sin impactar nada, se desvanece en SILENCIO al agotarse (el vacío de T3).")]
        [SerializeField, Min(0.5f)] private float vidaMax = 5f;

        [Tooltip("Tras un golpe seco, reposa este tiempo y desaparece (no ensuciar la sala).")]
        [SerializeField, Min(0.1f)] private float vidaTrasImpacto = 2f;

        private bool golpeado;   // ya sonó el golpe seco (no repetir en rebotes)
        private bool terminada;  // ya se resolvió en agua (se hundió): no hacer más

        private void Start() => StartCoroutine(VidaMaxima());

        private IEnumerator VidaMaxima()
        {
            yield return new WaitForSeconds(vidaMax);
            // Nunca golpeó nada: cayó al vacío → se destruye SIN sonido (silencio = vacío).
            Destroy(gameObject);
        }

        private void OnCollisionEnter2D(Collision2D _)
        {
            if (terminada || golpeado) return;
            golpeado = true;
            AudioManager.Instance?.PlayPiedraGolpe();      // golpe seco: hay suelo (T3)
            StartCoroutine(ReposarYDesaparecer());
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // El agua SIEMPRE suena al entrar, aunque la piedra haya golpeado suelo antes
            // y luego ruede/caiga a ella: es donde se hunde (REGLA DEL AGUA, T4).
            if (terminada) return;
            if (other.GetComponentInParent<AguaLetal>() == null) return;
            terminada = true;
            AudioManager.Instance?.PlayPiedraAgua();        // chapoteo: es agua (T4)
            Destroy(gameObject);                            // se hunde
        }

        private IEnumerator ReposarYDesaparecer()
        {
            yield return new WaitForSeconds(vidaTrasImpacto);
            Destroy(gameObject);
        }
    }
}
