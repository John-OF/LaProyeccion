using System.Collections;
using UnityEngine;
using LaProyeccion.Player;
using LaProyeccion.Puzzles;

namespace LaProyeccion.World
{
    /// <summary>
    /// El "fallo informativo" (F1.P6, Pilar 3): para elementos aéreos u ocultos.
    /// Si el jugador pasa a ≤ hintRadius SIN activar el elemento, al alejarse
    /// este parpadea 2-3 veces — o emite su silueta fantasma (SetGhostReveal
    /// puntual de F1.P5) si existe solo en el otro mundo. Así, quien salta "a
    /// ciegas" y falla SIEMPRE aprende algo.
    /// Con stayMarked, tras el primer fallo queda una marca tenue permanente.
    ///
    /// Se auto-engancha a DualSwitch.OnStateChanged e Interactable.OnInteract
    /// del mismo GameObject: al activarse el elemento, el hint se apaga solo.
    /// </summary>
    public class ProximityHint : MonoBehaviour
    {
        [Tooltip("Distancia a la que 'pasar cerca' cuenta como intento fallido al alejarse.")]
        [SerializeField] private float hintRadius = 2.5f;

        [Header("Parpadeo")]
        [SerializeField, Range(1, 6)] private int blinkCount = 3;
        [SerializeField] private float blinkOnTime = 0.18f;
        [SerializeField] private float blinkOffTime = 0.12f;

        [Header("Marca permanente (opcional)")]
        [Tooltip("Tras el primer fallo, deja una marca tenue visible en ambos mundos.")]
        [SerializeField] private bool stayMarked = false;
        [SerializeField] private Sprite markSprite;
        [SerializeField] private Color markColor = new Color(0.6f, 0.95f, 1f, 0.25f);
        [SerializeField] private float markScale = 0.5f;

        private Transform player;
        private Renderer[] ownRenderers;
        private WorldExclusivePresence exclusivePresence;
        private PlatformDual platformDual;

        private bool activated;
        private bool wasNear;
        private bool hinting;
        private GameObject markInstance;

        private void Awake()
        {
            ownRenderers = GetComponentsInChildren<Renderer>(true);
            exclusivePresence = GetComponent<WorldExclusivePresence>();
            platformDual = GetComponent<PlatformDual>();

            // Auto-enganche: el elemento activado deja de dar pistas.
            var ds = GetComponent<DualSwitch>();
            if (ds != null) ds.OnStateChanged.AddListener(OnSwitchStateChanged);
            var it = GetComponent<Interactable>();
            if (it != null) it.OnInteract.AddListener(NotifyActivated);
        }

        private void Start()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        private void OnSwitchStateChanged(bool _) => NotifyActivated();

        /// <summary>También cableable a mano desde UnityEvents en el Inspector.</summary>
        public void NotifyActivated()
        {
            activated = true;
            if (markInstance != null) Destroy(markInstance);
        }

        private void Update()
        {
            if (activated || player == null) return;

            bool near = Vector2.Distance(player.position, transform.position) <= hintRadius;
            if (near && !wasNear)
            {
                wasNear = true; // el jugador entró al radio
            }
            else if (!near && wasNear)
            {
                wasNear = false; // se alejó sin activar: fallo informativo
                if (!hinting) StartCoroutine(HintRoutine());
            }
        }

        private IEnumerator HintRoutine()
        {
            hinting = true;

            bool visible = false;
            foreach (var r in ownRenderers)
                if (r != null && r.enabled) { visible = true; break; }

            if (!visible && (exclusivePresence != null || platformDual != null) && GhostReveal.Ready)
            {
                // Existe solo en el otro mundo: pulsos de silueta fantasma.
                for (int i = 0; i < blinkCount; i++)
                {
                    SetGhost(true);
                    yield return new WaitForSeconds(blinkOnTime);
                    SetGhost(false);
                    yield return new WaitForSeconds(blinkOffTime);
                }
            }
            else if (visible)
            {
                // Visible en este mundo: parpadeo clásico de renderers.
                for (int i = 0; i < blinkCount; i++)
                {
                    SetRenderers(false);
                    yield return new WaitForSeconds(blinkOffTime);
                    SetRenderers(true);
                    yield return new WaitForSeconds(blinkOnTime);
                }
            }

            if (stayMarked && !activated && markInstance == null && markSprite != null)
                CreateMark();

            hinting = false;
        }

        private void SetGhost(bool on)
        {
            if (exclusivePresence != null) exclusivePresence.SetGhostReveal(on);
            if (platformDual != null) platformDual.SetGhostReveal(on);
        }

        private void SetRenderers(bool on)
        {
            foreach (var r in ownRenderers)
                if (r != null) r.enabled = on;
        }

        private void CreateMark()
        {
            // La marca es hija del elemento pero NO está en las listas cacheadas de
            // los componentes de presencia (se crean en Awake), así que se ve en
            // ambos mundos: es la memoria del fallo, no el objeto.
            markInstance = new GameObject("HintMark");
            markInstance.transform.SetParent(transform, false);
            markInstance.transform.localScale = Vector3.one * markScale;
            var sr = markInstance.AddComponent<SpriteRenderer>();
            sr.sprite = markSprite;
            sr.color = markColor;
            sr.sortingOrder = 5;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, hintRadius);
        }
    }
}
