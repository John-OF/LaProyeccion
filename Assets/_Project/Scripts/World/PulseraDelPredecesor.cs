using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Puzzles;

namespace LaProyeccion.World
{
    /// <summary>
    /// LA PULSERA DEL PREDECESOR, tal como está en el mundo antes de que la cojas
    /// (`ALCANCE.md` §4 v1.6). Es solo el objeto: **el estado vive en
    /// <see cref="EstadoDelAparato"/>** y **lo que se ve vive en `PulseraVisual`**, en el jugador.
    /// Este script no sabe de luces ni de etapas; su único trabajo es entregarla y desaparecer.
    ///
    /// **Al cogerla el objeto se va del mundo, no se cuelga del personaje** (corrección del autor,
    /// 2026-08-14). Antes se le pegaba al jugador con el sprite apagado, y la pieza se le quedaba
    /// flotando encima: ruido visual que no aportaba y que además contradecía a la propia v1.6, que
    /// dice que la pieza se absorbe. A escala real la pulsera son 2-3 px: lo que el jugador ve es
    /// **la luz sobre su silueta**, y eso lo pinta el jugador, no este objeto.
    ///
    /// Dónde está en el nivel 1: en el recoveco de la planta vacía, apagada y sin dueño.
    /// Encontrarla ahí es la lectura que §4 v1.6 pide ("sobre sus restos"), sin una línea de texto.
    ///
    /// Subclase de <see cref="Interactable"/> y no UnityEvent en el Inspector, por lo mismo que
    /// <see cref="SalidaDelNivel"/>: el verbo tiene efecto fijo y no puede quedarse a medio cablear.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PulseraDelPredecesor : Interactable
    {
        [Header("Qué APARECE al cogerla (decisión del autor, 2026-08-14)")]
        [Tooltip("Objetos que no existen hasta que se lleva la pulsera — hoy, la pieza del " +
                 "vestíbulo. No es un candado que dice que no: es que todavía no hay nada que " +
                 "coger, que es la forma limpia de cerrar un verbo (Pilar 3: no se ofrece algo " +
                 "para luego negarlo).\n\n" +
                 "Array de GameObjects y no UnityEvent porque un evento a medio cablear no da " +
                 "error, y aquí el fallo silencioso sería dejar el candado abierto.")]
        [SerializeField] private GameObject[] revela;

        private void Start()
        {
            // Start y no Awake: `GameSession` restaura el estado de la partida en su propio Start,
            // así que preguntar antes daría siempre "no la llevas" al continuar una partida.
            Aplicar(EstadoDelAparato.TienePulsera);
        }

        public override void Interact()
        {
            if (EstadoDelAparato.TienePulsera) return;

            // base.Interact() dispara OnInteract y consume el oneShot: `ProximityHint` se
            // auto-engancha ahí para apagarse.
            base.Interact();

            EstadoDelAparato.RecogerPulsera();
            Aplicar(true);
        }

        /// <summary>
        /// Un solo sitio decide las dos cosas —si el objeto sigue en el suelo y si lo que
        /// desbloquea existe—, y se llama tanto al arrancar como al cogerla. Así, volver a la
        /// escena o continuar una partida deja el mundo como debe estar, sin casos especiales.
        /// </summary>
        private void Aplicar(bool llevada)
        {
            if (revela != null)
                foreach (var go in revela)
                    if (go != null) go.SetActive(llevada);

            if (llevada) gameObject.SetActive(false);
        }
    }
}
