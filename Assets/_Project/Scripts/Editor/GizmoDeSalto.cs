using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LaProyeccion.Player;

// Carpeta "Editor" (compila a Assembly-CSharp-Editor, no entra en la build).
// Namespace EditorTools, como el Validador, para no chocar con UnityEditor.Editor.
namespace LaProyeccion.EditorTools
{
    /// <summary>
    /// Gizmo de arco de salto (idea #17 de Claude, 2026-07-17, ideas.md): con el modo
    /// activo (menú LaProyeccion → Gizmo de arco de salto, o Ctrl+Shift+J), la vista
    /// Scene dibuja desde el puntero los arcos REALES del jugador hacia ambos lados:
    /// salto (verde) y caminar fuera del borde sin saltar (naranja), con el alcance
    /// horizontal anotado a cada altura entera de aterrizaje.
    ///
    /// La trayectoria NO es la parábola analítica: se integra con Euler semi-implícito
    /// a Time.fixedDeltaTime — el mismo integrador de la física de Unity — replicando
    /// el orden real por paso (clamp de caída en FixedUpdate → gravedad → integración).
    /// Por eso el apex simulado (~2.25 u) queda por DEBAJO del teórico 2.45 u de
    /// CLAUDE.md: el teórico es el límite con velocidad cero, inalcanzable; lo que
    /// dibuja el gizmo es donde llega el Rigidbody de verdad.
    ///
    /// Los valores (moveSpeed, jumpForce, maxFallSpeed, gravityScale) se leen del
    /// Player de la escena abierta — o de PF_Player si la escena no tiene — nunca de
    /// constantes: los defaults del código (6/12) NO son los calibrados del prefab
    /// (8/21), y ese desfase ya costó un tramo imposible por 5 cm (E3 de
    /// P_CambioAereo, 2026-07-16). El Validador caza los gotchas después; este gizmo
    /// los previene antes de pintar el primer tile.
    /// </summary>
    [InitializeOnLoad]
    public static class GizmoDeSalto
    {
        private const string MENU = "LaProyeccion/Gizmo de arco de salto";
        private const string PREF = "LaProyeccion.GizmoDeSalto";
        private const string RUTA_PREFAB = "Assets/_Project/Prefabs/PF_Player.prefab";

        /// <summary>
        /// Hasta cuánto por debajo del ancla se sigue cada arco (aterrizajes en pisos
        /// inferiores). Era 8: la cola de sondeo pesaba tanto como el salto y la curva
        /// completa se leía como "el salto mide 11 cuadros" (feedback del autor,
        /// 2026-07-17). La parte bajo el ancla ahora además se dibuja tenue.
        /// </summary>
        private const float ProfundidadSondeo = 4.5f;
        private const int MaxPasos = 400;

        private static readonly Color ColorSalto = new Color(0.35f, 1f, 0.55f);
        private static readonly Color ColorCaida = new Color(1f, 0.62f, 0.22f, 0.9f);
        private static readonly Color ColorApex = new Color(0.8f, 0.8f, 0.8f, 0.6f);

        private struct Metricas
        {
            public float vx;          // moveSpeed
            public float vSalto;      // jumpForce (se fija como velocidad, no impulso)
            public float clampCaida;  // maxFallSpeed
            public float g;           // magnitud: -Physics2D.gravity.y * gravityScale
            public float anchoJugador; // BoxCollider2D del player (para la silueta a escala)
            public float altoJugador;
            public string fuente;
            public string advertencia; // null = todo en orden
        }

        private static bool activo;
        private static Metricas? cache;
        private static GUIStyle estiloEtiqueta;

