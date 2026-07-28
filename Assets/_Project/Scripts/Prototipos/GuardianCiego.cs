using UnityEngine;
using LaProyeccion.Core;
using LaProyeccion.Player;

namespace LaProyeccion.Prototipos
{
    /// <summary>
    /// PROTOTIPO (laboratorio Assets/Scenes/Pruebas/ — fuera de ALCANCE; promoverlo exige
    /// enmienda consciente de `ALCANCE.md`).
    ///
    /// EL GUARDIÁN de la Cueva (criatura de la leyenda Shuar; `PLAN_REDISENO.md` §4,
    /// parkeado desde el 20-jul). Prototipado el 2026-07-27 a petición del autor.
    ///
    /// ⚠️ POR QUÉ ESTA FORMA Y NO OTRA. `ALCANCE.md` §4 tiene en la lista ❌ "enemigos de
    /// combate o **persecución**", y `PLAN_REDISENO.md` §2 fija "sin combate, sin jefe
    /// físico". Un Guardián que te caza está vetado. Éste **NO te persigue: es CIEGO y va
    /// siempre al ÚLTIMO RUIDO**. Nunca consulta dónde estás tú — consulta dónde sonó algo.
    /// La distinción no es un tecnicismo: cambia de quién es la decisión. No lo esquivas,
    /// lo COLOCAS, y la herramienta es la piedra, un verbo que ya existe y tiene munición
    /// limitada.
    ///
    /// DETERMINISTA, sin IA ni pathfinding (Pilar 3, "sin aleatoriedad jamás"): se mueve por
    /// un raíl 1D a velocidad constante hacia la X del último ruido, y al llegar se para.
    /// Dos jugadores que hagan los mismos ruidos ven exactamente lo mismo.
    ///
    /// LEGIBILIDAD EN LA OSCURIDAD: emite su propia luz tenue (si no, sería una muerte sin
    /// causa visible) y **se gira hacia el ruido en el momento de oírlo** — el "te he oído"
    /// tiene que verse antes que el movimiento, o el jugador no conecta causa y efecto.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class GuardianCiego : MonoBehaviour
    {
        [Header("Raíl (se mueve en 1D; nunca busca al jugador)")]
        [SerializeField] private float limiteIzquierdo = -10f;
        [SerializeField] private float limiteDerecho = 10f;
        [Tooltip("Velocidad al desplazarse hacia el ruido. El jugador corre a 8.")]
        [SerializeField, Min(0.5f)] private float velocidad = 3.5f;
        [Tooltip("A qué distancia de la X objetivo se considera que ya llegó.")]
        [SerializeField, Min(0.05f)] private float tolerancia = 0.3f;

        [Header("Oído (qué le delata)")]
        [Tooltip("Oye las piedras: es el mando a distancia del jugador.")]
        [SerializeField] private bool oyePiedras = true;
        [Tooltip("Oye moverse al jugador. OJO: el juego NO tiene tecla de andar (moveSpeed=8 fijo), " +
                 "así que en la práctica esto es 'moverse suena, estar quieto no'. Si algún día se " +
                 "quiere el trato andar/correr, hay que tocar PlayerController — decisión del autor.")]
        [SerializeField] private bool oyePasos = true;
        [Tooltip("Velocidad horizontal a partir de la cual suena. Con moveSpeed=8, cualquier valor " +
                 "entre 0.5 y 8 significa 'moverse suena'; por encima de 8, el jugador es mudo.")]
        [SerializeField, Min(0.5f)] private float umbralCorrer = 5f;
        [Tooltip("EL DIAL INTERESANTE: si el sonar delata, tu única forma de ver es tu forma de morir.")]
        [SerializeField] private bool oyeSonar = true;
        [Tooltip("Distancia máxima a la que percibe un ruido. Fuera de esto, no se entera.")]
        [SerializeField, Min(1f)] private float alcanceOido = 18f;

        [Header("Contacto")]
        [Tooltip("Radio del cuerpo a efectos de corregir al jugador (se mide como cuerpo, no como punto).")]
        [SerializeField, Min(0.1f)] private float radioCuerpo = 0.9f;

        [Header("Legibilidad")]
        [SerializeField] private SpriteRenderer cuerpo;
        [Tooltip("Se gira hacia el ruido al oírlo: el 'te he oído' antes del movimiento.")]
        [SerializeField, Min(0.05f)] private float duracionGiro = 0.25f;
        [SerializeField] private Color colorReposo = new Color(0.55f, 0.50f, 0.42f, 1f);
        [SerializeField] private Color colorAlerta = new Color(0.85f, 0.72f, 0.45f, 1f);

        private Transform player;
        private Rigidbody2D playerRb;
        private float objetivoX;
        private bool tieneObjetivo;
        private float tiempoGiro;
        private int sentido = 1;

        private void Awake()
        {
            if (cuerpo == null) cuerpo = GetComponentInChildren<SpriteRenderer>();
            objetivoX = transform.position.x;
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) { player = pc.transform; playerRb = pc.GetComponent<Rigidbody2D>(); }
            if (cuerpo != null) cuerpo.color = colorReposo;
        }

