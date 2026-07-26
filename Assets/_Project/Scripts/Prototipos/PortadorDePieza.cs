using UnityEngine;
using UnityEngine.InputSystem;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/): quien CARGA la
    /// <see cref="PiezaDesactivador"/>, y donde vive su coste.
    ///
    /// Un botón (T / X del mando) hace todo según el contexto: recoger la pieza
    /// suelta, retirarla de un zócalo, colocarla en uno libre, o soltarla en el suelo.
    /// Input propio FUERA de PlayerInputActions, como el verbo piedra y el
    /// neutralizador: es un verbo de laboratorio (CATALOGO §17).
    ///
    /// **EL COSTE (PLAN_REDISENO §2): mientras la llevas NO PUEDES CAMBIAR DE MUNDO.**
    /// Eso convierte transportarla en el puzzle — hay tramos que solo se cruzan
    /// cambiando, así que llevar la pieza allí es imposible y hay que decidir dónde
    /// gastarla. Es la diferencia de fondo con el consumible instantáneo: el
    /// neutralizador cuesta un recurso, la pieza cuesta MOVILIDAD.
    ///
    /// Bloquea igual que <see cref="ZonaDeCambio"/>: guarda IsSwitchEnabled, llama a
    /// DisableSwitch y restaura con RestoreSwitchEnabled — **nunca EnableSwitch**, que
    /// dispararía el evento de desbloqueo narrativo (OnSwitchUnlocked).
    /// </summary>
    public class PortadorDePieza : MonoBehaviour
    {
        [Tooltip("Radio para recoger, colocar y retirar.")]
        [SerializeField, Min(0.5f)] private float alcance = 2.2f;
        [Tooltip("Dónde se ve la pieza mientras la llevas.")]
        [SerializeField] private Vector3 offsetCargada = new Vector3(0f, 1.3f, 0f);

        [Header("Aviso al intentar cambiar cargando (la tecla nunca falla en silencio)")]
        [SerializeField] private Color tintDenegado = new Color(1f, 0.25f, 0.25f);
        [SerializeField, Min(0.05f)] private float duracionFlash = 0.3f;

        private InputAction accion;
        private PiezaDesactivador cargada;
        private bool cambioEstabaHabilitado;
        private float flashHasta;
        private SpriteRenderer srCargada;
        private Color colorCargadaOriginal;

        public bool LlevaPieza => cargada != null;

        private void Awake()
        {
            accion = new InputAction("PiezaDesactivador", InputActionType.Button);
            accion.AddBinding("<Keyboard>/t");
            accion.AddBinding("<Gamepad>/buttonWest");
            accion.performed += OnAccion;
        }

        private void OnEnable()
        {
            accion.Enable();
            WorldManager.OnSwitchDenied += OnSwitchDenied;
        }

        private void OnDisable()
        {
            accion.Disable();
            WorldManager.OnSwitchDenied -= OnSwitchDenied;
            // Higiene: no dejar el cambio bloqueado si se descarga la escena cargando.
            if (cargada != null && WorldManager.Instance != null)
                WorldManager.Instance.RestoreSwitchEnabled(cambioEstabaHabilitado);
        }

        private void OnDestroy() { if (accion != null) accion.performed -= OnAccion; }

        private void Update()
        {
            if (srCargada == null) return;
            srCargada.color = Time.time < flashHasta ? tintDenegado : colorCargadaOriginal;
        }

        private void OnSwitchDenied()
        {
            if (cargada == null) return;   // el bloqueo no es mío: no doy feedback ajeno
            flashHasta = Time.time + duracionFlash;
        }

        private void OnAccion(InputAction.CallbackContext _)
        {
            if (cargada != null) { SoltarOColocar(); return; }
            Recoger();
        }

        private void SoltarOColocar()
        {
            var zocalo = ZocaloMasCercano(libre: true);
            if (zocalo != null) { zocalo.Colocar(cargada); }
            else { cargada.MarcarSuelta(transform.position + Vector3.up * 0.3f); }

            DevolverCambio();
            srCargada = null;
            cargada = null;
        }

        private void Recoger()
        {
            // Primero un zócalo ocupado (retirar), luego una pieza suelta.
            var ocupado = ZocaloMasCercano(libre: false);
            PiezaDesactivador pieza = null;

            if (ocupado != null)
            {
                pieza = ocupado.GetComponentInChildren<PiezaDesactivador>();
                if (pieza != null) ocupado.Retirar();
            }
            if (pieza == null) pieza = PiezaSueltaMasCercana();
            if (pieza == null) return;

            pieza.MarcarCargada(transform, offsetCargada);
            cargada = pieza;
            srCargada = pieza.GetComponent<SpriteRenderer>();
            if (srCargada != null) colorCargadaOriginal = srCargada.color;

            QuitarCambio();
        }

        private void QuitarCambio()
        {
            if (WorldManager.Instance == null) return;
            cambioEstabaHabilitado = WorldManager.Instance.IsSwitchEnabled;
            WorldManager.Instance.DisableSwitch();
        }

        private void DevolverCambio()
        {
            if (WorldManager.Instance == null) return;
            WorldManager.Instance.RestoreSwitchEnabled(cambioEstabaHabilitado);
        }

        private ZocaloDesactivador ZocaloMasCercano(bool libre)
        {
            ZocaloDesactivador mejor = null;
            float mejorD = float.MaxValue;
            foreach (var z in UnityEngine.Object.FindObjectsByType<ZocaloDesactivador>(FindObjectsSortMode.None))
            {
                if (z.Ocupado == libre) continue;   // libre=true => quiero Ocupado==false
                float d = Vector2.Distance(transform.position, z.transform.position);
                if (d > alcance || d >= mejorD) continue;
                mejor = z; mejorD = d;
            }
            return mejor;
        }

        private PiezaDesactivador PiezaSueltaMasCercana()
        {
            PiezaDesactivador mejor = null;
            float mejorD = float.MaxValue;
            foreach (var p in UnityEngine.Object.FindObjectsByType<PiezaDesactivador>(FindObjectsSortMode.None))
            {
                if (p.EstadoActual != PiezaDesactivador.Estado.Suelta) continue;
                float d = Vector2.Distance(transform.position, p.transform.position);
                if (d > alcance || d >= mejorD) continue;
                mejor = p; mejorD = d;
            }
            return mejor;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 1f, 0.75f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, alcance);
        }
    }
}