        static GizmoDeSalto()
        {
            // delayCall: Menu.SetChecked no es fiable durante la carga del dominio.
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(PREF, false)) Activar(true);
                Menu.SetChecked(MENU, activo);
            };
        }

        [MenuItem(MENU + " %#j")]
        private static void Alternar() => Activar(!activo);

        private static void Activar(bool encender)
        {
            if (activo == encender)
            {
                Menu.SetChecked(MENU, activo);
                return;
            }

            activo = encender;
            EditorPrefs.SetBool(PREF, activo);
            Menu.SetChecked(MENU, activo);

            if (activo)
            {
                SceneView.duringSceneGui += OnSceneGUI;
                EditorApplication.hierarchyChanged += Invalidar;
            }
            else
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                EditorApplication.hierarchyChanged -= Invalidar;
            }

            cache = null;
            SceneView.RepaintAll();
        }

        private static void Invalidar() => cache = null;

        // ==================== Métricas (del Player real, jamás hardcodeadas) ====================

        private static Metricas LeerMetricas()
        {
            if (cache.HasValue) return cache.Value;

            var m = new Metricas
            {
                vx = 8f, vSalto = 21f, clampCaida = 25f, g = 90f,
                anchoJugador = 0.6f, altoJugador = 1.2f,
                fuente = "constantes de CLAUDE.md",
                advertencia = "Sin PlayerController en escena NI en PF_Player: usando constantes documentadas."
            };

            var pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            string fuente = "Player de la escena";
            if (pc == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_PREFAB);
                if (prefab != null) pc = prefab.GetComponent<PlayerController>();
                fuente = "PF_Player (la escena no tiene Player)";
            }

            if (pc != null)
            {
                // SerializedObject: los campos son privados y los defaults del código
                // (6/12) mienten respecto a lo serializado (8/21) — leer SIEMPRE el asset.
                var so = new SerializedObject(pc);
                m.vx = so.FindProperty("moveSpeed").floatValue;
                m.vSalto = so.FindProperty("jumpForce").floatValue;
                m.clampCaida = so.FindProperty("maxFallSpeed").floatValue;

                var col = pc.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    m.anchoJugador = col.size.x;
                    m.altoJugador = col.size.y;
                }

                var rb = pc.GetComponent<Rigidbody2D>();
                float escala = rb != null ? rb.gravityScale : 3f;
                m.g = -Physics2D.gravity.y * escala;
                m.fuente = fuente;
                m.advertencia = Mathf.Approximately(escala, 3f)
                    ? null
                    : $"gravityScale={escala} (≠ 3 calibrada): los arcos son fieles a ESA física, pero está descalibrada.";
            }

            cache = m;
            return m;
        }

        // ==================== Dibujo ====================

        private static void OnSceneGUI(SceneView vista)
        {
            var e = Event.current;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
                vista.Repaint();
            if (e.type != EventType.Repaint) return;

            // Puntero → plano z=0 (mundo 2D).
            var rayo = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 punto = Mathf.Abs(rayo.direction.z) > 1e-4f
                ? rayo.origin - rayo.direction * (rayo.origin.z / rayo.direction.z)
                : rayo.origin;

            // Snap a 0.25 u: sin él, el jitter del mouse hace bailar los números.
            var ancla = new Vector2(Mathf.Round(punto.x * 4f) * 0.25f,
                                    Mathf.Round(punto.y * 4f) * 0.25f);

            var m = LeerMetricas();

            float apexSim = 0f, alcanceSim = 0f;
            foreach (float dir in new[] { 1f, -1f })
            {
                bool derecha = dir > 0f;

                var salto = Simular(ancla, dir, m.vSalto, m);
                DibujarArcoDeSalto(salto, ancla.y);

                // Caída al caminar del borde: secundaria, siempre tenue.
                var caida = Simular(ancla, dir, 0f, m);
                var naranjaTenue = ColorCaida;
                naranjaTenue.a = 0.45f;
                Handles.color = naranjaTenue;
                Handles.DrawAAPolyLine(2f, caida.ToArray());

                // Etiquetas solo a la derecha: el lado izquierdo es espejo exacto.
                MarcarAterrizajes(salto, ancla, ColorSalto, derecha);
                MarcarAterrizajes(caida, ancla, ColorCaida, derecha);

                if (derecha)
                {
                    apexSim = AltoMaximo(salto) - ancla.y;
                    alcanceSim = AlcanceAMismaAltura(salto, ancla);
                    // Silueta en el apex: el jugador en su punto más alto, a escala.
                    SiluetaJugador(salto[IndiceDelApex(salto)], m, 0.45f);
                }
            }

            // Ancla + silueta a escala del jugador parado en ella: la referencia
            // visual que faltaba — sin ella el arco "parecía de 10 cuadros"
            // (feedback del autor, 2026-07-17).
            Handles.color = Color.white;
            Handles.DrawWireDisc(ancla, Vector3.forward, 0.08f);
            SiluetaJugador(ancla, m, 1f);

            float apexTeorico = m.vSalto * m.vSalto / (2f * m.g);
            float yApex = ancla.y + apexSim;
            Handles.color = ColorApex;
            Handles.DrawDottedLine(new Vector3(ancla.x - alcanceSim, yApex),
                                   new Vector3(ancla.x + alcanceSim, yApex), 4f);
            Etiqueta(new Vector3(ancla.x + alcanceSim * 0.35f, yApex + 0.15f),
                     $"apex {apexSim:F2} u (teórico {apexTeorico:F2} — deja margen)", ColorApex);

            DibujarPanel(m, apexSim, alcanceSim, apexTeorico);
        }

        /// <summary>
        /// Trayectoria a paso fijo replicando el orden real por paso de física:
        /// el clamp de PlayerController.FixedUpdate corre ANTES de que la simulación
        /// aplique gravedad e integre — por eso la velocidad de caída efectiva es
        /// -(clamp + g·dt), no -clamp exacto. Fieles a eso, no a la fórmula.
        /// </summary>
        private static List<Vector3> Simular(Vector2 ancla, float dir, float vy0, in Metricas m)
        {
            float dt = Time.fixedDeltaTime;
            var puntos = new List<Vector3>(MaxPasos + 1) { ancla };

            float x = ancla.x, y = ancla.y, vy = vy0;
            for (int i = 0; i < MaxPasos && y > ancla.y - ProfundidadSondeo; i++)
            {
                if (vy < -m.clampCaida) vy = -m.clampCaida;
                vy -= m.g * dt;
                x += dir * m.vx * dt;
                y += vy * dt;
                puntos.Add(new Vector3(x, y, 0f));
            }
            return puntos;
        }

        /// <summary>
        /// El SALTO es la parte sólida: desde el ancla hasta volver a la altura de
        /// partida. Lo que sigue bajando es sondeo de aterrizaje en pisos inferiores
        /// y se dibuja tenue — no es altura ni alcance del salto.
        /// </summary>
        private static void DibujarArcoDeSalto(List<Vector3> puntos, float yAncla)
        {
            int corte = puntos.Count;
            for (int i = IndiceDelApex(puntos); i < puntos.Count; i++)
                if (puntos[i].y < yAncla) { corte = i; break; }

            Handles.color = ColorSalto;
            Handles.DrawAAPolyLine(3f, puntos.GetRange(0, Mathf.Min(corte + 1, puntos.Count)).ToArray());

            if (corte < puntos.Count - 1)
            {
                var verdeTenue = ColorSalto;
                verdeTenue.a = 0.28f;
                Handles.color = verdeTenue;
                Handles.DrawAAPolyLine(2f, puntos.GetRange(corte, puntos.Count - corte).ToArray());
            }
        }

        /// <summary>Rectángulo a escala del BoxCollider2D del jugador, con los pies en la posición dada.</summary>
        private static void SiluetaJugador(Vector2 pies, in Metricas m, float alfa)
        {
            float mitad = m.anchoJugador * 0.5f;
            var vertices = new Vector3[]
            {
                new Vector3(pies.x - mitad, pies.y),
                new Vector3(pies.x + mitad, pies.y),
                new Vector3(pies.x + mitad, pies.y + m.altoJugador),
                new Vector3(pies.x - mitad, pies.y + m.altoJugador)
            };
            Handles.DrawSolidRectangleWithOutline(vertices,
                new Color(1f, 1f, 1f, 0.05f * alfa),
                new Color(1f, 1f, 1f, 0.85f * alfa));
        }

        /// <summary>
        /// Punto y distancia de aterrizaje sobre la rama descendente, a cada altura
        /// entera desde +2 hasta -4 relativa al ancla (el número que el diseño de
        /// nivel necesita: "¿hasta dónde llego si el piso destino está a ESTA altura?").
        /// </summary>
        private static void MarcarAterrizajes(List<Vector3> puntos, Vector2 ancla, Color color, bool conEtiqueta)
        {
            int desde = IndiceDelApex(puntos);
            for (int nivel = 2; nivel >= -4; nivel--)
            {
                float yObjetivo = ancla.y + nivel;
                for (int i = Mathf.Max(desde, 1); i < puntos.Count - 1; i++)
                {
                    if (!(puntos[i].y >= yObjetivo && puntos[i + 1].y < yObjetivo)) continue;

                    float t = (puntos[i].y - yObjetivo) / (puntos[i].y - puntos[i + 1].y);
                    float xCruce = Mathf.Lerp(puntos[i].x, puntos[i + 1].x, t);

                    Handles.color = color;
                    Handles.DrawSolidDisc(new Vector3(xCruce, yObjetivo), Vector3.forward, 0.06f);
                    if (conEtiqueta)
                        Etiqueta(new Vector3(xCruce + 0.1f, yObjetivo + 0.28f),
                                 $"{Mathf.Abs(xCruce - ancla.x):F1}", color);
                    break;
                }
            }
        }

        private static int IndiceDelApex(List<Vector3> puntos)
        {
            int mejor = 0;
            for (int i = 1; i < puntos.Count; i++)
                if (puntos[i].y > puntos[mejor].y) mejor = i;
            return mejor;
        }

        private static float AltoMaximo(List<Vector3> puntos)
        {
            float max = float.MinValue;
            foreach (var p in puntos) if (p.y > max) max = p.y;
            return max;
        }

        private static float AlcanceAMismaAltura(List<Vector3> puntos, Vector2 ancla)
        {
            int desde = IndiceDelApex(puntos);
            for (int i = Mathf.Max(desde, 1); i < puntos.Count - 1; i++)
                if (puntos[i].y >= ancla.y && puntos[i + 1].y < ancla.y)
                {
                    float t = (puntos[i].y - ancla.y) / (puntos[i].y - puntos[i + 1].y);
                    return Mathf.Abs(Mathf.Lerp(puntos[i].x, puntos[i + 1].x, t) - ancla.x);
                }
            return 0f;
        }

        private static void Etiqueta(Vector3 posicion, string texto, Color color)
        {
            if (estiloEtiqueta == null)
                estiloEtiqueta = new GUIStyle(EditorStyles.miniBoldLabel);
            estiloEtiqueta.normal.textColor = color;
            Handles.Label(posicion, texto, estiloEtiqueta);
        }

        private static void DibujarPanel(in Metricas m, float apexSim, float alcanceSim, float apexTeorico)
        {
            float alcanceTeorico = m.vx * 2f * m.vSalto / m.g;

            // GUI de rects fijos, SIN GUILayout: OnSceneGUI solo dibuja en Repaint,
            // y GUILayout exige emitir los mismos controles también en Layout
            // (ArgumentException "Getting control 0's position..." si no).
            Handles.BeginGUI();
            var caja = new Rect(10f, 10f, 440f, m.advertencia == null ? 100f : 118f);
            GUI.Box(caja, GUIContent.none);

            var linea = new Rect(caja.x + 8f, caja.y + 5f, caja.width - 16f, 16f);
            GUI.Label(linea, $"Gizmo de salto — {m.fuente}", EditorStyles.boldLabel);
            linea.y += 19f;
            GUI.Label(linea, $"v {m.vx:F1} u/s · salto {m.vSalto:F1} · g {m.g:F0} · clamp caída {m.clampCaida:F0}");
            linea.y += 19f;
            GUI.Label(linea, $"apex sim {apexSim:F2} u (teórico {apexTeorico:F2}) · alcance {alcanceSim:F2} u (teórico {alcanceTeorico:F2})");
            linea.y += 19f;
            GUI.Label(linea, "verde sólido = el salto · silueta blanca = jugador a escala · números = alcance por altura",
                      EditorStyles.miniLabel);
            linea.y += 16f;
            GUI.Label(linea, "trazo tenue = sondeo de caída a pisos inferiores (NO es altura del salto)",
                      EditorStyles.miniLabel);
            if (m.advertencia != null)
            {
                linea.y += 18f;
                var rojo = new GUIStyle(EditorStyles.miniBoldLabel);
                rojo.normal.textColor = new Color(1f, 0.35f, 0.3f);
                GUI.Label(linea, "⚠ " + m.advertencia, rojo);
            }
            Handles.EndGUI();
        }
    }
}
