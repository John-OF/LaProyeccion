using UnityEngine;
using UnityEngine.Rendering.Universal;
using LaProyeccion.Core;

namespace LaProyeccion.Player
{
    /// <summary>
    /// Indicador diegético del inventario de Semillas (F1.P4, GDD §7: sin HUD).
    /// Hijo de PF_Player: por cada semilla en inventario, un punto de luminiscencia
    /// tenue flota junto al personaje (máx. 2). Los orbes se crean en runtime
    /// (sprite + Light2D débil) y se muestran/ocultan con SeedInventory.OnChanged.
    /// </summary>
    public class SeedOrbIndicator : MonoBehaviour
    {
        [Tooltip("Inventario a observar. Si se deja vacío se busca en el padre.")]
        [SerializeField] private SeedInventory inventory;
        [SerializeField] private Sprite orbSprite;
        [SerializeField] private Color orbColor = new Color(0.55f, 1f, 0.75f, 0.9f);
        [SerializeField, Range(0.05f, 1f)] private float orbScale = 0.35f;
        [SerializeField] private int maxOrbs = 2;

        [Header("Flotación")]
        [SerializeField] private float bobSpeed = 2.4f;
        [SerializeField] private float bobAmplitude = 0.08f;

        private static readonly Vector2[] offsets =
        {
            new Vector2(-0.55f, 1.35f),
            new Vector2(-0.9f, 1.05f)
        };

        private Transform[] orbs;

        private void Awake()
        {
            if (inventory == null) inventory = GetComponentInParent<SeedInventory>();

            orbs = new Transform[maxOrbs];
            for (int i = 0; i < maxOrbs; i++)
            {
                var go = new GameObject("SeedOrb_" + (i + 1));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = offsets[Mathf.Min(i, offsets.Length - 1)];
                go.transform.localScale = Vector3.one * orbScale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = orbSprite;
                sr.color = orbColor;
                sr.sortingOrder = 20; // por delante del personaje

                var light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = orbColor;
                light.intensity = 0.45f;
                light.pointLightOuterRadius = 1.1f;

                go.SetActive(false);
                orbs[i] = go.transform;
            }
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnChanged -= Refresh;
        }

        private void Update()
        {
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null || !orbs[i].gameObject.activeSelf) continue;
                var basePos = offsets[Mathf.Min(i, offsets.Length - 1)];
                float bob = Mathf.Sin(Time.time * bobSpeed + i * 1.7f) * bobAmplitude;
                orbs[i].localPosition = new Vector3(basePos.x, basePos.y + bob, 0f);
            }
        }

        private void Refresh()
        {
            int count = inventory != null ? inventory.Count : 0;
            for (int i = 0; i < orbs.Length; i++)
                if (orbs[i] != null) orbs[i].gameObject.SetActive(i < count);
        }
    }
}
