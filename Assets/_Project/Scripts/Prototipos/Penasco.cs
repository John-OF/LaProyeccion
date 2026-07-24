using UnityEngine;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Pruebas/, zona Cueva): marcador de PEÑASCO empujable.
    /// Va en un objeto con Rigidbody2D + Collider2D que el jugador empuja. Su única
    /// función es que un <see cref="ChorroEmpuje"/> lo reconozca al taparle la boca:
    /// meter el peñasco delante del chorro lo apaga (T6, ideas.md §Trampas).
    /// </summary>
    public class Penasco : MonoBehaviour
    {
    }
}
