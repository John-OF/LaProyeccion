using System.Collections.Generic;
using UnityEngine;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/): el ZÓCALO donde encaja la
    /// <see cref="PiezaDesactivador"/>. Mientras la pieza esté puesta, apaga las
    /// amenazas de su lista — y las devuelve intactas en cuanto se la llevan.
    ///
    /// El apagado **dura lo que dure la pieza puesta**, no un temporizador. Se comparó
    /// contra un consumible instantáneo que se gastaba a distancia y el autor eligió
    /// ESTO (2026-07-25): el coste no es un recurso que se agota, es TENER la pieza
    /// aquí y no allá. Además el consumible necesitaba reglas de puntería (qué cuenta
    /// como "cerca", qué pasa si señalas algo para lo que no llevas munición) que aquí
    /// simplemente no existen: o la pieza está en el zócalo o no está.
    ///
    /// Comparte con el consumible el contrato de legibilidad vía
    /// <see cref="ApagadoDeAmenaza"/> (azul hielo, y ReiniciarFase al restaurar).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ZocaloDesactivador : MonoBehaviour
    {
        [Tooltip("Las fuerzas de Keplin que este zócalo apaga. UNA por zócalo, salvo " +
                 "que el set-piece pida un grupo que se lee como una sola cosa.")]
        [SerializeField] private List<MonoBehaviour> amenazas = new List<MonoBehaviour>();

        [Tooltip("Desmarcado = la pieza se INSTALA y ya no sale. Es lo que separa las ranuras del " +
                 "APARATO DEL CAMBIO (ALCANCE §4, v1.5: se monta y no se desmonta) de la " +
                 "pieza-desactivador, que existe justo para lo contrario — se transporta y se " +
                 "recupera porque su coste ES tenerla aquí y no allá. Por defecto true: el " +
                 "comportamiento validado en P_PiezaDesactivador no cambia.")]
        [SerializeField] private bool permitirRetirar = true;

        [Header("Lectura")]
        [SerializeField] private Color colorVacio = new Color(0.35f, 0.35f, 0.40f);
        [SerializeField] private Color colorOcupado = new Color(0.55f, 0.75f, 0.90f);

        private readonly List<ApagadoDeAmenaza.Estado> apagadas = new List<ApagadoDeAmenaza.Estado>();
        private SpriteRenderer sr;

        public bool Ocupado { get; private set; }

        /// <summary>
        /// Si es false, la pieza puesta aquí es definitiva. Lo consulta `PortadorDePieza`
        /// **antes** de tocar nada: un zócalo sellado ni siquiera es candidato a recoger, y su
        /// cartel de tecla se oculta. Si solo se bloqueara `Retirar()`, el portador ya se habría
        /// quedado con la referencia de la pieza y te la llevarías dejando la ranura ocupada.
        /// </summary>
        public bool PuedeRetirar => permitirRetirar;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            GetComponent<Collider2D>().isTrigger = true;
            Pintar();
        }

        private void Pintar()
        {
            if (sr != null) sr.color = Ocupado ? colorOcupado : colorVacio;
        }

        /// <summary>Coloca la pieza: apaga las amenazas mientras siga puesta.</summary>
        public void Colocar(PiezaDesactivador pieza)
        {
            if (Ocupado || pieza == null) return;
            Ocupado = true;
            pieza.MarcarColocada(transform);

            foreach (var a in amenazas)
            {
                if (a == null) continue;
                apagadas.Add(ApagadoDeAmenaza.Apagar(a));
            }
            Pintar();
        }

        /// <summary>Retira la pieza: todo vuelve, con la fase reiniciada.</summary>
        public void Retirar()
        {
            if (!Ocupado || !permitirRetirar) return;
            Ocupado = false;
            foreach (var e in apagadas) ApagadoDeAmenaza.Restaurar(e);
            apagadas.Clear();
            Pintar();
        }

        private void OnDisable()
        {
            // Higiene: descargar la escena no deja amenazas apagadas.
            foreach (var e in apagadas) ApagadoDeAmenaza.Restaurar(e);
            apagadas.Clear();
        }
    }
}
