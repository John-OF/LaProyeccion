using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;
using LaProyeccion.Puzzles;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE;
    /// si se valida, exige enmienda consciente de ALCANCE antes de entrar al juego).
    ///
    /// Placa de presión (idea #10 de Claude, 2026-07-17, ideas.md): interruptor
    /// que se PISA en vez de pulsarse — activo mientras algo con presencia esté
    /// sobre su área, apagado en cuanto queda libre. No inventa cableado nuevo:
    /// CONDUCE el <see cref="DualSwitch"/> de su mismo GameObject vía SetState
    /// (que no pasa por el candado oneWay, a diferencia de Toggle), así Gate y
    /// todo el stack de puzzles existente escuchan sin un solo cambio.
    ///
    /// Quién pisa (cada familia da un puzzle distinto):
    /// - El JUGADOR (y por extensión su peso corporal como llave momentánea).
    /// - El ECO del cambio (<see cref="EcoDeCambio"/>): por POSICIÓN, no por
    ///   collider — el eco no tiene collider por diseño. Dejas el eco pisando
    ///   y corres. DECISIÓN A VALIDAR: el eco pisa sin importar el mundo (su
    ///   posición no sabe de mundos); si en el juego real molesta, se filtra.
    /// - Los CORRECTORES (y CorrectorVigilante): la patrulla determinista se
    ///   vuelve una llave rítmica que se lee (Pilar 3).
    /// - Las <see cref="PlataformaCongelable"/>: congela la plataforma ENCIMA
    ///   de la placa y queda pisada mientras dure la congelación.
    ///
    /// Detección por OverlapBox en Update, SIN OnTriggerEnter/Exit: inmune a
    /// los exits perdidos por teleport/respawn (lección de ZonaDeCambio). Los
    /// colliders deshabilitados no cuentan — lo que no existe en tu mundo no
    /// pisa (coherente con WorldExclusivePresence/PlatformDual).
    /// </summary>
    [RequireComponent(typeof(DualSwitch))]
    public class PlacaDePresion : MonoBehaviour
    {
        [Header("Área de detección (relativa a la placa)")]
        [Tooltip("Tamaño del volumen que cuenta como 'encima de la placa'.")]
        [SerializeField] private Vector2 tamano = new Vector2(2f, 1.2f);
        [Tooltip("Desplazamiento del centro del área respecto a la placa (por defecto, hacia arriba).")]
        [SerializeField] private Vector2 desplazamiento = new Vector2(0f, 0.6f);

        [Header("Quién puede pisarla")]
        [SerializeField] private bool pisaJugador = true;
        [SerializeField] private bool pisaEco = true;
        [SerializeField] private bool pisanCorrectores = true;
        [SerializeField] private bool pisanPlataformasCongelables = true;

        [Header("Audio")]
        [Tooltip("Sonido de interruptor al activarse (solo en el flanco de subida).")]
        [SerializeField] private bool sonarAlPisar = true;

        // Triggers incluidos: el collider del Corrector es un trigger kinemático.
        private static readonly Collider2D[] resultados = new Collider2D[16];

        private DualSwitch interruptor;
        private ContactFilter2D filtro;

        private void Awake()
        {
            interruptor = GetComponent<DualSwitch>();
            filtro = new ContactFilter2D { useTriggers = true };
        }

        private void Update()
        {
            bool pisada = HayEcoEncima() || HayPresenciaEncima();

            bool estaba = interruptor.IsOn;
            interruptor.SetState(pisada);
            if (sonarAlPisar && pisada && !estaba)
                AudioManager.Instance?.PlaySwitchActivate();
        }

        private Vector2 Centro => (Vector2)transform.position + desplazamiento;

        private bool HayEcoEncima()
        {
            if (!pisaEco) return false;
            Vector2? eco = EcoDeCambio.PosicionActiva;
            if (!eco.HasValue) return false;

            Vector2 delta = eco.Value - Centro;
            return Mathf.Abs(delta.x) <= tamano.x * 0.5f &&
                   Mathf.Abs(delta.y) <= tamano.y * 0.5f;
        }

        private bool HayPresenciaEncima()
        {
            int n = Physics2D.OverlapBox(Centro, tamano, 0f, filtro, resultados);
            for (int i = 0; i < n; i++)
            {
                var col = resultados[i];
                if (col == null || col.transform.IsChildOf(transform)) continue;

                if (pisaJugador && col.GetComponentInParent<PlayerController>() != null) return true;
                if (pisanCorrectores && (col.GetComponentInParent<Corrector>() != null ||
                                         col.GetComponentInParent<CorrectorVigilante>() != null)) return true;
                if (pisanPlataformasCongelables && col.GetComponentInParent<PlataformaCongelable>() != null) return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.9f, 0.5f);
            Gizmos.DrawWireCube(Centro, tamano);
        }
    }
}
