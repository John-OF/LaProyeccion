using UnityEngine;
using LaProyeccion.World;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio `Assets/Scenes/Pruebas/`). **IDEA 2** de `_docs/ideas.md`:
    /// una columna de lámparas junto a cada rellano que dice **en qué planta está el coche**.
    ///
    /// Qué se está probando: si arregla un agujero de legibilidad real. Hoy, desde el
    /// rellano no sabes dónde está el ascensor hasta que caminas hasta el hueco y te
    /// asomas — a un vacío de tres plantas. En un nivel cuya geometría entera es vertical,
    /// eso es pedirle al jugador que adivine (Pilar 3).
    ///
    /// Una lámpara por parada, de abajo a arriba. Quieto: se enciende la del coche.
    /// En marcha: **pulsa la de DESTINO**, no la de origen — lo que el jugador necesita
    /// saber es adónde va, no de dónde salió.
    ///
    /// Es un objeto del mundo, no un HUD: vive pegado a la pared, dentro de la escena.
    /// Compatible con la decisión "sin HUD" del 05-ago (`PROMTS.md` §14).
    /// </summary>
    public class IndicadorDeAscensor : MonoBehaviour
    {
        [SerializeField] private Ascensor ascensor;
        [Tooltip("Una por parada, ORDENADAS DE ABAJO A ARRIBA. El índice debe coincidir " +
                 "con el de las paradas del ascensor o el cartel mentirá.")]
        [SerializeField] private SpriteRenderer[] lamparas;

        [Header("Lectura")]
        [SerializeField] private Color apagada = new Color(0.22f, 0.25f, 0.31f);
        [SerializeField] private Color encendida = new Color(0.25f, 0.85f, 1f);
        [Tooltip("Parpadeos por segundo mientras el coche viaja.")]
        [SerializeField, Min(0.1f)] private float hzViaje = 2.5f;

        private void Update()
        {
            if (ascensor == null || lamparas == null) return;

            bool viajando = ascensor.EnMovimiento;
            int marcada = viajando ? ascensor.ParadaObjetivo : ascensor.ParadaActual;

            for (int i = 0; i < lamparas.Length; i++)
            {
                if (lamparas[i] == null) continue;

                if (i != marcada) { lamparas[i].color = apagada; continue; }

                lamparas[i].color = viajando
                    ? Color.Lerp(apagada, encendida, Mathf.PingPong(Time.time * hzViaje * 2f, 1f))
                    : encendida;
            }
        }
    }
}
