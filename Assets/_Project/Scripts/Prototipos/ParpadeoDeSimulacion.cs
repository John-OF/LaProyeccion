using System.Collections;
using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;
using LaProyeccion.World;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/ — fuera de ALCANCE): **la simulación se deshilacha**
    /// (idea #16, nunca prototipada). Destellos **breves e involuntarios** del Real en
    /// **puntos fijos** del escenario, mientras estás en la Simulación.
    ///
    /// No es una mecánica: es **vocabulario**. El GDD §7 ya lo lista entre las pistas de
    /// entorno —"parpadeos de la simulación sobre geometría oculta"— junto a las sombras
    /// que no corresponden a nada y las marcas de desgaste. Y lo lista con una condición
    /// dura: ese vocabulario **se fija una sola vez y no muta en todo el juego**. Lo que se
    /// valide aquí es lo que habrá que respetar después.
    ///
    /// Dos usos con el mismo sistema, y la diferencia es solo la **densidad**
    /// (<see cref="intervaloMin"/>/<see cref="intervaloMax"/>):
    /// - Zonas tempranas, parpadeo escaso → **pista**: "ahí hay algo que aquí no ves".
    /// - Zonas tardías, parpadeo denso → **la simulación cayéndose a pedazos**, sin una
    ///   línea de diálogo que lo cuente.
    ///
    /// Reusa el canal completo del fantasma (registro <see cref="GhostReveal"/> +
    /// SetGhostReveal de los tres componentes de presencia), igual que el radar y el
    /// vistazo: **solo renderers, jamás colliders**. Lo que parpadea es lo que existe en
    /// el Real y no en la Simulación — no hace falta marcar nada a mano.
    ///
    /// Se distingue del vistazo (<see cref="WorldPeekController"/>) en lo que importa:
    /// el vistazo es **voluntario, suave y centrado en ti**; esto es **involuntario, roto
    /// y centrado en un punto del mapa**. Por eso no hace un fundido sino varios
    /// pestañeos secos (<see cref="pestaneos"/>): un fundido bonito se leería como una
    /// herramienta del jugador, y esto tiene que leerse como un fallo del mundo.
    ///
    /// Los puntos son los **hijos** de este GameObject: se colocan a mano en el editor,
    /// sobre la geometría oculta que se quiera insinuar.
    ///
    /// LIMITACIÓN CONOCIDA, heredada y ampliada: el registro `GhostReveal` es **único y
    /// global**, así que solo puede haber UN destello a la vez (y no convive con un pulso
    /// de radar ni con un vistazo). El vistazo ya dejó anotado que, si esto entra al juego,
    /// hay que unificar los tres bajo un solo dueño del registro; este componente es el
    /// tercer inquilino y hace esa deuda más urgente, no menos.
    /// </summary>
    public class ParpadeoDeSimulacion : MonoBehaviour
    {
        [Header("Materiales (SpritePeekReveal, propios: no compartir con el vistazo)")]
        [SerializeField] private Material parpadeoSimMaterial;
        [SerializeField] private Material parpadeoRealMaterial;

        [Header("Densidad — ES EL DIAL DE ZONA")]
        [Tooltip("Segundos mínimos entre destellos. Bajarlo es 'la simulación se cae a pedazos'.")]
        [SerializeField, Min(0.1f)] private float intervaloMin = 3f;
        [Tooltip("Segundos máximos entre destellos.")]
        [SerializeField, Min(0.1f)] private float intervaloMax = 7f;

        [Header("El destello")]
        [Tooltip("Cuánto dura entero. Breve a propósito: si da tiempo a estudiarlo, es una herramienta.")]
        [SerializeField, Range(0.05f, 1f)] private float duracion = 0.25f;
        [Tooltip("Pestañeos secos dentro de la duración. 1 sería un fundido; 3-4 se lee como fallo.")]
        [SerializeField, Range(1, 8)] private int pestaneos = 3;
        [Tooltip("Radio revelado alrededor del punto. Corto: es un jirón, no un vistazo.")]
        [SerializeField, Min(0.5f)] private float radio = 3.5f;

        [Header("Solo lo que se puede ver")]
        [Tooltip("Un destello que ocurre fuera de pantalla se desperdicia (y gasta el turno, " +
                 "porque solo puede haber uno a la vez). Solo se eligen puntos a esta distancia.")]
        [SerializeField, Min(1f)] private float distanciaMaxima = 14f;

        private static readonly int RevealID = Shader.PropertyToID("_Reveal");
        private static readonly int CentroID = Shader.PropertyToID("_Centro");
        private static readonly int RadioID = Shader.PropertyToID("_Radio");

        private Transform jugador;
        private RadarPulseController radar;
        private bool enSimulacion;
        private bool destellando;
        private float proximoDestello;

        // El registro GhostReveal es único: una escena con varias densidades lleva varios
        // de estos componentes (uno por zona), y dos destellos a la vez se pisarían el
        // _Centro. Turno global, no por componente.
        private static bool alguienDestellando;

        // Materiales del dueño anterior del registro, mientras lo tenemos tomado.
        private Material previoSim, previoReal;

        private void Awake()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                jugador = pc.transform;
                radar = pc.GetComponent<RadarPulseController>();
            }
            ResetMaterialesDeterministas();   // gotcha del material sucio entre sesiones de Play
        }

        private void OnEnable()
        {
            WorldManager.OnWorldChanged += AlCambiarMundo;
            if (WorldManager.Instance != null) AlCambiarMundo(WorldManager.Instance.CurrentWorld);
            ProgramarSiguiente();
        }

        private void OnDisable()
        {
            WorldManager.OnWorldChanged -= AlCambiarMundo;
            if (destellando) TerminarDestello();   // descarga de escena a media parpadeo
        }

        private void OnApplicationQuit() => ResetMaterialesDeterministas();

        /// <summary>
        /// Deja los .mat en los valores del asset commiteado. Sin esto, el último destello
        /// de la sesión queda escrito en el material y git lo marca modificado tras cada
        /// Play (misma receta que el vistazo y el glitch del cambio de mundo).
        /// </summary>
        private void ResetMaterialesDeterministas()
        {
            foreach (var m in new[] { parpadeoSimMaterial, parpadeoRealMaterial })
            {
                if (m == null) continue;
                m.SetFloat(RevealID, 1f);
                m.SetVector(CentroID, Vector4.zero);
                m.SetFloat(RadioID, 6f);
            }
        }

        private void AlCambiarMundo(WorldState mundo)
        {
            // Solo se deshilacha la Simulación: en el Real no hay nada que se caiga a
            // pedazos, el Real ES lo que hay debajo.
            enSimulacion = mundo == WorldState.Simulation;
            if (!enSimulacion && destellando) TerminarDestello();
        }

        private void ProgramarSiguiente()
        {
            proximoDestello = Time.time + Random.Range(intervaloMin, intervaloMax);
        }

        private void Update()
        {
            if (destellando || !enSimulacion || Time.time < proximoDestello) return;
            if (Time.timeScale == 0f) return;
            if (!GhostReveal.Ready && (parpadeoSimMaterial == null || parpadeoRealMaterial == null)) return;

            // El registro es único: si el radar o el vistazo lo están usando, este turno
            // se pierde y se reintenta luego (no se encola: un destello atrasado ya no
            // significa nada).
            if (alguienDestellando) { ProgramarSiguiente(); return; }
            if (radar != null && radar.Revelando) { ProgramarSiguiente(); return; }

            var punto = ElegirPunto();
            if (punto == null) { ProgramarSiguiente(); return; }

            StartCoroutine(Destellar(punto.position));
        }

        /// <summary>
        /// Dispara UN destello YA, saltándose el reloj (añadido 2026-08-09 para el momento
        /// guionizado de la Zona 1: la primera grieta, al recoger la pieza). Respeta las
        /// mismas guardas que el goteo ambiental —registro `GhostReveal` libre, en
        /// Simulación, materiales puestos— y devuelve **false** si no pudo, para que quien
        /// lo pide pueda reintentarlo en vez de creerse que sonó.
        ///
        /// Retrocompatible: nada lo llama solo, así que el comportamiento ambiental de
        /// `P_AparatoPorPiezas` no cambia.
        /// </summary>
        public bool DestellarAhora(Transform punto = null)
        {
            if (destellando || !enSimulacion || alguienDestellando) return false;
            if (radar != null && radar.Revelando) return false;
            if (!GhostReveal.Ready && (parpadeoSimMaterial == null || parpadeoRealMaterial == null)) return false;

            Transform p = punto != null ? punto : ElegirPunto();
            if (p == null) return false;

            ProgramarSiguiente();   // el goteo ambiental se reengancha después de este
            StartCoroutine(Destellar(p.position));
            return true;
        }

        /// <summary>
        /// Un punto al azar de entre los que el jugador podría llegar a ver. Al azar y no
        /// por turnos: un patrón regular se aprende, y esto tiene que parecer un fallo.
        /// </summary>
        private Transform ElegirPunto()
        {
            if (jugador == null || transform.childCount == 0) return null;

            int candidatos = 0;
            Transform elegido = null;
            foreach (Transform p in transform)
            {
                if (Vector2.Distance(p.position, jugador.position) > distanciaMaxima) continue;
                candidatos++;
                // Muestreo de reserva: un solo recorrido, sin lista intermedia.
                if (Random.Range(0, candidatos) == 0) elegido = p;
            }
            return elegido;
        }

        private IEnumerator Destellar(Vector3 centro)
        {
            destellando = true;
            alguienDestellando = true;

            previoSim = GhostReveal.SimMaterial;
            previoReal = GhostReveal.RealMaterial;
            GhostReveal.SimMaterial = parpadeoSimMaterial;
            GhostReveal.RealMaterial = parpadeoRealMaterial;

            foreach (var m in new[] { parpadeoSimMaterial, parpadeoRealMaterial })
            {
                if (m == null) continue;
                m.SetVector(CentroID, centro);
                m.SetFloat(RadioID, radio);
                m.SetFloat(RevealID, 0f);
            }
            AplicarFantasma(true);

            // Pestañeos secos, no fundido: encendido/apagado dentro de la duración total.
            float porPestaneo = duracion / pestaneos;
            for (int i = 0; i < pestaneos; i++)
            {
                SetReveal(Random.Range(0.6f, 1f));   // ni siquiera llega igual de fuerte cada vez
                yield return new WaitForSeconds(porPestaneo * 0.6f);
                SetReveal(0f);
                yield return new WaitForSeconds(porPestaneo * 0.4f);
            }

            TerminarDestello();
        }

        private void TerminarDestello()
        {
            StopAllCoroutines();
            AplicarFantasma(false);
            SetReveal(1f);   // contrato del canal: _Reveal limpio al soltarlo

            if (previoSim != null) GhostReveal.SimMaterial = previoSim;
            if (previoReal != null) GhostReveal.RealMaterial = previoReal;
            previoSim = previoReal = null;

            destellando = false;
            alguienDestellando = false;
            ProgramarSiguiente();
        }

        private void AplicarFantasma(bool on)
        {
            foreach (var t in FindObjectsByType<TilemapDualLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                t.SetGhostReveal(on);
            foreach (var w in FindObjectsByType<WorldExclusivePresence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                w.SetGhostReveal(on);
            foreach (var p in FindObjectsByType<PlatformDual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                p.SetGhostReveal(on);
        }

        private void SetReveal(float v)
        {
            if (parpadeoSimMaterial != null) parpadeoSimMaterial.SetFloat(RevealID, v);
            if (parpadeoRealMaterial != null) parpadeoRealMaterial.SetFloat(RevealID, v);
        }

        private void OnDrawGizmos()
        {
            // Dónde puede aparecer un jirón, para colocarlos sobre la geometría oculta.
            Gizmos.color = new Color(0.95f, 0.40f, 0.22f, 0.55f);
            foreach (Transform p in transform)
            {
                Gizmos.DrawWireSphere(p.position, radio);
                Gizmos.DrawLine(p.position + Vector3.left * 0.4f, p.position + Vector3.right * 0.4f);
                Gizmos.DrawLine(p.position + Vector3.down * 0.4f, p.position + Vector3.up * 0.4f);
            }
        }
    }
}
