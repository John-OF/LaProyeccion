// PROTOTIPO — Vistazo al otro mundo ("peek", idea #1 de Claude, 2026-07-13).
// Variante de SpriteGhostReveal con MÁSCARA RADIAL en espacio de mundo: el fantasma
// solo se ve dentro de un radio alrededor del jugador (_Centro/_Radio, los alimenta
// WorldPeekController cada frame). Es la diferencia de diseño con el radar: el radar
// revela TODO ~4 s (consume Semilla); el vistazo es local, breve y gratuito.
// Mismo lenguaje visual (ruido de mundo, bandas, scanlines) para que se lean como
// la misma familia de tecnología.
//
// Contrato con WorldPeekController:
//   _Reveal 0→1 al abrir el vistazo y 1→0 al cerrarlo (disolución en trazo roto).
//   _Centro (xy, mundo) = posición del jugador, cada frame mientras está activo.
//   Materiales: M_Peek_Sim (cyan) / M_Peek_Real (óxido).
Shader "LaProyeccion/SpritePeekReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _GhostColor ("Color fantasma (RGB + alpha global)", Color) = (0.35, 0.95, 1.0, 0.5)
        _Reveal ("Revelado (lo anima el peek)", Range(0, 1)) = 1
        _Centro ("Centro del vistazo (mundo, xy)", Vector) = (0, 0, 0, 0)
        _Radio ("Radio del vistazo (unidades de mundo)", Float) = 6
        _Borde ("Ancho del borde difuso del radio", Float) = 1.5
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
            float4 _Centro;
            float _Radio;
            float _Borde;
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

            // Hash y ruido de valor baratos (mismos que SpriteGhostReveal).
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

                // ---- Máscara radial del vistazo (la novedad frente al radar) ----
                float dist = distance(i.worldPos, _Centro.xy);
                // El borde "respira" con ruido: el límite del vistazo no es un
                // círculo perfecto sino una frontera de interferencia viva.
                float irregular = (ValueNoise(i.worldPos * 2.3 + _Time.y * 1.7) - 0.5) * _Borde;
                float mascara = smoothstep(_Radio + irregular, _Radio - _Borde + irregular, dist);
                // Aro tenue justo en la frontera (lee dónde termina tu vistazo).
                float aro = saturate(1.0 - abs(dist - _Radio + _Borde * 0.5) / max(_Borde, 1e-3)) * 0.4;

                float trazo = saturate(fwidth(tex.a) * _OutlineBoost);

                float n = ValueNoise(i.worldPos * _NoiseScale + _Time.y * float2(3.1, -2.3));

                float banda = floor(i.worldPos.y / max(_BandHeight, 1e-4));
                float bruido = Hash21(float2(banda, floor(_Time.y * _FlickerSpeed)));
                float interferencia = 1.0 - _NoiseIntensity * 0.6 * step(0.72, bruido);

                float scan = 1.0 - _ScanlineIntensity *
                    (0.5 + 0.5 * sin(i.worldPos.y * _ScanlineDensity * 6.28318));

                float disolucion = smoothstep(0.0, 0.35, _Reveal - n * 0.55);

                float parpadeo = 1.0 - _NoiseIntensity * 0.25 * Hash11(floor(_Time.y * _FlickerSpeed));

                float relleno = _FillOpacity * (0.7 + 0.3 * n);
                float alpha = silueta * saturate(relleno + trazo + aro * mascara) * _GhostColor.a
                            * disolucion * interferencia * scan * parpadeo * mascara;

                float3 col = _GhostColor.rgb * (1.0 + trazo * 0.8 + aro * 0.5);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
