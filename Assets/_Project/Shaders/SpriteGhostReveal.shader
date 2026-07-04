// Silueta "fantasma" del pulso del radar (GDD §3.2 / §7, PLAN F1.P5 + Apéndice C.1).
// Se aplica temporalmente a los renderers del mundo opuesto durante el revelado:
// pinta la geometría/interactuables como trazo ruidoso semitransparente, sin tocar colliders.
// El ruido vive en espacio de mundo para que un tilemap entero se lea como un solo
// campo continuo (sin costuras por tile) y la estética hereda del glitch del cambio
// de mundo (interferencia en bandas + scanlines).
//
// Contrato con RadarPulseController (F1.P5):
//   _Reveal 0→1 al aparecer y 1→0 al expirar (la disolución con ruido ya la hace el shader).
//   Materiales: M_GhostReveal_Sim (cyan, revela geometría de la Simulación)
//               M_GhostReveal_Real (óxido, revela geometría del Real).
Shader "LaProyeccion/SpriteGhostReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _GhostColor ("Color fantasma (RGB + alpha global)", Color) = (0.35, 0.95, 1.0, 0.5)
        _Reveal ("Revelado (lo anima el radar)", Range(0, 1)) = 1
        _FillOpacity ("Opacidad del relleno", Range(0, 1)) = 0.35
        _OutlineBoost ("Refuerzo del trazo (borde)", Range(0, 8)) = 3
        _NoiseScale ("Escala del ruido (unidades de mundo)", Float) = 14
        _NoiseIntensity ("Intensidad del ruido", Range(0, 1)) = 0.55
        _BandHeight ("Alto de banda de interferencia", Float) = 0.14
        _FlickerSpeed ("Velocidad de parpadeo", Float) = 9
        _ScanlineDensity ("Densidad de scanlines (por unidad)", Float) = 6
        _ScanlineIntensity ("Intensidad de scanlines", Range(0, 1)) = 0.18
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
            fixed4 _GhostColor;
            float _Reveal;
            float _FillOpacity;
            float _OutlineBoost;
            float _NoiseScale;
            float _NoiseIntensity;
            float _BandHeight;
            float _FlickerSpeed;
            float _ScanlineDensity;
            float _ScanlineIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            // Hash y ruido de valor baratos (sin texturas extra).
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float Hash11(float p)
            {
                return frac(sin(p * 127.1) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float silueta = tex.a * i.color.a;

                // Trazo: el borde de la silueta se refuerza (derivada del alpha en pantalla).
                // En el interior de un tilemap sólido no hay transición, así que el trazo
                // solo aparece en el contorno real de la geometría.
                float trazo = saturate(fwidth(tex.a) * _OutlineBoost);

                // Ruido animado en espacio de mundo: textura viva del relleno + disolución.
                float n = ValueNoise(i.worldPos * _NoiseScale + _Time.y * float2(3.1, -2.3));

                // Bandas horizontales de interferencia que caen y cambian con el tiempo.
                float banda = floor(i.worldPos.y / max(_BandHeight, 1e-4));
                float bruido = Hash21(float2(banda, floor(_Time.y * _FlickerSpeed)));
                float interferencia = 1.0 - _NoiseIntensity * 0.6 * step(0.72, bruido);

                // Scanlines suaves (eco del glitch del cambio de mundo).
                float scan = 1.0 - _ScanlineIntensity *
                    (0.5 + 0.5 * sin(i.worldPos.y * _ScanlineDensity * 6.28318));

                // Materialización: _Reveal disuelve la silueta contra el ruido
                // (aparece/desaparece en trazo roto, no en fade plano).
                float disolucion = smoothstep(0.0, 0.35, _Reveal - n * 0.55);

                // Parpadeo global sutil.
                float parpadeo = 1.0 - _NoiseIntensity * 0.25 * Hash11(floor(_Time.y * _FlickerSpeed));

                float relleno = _FillOpacity * (0.7 + 0.3 * n);
                float alpha = silueta * saturate(relleno + trazo) * _GhostColor.a
                            * disolucion * interferencia * scan * parpadeo;

                // El trazo brilla un poco más que el relleno (lo que brilla se lee).
                float3 col = _GhostColor.rgb * (1.0 + trazo * 0.8);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
