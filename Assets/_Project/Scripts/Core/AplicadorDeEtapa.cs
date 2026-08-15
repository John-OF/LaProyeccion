using System.Collections.Generic;
using UnityEngine;
using LaProyeccion.Player;
using LaProyeccion.Prototipos;
using LaProyeccion.World;

namespace LaProyeccion.Core
{
    /// <summary>
    /// QUIEN ENCIENDE LO QUE CADA ETAPA DA (`ALCANCE.md` §4 v1.5/v1.6). Uno por escena jugable.
    ///
    /// Sustituye a `AparatoDeCambio`, que hacía dos trabajos: contar piezas y aplicarlas. Contar ya
    /// lo hace <see cref="EstadoDelAparato"/> desde que la pulsera es estado de partida, así que
    /// aquí solo queda **aplicar** — y esa mitad sigue haciendo falta, que es justo por lo que
    /// aquel script no se borró hasta tener este.
    ///
    /// LAS CUATRO ETAPAS, y cada una **sustituye** a la anterior — nunca dos a la vez:
    ///   E1 · DESHILACHA — el mundo destella solo. Información, no verbo.
    ///   E2 · VISTAZO    — mantienes la tecla y ves un fantasma local; **no puedes actuar en él**.
    ///   E3 · NODOS      — cambias de verdad, pero solo dentro de un nodo marcado.
    ///   E4 · COMPLETO   — el verbo entero. Aquí Keplin se entera (`OnSwitchUnlocked`).
    ///
    /// ⚠️ **Por qué sustituyen y no se suman:** el registro `GhostReveal` es ÚNICO Y GLOBAL —radar,
    /// vistazo y deshilacha se pisan entre sí—, así que al apagar la etapa anterior el conflicto
    /// desaparece por construcción en vez de arreglarse. Y encima se lee mejor: el deshilache PARA
    /// cuando llega el vistazo porque el aparato ya no gotea solo; ahora sabes mirar.
    ///
    /// Observer y no polling, como todo lo reactivo del proyecto: se suscribe a
    /// `EstadoDelAparato.OnCambiado`. Los campos que una escena no use se dejan vacíos.
    /// </summary>
    public class AplicadorDeEtapa : MonoBehaviour
    {
        [Header("Lo que enciende cada etapa (vacío = esta escena no lo usa)")]
        [Tooltip("E1. Componente de la escena cuyos hijos son los puntos que destellan.")]
        [SerializeField] private ParpadeoDeSimulacion deshilacha;

        [Tooltip("E2. Componente en el JUGADOR (pide PlayerController). Vacío = se busca en él.")]
        [SerializeField] private WorldPeekController vistazo;

        [Tooltip("E3. Los nodos donde el aparato a medias sí consigue cambiar.")]
        [SerializeField] private List<NodoDeCambio> nodos = new List<NodoDeCambio>();

        private bool desbloqueoYaDisparado;

        private void OnEnable()
        {
            EstadoDelAparato.OnCambiado += Aplicar;
        }

        private void Start()
        {
            // Start y no OnEnable: `GameSession` restaura el estado de la partida en su Start, y
            // `WorldManager` dispara su OnWorldChanged inicial ahí también. Aquí ya está todo en pie.
            if (vistazo == null)
            {
                var pc = FindFirstObjectByType<LaProyeccion.Player.PlayerController>();
                if (pc != null) vistazo = pc.GetComponent<WorldPeekController>();
            }
            Aplicar();
        }

        private void OnDisable()
        {
            EstadoDelAparato.OnCambiado -= Aplicar;

            // Higiene: descargar la escena no deja el vistazo colgado ni nodos armados.
            if (vistazo != null) vistazo.enabled = false;
            foreach (var n in nodos)
                if (n != null) n.Armar(this, false);
        }

        private void Aplicar()
        {
            var etapa = EstadoDelAparato.EtapaActual;

            if (deshilacha != null) deshilacha.enabled = etapa == EstadoDelAparato.Etapa.Deshilacha;
            if (vistazo != null) vistazo.enabled = etapa == EstadoDelAparato.Etapa.Vistazo;

            bool nodosArmados = etapa == EstadoDelAparato.Etapa.Nodos;
            foreach (var n in nodos)
                if (n != null) n.Armar(this, nodosArmados);

            ReaplicarPuerta();
        }

        /// <summary>
        /// EL ÚNICO DUEÑO DE LA PUERTA GLOBAL del cambio. Los nodos no guardan una foto del estado
        /// anterior: al salir preguntan aquí. Si guardaran la foto, recoger la última pieza estando
        /// DENTRO de un nodo dejaría al salir un "estaba bloqueado" caducado, y el aparato completo
        /// dejaría de funcionar sin que nada avisara.
        /// </summary>
        public void ReaplicarPuerta()
        {
            var wm = WorldManager.Instance;
            if (wm == null) return;

            if (EstadoDelAparato.EtapaActual == EstadoDelAparato.Etapa.Completo)
            {
                // El desbloqueo narrativo (OnSwitchUnlocked → Keplin) se dispara UNA vez.
                if (!desbloqueoYaDisparado) { wm.EnableSwitch(); desbloqueoYaDisparado = true; }
                else wm.RestoreSwitchEnabled(true);
            }
            else
            {
                // E3 incluida: en nodos la puerta global está CERRADA y cada nodo la abre
                // mientras estás dentro.
                wm.DisableSwitch();
            }
        }
    }
}
