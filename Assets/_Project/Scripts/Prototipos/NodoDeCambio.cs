using System.Collections;
using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE).
    ///
    /// NODO DE CAMBIO — la etapa 3 del aparato (<see cref="LaProyeccion.Core.AplicadorDeEtapa"/>): con tres piezas de
    /// cuatro, el aparato ya consigue cambiar de mundo, pero **solo aquí dentro**. Fuera,
    /// la tecla no responde.
    ///
    /// ⚠️ POR QUÉ NO ES UN DIAL DE `ZonaDeCambio`, que a primera vista hace lo mismo: son
    /// opuestos, y mezclarlos habría hecho ilegibles los dos. `ZonaDeCambio` es una
    /// EXCEPCIÓN QUE QUITA — el mundo se cambia en todas partes y aquí no. Esto es una
    /// EXCEPCIÓN QUE DA — no se cambia en ninguna parte y aquí sí. El jugador tiene que
    /// leerlas como cosas distintas nada más verlas (Pilar 3), así que llevan componente,
    /// color y forma distintos. Además `ZonaDeCambio` está validado y en uso en labs reales:
    /// añadirle un modo invertido obligaría a re-validarlos.
    ///
    /// NO GUARDA FOTO DEL ESTADO ANTERIOR, a diferencia de `ZonaDeCambio` y
    /// `PortadorDePieza`: al salir le pregunta al aparato (<see cref="LaProyeccion.Core.AplicadorDeEtapa.ReaplicarPuerta"/>).
    /// Con foto, colocar la 4ª pieza estando dentro de un nodo dejaría al salir un
    /// "estaba bloqueado" caducado y el aparato completo se apagaría solo, sin error.
    /// Un solo dueño de la puerta global.
    ///
    /// Usa `RestoreSwitchEnabled` y **nunca** `EnableSwitch`, que dispararía el desbloqueo
    /// narrativo (`OnSwitchUnlocked`) cada vez que entras en un nodo.
    ///
    /// LIMITACIÓN CONOCIDA (aceptable en laboratorio, heredada de `ZonaDeCambio`): no
    /// solapar un nodo con una `ZonaDeCambio` bloqueante ni recoger una pieza dentro de un
    /// nodo — dos dueños de la misma puerta se pisan. En el banco están separados a propósito.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class NodoDeCambio : MonoBehaviour
    {
        [Header("Lectura (el nodo se ve SIEMPRE: la regla se decide antes de entrar)")]
        [SerializeField] private SpriteRenderer overlay;
        [Tooltip("Armado = el aparato ya tiene 3 piezas y este nodo sirve.")]
        [SerializeField] private Color colorArmado = new Color(0.25f, 0.85f, 1f, 0.30f);
        [Tooltip("Dormido = aún no tienes suficientes piezas. Se ve, pero apagado: " +
                 "verlo antes de poder usarlo es la pista de que el aparato mejora.")]
        [SerializeField] private Color colorDormido = new Color(0.45f, 0.45f, 0.50f, 0.14f);
        [SerializeField, Min(0.05f)] private float duracionFlash = 0.3f;
        [SerializeField] private Color tintDenegado = new Color(1f, 0.25f, 0.25f, 0.45f);

        private BoxCollider2D zona;
        private Transform jugador;
        private LaProyeccion.Core.AplicadorDeEtapa aparato;
        private bool armado;
        private bool jugadorDentro;
        private Coroutine flash;

        private void Awake()
        {
            zona = GetComponent<BoxCollider2D>();
            zona.isTrigger = true;
            if (overlay == null) overlay = GetComponent<SpriteRenderer>();
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) jugador = pc.transform;
            Pintar();
        }

        private void OnEnable()
        {
            WorldManager.OnSwitchDenied += OnSwitchDenied;
            GameSession.OnPlayerRespawned += OnPlayerRespawned;
        }

        private void OnDisable()
        {
            WorldManager.OnSwitchDenied -= OnSwitchDenied;
            GameSession.OnPlayerRespawned -= OnPlayerRespawned;
            if (jugadorDentro) Salir();
        }

        /// <summary>Lo llama el aparato al cambiar de etapa. Fuera de la etapa 3 el nodo
        /// sigue viéndose (es una pista de lo que vendrá) pero no hace nada.</summary>
        public void Armar(LaProyeccion.Core.AplicadorDeEtapa duenio, bool activo)
        {
            aparato = duenio;
            if (armado == activo) { Pintar(); return; }
            armado = activo;

            // Dejar de estar armado con el jugador dentro no puede dejarle el cambio abierto.
            if (!armado && jugadorDentro) Salir();
            else if (armado && jugadorDentro) Entrar();

            Pintar();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (jugadorDentro || other.GetComponentInParent<PlayerController>() == null) return;
            jugadorDentro = true;
            if (armado) Entrar();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!jugadorDentro || other.GetComponentInParent<PlayerController>() == null) return;
            jugadorDentro = false;
            Salir();
        }

        private void Entrar()
        {
            WorldManager.Instance?.RestoreSwitchEnabled(true);
        }

        private void Salir()
        {
            jugadorDentro = false;
            // El aparato manda: nunca una foto caducada (ver cabecera).
            if (aparato != null) aparato.ReaplicarPuerta();
            else WorldManager.Instance?.DisableSwitch();
        }

        /// <summary>
        /// Mismo cinturón y tirantes que `ZonaDeCambio`: `MuerteCorreccion` congela la
        /// física y el teletransporte puede no disparar OnTriggerExit2D de forma fiable.
        /// </summary>
        private void OnPlayerRespawned()
        {
            if (!jugadorDentro || jugador == null) return;
            if (!zona.OverlapPoint(jugador.position)) Salir();
        }

        /// <summary>
        /// El nodo dormido es quien mejor puede explicar por qué la tecla no hizo nada:
        /// estás en el sitio correcto con el aparato incompleto. Solo habla si el jugador
        /// está dentro — si no, el bloqueo no es asunto suyo.
        /// </summary>
        private void OnSwitchDenied()
        {
            if (!jugadorDentro || armado || overlay == null) return;
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            Color baseColor = armado ? colorArmado : colorDormido;
            overlay.color = tintDenegado;
            for (float t = 0f; t < duracionFlash; t += Time.deltaTime)
            {
                overlay.color = Color.Lerp(tintDenegado, baseColor, t / duracionFlash);
                yield return null;
            }
            overlay.color = baseColor;
            flash = null;
        }

        private void Pintar()
        {
            if (overlay != null && flash == null) overlay.color = armado ? colorArmado : colorDormido;
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
}
