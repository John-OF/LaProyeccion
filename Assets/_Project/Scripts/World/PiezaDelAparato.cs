using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.World
{
    /// <summary>
    /// UNA PIEZA DEL APARATO DEL CAMBIO. Se recoge **tocándola** y desaparece: no se carga, no se
    /// transporta, no se instala en ningún sitio (`ALCANCE.md` §4 v1.6, *"la pieza se recoge y ya
    /// está… el progreso ocurre en el momento de tocarla"*). Lo único que pasa es que **sube el
    /// brillo de la pulsera**.
    ///
    /// **Por qué existe este script y no se reutiliza `PiezaDesactivador`,** que es lo que hacía el
    /// nivel 1 hasta hoy: aquella es la pieza de *la otra* mecánica, la que **se carga** y cuyo
    /// coste es tenerla aquí y no allá — así que venía con `PortadorDePieza`, con su verbo (T) y
    /// con el objeto colgado del personaje. Eso es exactamente el gesto que la v1.6 vino a matar, y
    /// se veía: un cuadrado flotando sobre la cabeza. Dos significados con un solo verbo, otra vez.
    /// Aquí no hay verbo ninguno: tocas y ya.
    ///
    /// **Cómo sabe que ya la cogiste** (sin una clave de guardado más): cada pieza lleva su
    /// <see cref="numero"/> (1..4) y al arrancar se esconde si el aparato ya tiene esa pieza o más.
    /// El recuento que ya se guarda hace de memoria, así que no puede haber dos verdades que se
    /// contradigan.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PiezaDelAparato : MonoBehaviour
    {
        [Tooltip("Cuál de las cuatro es (1..4). Si el aparato ya tiene este número de piezas, " +
                 "esta ya se recogió y el objeto no aparece.")]
        [SerializeField, Range(1, 4)] private int numero = 1;

        [Header("Lectura (GDD §7: lo que late, se toca)")]
        [SerializeField] private float velocidadPulso = 2.0f;
        [SerializeField, Range(0f, 0.5f)] private float amplitudPulso = 0.10f;
        [Tooltip("Verde menta = recurso vivo, la misma familia que la Semilla.")]
        [SerializeField] private Color color = new Color(0.55f, 1f, 0.75f);

        private Vector3 escalaBase;
        private float fase;

        private void Awake()
        {
            escalaBase = transform.localScale;
            GetComponent<Collider2D>().isTrigger = true;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = color;
        }

        private void Start()
        {
            // Ya recogida en esta partida: no reaparece.
            if (EstadoDelAparato.Piezas >= numero) gameObject.SetActive(false);
        }

        private void Update()
        {
            fase += Time.deltaTime * velocidadPulso;
            transform.localScale = escalaBase * (1f + Mathf.Sin(fase) * amplitudPulso);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;

            // Sin pulsera no hay dónde meterla. No debería poder pasar —la pieza no existe hasta
            // llevarla— pero si algún día se coloca una suelta, que no se pierda en silencio.
            if (!EstadoDelAparato.SumarPieza()) return;

            gameObject.SetActive(false);
        }
    }
}