        private void OnEnable() { RuidoCueva.OnRuido += Oir; }
        private void OnDisable() { RuidoCueva.OnRuido -= Oir; }

        /// <summary>
        /// Única entrada de información del Guardián. Fíjate en que NO recibe al jugador:
        /// recibe una posición. Si el jugador está callado, para él no existe.
        /// </summary>
        private void Oir(Vector2 pos, TipoRuido tipo)
        {
            // SE COMPROMETE: mientras va hacia un ruido, no escucha nada más hasta llegar.
            // Sin esto el prototipo no tiene puzle: como el jugador hace ruido CONTINUAMENTE
            // al moverse (no hay tecla de andar — moveSpeed es 8 y punto), el último ruido
            // sería siempre él, y la piedra no compraría nada. Con el compromiso, la piedra
            // pasa a ser lo ÚNICO que pone el ruido lejos de ti, que es su razón de existir.
            if (tieneObjetivo) return;

            if (tipo == TipoRuido.Piedra && !oyePiedras) return;
            if (tipo == TipoRuido.Pasos && !oyePasos) return;
            if (tipo == TipoRuido.Sonar && !oyeSonar) return;
            if (Mathf.Abs(pos.x - transform.position.x) > alcanceOido) return;

            objetivoX = Mathf.Clamp(pos.x, limiteIzquierdo, limiteDerecho);
            tieneObjetivo = true;
            tiempoGiro = duracionGiro;

            int nuevoSentido = objetivoX < transform.position.x ? -1 : 1;
            if (nuevoSentido != sentido)
            {
                sentido = nuevoSentido;
                if (cuerpo != null) cuerpo.flipX = sentido < 0;
            }
        }

        private void FixedUpdate()
        {
            // Los pasos se detectan por SONDEO, para no tocar PlayerController
            // (física del jugador intocable; y así este prototipo no ensucia el núcleo).
            if (oyePasos && playerRb != null && player != null)
            {
                if (Mathf.Abs(playerRb.linearVelocity.x) >= umbralCorrer)
                    RuidoCueva.Emitir(player.position, TipoRuido.Pasos);
            }

            // El giro se ve ANTES de que se mueva: primero "te he oído", luego voy.
            if (tiempoGiro > 0f)
            {
                tiempoGiro -= Time.fixedDeltaTime;
                if (cuerpo != null) cuerpo.color = colorAlerta;
                return;
            }

            if (tieneObjetivo)
            {
                float dx = objetivoX - transform.position.x;
                if (Mathf.Abs(dx) <= tolerancia)
                {
                    tieneObjetivo = false;
                    if (cuerpo != null) cuerpo.color = colorReposo;
                }
                else
                {
                    float paso = Mathf.Sign(dx) * velocidad * Time.fixedDeltaTime;
                    if (Mathf.Abs(paso) > Mathf.Abs(dx)) paso = dx;
                    var p = transform.position;
                    p.x = Mathf.Clamp(p.x + paso, limiteIzquierdo, limiteDerecho);
                    transform.position = p;
                }
            }

            // Contacto = corrección. Se mide el jugador como CUERPO, no como punto
            // (lección del Corrector Vigilante).
            if (player != null &&
                Vector2.Distance(player.position, transform.position) <= radioCuerpo)
            {
                var gs = GameSession.Instance;
                if (gs != null) gs.RespawnPlayer();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.72f, 0.45f, 0.9f);
            Gizmos.DrawLine(new Vector3(limiteIzquierdo, transform.position.y - 1f, 0f),
                            new Vector3(limiteIzquierdo, transform.position.y + 1f, 0f));
            Gizmos.DrawLine(new Vector3(limiteDerecho, transform.position.y - 1f, 0f),
                            new Vector3(limiteDerecho, transform.position.y + 1f, 0f));
            Gizmos.DrawWireSphere(transform.position, radioCuerpo);
            Gizmos.color = new Color(0.85f, 0.72f, 0.45f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, alcanceOido);
        }
    }
}
