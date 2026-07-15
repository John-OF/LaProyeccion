using System.Collections;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// El eco del cambio (idea #5 de Claude, 2026-07-13, ideas.md): al cambiar de
    /// mundo, el jugador deja una silueta glitcheada donde estaba durante unos
    /// segundos. Los Correctores con <c>atiendeEcos</c> acuden al eco, no al
    /// jugador — el cambio de mundo se vuelve verbo de sigilo activo (distracción).
    ///
    /// Versión DETERMINISTA acordada con el autor (2026-07-14): el eco es un imán
    /// posicional, no una IA. El guardia nunca persigue al jugador ni abandona su
    /// raíl A-B (ver <see cref="Corrector"/>); el eco solo lo desvía por su raíl.
    ///
    /// Implementación: componente en el jugador. Se suscribe a
    /// WorldManager.OnWorldChanged (ignorando el sync inicial de Start); en cada
    /// cambio real reposiciona UNA silueta (sprite del jugador congelado en ese
    /// instante) con una instancia runtime del material fantasma del radar
    /// (instancia: animar _Reveal aquí no puede pisar el .mat compartido que
    /// anima RadarPulseController). El color es el del mundo ABANDONADO — el eco
    /// es el residuo que dejaste en el mundo del que saliste. Solo renderer,
    /// jamás collider. Morir borra el eco (que no quede un imán en el sitio de
    /// la muerte). Cambiar de nuevo REUBICA el eco (uno solo a la vez: spamear
    /// el cambio no fabrica señuelos, solo mueve el único que hay).
    ///
    /// Registro para los guardias: <see cref="PosicionActiva"/> (estática,
    /// null cuando no hay eco). La consulta Corrector.AtenderEco().
    /// </summary>
    public class EcoDeCambio : MonoBehaviour
    {
        [Header("Materiales fuente (Assets/_Project/Shaders — se instancian en runtime)")]
        [Tooltip("Fuente para el eco dejado en la Simulación (M_GhostReveal_Sim, cyan).")]
        [SerializeField] private Material materialEcoSim;
        [Tooltip("Fuente para el eco dejado en el Real (M_GhostReveal_Real, óxido).")]
        [SerializeField] private Material materialEcoReal;

        [Header("Vida del eco")]
        [Tooltip("Segundos que la silueta atrae guardias mientras se disuelve (idea original: 2-3 s).")]
        [SerializeField, Range(0.5f, 6f)] private float duracion = 2.5f;
        [Tooltip("Materialización inicial (_Reveal 0→1) antes de empezar a disolverse.")]
        [SerializeField, Range(0.05f, 0.5f)] private float aparicion = 0.1f;

        private static readonly int RevealID = Shader.PropertyToID("_Reveal");

        /// <summary>Posición del eco vivo, o null si no hay. La leen los Correctores.</summary>
        public static Vector2? PosicionActiva { get; private set; }

        private SpriteRenderer playerSprite;
        private WorldState mundoAnterior;
        private bool sincronizado; // OnWorldChanged también dispara en Start(): ese no deja eco

        private GameObject eco;
        private SpriteRenderer ecoSprite;
        private Material matSim, matReal; // instancias runtime (no tocar los assets)
        private Coroutine vida;

        private void Awake()
        {
            playerSprite = GetComponentInChildren<SpriteRenderer>();
            if (materialEcoSim != null) matSim = new Material(materialEcoSim);
            if (materialEcoReal != null) matReal = new Material(materialEcoReal);
            PosicionActiva = null; // estática: limpiar residuo de otra escena/Play
        }

        private void OnEnable()
        {
            WorldManager.OnWorldChanged += OnWorldChanged;
            GameSession.OnPlayerRespawned += OnPlayerRespawned;
        }

        private void OnDisable()
        {
            WorldManager.OnWorldChanged -= OnWorldChanged;
            GameSession.OnPlayerRespawned -= OnPlayerRespawned;
            MatarEco();
        }

        private void OnDestroy()
        {
            if (matSim != null) Destroy(matSim);
            if (matReal != null) Destroy(matReal);
        }

        private void OnWorldChanged(WorldState nuevo)
        {
            if (!sincronizado)
            {
                sincronizado = true;
                mundoAnterior = nuevo;
                return;
            }

            WorldState dejado = mundoAnterior;
            mundoAnterior = nuevo;
            DejarEco(dejado);
        }

        private void DejarEco(WorldState mundoDejado)
        {
            if (playerSprite == null) return;
            Material mat = mundoDejado == WorldState.Simulation ? matSim : matReal;
            if (mat == null) return;

            if (eco == null)
            {
                eco = new GameObject("Eco_DeCambio");
                ecoSprite = eco.AddComponent<SpriteRenderer>();
            }

            // Congelar la pose del jugador en el instante del cambio.
            eco.transform.SetPositionAndRotation(
                playerSprite.transform.position, playerSprite.transform.rotation);
            eco.transform.localScale = playerSprite.transform.lossyScale;
            ecoSprite.sprite = playerSprite.sprite;
            ecoSprite.flipX = playerSprite.flipX;
            ecoSprite.sortingLayerID = playerSprite.sortingLayerID;
            ecoSprite.sortingOrder = playerSprite.sortingOrder - 1; // justo detrás del jugador
            ecoSprite.sharedMaterial = mat;
            ecoSprite.enabled = true;

            PosicionActiva = eco.transform.position;

            if (vida != null) StopCoroutine(vida);
            vida = StartCoroutine(VidaDelEco(mat));
        }

        private IEnumerator VidaDelEco(Material mat)
        {
            // Materialización breve y disolución durante TODA la vida: el shader
            // ya rompe la silueta contra ruido, así el eco muere en trazo roto.
            float t = 0f;
            while (t < aparicion)
            {
                t += Time.deltaTime; // escalado: la pausa congela al eco
                mat.SetFloat(RevealID, Mathf.Lerp(0f, 1f, t / aparicion));
                yield return null;
            }

            t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                mat.SetFloat(RevealID, Mathf.Lerp(1f, 0f, t / duracion));
                yield return null;
            }

            MatarEco();
        }

        private void OnPlayerRespawned() => MatarEco();

        private void MatarEco()
        {
            if (vida != null)
            {
                StopCoroutine(vida);
                vida = null;
            }
            if (ecoSprite != null) ecoSprite.enabled = false;
            PosicionActiva = null;
        }
    }
}
