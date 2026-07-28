using UnityEngine;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE).
    ///
    /// Canal de RUIDO de la cueva. Existe para que el <see cref="GuardianCiego"/> no
    /// tenga que conocer a nadie: quien hace ruido lo anuncia, y quien oye se suscribe.
    /// Mismo patrón que los eventos estáticos del proyecto (`WorldManager.OnWorldChanged`,
    /// `GameSession.OnPlayerRespawned`).
    ///
    /// El TIPO importa porque el Guardián filtra por él: que el sonar delate o no es
    /// justo la pregunta que este prototipo existe para responder.
    /// </summary>
    public enum TipoRuido
    {
        /// <summary>Pasos del jugador al CORRER (andar es silencioso).</summary>
        Pasos,
        /// <summary>Piedra que golpea suelo o cae al agua. El mando a distancia del jugador.</summary>
        Piedra,
        /// <summary>Pulso del sonar. Tu única forma de ver puede ser tu forma de delatarte.</summary>
        Sonar
    }

    public static class RuidoCueva
    {
        /// <summary>Posición del ruido y su tipo. No lleva intensidad a propósito:
        /// el Guardián va SIEMPRE al último ruido que escucha, sin ponderar. Determinista
        /// y explicable en una frase (Pilar 3).</summary>
        public static event System.Action<Vector2, TipoRuido> OnRuido;

        public static void Emitir(Vector2 pos, TipoRuido tipo)
        {
            if (OnRuido != null) OnRuido(pos, tipo);
        }
    }
}
