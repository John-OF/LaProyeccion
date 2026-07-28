using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LaProyeccion.Narrative
{
    /// <summary>Cuánto pesa un pensamiento cuando compite con el silencio.</summary>
    public enum PesoPensamiento
    {
        /// <summary>Comentario de color. Si no cabe, SE PIERDE y no pasa nada.</summary>
        Ambiental,
        /// <summary>Hito o información que el jugador necesita. Espera su turno, no se pierde.</summary>
        Importante
    }

    /// <summary>
    /// LA VOZ INTERIOR DE LA ANOMALÍA (`PLAN_REDISENO.md` §2, fijado 2026-07-27).
    /// "Muda" significa que no habla en voz alta: piensa, y su razonamiento se lee como texto.
    /// Despierta sin saber quién es ni dónde está — ni ella ni el jugador saben qué son, y lo
    /// descubren juntos.
    ///
    /// NO ES "KeplinMessageController con otro color". Son dos personajes y el jugador no puede
    /// dudar nunca de quién habla, así que se distinguen en CUATRO ejes a la vez:
    ///   Keplin: ARRIBA · grande (48) · blanco puro · CON sonido (se anuncia: es un sistema
    ///           notificándote).
    ///   Ella:   ABAJO · pequeña (34) · gris cálido apagado · EN SILENCIO (un pensamiento no suena)
    ///           y en cursiva.
    /// El silencio es el diferenciador más fuerte y además sale gratis.
    ///
    /// SABER CALLARSE ES LA MITAD DEL SISTEMA, y es requisito explícito del autor: "no es que cada
    /// 3 minutos el pj dirá algo, tendrá un equilibrio de razonamiento y silencio para que el
    /// jugador piense, pero no se sienta solo todo el tiempo". Por eso esto no es un
    /// `Mostrar(texto)`: es un portero. Tres reglas, y las tres pueden hacer que un pensamiento
    /// NO se muestre:
    ///   1. **No se repite.** Con `id`, un pensamiento ocurre UNA VEZ por partida.
    ///   2. **No se pisa.** Mientras uno está en pantalla, otro no entra.
    ///   3. **No se amontona.** Tras terminar, hay un `silencioMinimo` de tregua.
    /// Un `Ambiental` que choque con cualquiera de las tres **se descarta sin más**; un
    /// `Importante` espera turno. Descartar es la función, no un fallo.
    ///
    /// Encaja con ALCANCE §5 ("peso narrativo ambiental y de descubrimiento, sin diálogos extensos,
    /// modelo Inside/Limbo") y con GDD §7 ("UI mínima, sin HUD permanente"): esto no es un HUD, es
    /// un texto que aparece y se va.
    /// </summary>
    public class PensamientoController : MonoBehaviour
    {
        public static PensamientoController Instance { get; private set; }

        [Header("Texto")]
        [SerializeField] private TMP_Text texto;

        [Header("Ritmo de lectura")]
        [SerializeField, Min(0.05f)] private float fadeIn = 0.8f;
        [Tooltip("Tiempo en pantalla = esta base + segundosPorPalabra × palabras. Un pensamiento " +
                 "corto no se queda colgado y uno largo da tiempo a leerlo.")]
        [SerializeField, Min(0.2f)] private float lecturaBase = 1.6f;
        [SerializeField, Min(0.05f)] private float segundosPorPalabra = 0.28f;
        [SerializeField, Min(0.05f)] private float fadeOut = 1.2f;

        [Header("Saber callarse")]
        [Tooltip("Tregua obligatoria tras terminar un pensamiento. Un Ambiental que caiga dentro se " +
                 "DESCARTA; un Importante espera a que pase.")]
        [SerializeField, Min(0f)] private float silencioMinimo = 12f;

        /// <summary>Ids ya pensados. Estático: sobrevive a la muerte y al cambio de escena,
        /// igual que las Semillas recogidas (`SeedPickup.collected`).</summary>
        private static readonly HashSet<string> yaPensados = new HashSet<string>();

        private Coroutine activo;
        private float finDelUltimo = -9999f;
        private string pendienteTexto;
        private string pendienteId;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (texto != null)
            {
                var c = texto.color; c.a = 0f; texto.color = c;
                texto.gameObject.SetActive(false);
            }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Partida nueva: que no recuerde lo que pensó en la partida anterior.
        /// Lo llama `GameSession` junto a `SeedPickup.ClearSessionState()`.</summary>
        public static void ClearSessionState() => yaPensados.Clear();

        /// <summary>
        /// Piensa algo. Devuelve <c>false</c> si NO se va a mostrar (repetido, o descartado por
        /// silencio) — que es un resultado normal, no un error.
        /// </summary>
        /// <param name="contenido">El texto. Corto: modelo Inside/Limbo (ALCANCE §5).</param>
        /// <param name="id">Identificador para que ocurra UNA sola vez por partida. Vacío = puede repetirse.</param>
        /// <param name="peso">Ambiental se descarta si no cabe; Importante espera turno.</param>
        public bool Pensar(string contenido, string id = null, PesoPensamiento peso = PesoPensamiento.Ambiental)
        {
            if (string.IsNullOrWhiteSpace(contenido) || texto == null) return false;

            // 1. No se repite
            if (!string.IsNullOrEmpty(id) && yaPensados.Contains(id)) return false;

            bool ocupado = activo != null;
            bool enTregua = Time.time - finDelUltimo < silencioMinimo;

            if (ocupado || enTregua)
            {
                // 2 y 3. Un Ambiental que no cabe se pierde, y está bien que se pierda.
                if (peso == PesoPensamiento.Ambiental) return false;

                // Un Importante espera turno. Solo se guarda UNO: si llegan dos hitos a la vez,
                // manda el primero — encadenarlos sería justo el muro de texto que se evita.
                if (pendienteTexto == null)
                {
                    pendienteTexto = contenido;
                    pendienteId = id;
                    if (!string.IsNullOrEmpty(id)) yaPensados.Add(id);
                    return true;
                }
                return false;
            }

            if (!string.IsNullOrEmpty(id)) yaPensados.Add(id);
            activo = StartCoroutine(Rutina(contenido));
            return true;
        }

        private IEnumerator Rutina(string contenido)
        {
            texto.text = contenido;
            texto.gameObject.SetActive(true);

            yield return Fade(0f, 1f, fadeIn);

            int palabras = contenido.Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
            yield return new WaitForSeconds(lecturaBase + palabras * segundosPorPalabra);

            yield return Fade(1f, 0f, fadeOut);

            texto.gameObject.SetActive(false);
            activo = null;
            finDelUltimo = Time.time;

            // El Importante que esperaba entra cuando pase la tregua, no antes.
            if (pendienteTexto != null)
            {
                yield return new WaitForSeconds(silencioMinimo);
                string t = pendienteTexto;
                pendienteTexto = null; pendienteId = null;
                activo = StartCoroutine(Rutina(t));
            }
        }

        private IEnumerator Fade(float de, float a, float duracion)
        {
            float t = 0f;
            Color c = texto.color;
            while (t < duracion)
            {
                t += Time.deltaTime;
                c.a = Mathf.Lerp(de, a, Mathf.Clamp01(t / duracion));
                texto.color = c;
                yield return null;
            }
            c.a = a; texto.color = c;
        }

        /// <summary>Atajo para cablear desde el Inspector (UnityEvent&lt;string&gt;), donde no se
        /// pueden pasar los parámetros opcionales. Un pensamiento cableado así es Importante:
        /// si alguien se molestó en conectarlo a un evento, es que quiere que se lea.</summary>
        public void PensarImportante(string contenido)
            => Pensar(contenido, contenido, PesoPensamiento.Importante);
    }
}
