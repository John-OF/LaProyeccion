using System;
using System.Collections.Generic;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (herramienta de laboratorio, idea #8 de Claude, ideas.md).
    ///
    /// Una pasada grabada del jugador: los inputs semánticos (eje X + botones)
    /// muestreados por tick de FixedUpdate, MÁS la posición del jugador en cada
    /// tick (la "trayectoria dorada"). Grabar también la trayectoria es la
    /// decisión clave: la física de Unity no es determinista frame a frame
    /// (coyote/buffer usan Time.time en Update), así que la verificación no
    /// persigue determinismo binario — reproduce los INPUTS y comprueba que la
    /// trayectoria resultante no se desvíe de la grabada más allá de una
    /// tolerancia. El ghost, en cambio, reproduce las POSICIONES directamente
    /// (100% fiel siempre, sin física).
    ///
    /// La graba <see cref="GrabadorDePasada"/> (F9) y la consume
    /// <see cref="ReproductorDePasada"/> (F10 replay verificado, F11 ghost).
    /// </summary>
    public class PasadaGrabada : ScriptableObject
    {
        [Flags]
        public enum Botones : byte
        {
            Ninguno     = 0,
            Salto       = 1 << 0,
            CambioMundo = 1 << 1,
            Interactuar = 1 << 2,
            Sondear     = 1 << 3,
        }

        [Serializable]
        public struct Muestra
        {
            public float moveX;   // eje horizontal en este tick
            public byte botones;  // Botones pulsados DESDE el tick anterior
            public Vector2 pos;   // posición del jugador al inicio del tick
        }

        [Header("Metadata (la rellena el Grabador)")]
        public string escena;
        public string fecha;
        public Vector2 spawn;
        public WorldState mundoInicial;
        public float fixedDeltaTime = 0.02f;

        [Header("Datos")]
        public List<Muestra> muestras = new List<Muestra>();

        public float Duracion => muestras.Count * fixedDeltaTime;
    }
}
