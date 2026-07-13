// Overlay de las Zonas Vigiladas LETALES de Keplin (prototipo, laboratorio Pruebas/).
// Variante instakill de la familia "tecnología Keplin": tocar la luz MIENTRAS VIGILA
// mata (contacto), no solo el cambio de mundo. El lenguaje visual debe gritar
// "peligro" a primera vista y no mentir jamás (Pilar 3):
//   - carmesí profundo + FRANJAS DIAGONALES blancas incandescentes en movimiento
//     (cinta de peligro viva) — inconfundible frente al rojo "mirada" del normal;
//   - borde DURO y brillante: la frontera de muerte es exacta;
//   - pulso de ataque rápido (~2.5 Hz): la zona "respira" como algo que quema.
// En descanso queda gris casi invisible, igual que la normal: la ventana de paso
// se lee idéntica en ambas variantes.
//
// Contrato con ZonaVigilada.cs — IDÉNTICO a ZonaVigiladaOverlay (mismo MPB):
//   _Estado   0 = descansa (gris, casi invisible) → 1 = vigila (letal activo).
//   _ZoneSize tamaño de la región en unidades de mundo.
Shader "LaProyeccion/ZonaLetalOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ColorVigila ("Color vigila (carmesi)", Color) = (0.92, 0.06, 0.10, 1)
        _ColorFranja ("Color franjas (incandescente)", Color) = (1.0, 0.92, 0.85, 1)
        _ColorDescansa ("Color descansa", Color) = (0.55, 0.6, 0.68, 1)
        _Estado ("Estado (0 descansa, 1 vigila)", Range(0, 1)) = 1
        _ZoneSize ("Tamaño de la zona (unidades)", Vector) = (10, 4, 0, 0)

        _FillVigila ("Relleno en vigila", Range(0, 1)) = 0.22
        _FillDescansa ("Relleno en descansa", Range(0, 1)) = 0.03
        _BordeGrosor ("Grosor del borde (u)", Float) = 0.16
        _BordeAlpha ("Alpha del borde", Range(0, 1)) = 0.85

        _FranjaDensidad ("Franjas por unidad", Float) = 0.55
        _FranjaVelocidad ("Velocidad de franjas (u/s)", Float) = 1.6
        _FranjaAlpha ("Alpha de franjas", Range(0, 1)) = 0.30
        _PulsoHz ("Pulso (Hz)", Float) = 2.5
        _PulsoFuerza ("Fuerza del pulso", Range(0, 1)) = 0.22

        _ScanDensity ("Scanlines por unidad", Float) = 3
        _ScanAlpha ("Alpha de scanlines", Range(0, 1)) = 0.10
        _NoiseAlpha ("Grano CCTV", Range(0, 1)) = 0.22
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
            fixed4 _ColorFranja;
            fixed4 _ColorDescansa;
            float _Estado;
            float4 _ZoneSize;
            float _FillVigila;
            float _FillDescansa;
            float _BordeGrosor;
            float _BordeAlpha;
            float _FranjaDensidad;
            float _FranjaVelocidad;
            float _FranjaAlpha;
            float _PulsoHz;
            float _PulsoFuerza;
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
                // Coordenadas en unidades de zona (densidades consistentes).
                float2 p = i.uv * _ZoneSize.xy;

                // ---- pulso: ataque instantáneo, decaimiento rápido (late/quema) ----
                float puls = 1.0 - frac(_Time.y * _PulsoHz);
                puls *= puls;
                float intens = 1.0 - _PulsoFuerza + _PulsoFuerza * puls;

                // ---- borde DURO delimitado: la frontera de muerte es exacta ----
                float2 dEdge = min(p, _ZoneSize.xy - p);
                float edge = 1.0 - smoothstep(0.0, _BordeGrosor, min(dEdge.x, dEdge.y));

                // ---- franjas diagonales de peligro (45°), en movimiento continuo ----
                float f = frac((p.x + p.y) * _FranjaDensidad - _Time.y * _FranjaVelocidad);
                // banda dura con antialias mínimo: cinta de peligro, no gradiente
                float franja = smoothstep(0.44, 0.5, f) * (1.0 - smoothstep(0.72, 0.78, f));

                // ---- scanlines suaves (herencia de la familia) ----
                float scan = 0.5 + 0.5 * sin(p.y * _ScanDensity * 6.28318 + _Time.y * 1.7);
                scan *= scan;

                // ---- grano CCTV animado ----
                float n = hash21(floor(p * 9.0) + floor(_Time.y * 14.0));

                // ---- composición por estado ----
                float fill = lerp(_FillDescansa, _FillVigila, _Estado);
                float a = fill * intens
                        + edge * _BordeAlpha * lerp(0.35, intens, _Estado)
                        + franja * _FranjaAlpha * _Estado * intens
                        + scan * _ScanAlpha * _Estado * 0.5
                        + (n - 0.5) * _NoiseAlpha * lerp(0.15, 1.0, _Estado) * 0.22;

                float3 col = lerp(_ColorDescansa.rgb, _ColorVigila.rgb, _Estado);
                // las franjas y el borde queman en blanco incandescente
                col = lerp(col, _ColorFranja.rgb, saturate(franja * 0.85 + edge * 0.4) * _Estado);

                fixed4 tex = tex2D(_MainTex, i.uv); // sprite blanco: conserva compatibilidad
                fixed4 outCol = fixed4(col, saturate(a)) * i.color;
                outCol.a *= tex.a;
                return outCol;
            }
            ENDCG
        }
    }
}
