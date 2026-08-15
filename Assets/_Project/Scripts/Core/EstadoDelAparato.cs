using System;
using UnityEngine;

namespace LaProyeccion.Core
{
    /// <summary>
    /// EL ESTADO DEL APARATO DEL CAMBIO — la pulsera y sus piezas (`ALCANCE.md` §4 v1.6).
    ///
    /// Dos datos, y **dos y no uno a propósito**: "sin pulsera" y "pulsera con 0 piezas" son
    /// estados distintos que existen los dos dentro del nivel 1 (entre la P3 y el vestíbulo).
    /// Meterlos en un solo entero obligaría a un desfase de uno —"¿la etapa 1 es 1 o 2?"— que es
    /// de los errores que aparecen tres meses después. Así, además, `Etapa` se sigue derivando
    /// del NÚMERO DE PIEZAS, que es como ya lo hacía `AparatoDeCambio`: solo cambia de dónde sale
    /// la cuenta.
    ///
    /// **Dónde vive y por qué aquí.** Es estado de PARTIDA, así que va donde ya viven el
    /// checkpoint, los interruptores y las Semillas: un contador estático en memoria que
    /// <see cref="GameSession"/> escribe en <see cref="SaveSystem"/> y restaura al continuar.
    /// Mismo patrón exacto que `SeedInventory` + `SeedPickup`. Las dos alternativas que parecían
    /// más simples fallan, y conviene dejar escrito por qué:
    ///   · **Un valor serializado en el prefab** sería estado del ASSET: en el editor se queda
    ///     escrito entre sesiones de Play, en una build es de solo lectura, y sería el mismo para
    ///     todas las partidas — "Nueva partida" no lo limpiaría.
    ///   · **Un `static` a secas** muere al cerrar el juego (así que "Continuar" te devolvería sin
    ///     pulsera) y sobrevive dentro de la sesión (así que "Nueva partida" arrancaría con lo de
    ///     la anterior). Por eso hay `ClearSessionState`, y por eso `GameSession` lo llama.
    ///
    /// **No tiene visual ni sonido.** Solo estado y un evento; quien quiera reaccionar se
    /// suscribe (`PulseraVisual` en el jugador). Es el mismo reparto que `WorldManager`, que
    /// tampoco pinta nada: muta estado y avisa.
    /// </summary>
    public static class EstadoDelAparato
    {
        /// <summary>Las cuatro etapas de `ALCANCE.md` §4. Cada una SUSTITUYE a la anterior.</summary>
        public enum Etapa { Muerto = 0, Deshilacha = 1, Vistazo = 2, Nodos = 3, Completo = 4 }

        /// <summary>Cuántas piezas tiene el aparato completo (decisión del autor 2026-08-14).</summary>
        public const int PiezasTotales = 4;

        /// <summary>¿Lleva puesta la pulsera? Sin ella no hay piezas que valgan.</summary>
        public static bool TienePulsera { get; private set; }

        /// <summary>Piezas recogidas, 0..<see cref="PiezasTotales"/>.</summary>
        public static int Piezas { get; private set; }

        /// <summary>La etapa sale del número de piezas, no de un contador aparte.</summary>
        public static Etapa EtapaActual => (Etapa)Mathf.Clamp(Piezas, 0, PiezasTotales);

        /// <summary>Cambió algo: pulsera recogida o pieza sumada. Lo escucha el visual.</summary>
        public static event Action OnCambiado;

        /// <summary>La recoge el jugador. Idempotente: cogerla dos veces no hace nada.</summary>
        public static void RecogerPulsera()
        {
            if (TienePulsera) return;
            TienePulsera = true;
            OnCambiado?.Invoke();
        }

        /// <summary>
        /// Suma una pieza. Devuelve <c>false</c> si no procede —sin pulsera no hay dónde
        /// meterla, y por encima del total no se acumula— para que quien la ofrezca pueda
        /// enterarse en vez de creerse que contó.
        /// </summary>
        public static bool SumarPieza()
        {
            if (!TienePulsera) return false;
            if (Piezas >= PiezasTotales) return false;
            Piezas++;
            OnCambiado?.Invoke();
            return true;
        }

        /// <summary>Lo llama `GameSession` al continuar una partida.</summary>
        public static void RestoreSession(bool pulsera, int piezas)
        {
            TienePulsera = pulsera;
            Piezas = Mathf.Clamp(piezas, 0, PiezasTotales);
            OnCambiado?.Invoke();
        }

        /// <summary>
        /// Partida nueva: olvidar. Va junto a `SeedPickup.ClearSessionState()` en `GameSession`,
        /// porque es el mismo problema — estado estático que sobrevive a los `LoadScene`.
        /// </summary>
        public static void ClearSessionState()
        {
            TienePulsera = false;
            Piezas = 0;
            OnCambiado?.Invoke();
        }
    }
}
