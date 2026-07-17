using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using LaProyeccion.Core;
using LaProyeccion.Player;

// Carpeta "Editor" (mágica de Unity: compila a Assembly-CSharp-Editor y no entra
// en la build). Namespace EditorTools y no .Editor para no chocar con UnityEditor.Editor.
namespace LaProyeccion.EditorTools
{
    /// <summary>
    /// Validador de escenas (idea #9 de Claude, 2026-07-13, ideas.md): chequea la
    /// escena abierta — o todos los laboratorios de Pruebas/ — contra los gotchas
    /// que este proyecto YA PAGÓ una vez (CLAUDE.md § "Unity 6 gotchas" y bitácora).
    /// Cada check cita su bug de origen y dice QUÉ hacer, no solo qué está mal.
    ///
    /// Los resultados van a la consola (click en cada línea = ping al objeto).
    /// Sin diálogos modales: también se puede lanzar desde MCP/scripts sin bloquear.
    /// </summary>
    public static class ValidadorDeEscenas
    {
        private struct Hallazgo
        {
            public bool esError;         // error = rompe el juego; aviso = huele mal
            public string mensaje;
            public Object contexto;
        }

        // ==================== Menú ====================

        [MenuItem("LaProyeccion/Validador/Validar escena abierta")]
        public static void ValidarEscenaAbierta()
        {
            var hallazgos = new List<Hallazgo>();
            ValidarEscenaActual(hallazgos);
            ValidarProyecto(hallazgos);
            Reportar(hallazgos, EditorSceneManager.GetActiveScene().name);
        }

        [MenuItem("LaProyeccion/Validador/Validar todos los laboratorios (Pruebas)")]
        public static void ValidarTodosLosLabs()
        {
            // Sin diálogo de guardado (bloquearía una sesión MCP): si hay cambios
            // sin guardar, se aborta y se avisa.
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[Validador] Hay cambios sin guardar en la escena abierta. " +
                                   "Guarda antes de validar en lote (abre cada lab y descartaría los cambios).");
                    return;
                }

            string escenaOriginal = EditorSceneManager.GetActiveScene().path;
            var total = new List<Hallazgo>();

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/Pruebas" }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);

