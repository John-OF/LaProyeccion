using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;
using LaProyeccion.Puzzles;
using LaProyeccion.Prototipos;
using LaProyeccion.World;

namespace LaProyeccion.Puzzles
{
    /// <summary>
    /// SISTEMA DEL JUEGO (`ALCANCE.md` §4 v1.3, "objetos físicos manipulables"). Corre en
    /// `Zona1` desde 2026-08-14, y además en varios laboratorios de `Pruebas/`.
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
    /// - El <see cref="CorrectorCongelable"/> (extensión retrocombatible 2026-07-25,
    ///   para el lab de composición): faltaba en la lista porque NO deriva de
    ///   Corrector, es un MonoBehaviour aparte. Cruce que habilita: esperar a que
    ///   el guardia pise la placa, cambiar de mundo y dejarlo CONGELADO encima —
    ///   el peligro se convierte en el lastre que te abre la puerta. Va bajo el
    ///   mismo flag `pisanCorrectores` (es la misma familia) para no añadir un
    ///   campo serializado nuevo a placas ya colocadas en escenas.
    /// - Las <see cref="PlataformaCongelable"/>: congela la plataforma ENCIMA
    ///   de la placa y queda pisada mientras dure la congelación.
    /// - Las <see cref="CajaEmpujable"/> (idea #14, extensión retrocompatible):
    ///   la caja como pisapapeles — aparcada encima mantiene la placa pisada,
    ///   también clavada en el mundo donde no se empuja (sigue siendo sólida).
    /// - Los <see cref="BloqueSuspendido"/> (extensión retrocompatible 2026-07-26):
    ///   el peso que cae desde arriba. Es la mitad "herramienta" de esa idea —
    ///   sueltas el bloque sobre una placa que no podías pisar tú, y se queda.
    ///   Campo nuevo con default true: las placas ya colocadas en escenas lo
    ///   heredan sin tocarlas.
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
        [SerializeField] private bool pisanCajas = true;
        [SerializeField] private bool pisanBloques = true;
        [Tooltip("Los NPCs residentes (extensión retrocompatible 2026-08-14). Con esto en true y " +
                 "`pisaJugador` en FALSE sale el puzzle de la calle: una placa que solo responde a " +
                 "los habitantes de la simulación, nunca a ti. La lectura del nivel, hecha regla.")]
        [SerializeField] private bool pisanResidentes = true;

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
                                         col.GetComponentInParent<CorrectorVigilante>() != null ||
                                         col.GetComponentInParent<CorrectorCongelable>() != null)) return true;
                if (pisanPlataformasCongelables && col.GetComponentInParent<PlataformaCongelable>() != null) return true;
                if (pisanCajas && col.GetComponentInParent<CajaEmpujable>() != null) return true;
                if (pisanBloques && col.GetComponentInParent<BloqueSuspendido>() != null) return true;
                if (pisanResidentes && col.GetComponentInParent<LaProyeccion.NPC.ResidenteEnBucle>() != null) return true;
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
