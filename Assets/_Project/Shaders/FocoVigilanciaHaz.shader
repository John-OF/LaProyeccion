// Haz del Foco de vigilancia de Keplin (variante "searchlight" — laboratorio Pruebas/).
// A diferencia de la caja (ZonaVigiladaOverlay: vigilancia TEMPORAL por ciclo),
// aquí SOLO EL HAZ detecta: vigilancia ESPACIAL. El visual DEBE coincidir con el
// área de detección (Pilar 3) — FocoVigilancia.cs usa la misma apertura/largo.
//
// Misma familia visual "tecnología Keplin" que el overlay: rojo, scanlines
// (aquí arcos concéntricos: frentes de la mirada), grano de interferencia.
//
// Contrato con FocoVigilancia.cs (MaterialPropertyBlock por instancia):
//   _Angulo   ángulo actual del haz en radianes (0 = recto hacia abajo, + = derecha)
//   _Apertura semi-apertura del cono en radianes
//   _QuadSize tamaño del quad en unidades (ancho, largo). El origen del cono es
//             el CENTRO-SUPERIOR del quad (uv 0.5, 1).
Shader "LaProyeccion/FocoVigilanciaHaz"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ColorHaz ("Color del haz", Color) = (1.0, 0.24, 0.16, 1)
        _Angulo ("Angulo actual (rad)", Float) = 0
        _Apertura ("Semi-apertura (rad)", Float) = 0.19
        _QuadSize ("Tamaño del quad (u)", Vector) = (10, 8, 0, 0)
        _HazAlpha ("Alpha del haz", Range(0, 1)) = 0.3
        _NucleoAlpha ("Alpha del núcleo central", Range(0, 1)) = 0.22
        _ScanDensity ("Arcos por unidad", Float) = 1.4
        _ScanAlpha ("Alpha de los arcos", Range(0, 1)) = 0.16
        _NoiseAlpha ("Grano", Range(0, 1)) = 0.22
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
            fixed4 _ColorHaz;
            float _Angulo;
            float _Apertura;
            float4 _QuadSize;
            float _HazAlpha;
            float _NucleoAlpha;
            float _ScanDensity;
            float _ScanAlpha;
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

                // ---- cono: lleno por dentro, borde suave hacia la apertura ----
                float lit = 1.0 - smoothstep(_Apertura * 0.72, _Apertura, delta);
                // núcleo más caliente en el eje del haz
                float nucleo = 1.0 - smoothstep(0.0, _Apertura * 0.45, delta);

                // ---- atenuación con la distancia (fuente arriba), sin morir del todo ----
                float fall = saturate(1.0 - dist / max(_QuadSize.y, 0.001));
                fall = 0.35 + 0.65 * fall;

                // ---- arcos concéntricos descendentes: los "frentes" de la mirada ----
                float arcos = 0.5 + 0.5 * sin(dist * _ScanDensity * 6.28318 - _Time.y * 2.2);
                arcos *= arcos;

                // ---- grano de interferencia ----
                float n = hash21(floor(float2(pixAng * 40.0, dist * 6.0)) + floor(_Time.y * 13.0));

                float a = lit * _HazAlpha * fall
                        + nucleo * _NucleoAlpha * fall
                        + lit * arcos * _ScanAlpha
                        + lit * (n - 0.5) * _NoiseAlpha * 0.3;

                float3 col = _ColorHaz.rgb + nucleo * 0.25;

                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 outCol = fixed4(col, saturate(a)) * i.color;
                outCol.a *= tex.a;
                return outCol;
            }
            ENDCG
        }
    }
}
