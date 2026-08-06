using System;
using System.Collections.Generic;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE; promoverlo exige
    /// enmienda consciente de `ALCANCE.md`, ya anotada en `pendientes.md` §❌).
    ///
    /// EL APARATO DEL CAMBIO DE MUNDO — identidad de la ZONA 1 (decisión del autor 2026-08-05).
    ///
    /// Hoy el poder del cambio lo regala "una puerta". Aquí se **monta por piezas**, y el
    /// aparato a medio construir **funciona mal**: cada pieza da un pedazo del verbo. Eso es
    /// lo que convierte la zona en un arco (enseñar → componer → Filtro) en vez de un recado
    /// con premio en la puerta, que es el riesgo real que el autor detectó.
    ///
    /// ⚠️ LO QUE ESTE LABORATORIO VIENE A RESPONDER — y no es "¿funciona?":
    ///    ¿el escalado se SIENTE como progreso, o las etapas 1 y 2 se sienten como que
    ///    el juego te está negando el juguete?
    ///
    /// LAS CUATRO ETAPAS. Cada una **sustituye** a la anterior, nunca se acumulan:
    ///   E1 · DESHILACHA  — el mundo destella solo, en puntos fijos. INVOLUNTARIO, no es un
    ///                      verbo: es información. (`ParpadeoDeSimulacion`, ya validado.)
    ///   E2 · VISTAZO     — mantienes la tecla y ves un fantasma local. VOLUNTARIO, pero no
    ///                      puedes actuar en él. (`WorldPeekController`, ya construido.)
    ///   E3 · NODOS       — ya cambias de verdad, pero solo dentro de un nodo marcado.
    ///                      (`NodoDeCambio`, escrito con este prototipo.)
    ///   E4 · COMPLETO    — el verbo entero, en cualquier sitio. Aquí Keplin se entera
    ///                      (`OnSwitchUnlocked`), que es el clímax de la zona.
    ///
    /// ⚠️ POR QUÉ SUSTITUYEN Y NO SE SUMAN, que es la decisión de diseño de este script:
    /// el registro `GhostReveal` es ÚNICO Y GLOBAL — radar, vistazo y deshilacha se pisan
    /// entre sí (deuda anotada por los tres). Al hacer que cada etapa apague la anterior, el
    /// conflicto desaparece por construcción en vez de arreglarse. Y encima se lee mejor:
    /// el deshilache PARA cuando llega el vistazo porque el aparato ya no gotea solo — ahora
    /// sabes mirar. Si al jugarlo el deshilache se echa de menos en E2+, entonces sí hay que
    /// pagar la deuda de unificar el registro bajo un solo dueño.
    ///
    /// LAS RANURAS SON `ZocaloDesactivador` SIN AMENAZAS y con `permitirRetirar = false`, a
    /// propósito: así el bucle de coger, cargar (con su coste — no puedes cambiar de mundo
    /// llevando una pieza) y colocar llega ya validado, con sus prompts y su cableado. No se
    /// ensambla un banco nuevo: se reutiliza el interactuable que funciona (lección de
    /// `P_CuevaOscuridad`).
    ///
    /// ⚠️ LAS PIEZAS NO SE SACAN (decisión del autor 2026-08-05, `ALCANCE.md` §4 v1.5). Es lo
    /// que separa esto de la pieza-desactivador, que existe justo para lo contrario: aquella se
    /// transporta y se recupera porque su coste ES tenerla aquí y no allá; ésta se **instala**.
    /// Cierra por diseño la pregunta de qué significa bajar de etapa, en vez de dejar que el
    /// jugador la descubra rompiendo el aparato.
    ///
    /// El código de bajada de etapa se conserva igualmente: `Reevaluar` no asume que el número
    /// solo suba. Si algún día una ranura vuelve a ser retirable, sigue funcionando.
    /// </summary>
    public class AparatoDeCambio : MonoBehaviour
    {
        public enum Etapa { Muerto = 0, Deshilacha = 1, Vistazo = 2, Nodos = 3, Completo = 4 }

        [Header("Ranuras del banco (ZocaloDesactivador con la lista de amenazas VACÍA)")]
        [Tooltip("El orden no importa: lo que manda es CUÁNTAS están ocupadas.")]
        [SerializeField] private List<ZocaloDesactivador> ranuras = new List<ZocaloDesactivador>();

        [Header("Lo que enciende cada etapa")]
        [Tooltip("E1. Componente en la escena cuyos hijos son los puntos que destellan.")]
        [SerializeField] private ParpadeoDeSimulacion deshilacha;
        [Tooltip("E2. Componente en el JUGADOR (pide PlayerController).")]
        [SerializeField] private WorldPeekController vistazo;
        [Tooltip("E3. Los nodos donde el aparato a medias sí consigue cambiar.")]
        [SerializeField] private List<NodoDeCambio> nodos = new List<NodoDeCambio>();

        [Header("Lectura del banco (Pilar 3: cuántas llevas se ve sin contar a mano)")]
        [SerializeField] private SpriteRenderer indicador;
        [SerializeField] private Color colorMuerto = new Color(0.30f, 0.30f, 0.34f);
        [SerializeField] private Color colorCompleto = new Color(0.25f, 0.85f, 1f);

        /// <summary>Para que la escena reaccione sin acoplarse (letreros, pensamiento, SFX).</summary>
        public static event Action<Etapa> OnEtapaCambiada;

        public Etapa EtapaActual { get; private set; } = Etapa.Muerto;

        private int ocupadasPrevias = -1;
        private bool desbloqueoYaDisparado;

        private void Start()
        {
            // Start y no Awake: los zócalos se pintan en su propio Awake, y WorldManager
            // dispara su OnWorldChanged inicial en Start. Aquí ya está todo en pie.
            Reevaluar(forzar: true);
        }

        /// <summary>
        /// `ZocaloDesactivador` no expone evento de colocar/retirar y es código VALIDADO:
        /// se sondea en vez de tocarlo. Son cuatro referencias y solo se actúa cuando el
        /// número cambia, así que el coste es irrelevante y no se ensucia lo que ya funciona.
        /// </summary>
        private void Update() => Reevaluar(forzar: false);

        private void Reevaluar(bool forzar)
        {
            int ocupadas = 0;
            foreach (var r in ranuras)
                if (r != null && r.Ocupado) ocupadas++;

            if (!forzar && ocupadas == ocupadasPrevias) return;
            ocupadasPrevias = ocupadas;

            EtapaActual = (Etapa)Mathf.Clamp(ocupadas, 0, 4);
            Aplicar();
            OnEtapaCambiada?.Invoke(EtapaActual);
        }

        private void Aplicar()
        {
            // Cada etapa SUSTITUYE a la anterior (ver cabecera): nunca dos canales de
            // revelado vivos a la vez, que es lo que rompería el registro GhostReveal.
            if (deshilacha != null) deshilacha.enabled = EtapaActual == Etapa.Deshilacha;
            if (vistazo != null) vistazo.enabled = EtapaActual == Etapa.Vistazo;

            bool nodosArmados = EtapaActual == Etapa.Nodos;
            foreach (var n in nodos)
                if (n != null) n.Armar(this, nodosArmados);

            ReaplicarPuerta();
            Pintar();
        }

        /// <summary>
        /// EL ÚNICO DUEÑO DE LA PUERTA GLOBAL. Los nodos no guardan una foto del estado
        /// anterior (como sí hacen `ZonaDeCambio` y `PortadorDePieza`): al salir preguntan
        /// aquí. Si guardaran la foto, instalar la 4ª pieza estando DENTRO de un nodo dejaría
        /// al salir un "estaba bloqueado" caducado — y el aparato completo dejaría de
        /// funcionar sin que nada avisara.
        /// </summary>
        public void ReaplicarPuerta()
        {
            var wm = WorldManager.Instance;
            if (wm == null) return;

            if (EtapaActual == Etapa.Completo)
            {
                // El desbloqueo narrativo (OnSwitchUnlocked → Keplin) se dispara UNA vez.
                // Si se retira una pieza y se vuelve a completar, se restaura en silencio:
                // Keplin no se entera dos veces de lo mismo.
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

        private void Pintar()
        {
            if (indicador == null) return;
            indicador.color = Color.Lerp(colorMuerto, colorCompleto, (int)EtapaActual / 4f);
        }

        private void OnDisable()
        {
            // Higiene: descargar la escena no deja el vistazo colgado ni nodos armados.
            if (vistazo != null) vistazo.enabled = false;
            foreach (var n in nodos)
                if (n != null) n.Armar(this, false);
        }
    }
}
