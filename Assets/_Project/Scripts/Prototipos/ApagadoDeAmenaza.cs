using UnityEngine;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/): utilidad compartida para APAGAR una amenaza
    /// desde fuera, sin tocar su script. Hoy la usa <see cref="ZocaloDesactivador"/>.
    ///
    /// Nació para que dos candidatos —la pieza que se transporta y un consumible
    /// instantáneo que se gastaba a distancia— apagaran EXACTAMENTE igual, y así la
    /// comparación midiera el diseño y no una diferencia de implementación. El autor
    /// eligió la pieza el 2026-07-25 y el consumible se borró; la utilidad se queda
    /// porque es donde vive el contrato de legibilidad del apagado.
    ///
    /// ⚠️ EL VISUAL NO PUEDE MENTIR (Pilar 3, CATALOGO §16.3). Poner `enabled=false`
    /// detiene el Update y los OnTrigger de la amenaza, así que deja de matar — pero
    /// SEGUIRÍA DIBUJÁNDOSE IGUAL, y un haz rojo que no mata es exactamente la mentira
    /// que el proyecto prohíbe. Por eso apagar SIEMPRE tiñe de AZUL HIELO (#8CBFE6),
    /// el color que el GDD §7 reserva para "congelado, inofensivo".
    /// Una amenaza futura sin renderer teñible necesita otra señal ANTES de ser apagable.
    /// </summary>
    public static class ApagadoDeAmenaza
    {
        public static readonly Color TintApagado = new Color(0.55f, 0.75f, 0.90f);

        /// <summary>Estado guardado para poder devolver la amenaza como estaba.</summary>
        public class Estado
        {
            public MonoBehaviour comp;
            public SpriteRenderer[] renderers;
            public Color[] colores;
            public Collider2D[] triggers;      // solo los trigger: los sólidos NO se tocan
            public bool[] triggersActivos;
        }

        /// <summary>
        /// Apaga la amenaza y devuelve lo necesario para restaurarla.
        ///
        /// Apaga DOS cosas, no una:
        /// 1. El componente (`enabled=false`) — detiene su Update.
        /// 2. **Sus colliders TRIGGER.** Sin esto el apagado no era fiable: hay amenazas
        ///    cuyo volumen letal es un collider aparte que ellas mismas encienden y apagan
        ///    (`SueloGlicheadoParpadeante` crea uno en Awake). Si el componente se congela
        ///    justo en fase LETAL, ese trigger se queda **encendido**, y basta que algo
        ///    reactive el componente —o que otro script consulte el collider— para que
        ///    vuelva a matar. Apagar el trigger deja la amenaza inofensiva pase lo que pase.
        ///
        /// Los colliders SÓLIDOS se dejan intactos a propósito: la superficie de un suelo
        /// corrupto tiene que seguir siendo pisable (GDD §3.4 — "la superficie es siempre
        /// sólida, el peligro es estar encima cuando letaliza"), y un guardia congelado
        /// tiene que seguir siendo escalón.
        /// </summary>
        public static Estado Apagar(MonoBehaviour amenaza)
        {
            if (amenaza == null) return null;
            var e = new Estado
            {
                comp = amenaza,
                renderers = amenaza.GetComponentsInChildren<SpriteRenderer>(true)
            };

            e.colores = new Color[e.renderers.Length];
            for (int i = 0; i < e.renderers.Length; i++)
            {
                e.colores[i] = e.renderers[i].color;
                e.renderers[i].color = TintApagado;
            }

            var todos = amenaza.GetComponentsInChildren<Collider2D>(true);
            var lista = new System.Collections.Generic.List<Collider2D>();
            foreach (var c in todos) if (c.isTrigger) lista.Add(c);
            e.triggers = lista.ToArray();
            e.triggersActivos = new bool[e.triggers.Length];
            for (int i = 0; i < e.triggers.Length; i++)
            {
                e.triggersActivos[i] = e.triggers[i].enabled;
                e.triggers[i].enabled = false;
            }

            amenaza.enabled = false;
            return e;
        }

        /// <summary>
        /// Devuelve la amenaza a su estado original. **REANUDA donde se quedó; NO
        /// reinicia la fase.**
        ///
        /// Antes sí llamaba a `ReiniciarFase()`, con el argumento de que el patrón
        /// debía arrancar legible desde cero. Jugado, era **al revés** (autor,
        /// 2026-07-25): la amenaza se apaga **congelada a la vista**, así que al
        /// volver tiene que continuar desde ahí. Reiniciar la fase la **teletransporta**
        /// —`Corrector.ReiniciarFase()` la devuelve a `puntoA`, `FocoVigilancia` salta a
        /// haz recto— y un salto sin causa visible es justo lo que el Pilar 3 prohíbe.
        /// Además rompía la promesa del verbo: la pieza PAUSA la fuerza, no la resetea.
        ///
        /// No hace falta nada para que reanude: las dos amenazas acumulan su propio
        /// estado (`Corrector` en su `transform`, `FocoVigilancia` en su `fase`), así
        /// que con el componente apagado simplemente **no avanzan**. Las que van con el
        /// reloj compartido (`SueloGlicheadoParpadeante`) reanudan con el reloj global,
        /// que es lo correcto: los grupos sincronizados deben seguir sincronizados.
        ///
        /// `ReiniciarFase()` sigue existiendo y NO se toca: es la convención de FASE
        /// CERO del grabador/replay de pasadas.
        /// </summary>
        public static void Restaurar(Estado e)
        {
            if (e == null || e.comp == null) return;
            for (int i = 0; i < e.renderers.Length; i++)
                if (e.renderers[i] != null) e.renderers[i].color = e.colores[i];

            if (e.triggers != null)
                for (int i = 0; i < e.triggers.Length; i++)
                    if (e.triggers[i] != null) e.triggers[i].enabled = e.triggersActivos[i];

            // Reanudar = solo volver a habilitar. Sin ReiniciarFase (ver resumen arriba).
            e.comp.enabled = true;
        }
    }
}
