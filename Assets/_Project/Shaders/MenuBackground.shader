// Fondo del Menú Principal de La Proyección (shader propio, pieza nueva 2026-07-13).
// Narra el eje del juego en una sola imagen: TECNOLOGÍA VIEJA Y OXIDADA que se
// transforma en TECNOLOGÍA PERFECTA Y FUTURISTA y vuelve, en ciclo. Es el mismo
// contraste Real↔Simulación del núcleo del juego, contado en el menú.
//
// UN shader, dos estados mezclados por `_Morph` (0 = óxido, 1 = futurista) con un
// FRENTE de transformación diagonal que barre la pantalla (nanoreparación): delante
// del frente, metal corroído; detrás, rejilla holográfica limpia; en el frente, una
// banda de energía con chispas. Auto-cicla por tiempo (con mesetas en cada extremo
// para leer ambos estados) salvo que `_AutoVelocidad<=0`, entonces usa `_Morph` a mano.
//
// Partículas procedurales incluidas: chispas que caen (óxido) y motas de datos que
// flotan (futurista), además de scanlines, parpadeo fluorescente, grano, rejilla,
// flujo de circuito y ondas de energía. Autónomo: deriva el aspecto de _ScreenParams.
// Todo lo ESPACIAL se calcula en `q` (uv centrado y corregido por aspecto) para que
// remaches, manchas, motas y ondas sean isótropos (círculos redondos, no óvalos).
Shader "LaProyeccion/MenuBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        [Header(Morph)]
        _Morph ("Morph manual (0 oxido, 1 futurista)", Range(0,1)) = 0
        _AutoVelocidad ("Velocidad de ciclo (0 = manual)", Float) = 1
        _CicloPeriodo ("Periodo del ciclo (s)", Float) = 18

        [Header(Paleta oxido)]
        _OxidoFondo ("Oxido fondo (metal sucio)", Color) = (0.07, 0.055, 0.045, 1)
        _OxidoMancha ("Oxido mancha (corrosion)", Color) = (0.46, 0.20, 0.07, 1)
        _OxidoVerdin ("Oxido cardenillo", Color) = (0.14, 0.24, 0.17, 1)
        _OxidoChispa ("Chispa (naranja incandescente)", Color) = (1.0, 0.62, 0.22, 1)

        [Header(Paleta futurista)]
        _FuturFondo ("Futurista fondo (azul profundo)", Color) = (0.02, 0.06, 0.12, 1)
        _FuturNeon ("Futurista neon (cyan)", Color) = (0.25, 0.85, 1.0, 1)
        _FuturData ("Futurista datos (blanco azulado)", Color) = (0.75, 0.97, 1.0, 1)
        _EnergiaFrente ("Energia del frente", Color) = (0.85, 1.0, 1.0, 1)

        [Header(Intensidades)]
        _Vineta ("Vineta", Range(0,2)) = 1.0
        _GranoAlpha ("Grano", Range(0,1)) = 0.08
        _ScanAlpha ("Scanlines", Range(0,1)) = 0.10
        _ChispaAmt ("Cantidad de chispas", Range(0,2)) = 1.0
        _MoteAmt ("Cantidad de motas", Range(0,2)) = 1.0
        _Brillo ("Brillo general", Range(0.5,2)) = 1.05
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Morph, _AutoVelocidad, _CicloPeriodo;
            fixed4 _OxidoFondo, _OxidoMancha, _OxidoVerdin, _OxidoChispa;
            fixed4 _FuturFondo, _FuturNeon, _FuturData, _EnergiaFrente;
            float _Vineta, _GranoAlpha, _ScanAlpha, _ChispaAmt, _MoteAmt, _Brillo;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // ---------- ruido ----------
            float hash11(float n) { return frac(sin(n) * 43758.5453); }
            float hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 5; k++) { v += a * vnoise(p); p *= 2.03; a *= 0.5; }
                return v;
            }

            // ---------- ESTADO OXIDADO ----------
            // metal corroído + placas remachadas + chispas cayendo + parpadeo fluorescente
            // q: espacio isótropo centrado. uv: 0..1 para las chispas (caen en pantalla).
            float3 estadoOxido(float2 q, float2 uv, float t, out float emis)
            {
                // corrosión: dos escalas de fbm, manchas marcadas
                float base = fbm(q * 3.2 + 10.0);
                float detalle = fbm(q * 8.5 - 5.0);
                float oxido = saturate((base * 0.75 + detalle * 0.45 - 0.34) * 2.3);

                float3 metal = _OxidoFondo.rgb * (0.45 + 1.0 * detalle);
                float3 col = lerp(metal, _OxidoMancha.rgb, oxido);
                col = lerp(col, _OxidoVerdin.rgb, saturate(detalle * detalle * 1.6) * (1.0 - oxido) * 0.55);

                // placas de metal: ranuras + 4 tornillos redondos por placa
                float2 pl = q * 7.0;
                float2 gg = abs(frac(pl) - 0.5);
                float ranura = smoothstep(0.44, 0.5, max(gg.x, gg.y));
                col *= 1.0 - ranura * 0.55;
                float2 esq = abs(frac(pl) - 0.5) - 0.34;
                float tornillo = smoothstep(0.055, 0.02, length(esq));
                col += tornillo * 0.12 * (0.5 + detalle);      // cabeza metálica
                col -= tornillo * ranura * 0.1;

                // parpadeo de tubo fluorescente moribundo
                float flick = 0.7 + 0.3 * sin(t * 2.3);
                float fallo = step(0.9, hash11(floor(t * 8.0))); // cortes bruscos
                flick *= 1.0 - fallo * 0.55;
                col *= 0.6 + 0.5 * flick;

                // CHISPAS que caen (partículas): columnas con una chispa periódica
                float chispa = 0.0;
                float cols = 16.0;
                float gx = uv.x * cols;
                float col_i = floor(gx);
                float fx = frac(gx) - 0.5;
                float rnd = hash11(col_i * 1.7 + 0.3);
                float vel = 0.6 + rnd * 1.2;
                float ph = frac(t * vel * 0.22 + rnd);          // 0..1 caída
                float sy = 1.06 - ph * 1.2;                     // de arriba hacia abajo
                float sx = (hash11(col_i + 5.1) - 0.5) * 0.5;
                float2 dvec = float2((fx - sx) * cols / 3.0, (uv.y - sy) * 3.0);
                float glow = exp(-dot(dvec, dvec) * 3.0);
                float estela = exp(-abs(fx - sx) * cols * 1.6) * exp(-max(0.0, sy - uv.y) * 14.0) * step(uv.y, sy);
                float vivo = smoothstep(0.0, 0.05, ph) * smoothstep(1.0, 0.7, ph);
                float titila = 0.55 + 0.45 * sin(t * 50.0 + rnd * 20.0);
                chispa = (glow + estela * 0.3) * vivo * titila * _ChispaAmt;

                emis = chispa * 1.6;
                col += _OxidoChispa.rgb * chispa;
                return col;
            }

            // ---------- ESTADO FUTURISTA ----------
            // rejilla holográfica + flujo de circuito + ondas de energía + motas de datos
            float3 estadoFuturista(float2 q, float2 uv, float t, out float emis)
            {
                float3 col = _FuturFondo.rgb * (1.0 + (0.5 - uv.y) * 0.7);

                // rejilla holográfica fina (isótropa)
                float2 g = q * 12.0;
                float2 gl = abs(frac(g) - 0.5);
                float lineas = smoothstep(0.5, 0.44, gl.x) + smoothstep(0.5, 0.44, gl.y);
                float rejilla = saturate(lineas) * (0.14 + 0.08 * sin(t * 1.5));

                // flujo de circuito: pulsos que corren por las verticales
                float flujo = frac(q.y * 3.0 - t * 0.4);
                flujo = smoothstep(0.0, 0.05, flujo) * smoothstep(0.45, 0.05, flujo);
                float carril = smoothstep(0.5, 0.44, gl.x);
                float circuito = flujo * carril;

                // ondas de energía concéntricas desde el centro
                float dc = length(q);
                float onda = sin(dc * 13.0 - t * 2.4);
                onda = smoothstep(0.8, 1.0, onda) * exp(-dc * 1.2);

                float neon = rejilla + circuito * 1.0 + onda * 0.8;
                col += _FuturNeon.rgb * neon;

                // MOTAS de datos flotando (partículas): 3 capas de bokeh nítido ascendente
                float motas = 0.0;
                for (int i = 0; i < 3; i++)
                {
                    float fi = float(i);
                    float esc = 4.5 + fi * 2.5;
                    float2 gp = q * esc + fi * 21.3;
                    float2 id = floor(gp);
                    float2 f = frac(gp) - 0.5;
                    float2 rnd = hash22(id);
                    float2 centro = (rnd - 0.5) * 0.55;
                    centro.y += sin(t * (0.3 + rnd.x * 0.4) + rnd.y * 6.28) * 0.15;
                    centro.y += frac(t * 0.04 * (1.0 + rnd.x)) - 0.5; // deriva ascendente
                    float dd = length(f - centro);
                    float m = exp(-dd * dd * 140.0) * (0.4 + 0.6 * rnd.y);
                    m *= 0.55 + 0.45 * sin(t * 3.0 + rnd.x * 12.0); // titileo
                    motas += m;
                }
                motas *= _MoteAmt;
                col += _FuturData.rgb * motas;

                emis = (neon + motas) * 1.3;
                return col;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 q = (uv - 0.5) * float2(aspect, 1.0); // isótropo, centrado
                float t = _Time.y;

                // ---- morph: auto-ciclo con mesetas, o manual ----
                float morph = _Morph;
                if (_AutoVelocidad > 0.001)
                {
                    float ph = frac(t * _AutoVelocidad / max(_CicloPeriodo, 0.1));
                    float sube = smoothstep(0.10, 0.40, ph);
                    float baja = smoothstep(0.60, 0.90, ph);
                    morph = sube - baja; // 0 → 1 (meseta) → 0
                }

                // ---- frente de transformación diagonal ----
                float diag = dot(uv, normalize(float2(1.0, 0.5))) / 1.34; // ~0..1
                float ondulacion = fbm(uv * 5.0 + t * 0.3) * 0.09;        // borde irregular vivo
                float frentePos = morph * 1.18 - 0.09;
                float edge = diag - frentePos + ondulacion;
                float estado = smoothstep(0.03, -0.03, edge); // 1 = ya futurista
                float banda = exp(-pow(edge / 0.04, 2.0));     // energía en el frente

                // ---- ambos estados ----
                float emisOx, emisFu;
                float3 colOx = estadoOxido(q, uv, t, emisOx);
                float3 colFu = estadoFuturista(q, uv, t, emisFu);
                float3 col = lerp(colOx, colFu, estado);
                float emis = lerp(emisOx, emisFu, estado);

                // ---- banda de energía del frente (solo durante la transición) ----
                float transActiva = smoothstep(0.0, 0.05, morph) * smoothstep(1.0, 0.95, morph);
                float chispaFrente = banda * banda * (0.5 + hash21(floor(uv * float2(120.0, 90.0)) + floor(t * 45.0)));
                col += _EnergiaFrente.rgb * (banda * 1.1 + chispaFrente * 0.7) * transActiva;
                emis += banda * transActiva;

                // ---- scanlines (más fuertes en óxido) ----
                float scan = 0.5 + 0.5 * sin(uv.y * _ScreenParams.y * 0.9 - t * 3.0);
                col *= 1.0 - _ScanAlpha * scan * (0.6 + 0.4 * (1.0 - estado));

                // ---- grano ----
                float grano = hash21(uv * _ScreenParams.xy * 0.5 + frac(t) * 91.7) - 0.5;
                col += grano * _GranoAlpha;

                // ---- viñeta ----
                float vin = 1.0 - _Vineta * dot(q, q) * 0.55;
                col *= saturate(vin);

                col *= _Brillo;
                col += emis * 0.06; // realce leve del glow

                fixed4 tex = tex2D(_MainTex, uv);
                fixed4 outCol = fixed4(col, 1.0) * i.color;
                outCol.a *= tex.a;
                return outCol;
            }
            ENDCG
        }
    }
}
