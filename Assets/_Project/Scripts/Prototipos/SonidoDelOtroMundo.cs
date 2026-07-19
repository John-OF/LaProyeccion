using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// El otro mundo se OYE (idea #15): fuente posicional que suena SOLO cuando
    /// estás en el mundo OPUESTO a <see cref="mundoDelObjeto"/> — el zumbido de
    /// una plataforma que no ves, el guardia del otro lado que se oye pasar.
    /// Canal nuevo de pistas para la regla de oro (GDD §3.2: el radar nunca es
    /// la única llave) y venta literal del pitch: "nunca ves el otro lado —
    /// pero lo intuyes". Se cuelga del objeto (si el objeto patrulla, el sonido
    /// patrulla con él).
    ///
    /// Audio SIN AudioSource suelto (lección del tick del suelo parpadeante):
    /// la fuente es local porque es posicional, pero se rutea por el grupo
    /// <c>OtroMundo</c> del mixer vía <see cref="AudioManager.GrupoOtroMundo"/>
    /// (hijo de SFX: los sliders de Opciones la gobiernan).
    ///
    /// Calibración de distancias: el AudioListener vive en la Main Camera, a
    /// z≈−10 del plano de juego — la distancia mínima real de cualquier fuente
    /// es ~10, no 0. Por eso los defaults son distanciaMin≈10.5 ("estás encima")
    /// y distanciaMax≈20 (se apaga a ~±17 u en horizontal). El paneo estéreo
    /// sale gratis del offset en x.
    /// </summary>
    public class SonidoDelOtroMundo : MonoBehaviour
    {
        [Header("Presencia")]
        [Tooltip("Mundo al que pertenece el objeto que suena. El zumbido se oye desde el OTRO.")]
        [SerializeField] private WorldState mundoDelObjeto = WorldState.Real;

        [Header("Sonido")]
        [Tooltip("Loop del zumbido. Vacío = mudo (el componente no hace nada).")]
        [SerializeField] private AudioClip zumbido;
        [SerializeField, Range(0f, 1f)] private float volumen = 0.5f;
        [Tooltip("Distancia de volumen máximo. El listener está en la cámara (z≈−10): ~10.5 = 'estás justo encima'.")]
        [SerializeField, Min(0f)] private float distanciaMin = 10.5f;
        [Tooltip("Distancia de silencio (rolloff lineal). 20 ≈ deja de oírse a ±17 u en horizontal.")]
        [SerializeField, Min(1f)] private float distanciaMax = 20f;
        [Tooltip("Segundos del fundido al aparecer/desaparecer con el cambio de mundo.")]
        [SerializeField, Min(0.05f)] private float fundido = 0.5f;

        private AudioSource fuente;
        private bool audible;

        private void Awake()
        {
            fuente = gameObject.AddComponent<AudioSource>();
            fuente.clip = zumbido;
            fuente.loop = true;
            fuente.playOnAwake = false;
            fuente.volume = 0f;
            fuente.spatialBlend = 1f;
            fuente.rolloffMode = AudioRolloffMode.Linear;
            fuente.minDistance = distanciaMin;
            fuente.maxDistance = distanciaMax;
            fuente.dopplerLevel = 0f;
        }

        private void Start()
        {
            // El grupo se resuelve aquí y no en Awake: AudioManager.Instance se
            // asigna en SU Awake y el orden entre scripts no está garantizado.
            if (AudioManager.Instance != null)
                fuente.outputAudioMixerGroup = AudioManager.Instance.GrupoOtroMundo;
        }

        private void OnEnable() => WorldManager.OnWorldChanged += OnWorldChanged;
        private void OnDisable() => WorldManager.OnWorldChanged -= OnWorldChanged;

        private void OnWorldChanged(WorldState actual)
        {
            // También sincroniza el estado inicial (OnWorldChanged dispara en Start).
            audible = actual != mundoDelObjeto;
            if (audible && !fuente.isPlaying && zumbido != null) fuente.Play();
        }

        private void Update()
        {
            float objetivo = audible ? volumen : 0f;
            float paso = Mathf.Max(0.01f, volumen) / fundido * Time.deltaTime;
            fuente.volume = Mathf.MoveTowards(fuente.volume, objetivo, paso);
            if (!audible && fuente.volume <= 0f && fuente.isPlaying) fuente.Stop();
        }

        private void OnDrawGizmosSelected()
        {
            // El radio audible REAL en horizontal, descontando el offset z del listener.
            float zCam = 10f;
            float r = Mathf.Sqrt(Mathf.Max(0f, distanciaMax * distanciaMax - zCam * zCam));
            Gizmos.color = new Color(0.9f, 0.8f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, r);
        }
    }
}
