// Glitch radial BRUTAL del cambio de mundo (GDD §3.1, PLAN Apéndice C.2).
// Puerto fiel del PostFX del prototipo web (_docs/radar/cambio/RadialGlitchPostFX.ts):
// onda de distorsión que se expande desde el jugador durante la transición (0.3 s),
// muestreando la imagen ya renderizada — por eso "deforma el piso" de verdad.
// Anatomía (idéntica a la web):
//   1) refracción radial del frente (el empujón que deforma la geometría),
//   2) slicing horizontal por filas re-sorteado (parpadeo digital),
//   3) aberración cromática radial,
//   4) tinte del mundo destino + estática,
//   5) granos de luz que chispean en el frente,
//   6) el frente brilla como anillo de luz propio.
// El frente tiene borde definido por delante y cola larga por detrás; la zona ya
// barrida queda con glitch residual hasta el final. Envolvente plena casi toda la
// transición (cae del 65% al 100%): el frente llega VISIBLE a los bordes.
//
// Se usa en el FullScreenPassRendererFeature de Renderer2D (fetchColorBuffer=1 →
// _BlitTexture trae la pantalla). Lo anima WorldSwitchEffectController con las MISMAS
// propiedades del shadergraph anterior (_Progress 0→1, _PlayerScreenPos en viewport,
// _TintColor) + _Seed opcional (cada cambio glitchea distinto). _Progress fuera de
// (0,1) = passthrough: el controller deja 0 al terminar (y en OnApplicationQuit).
//
// Las métricas en px son de la resolución interna de la web (480×270) y se escalan
// automáticamente a la resolución real; _PixelScale multiplica encima si se quiere
// más gordo/fino.
Shader "LaProyeccion/WorldSwitchGlitchBrutal"
{
    Properties
    {
        _Progress ("Progreso de la transicion (0-1)", Range(0, 1)) = 0
        _PlayerScreenPos ("Epicentro (viewport 0-1)", Vector) = (0.5, 0.5, 0, 0)
        _TintColor ("Tinte del mundo destino", Color) = (0.4, 0.9, 1.0, 1.0)
        _Seed ("Semilla aleatoria del disparo", Float) = 0
        _PixelScale ("Escala extra del efecto", Range(0.25, 4)) = 1

        [Header(Frente de onda)]
        _FrontEdgePx ("Borde delantero (px web)", Float) = 14
        _FrontTailPx ("Cola del frente (px web)", Float) = 70
        _ResidueGain ("Glitch residual (0-1)", Range(0, 1)) = 0.3
        _RingSectors ("Sectores angulares del frente", Float) = 28
        _PatternRerolls ("Re-sorteos del patron", Float) = 8
        _RimGain ("Brillo del anillo (aditivo)", Range(0, 2)) = 0.4

        [Header(Distorsion)]
        _RefractionPx ("Refraccion radial (px web)", Float) = 10
        _SliceJitterPx ("Slicing horizontal (px web)", Float) = 10
        _SliceRowPx ("Alto de fila del slicing (px web)", Float) = 3
        _ChromaticPx ("Aberracion cromatica (px web)", Float) = 4

        [Header(Textura)]
        _TintMix ("Mezcla del tinte (0-1)", Range(0, 1)) = 0.22
        _StaticNoise ("Estatica (0-1)", Range(0, 1)) = 0.3
        _SparkCellPx ("Celda de los granos (px web)", Float) = 6
        _SparkThreshold ("Umbral de granos (mas alto = menos)", Range(0, 1)) = 0.9
        _SparkGain ("Brillo de los granos", Range(0, 3)) = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "WorldSwitchGlitchBrutal"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            float _Progress;
            float4 _PlayerScreenPos;
            half4 _TintColor;
            float _Seed;
            float _PixelScale;

            float _FrontEdgePx;
            float _FrontTailPx;
            float _ResidueGain;
            float _RingSectors;
            float _PatternRerolls;
            float _RimGain;

            float _RefractionPx;
            float _SliceJitterPx;
            float _SliceRowPx;
            float _ChromaticPx;

            float _TintMix;
            float _StaticNoise;
            float _SparkCellPx;
            float _SparkThreshold;
            float _SparkGain;

            // Hash idéntico al de la web.
            float HashWeb(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float3 MuestraPantalla(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // Inactivo: passthrough (el controller deja _Progress en 0 al terminar).
                if (_Progress <= 0.001 || _Progress >= 0.999)
                {
                    return float4(MuestraPantalla(uv), 1.0);
                }

                float2 res = _ScreenParams.xy;
                // px de diseño web (resolución interna 480×270) → px reales.
                float esc = (res.y / 270.0) * max(_PixelScale, 0.01);

                float2 fragPx = uv * res;
                float2 centro = _PlayerScreenPos.xy * res;
                float2 aFrag = fragPx - centro;
                float dist = length(aFrag);
                float2 dir = dist > 0.001 ? aFrag / dist : float2(0.0, 0.0);

                // Frente de onda: alcanza la esquina más lejana justo al terminar.
                float bordePx = _FrontEdgePx * esc;
                float maxRadio = length(max(centro, res - centro)) + bordePx;
                float radio = _Progress * maxRadio;

                // Envolvente temporal: plena casi toda la transición, cae solo al final.
                float envolvente = 1.0 - smoothstep(0.65, 1.0, _Progress);

                // Frente asimétrico: borde definido por delante, cola larga por detrás.
                float dr = dist - radio;
                float frente = 1.0 - smoothstep(0.0, bordePx, dr);
                float cola = 1.0 - smoothstep(0.0, _FrontTailPx * esc, -dr);
                float anillo = frente * cola;

                // Residuo: toda la zona ya barrida queda perturbada hasta el final.
                float dentro = frente;

                // Frente roto en sectores re-sorteados (parpadeo digital).
                float reroll = floor(_Progress * _PatternRerolls);
                float angulo = atan2(aFrag.y, aFrag.x);
                float sector = floor((angulo / 6.2831853 + 0.5) * _RingSectors);
                float ganSector = 0.55 + 0.45 * HashWeb(float2(sector, _Seed + reroll));

                float fuerza = min(anillo * ganSector + dentro * _ResidueGain, 1.0) * envolvente;

                // 1) refracción radial del frente (la deformación del piso).
                float2 offsetPx = dir * (fuerza * _RefractionPx * esc);

                // 2) slicing horizontal por filas, re-sorteado con el mismo parpadeo.
                float fila = floor(fragPx.y / (_SliceRowPx * esc));
                float jitter = HashWeb(float2(fila, _Seed + reroll)) * 2.0 - 1.0;
                offsetPx.x += jitter * fuerza * _SliceJitterPx * esc;

                float2 uvMuestra = uv - offsetPx / res;

                // 3) aberración cromática radial.
                float2 ca = dir * (fuerza * _ChromaticPx * esc) / res;
                float r = MuestraPantalla(uvMuestra + ca).r;
                float g = MuestraPantalla(uvMuestra).g;
                float b = MuestraPantalla(uvMuestra - ca).b;
                float3 color = float3(r, g, b);

                // 4) tinte del mundo destino + estática.
                color = lerp(color, _TintColor.rgb, fuerza * _TintMix);
                color += (HashWeb(fragPx + _Seed * 251.0) - 0.5) * (fuerza * _StaticNoise);

                // 5) granos de luz: celdas que chispean en el frente (y algo en el residuo).
                float2 celda = floor(fragPx / (_SparkCellPx * esc));
                float semillaGrano = HashWeb(celda + float2(_Seed, reroll * 7.0));
                float encendido = step(_SparkThreshold, semillaGrano);
                float2 uvCelda = frac(fragPx / (_SparkCellPx * esc)) - 0.5;
                float punto = 1.0 - smoothstep(0.1, 0.5, length(uvCelda));
                float chispa = encendido * punto * (anillo + dentro * 0.25) * envolvente;
                float3 colorChispa = lerp(_TintColor.rgb, float3(1.0, 1.0, 1.0), 0.55);
                color += colorChispa * (chispa * _SparkGain);

                // 6) el frente brilla como anillo de luz propio.
                color += _TintColor.rgb * (anillo * envolvente * _RimGain);

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
