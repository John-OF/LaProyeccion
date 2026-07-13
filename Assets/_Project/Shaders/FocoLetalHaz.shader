// Haz del Foco de vigilancia LETAL de Keplin (prototipo, laboratorio Pruebas/).
// Variante instakill del searchlight: TOCAR el haz mata, como tocar a un guardia.
// Mismo lenguaje de peligro que ZonaLetalOverlay:
//   - carmesí profundo + FRANJAS incandescentes que fluyen desde el ojo hacia
//     fuera (perpendiculares al haz): se lee como energía que EMANA y quema,
//     no como una mirada;
//   - borde angular DURO: la frontera de muerte es exacta (Pilar 3);
//   - núcleo blanco casi sólido + pulso de ataque rápido.
// Sin atenuación fuerte con la distancia: el haz mata igual en toda su longitud,
// así que debe verse igual de denso (el visual no miente).
//
// Contrato con FocoVigilancia.cs — IDÉNTICO a FocoVigilanciaHaz (mismo MPB):
//   _Angulo   ángulo actual del haz en radianes (0 = recto hacia abajo, + = derecha)
//   _Apertura semi-apertura del cono en radianes
//   _QuadSize tamaño del quad en unidades (ancho, largo); origen = centro-superior.
Shader "LaProyeccion/FocoLetalHaz"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        // Sombra 1D (oclusión visual): distancia máxima normalizada por ángulo,
        // escrita por FocoVigilancia con un abanico de raycasts. Blanco = sin recorte.
        _SombraTex ("Sombra 1D (dist/largo por angulo)", 2D) = "white" {}
        _ColorHaz ("Color del haz (carmesi)", Color) = (0.92, 0.06, 0.10, 1)
        _ColorFranja ("Color franjas (incandescente)", Color) = (1.0, 0.92, 0.85, 1)
        _Angulo ("Angulo actual (rad)", Float) = 0
        _Apertura ("Semi-apertura (rad)", Float) = 0.19
        _QuadSize ("Tamaño del quad (u)", Vector) = (10, 8, 0, 0)
        _HazAlpha ("Alpha del haz", Range(0, 1)) = 0.42
        _NucleoAlpha ("Alpha del núcleo central", Range(0, 1)) = 0.45
        _FranjaDensidad ("Franjas por unidad", Float) = 0.7
        _FranjaVelocidad ("Velocidad de franjas (u/s)", Float) = 3.5
        _FranjaAlpha ("Alpha de franjas", Range(0, 1)) = 0.28
        _PulsoHz ("Pulso (Hz)", Float) = 2.5
        _PulsoFuerza ("Fuerza del pulso", Range(0, 1)) = 0.22
        _NoiseAlpha ("Grano", Range(0, 1)) = 0.18
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
            sampler2D _SombraTex;
            fixed4 _ColorHaz;
            fixed4 _ColorFranja;
            float _Angulo;
            float _Apertura;
            float4 _QuadSize;
            float _HazAlpha;
            float _NucleoAlpha;
            float _FranjaDensidad;
            float _FranjaVelocidad;
            float _FranjaAlpha;
            float _PulsoHz;
            float _PulsoFuerza;
            float _NoiseAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Coordenadas locales: origen del cono en el centro-superior del quad.
                float dx = (i.uv.x - 0.5) * _QuadSize.x;
                float dyAbajo = (1.0 - i.uv.y) * _QuadSize.y; // positivo hacia abajo
                float dist = sqrt(dx * dx + dyAbajo * dyAbajo);
                float pixAng = atan2(dx, max(dyAbajo, 0.0001)); // 0 = recto abajo

                float delta = abs(pixAng - _Angulo);

                // ---- pulso: ataque instantáneo, decaimiento rápido ----
                float puls = 1.0 - frac(_Time.y * _PulsoHz);
                puls *= puls;
                float intens = 1.0 - _PulsoFuerza + _PulsoFuerza * puls;

                // ---- cono con borde angular DURO: la frontera de muerte es exacta ----
                float lit = 1.0 - smoothstep(_Apertura * 0.93, _Apertura, delta);
                // núcleo blanco casi sólido en el eje
                float nucleo = 1.0 - smoothstep(0.0, _Apertura * 0.5, delta);

                // ---- atenuación mínima: el haz mata igual en toda su longitud ----
                float fall = saturate(1.0 - dist / max(_QuadSize.y, 0.001));
                fall = 0.75 + 0.25 * fall;

                // ---- franjas de peligro fluyendo desde el ojo hacia fuera ----
                float f = frac(dist * _FranjaDensidad - _Time.y * _FranjaVelocidad);
                float franja = smoothstep(0.44, 0.5, f) * (1.0 - smoothstep(0.72, 0.78, f));

                // ---- grano de interferencia ----
                float n = hash21(floor(float2(pixAng * 40.0, dist * 6.0)) + floor(_Time.y * 13.0));

                // ---- sombra 1D: la mirada no atraviesa muros (oclusión visual) ----
                float u = saturate((pixAng - (_Angulo - _Apertura)) / max(2.0 * _Apertura, 0.0001));
                float distMax = tex2D(_SombraTex, float2(u, 0.5)).r * _QuadSize.y;
                float sombra = 1.0 - smoothstep(distMax - 0.18, distMax + 0.04, dist);

                float a = ((lit * _HazAlpha
                        + nucleo * _NucleoAlpha
                        + lit * franja * _FranjaAlpha) * fall * intens
                        + lit * (n - 0.5) * _NoiseAlpha * 0.3) * sombra;

                // franjas y núcleo queman en blanco incandescente
                float3 col = lerp(_ColorHaz.rgb, _ColorFranja.rgb,
                                  saturate(franja * 0.85 + nucleo * 0.75) * lit);

                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 outCol = fixed4(col, saturate(a)) * i.color;
                outCol.a *= tex.a;
                return outCol;
            }
            ENDCG
        }
    }
}
