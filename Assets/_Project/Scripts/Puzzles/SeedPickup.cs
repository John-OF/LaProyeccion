using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using LaProyeccion.Core;

namespace LaProyeccion.Puzzles
{
    /// <summary>
    /// Semilla recogible (F1.P4). Trigger: al tocarla el jugador la recoge SI hay
    /// espacio en el inventario; si está lleno, no se recoge y permanece.
    /// Cada pickup tiene un <see cref="id"/> único por escena: las recogidas no
    /// reaparecen tras morir (estado de sesión) y se guardan/restauran vía
    /// GameSession/SaveSystem (save.seedsCollected).
    /// Compatible con WorldExclusivePresence (semilla que existe en un solo mundo).
    /// El pulso de escala/luz se anima por código (GDD §7: luminiscencia tenue,
    /// la única luz "viva" del entorno).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SeedPickup : MonoBehaviour
    {
        [Tooltip("Identificador ÚNICO dentro de la escena (p. ej. 'z2_semilla_beat').")]
        [SerializeField] private string id;

        [Header("Pulso")]
        [SerializeField] private float pulseSpeed = 2.2f;
        [SerializeField, Range(0f, 0.5f)] private float pulseScaleAmount = 0.1f;
        [SerializeField, Range(0f, 1f)] private float pulseLightAmount = 0.35f;
        [Tooltip("Luz de la semilla. Si se deja vacío se busca en hijos.")]
        [SerializeField] private Light2D glowLight;

        // Ids recogidos en la sesión (clave "escena:id"). Persiste entre muertes
        // y escenas; GameSession lo serializa al guardar y lo restaura al continuar.
        private static readonly HashSet<string> collected = new HashSet<string>();

        private Vector3 baseScale;
        private float baseIntensity;
        private float phase;
        private bool picked;

        private string Key => gameObject.scene.name + ":" + id;

        private void Awake()
        {
            baseScale = transform.localScale;
            if (glowLight == null) glowLight = GetComponentInChildren<Light2D>(true);
            if (glowLight != null) baseIntensity = glowLight.intensity;
            phase = Random.Range(0f, Mathf.PI * 2f); // que no pulsen todas a la vez
            if (string.IsNullOrEmpty(id))
                Debug.LogWarning($"SeedPickup '{name}' sin id: no podrá guardarse.", this);
        }

        private IEnumerator Start()
        {
            // Esperar un frame: si venimos de Continuar, GameSession.Start restaura
            // primero la lista de recogidas (orden de Start no garantizado).
            yield return null;
            if (!string.IsNullOrEmpty(id) && collected.Contains(Key))
                Destroy(gameObject);
        }

        private void Update()
        {
            float s = Mathf.Sin(Time.time * pulseSpeed + phase);
            transform.localScale = baseScale * (1f + s * pulseScaleAmount);
            if (glowLight != null)
                glowLight.intensity = baseIntensity * (1f + s * pulseLightAmount);
        }

        private void OnTriggerEnter2D(Collider2D other) => TryPickup(other);
        private void OnTriggerStay2D(Collider2D other) => TryPickup(other);

        private void TryPickup(Collider2D other)
        {
            if (picked) return;
            var inv = other.GetComponentInParent<SeedInventory>();
            if (inv == null) return;
            if (!inv.TryAdd()) return; // inventario lleno: la semilla queda

            picked = true;
            if (!string.IsNullOrEmpty(id)) collected.Add(Key);
            AudioManager.Instance?.PlaySeedPickup();
            Destroy(gameObject);
        }

        // ==================== Estado de sesión / guardado ====================

        /// <summary>Serializa las recogidas como "escena:id;escena:id2;..." (para SaveSystem).</summary>
        public static string SerializeCollected() => string.Join(";", collected);

        /// <summary>Restaura la lista al continuar una partida (lo llama GameSession).</summary>
        public static void LoadCollected(string data)
        {
            collected.Clear();
            if (string.IsNullOrEmpty(data)) return;
            foreach (var entry in data.Split(';'))
                if (!string.IsNullOrEmpty(entry)) collected.Add(entry);
        }

        /// <summary>Nueva partida: olvida todas las recogidas (lo llama GameSession).</summary>
        public static void ClearSessionState() => collected.Clear();
    }
}
