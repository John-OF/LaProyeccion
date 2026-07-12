// Overlay de las Zonas Vigiladas de Keplin (ALCANCE v1.2, prototipo aprobado 2026-07-11).
// Primera pieza del lenguaje visual "tecnología Keplin": si funciona, el ghost reveal
// del radar y los Correctores derivarán de esta misma familia (scanlines + barrido +
// grano de interferencia).
//
// Contrato con ZonaVigilada.cs (MaterialPropertyBlock por instancia):
//   _Estado   0 = descansa (gris, casi invisible) → 1 = vigila (rojo, barrido activo).
//             El preaviso lo produce el controller pulsando _Estado (parpadeo suavizado).
//   _ZoneSize tamaño de la región en unidades de mundo (para que scanlines/barrido/borde
//             tengan la misma densidad en zonas de distinto tamaño).
Shader "LaProyeccion/ZonaVigiladaOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ColorVigila ("Color vigila", Color) = (1.0, 0.22, 0.15, 1)
        _ColorDescansa ("Color descansa", Color) = (0.55, 0.6, 0.68, 1)
        _Estado ("Estado (0 descansa, 1 vigila)", Range(0, 1)) = 1
        _ZoneSize ("Tamaño de la zona (unidades)", Vector) = (10, 4, 0, 0)

        _FillVigila ("Relleno en vigila", Range(0, 1)) = 0.10
        _FillDescansa ("Relleno en descansa", Range(0, 1)) = 0.03
        _BordeGrosor ("Grosor del borde (u)", Float) = 0.14
        _BordeAlpha ("Alpha del borde", Range(0, 1)) = 0.6

        _SweepSpeed ("Velocidad del barrido (u/s)", Float) = 2.5
        _SweepWidth ("Ancho del barrido (u)", Float) = 1.6
        _SweepAlpha ("Alpha del barrido", Range(0, 1)) = 0.35
        _GradAlpha ("Gradiente desde el ojo", Range(0, 1)) = 0.09

        _ScanDensity ("Scanlines por unidad", Float) = 3
        _ScanAlpha ("Alpha de scanlines", Range(0, 1)) = 0.22
        _NoiseAlpha ("Grano CCTV", Range(0, 1)) = 0.3
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
            fixed4 _ColorVigila;
            fixed4 _ColorDescansa;
            float _Estado;
            float4 _ZoneSize;
            float _FillVigila;
            float _FillDescansa;
            float _BordeGrosor;
            float _BordeAlpha;
            float _SweepSpeed;
            float _SweepWidth;
            float _SweepAlpha;
            float _GradAlpha;
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
                // Coordenadas en unidades de zona (densidades consistentes
                // entre zonas de distinto tamaño).
                float2 p = i.uv * _ZoneSize.xy;

                // ---- borde delimitado (la región se LEE, Pilar 3) ----
                float2 dEdge = min(p, _ZoneSize.xy - p);
                float edge = 1.0 - smoothstep(0.0, _BordeGrosor, min(dEdge.x, dEdge.y));

                // ---- scanlines horizontales con deriva lenta ----
                float scan = 0.5 + 0.5 * sin(p.y * _ScanDensity * 6.28318 + _Time.y * 1.7);
                scan *= scan; // afilar

                // ---- barrido de escaneo VERTICAL: la mirada del ojo cae desde arriba ----
                // (el ojo/cámara está sobre la zona: la banda desciende desde él,
                // como cortina de luz, y vuelve a empezar arriba)
                float recorrido = _ZoneSize.y + _SweepWidth * 3.0;
                float sweepPos = _ZoneSize.y + _SweepWidth * 1.5
                               - fmod(_Time.y * _SweepSpeed, recorrido);
                float sweep = 1.0 - saturate(abs(p.y - sweepPos) / _SweepWidth);
                sweep *= sweep;
                // estela sutil por encima de la banda (por donde ya pasó la mirada)
                float estela = saturate(1.0 - (p.y - sweepPos) / (_SweepWidth * 3.0));
                estela = (p.y > sweepPos) ? estela * estela * 0.35 : 0.0;

                // ---- gradiente de emanación: más denso cerca del ojo (arriba) ----
                float desdeArriba = saturate(p.y / max(_ZoneSize.y, 0.001));
                desdeArriba *= desdeArriba;

                // ---- grano CCTV animado ----
                float n = hash21(floor(p * 9.0) + floor(_Time.y * 14.0));

                // ---- composición por estado ----
                float fill = lerp(_FillDescansa, _FillVigila, _Estado);
                float a = fill
                        + desdeArriba * _GradAlpha * lerp(0.3, 1.0, _Estado)
                        + edge * _BordeAlpha * lerp(0.35, 1.0, _Estado)
                        + scan * _ScanAlpha * _Estado * 0.5
                        + (sweep + estela) * _SweepAlpha * _Estado
                        + (n - 0.5) * _NoiseAlpha * lerp(0.15, 1.0, _Estado) * 0.22;

                float3 col = lerp(_ColorDescansa.rgb, _ColorVigila.rgb, _Estado);
                col += sweep * 0.3 * _Estado; // la banda quema un poco más

                fixed4 tex = tex2D(_MainTex, i.uv); // sprite blanco: conserva compatibilidad
                fixed4 outCol = fixed4(col, saturate(a)) * i.color;
                outCol.a *= tex.a;
                return outCol;
            }
            ENDCG
        }
    }
}
