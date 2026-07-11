using System.Collections;
using UnityEngine;
using LaProyeccion.Core;

namespace LaProyeccion.World
{
    /// <summary>
    /// Marcador de entrada de zona (F1.P3). Si la escena NO arrancó desde
    /// "Continuar", dispara un autoguardado inicial: así, morir nada más entrar
    /// reaparece en esta zona y no devuelve a la anterior. Espera un frame para
    /// que GameSession.Start haya resuelto primero la restauración del guardado
    /// (orden de ejecución).
    /// El PF_Player de la escena se coloca directamente en la posición de
    /// entrada; este componente no mueve al jugador.
    /// </summary>
    public class ZoneEntry : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // GameSession.Start corre en esta misma fase; tras el primer frame
            // ya consumió ContinueRequested y sabemos si restauró.
            yield return null;

            if (GameSession.Instance != null && !GameSession.Instance.RestoredFromContinue)
                GameSession.AutoSave();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawLine(transform.position + Vector3.down * 0.4f,
                            transform.position + Vector3.up * 0.4f);
        }
    }
}