                var deEsta = new List<Hallazgo>();
                ValidarEscenaActual(deEsta);
                string nombre = System.IO.Path.GetFileNameWithoutExtension(ruta);
                foreach (var h in deEsta)
                    total.Add(new Hallazgo { esError = h.esError, mensaje = $"[{nombre}] {h.mensaje}", contexto = null });
                // contexto = null a propósito: al cambiar de escena el objeto muere y
                // un ping colgado confunde más de lo que ayuda.
            }

            ValidarProyecto(total);
            if (!string.IsNullOrEmpty(escenaOriginal))
                EditorSceneManager.OpenScene(escenaOriginal, OpenSceneMode.Single);

            Reportar(total, "todos los laboratorios");
        }

        // ==================== Checks de la escena abierta ====================

        private static void ValidarEscenaActual(List<Hallazgo> h)
        {
            ValidarCamara(h);
            ValidarTilemaps(h);
            ValidarPlayer(h);
            ValidarKitDeEscena(h);
        }

        /// <summary>Gotcha: "Post Processing ON o los efectos solo se ven en Scene view".</summary>
        private static void ValidarCamara(List<Hallazgo> h)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                h.Add(new Hallazgo { esError = true, mensaje = "No hay Main Camera (tag MainCamera) en la escena." });
                return;
            }

            var datos = cam.GetComponent<UniversalAdditionalCameraData>();
            if (datos != null && !datos.renderPostProcessing)
                h.Add(new Hallazgo
                {
                    esError = true,
                    contexto = cam,
                    mensaje = "Main Camera con Post Processing APAGADO: los volúmenes de mundo solo se verán en Scene view. " +
                              "Activa el checkbox Rendering > Post Processing."
                });

            // Gotcha: "Cinemachine 3 llega SIN componente de follow; añadir Position Composer a mano".
            foreach (var cine in Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cine.GetComponent<Unity.Cinemachine.CinemachinePositionComposer>() == null)
                    h.Add(new Hallazgo
                    {
                        esError = false,
                        contexto = cine,
                        mensaje = $"CinemachineCamera '{cine.name}' sin CinemachinePositionComposer: no seguirá a su target."
                    });
        }

        /// <summary>
        /// Gotchas de tilemap: Polygons nunca Outlines; Composite Operation = Merge;
        /// orden TilemapCollider2D antes que CompositeCollider2D (TilemapDualLayer
        /// cachea "el primer Collider2D"); y la colisión fantasma tras duplicar
        /// escenas o borrar tiles por script.
        /// </summary>
        private static void ValidarTilemaps(List<Hallazgo> h)
        {
            foreach (var tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var tmCol = tilemap.GetComponent<TilemapCollider2D>();
                var compo = tilemap.GetComponent<CompositeCollider2D>();
                if (tmCol == null && compo == null) continue;

                if (compo != null && compo.geometryType != CompositeCollider2D.GeometryType.Polygons)
                    h.Add(new Hallazgo
                    {
                        esError = true,
                        contexto = tilemap,
                        mensaje = $"'{tilemap.name}': CompositeCollider2D con Geometry Type = {compo.geometryType}. " +
                                  "Debe ser POLYGONS — con Outlines los colliders son huecos y PlayerSafePush no detecta el solape."
                    });

                if (compo != null && tmCol != null &&
                    tmCol.compositeOperation != Collider2D.CompositeOperation.Merge)
                    h.Add(new Hallazgo
                    {
                        esError = true,
                        contexto = tilemap,
                        mensaje = $"'{tilemap.name}': TilemapCollider2D con Composite Operation = {tmCol.compositeOperation}. " +
                                  "Debe ser MERGE o el composite no recibe las formas del tilemap."
                    });

                if (compo != null && tmCol != null)
                {
                    var comps = tilemap.GetComponents<Component>();
                    if (System.Array.IndexOf(comps, tmCol) > System.Array.IndexOf(comps, compo))
                        h.Add(new Hallazgo
                        {
                            esError = false,
                            contexto = tilemap,
                            mensaje = $"'{tilemap.name}': el TilemapCollider2D está DESPUÉS del CompositeCollider2D. " +
                                      "TilemapDualLayer cachea el primer Collider2D del objeto — súbelo con Move Up " +
                                      "(gotcha del fix de colisión fantasma, 2026-07-13)."
                        });

                    ValidarGeometriaFantasma(h, tilemap, compo);
                }
            }
        }

        /// <summary>
        /// La colisión fantasma: el composite serializa sus formas EN la escena, y
        /// duplicar escena + borrar/editar tiles deja plataformas invisibles. Se
        /// detecta comparando la geometría horneada contra los tiles reales.
        /// </summary>
        private static void ValidarGeometriaFantasma(List<Hallazgo> h, Tilemap tilemap, CompositeCollider2D compo)
        {
            const string ritual = "Ritual: RefreshAllTiles + toggle del TilemapCollider2D + ProcessTilemapChanges + " +
                                  "GenerateGeometry y guardar; si no purga, destruir y re-crear el TilemapCollider2D " +
                                  "(compositeOperation=Merge) y restaurar el orden de componentes (CLAUDE.md, gotchas).";

            tilemap.CompressBounds();
            bool sinTiles = tilemap.GetUsedTilesCount() == 0;

            if (sinTiles && compo.pathCount > 0)
            {
                h.Add(new Hallazgo
                {
                    esError = true,
                    contexto = tilemap,
                    mensaje = $"'{tilemap.name}': COLISIÓN FANTASMA — 0 tiles pintados pero el composite tiene " +
                              $"{compo.pathCount} paths horneados. {ritual}"
                });
                return;
            }
            if (sinTiles || compo.pathCount == 0) return;

            // Bounds de los tiles reales (mundo) + margen de una celda: el composite
            // no debería sobresalir de ahí.
            Bounds tiles = tilemap.localBounds;
            tiles = new Bounds(tilemap.transform.TransformPoint(tiles.center), tiles.size);
            float margen = Mathf.Max(tilemap.cellSize.x, tilemap.cellSize.y) * 1.5f;
            tiles.Expand(margen * 2f);

            Bounds horneada = compo.bounds;
            if (horneada.min.x < tiles.min.x || horneada.max.x > tiles.max.x ||
                horneada.min.y < tiles.min.y || horneada.max.y > tiles.max.y)
                h.Add(new Hallazgo
                {
                    esError = true,
                    contexto = tilemap,
                    mensaje = $"'{tilemap.name}': posible COLISIÓN FANTASMA — la geometría horneada " +
                              $"({horneada.min.x:F1},{horneada.min.y:F1})..({horneada.max.x:F1},{horneada.max.y:F1}) " +
                              $"sobresale de los tiles reales ({tiles.min.x:F1},{tiles.min.y:F1})..({tiles.max.x:F1},{tiles.max.y:F1}). {ritual}"
                });
        }

        /// <summary>
        /// Físicas del jugador: gravityScale=3 es la fuente de verdad única desde el
        /// 2026-07-16 (antes Awake la pisaba y el Inspector mentía — costó un tramo
        /// de lab imposible por 5 cm). groundCheck null = NRE en el primer Update.
        /// </summary>
        private static void ValidarPlayer(List<Hallazgo> h)
        {
            foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var rb = pc.GetComponent<Rigidbody2D>();
                if (rb != null && !Mathf.Approximately(rb.gravityScale, 3f))
                    h.Add(new Hallazgo
                    {
                        esError = true,
                        contexto = pc,
                        mensaje = $"Player '{pc.name}' con gravityScale={rb.gravityScale} (debe ser 3, la calibrada). " +
                                  "Con otra gravedad TODAS las métricas de salto de los niveles quedan mal " +
                                  "(apex 2.45 u / alcance 3.73 u solo valen con 3)."
                    });

                var so = new SerializedObject(pc);
                if (so.FindProperty("groundCheck").objectReferenceValue == null)
                    h.Add(new Hallazgo
                    {
                        esError = true,
                        contexto = pc,
                        mensaje = $"Player '{pc.name}' sin groundCheck asignado: NullReference en el primer Update."
                    });

                var col = pc.GetComponent<BoxCollider2D>();
                if (col != null && col.sharedMaterial == null)
                    h.Add(new Hallazgo
                    {
                        esError = false,
                        contexto = pc,
                        mensaje = $"Player '{pc.name}' sin material de fricción (PlayerNoFriction): se pegará a las paredes."
                    });
            }
        }

        /// <summary>Kit mínimo que los sistemas asumen presente (fallan silencioso o con warning en runtime).</summary>
        private static void ValidarKitDeEscena(List<Hallazgo> h)
        {
            bool hayPlayer = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include) != null;
            if (!hayPlayer) return; // escena no jugable (menú, template): nada que exigir

            if (Object.FindFirstObjectByType<WorldManager>(FindObjectsInactive.Include) == null)
                h.Add(new Hallazgo
                {
                    esError = true,
                    mensaje = "Hay Player pero NO hay WorldManager: la tecla de cambio no hará nada y los " +
                              "subscribers de OnWorldChanged jamás se sincronizarán."
                });

            if (Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include) == null)
                h.Add(new Hallazgo
                {
                    esError = true,
                    mensaje = "Hay Player pero NO hay GameSession: todas las muertes (Corrector, vigilancia, " +
                              "suelo glicheado, caída) llaman GameSession.RespawnPlayer y quedarán en nada."
                });

            if (Object.FindFirstObjectByType<PlayerSafePush>(FindObjectsInactive.Include) != null &&
                Object.FindFirstObjectByType<Checkpoint>(FindObjectsInactive.Include) == null)
                h.Add(new Hallazgo
                {
                    esError = false,
                    mensaje = "Hay PlayerSafePush pero ningún Checkpoint: si el empuje no encuentra salida, " +
                              "su respawn se cancela con warning (Checkpoint.Active == null)."
                });

            var keplin = Object.FindFirstObjectByType<LaProyeccion.Narrative.KeplinMessageController>(FindObjectsInactive.Include);
            if (keplin != null)
            {
                var so = new SerializedObject(keplin);
                if (so.FindProperty("messageText").objectReferenceValue == null)
                    h.Add(new Hallazgo
                    {
                        esError = false,
                        contexto = keplin,
                        mensaje = "KeplinMessageController sin messageText asignado: los mensajes se perderán con warning."
                    });
            }
        }

        // ==================== Checks de proyecto (independientes de la escena) ====================

        private static void ValidarProyecto(List<Hallazgo> h)
        {
            // Gravedad global calibrada (CLAUDE.md: physics del player intocable).
            if (!Mathf.Approximately(Physics2D.gravity.y, -30f) || !Mathf.Approximately(Physics2D.gravity.x, 0f))
                h.Add(new Hallazgo
                {
                    esError = true,
                    mensaje = $"Physics2D.gravity = {Physics2D.gravity} (calibrada: (0, -30)). " +
                              "Toda la física de salto del juego depende de ese valor."
                });

            // Convención: los laboratorios P_ JAMÁS entran en Build Settings.
            foreach (var esc in EditorBuildSettings.scenes)
                if (esc.enabled && esc.path.StartsWith("Assets/Scenes/Pruebas/"))
                    h.Add(new Hallazgo
                    {
                        esError = true,
                        mensaje = $"Escena de laboratorio EN BUILD SETTINGS: {esc.path}. La convención de Pruebas/ es no entrar jamás en la build."
                    });

            // Materiales compartidos sucios tras Play (bug pagado 2 veces: glitch 2026-07-11
            // y peek 2026-07-15): los animados deben quedar en sus valores deterministas.
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Shaders" }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                if (mat == null) continue;

                if (mat.HasProperty("_Progress") && !Mathf.Approximately(mat.GetFloat("_Progress"), 0f))
                    h.Add(new Hallazgo
                    {
                        esError = false,
                        contexto = mat,
                        mensaje = $"Material posiblemente SUCIO de una sesión de Play: {ruta} con _Progress={mat.GetFloat("_Progress"):F2} " +
                                  "(debe reposar en 0; ver los resets deterministas de OnApplicationQuit)."
                    });

                if (mat.HasProperty("_Progress") && mat.HasProperty("_Seed") && !Mathf.Approximately(mat.GetFloat("_Seed"), 0f))
                    h.Add(new Hallazgo
                    {
                        esError = false,
                        contexto = mat,
                        mensaje = $"Material posiblemente SUCIO: {ruta} con _Seed={mat.GetFloat("_Seed"):F1} (debe reposar en 0 — bug del 2026-07-11)."
                    });
            }
        }

        // ==================== Reporte ====================

        private static void Reportar(List<Hallazgo> hallazgos, string ambito)
        {
            int errores = 0, avisos = 0;
            foreach (var h in hallazgos)
            {
                if (h.esError) { errores++; Debug.LogError($"[Validador] ✖ {h.mensaje}", h.contexto); }
                else { avisos++; Debug.LogWarning($"[Validador] ⚠ {h.mensaje}", h.contexto); }
            }

            if (hallazgos.Count == 0)
                Debug.Log($"[Validador] ✔ {ambito}: limpio — ningún gotcha conocido presente.");
            else
                Debug.Log($"[Validador] {ambito}: {errores} error(es), {avisos} aviso(s). " +
                          "Click en cada línea para ir al objeto (validación en lote: sin ping, la escena ya cambió).");
        }
    }
}
