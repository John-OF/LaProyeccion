using UnityEngine;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): indicador de la munición de PIEDRAS.
    /// Hermano del <see cref="LaProyeccion.Player.SeedOrbIndicator"/> (mismo espíritu
    /// diegético, sin HUD, GDD §7): una fila de pips flota sobre el jugador, uno por
    /// piedra en el <see cref="InventarioPiedras"/>, hasta la capacidad.
    ///
    /// Diferencia clave con los orbes de Semilla: las piedras NO emiten luz, así que en la
    /// oscuridad de la cueva (luz global ~0.03) unos guijarros oscuros serían invisibles.
    /// Por eso los pips usan un material UNLIT (Sprites/Default): se ven a plena intensidad
    /// pese a la negrura, sin fingir que son fuentes de luz.
    /// </summary>
    public class IndicadorPiedras : MonoBehaviour
    {
        [Tooltip("Inventario a observar. Si se deja vacío se busca en el padre.")]
        [SerializeField] private InventarioPiedras inventario;
        [SerializeField] private Sprite pipSprite;
        [SerializeField] private Color pipColor = new Color(0.78f, 0.74f, 0.68f, 0.95f);
        [SerializeField, Range(0.05f, 0.5f)] private float pipScale = 0.16f;
        [Tooltip("Separación horizontal entre pips (unidades de mundo).")]
        [SerializeField] private float separacion = 0.26f;
        [Tooltip("Altura de la fila sobre el origen del jugador.")]
        [SerializeField] private float alturaFila = 1.55f;

        [Header("Flotación")]
        [SerializeField] private float bobSpeed = 2.4f;
        [SerializeField] private float bobAmplitude = 0.05f;

        private Transform[] pips;
        private Material matUnlit;

        private void Awake()
        {
            if (inventario == null) inventario = GetComponentInParent<InventarioPiedras>();
            int max = inventario != null ? inventario.Capacity : 5;

            // Material unlit compartido: se ve pese a la oscuridad de la cueva.
            matUnlit = new Material(Shader.Find("Sprites/Default"));

            pips = new Transform[max];
            for (int i = 0; i < max; i++)
            {
                var go = new GameObject("PiedraPip_" + (i + 1));
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * pipScale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = pipSprite;
                sr.sharedMaterial = matUnlit;
                sr.color = pipColor;
                sr.sortingOrder = 22; // por delante del personaje y de los orbes de semilla

                go.SetActive(false);
                pips[i] = go.transform;
            }
            ColocarFila();
        }

        private void OnEnable()
        {
            if (inventario != null) inventario.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventario != null) inventario.OnChanged -= Refresh;
        }

        private void Update()
        {
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null || !pips[i].gameObject.activeSelf) continue;
                float x = (i - (pips.Length - 1) * 0.5f) * separacion;
                float bob = Mathf.Sin(Time.time * bobSpeed + i * 1.7f) * bobAmplitude;
                pips[i].localPosition = new Vector3(x, alturaFila + bob, 0f);
            }
        }

        private void ColocarFila()
        {
            for (int i = 0; i < pips.Length; i++)
            {
                float x = (i - (pips.Length - 1) * 0.5f) * separacion;
                pips[i].localPosition = new Vector3(x, alturaFila, 0f);
            }
        }

        private void Refresh()
        {
            int count = inventario != null ? inventario.Count : 0;
            for (int i = 0; i < pips.Length; i++)
                if (pips[i] != null) pips[i].gameObject.SetActive(i < count);
        }
    }
}
