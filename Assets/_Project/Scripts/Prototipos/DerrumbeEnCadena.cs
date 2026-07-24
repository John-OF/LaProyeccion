using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): derrumbe en cadena (T2, ideas.md
    /// §Trampas). Asciende la <see cref="RepisaFragil"/> ya validada a SET-PIECE de
    /// compromiso: una fila de losas sobre un pozo que ceden EN SECUENCIA cuando el
    /// jugador pisa la primera — no hay vuelta atrás, hay que seguir hacia adelante.
    ///
    /// Peligro físico indiferente, telegrafiado con TEMBLOR por losa (Pilar 3, nunca color
    /// de estado). El frente del derrumbe propaga a `retardoEntreLosas` por losa: afínalo
    /// para que persiga justo (recto = más rápido). Tras cruzar (o morir) todo el conjunto
    /// se reconstruye para que el respawn lo encuentre entero.
    ///
    /// Las losas son los hijos de este objeto (con SpriteRenderer + Collider2D en capa
    /// Ground); el coordinador las ordena por X (entrada → salida) y las gestiona.
    /// </summary>
    public class DerrumbeEnCadena : MonoBehaviour
    {
        [Header("Disparo (sobre la primera losa)")]
        [SerializeField] private Vector2 zonaOffset = new Vector2(0f, 0.75f);
        [SerializeField] private Vector2 zonaSize = new Vector2(1.8f, 2f);

        [Header("Ritmo del derrumbe")]
        [Tooltip("Gracia tras pisar la primera losa, antes de que empiece a caer.")]
        [SerializeField, Min(0f)] private float retardoInicial = 0.25f;
        [Tooltip("Temblor de aviso de cada losa antes de ceder.")]
        [SerializeField, Min(0f)] private float retardoCae = 0.35f;
        [Tooltip("Propagación del frente: tiempo entre el inicio de una losa y la siguiente.")]
        [SerializeField, Min(0f)] private float retardoEntreLosas = 0.28f;
        [Tooltip("Segundos hasta reconstruir todo el conjunto (para que el respawn lo encuentre entero).")]
        [SerializeField, Min(0.1f)] private float retardoReaparece = 3.5f;
        [SerializeField, Min(0f)] private float temblor = 0.05f;

        private readonly List<Transform> losas = new List<Transform>();
        private readonly List<Vector3> posBase = new List<Vector3>();
        private readonly List<Collider2D> cols = new List<Collider2D>();
        private readonly List<SpriteRenderer> srs = new List<SpriteRenderer>();
        private readonly List<Color> colores = new List<Color>();
        private bool armado = true;

        private void Awake()
        {
            var list = new List<Transform>();
            foreach (Transform c in transform) list.Add(c);
            list.Sort((a, b) => a.position.x.CompareTo(b.position.x)); // entrada → salida
            foreach (var t in list)
            {
                losas.Add(t);
                posBase.Add(t.localPosition);
                cols.Add(t.GetComponent<Collider2D>());
                var sr = t.GetComponent<SpriteRenderer>();
                srs.Add(sr);
                colores.Add(sr != null ? sr.color : Color.white);
            }
        }

        private void Update()
        {
            if (!armado || losas.Count == 0) return;
            Vector2 c = (Vector2)losas[0].position + zonaOffset;
            var hits = Physics2D.OverlapBoxAll(c, zonaSize, 0f);
            foreach (var h in hits)
            {
                if (h.GetComponentInParent<PlayerController>() != null)
                {
                    armado = false;
                    StartCoroutine(Cascada());
                    break;
                }
            }
        }

        private IEnumerator Cascada()
        {
            yield return new WaitForSeconds(retardoInicial);
            for (int i = 0; i < losas.Count; i++)
            {
                StartCoroutine(TemblarYCaer(i));
                yield return new WaitForSeconds(retardoEntreLosas);
            }
            // Espera a que caiga la última + margen, y reconstruye el set-piece completo.
            yield return new WaitForSeconds(retardoCae + retardoReaparece);
            ReconstruirTodo();
            armado = true;
        }

        private IEnumerator TemblarYCaer(int i)
        {
            var t = losas[i];
            var sr = srs[i];
            var baseC = colores[i];
            float e = 0f;
            while (e < retardoCae)
            {
                e += Time.deltaTime;
                Vector2 j = Random.insideUnitCircle * temblor;
                t.localPosition = posBase[i] + new Vector3(j.x, j.y, 0f);
                if (sr != null)
                {
                    float k = Mathf.PingPong(e * 9f, 1f) * 0.4f;
                    sr.color = new Color(baseC.r * (1f - k), baseC.g * (1f - k), baseC.b * (1f - k), baseC.a);
                }
                yield return null;
            }
            // Cede: deja de ser sólida y el jugador que siga encima cae al pozo.
            t.localPosition = posBase[i];
            if (cols[i] != null) cols[i].enabled = false;
            if (sr != null) sr.enabled = false;
            AudioManager.Instance?.PlayPiedraGolpe(); // desprendimiento pétreo (provisional)
        }

        private void ReconstruirTodo()
        {
            for (int i = 0; i < losas.Count; i++)
            {
                losas[i].localPosition = posBase[i];
                if (cols[i] != null) cols[i].enabled = true;
                if (srs[i] != null)
                {
                    srs[i].enabled = true;
                    srs[i].color = colores[i];
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (transform.childCount == 0) return;
            Transform primera = null;
            float minX = float.MaxValue;
            foreach (Transform c in transform)
                if (c.position.x < minX) { minX = c.position.x; primera = c; }
            if (primera == null) return;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
            Gizmos.DrawWireCube((Vector2)primera.position + zonaOffset, zonaSize);
        }
    }
}
