// Overlay de las Zonas de no-cambio / cambio forzado (prototipo idea #4, 2026-07-16).
// Misma familia visual "tecnología Keplin" que ZonaVigiladaOverlay (scanlines + grano),
// pero con identidad propia: la zona LATE como un corazón (pedido del autor) — doble
// golpe lub-dub y descanso, con una onda que se expande desde el centro en cada latido
// y motas/partículas procedurales que titilan al ritmo. La regla del mundo está VIVA.
//
// Contrato con ZonaDeCambio.cs (MaterialPropertyBlock por instancia — el .mat compartido
// nunca se escribe en runtime, no hay estado que limpiar en OnApplicationQuit):
//   _ZoneSize      tamaño de la región en unidades de mundo (densidades consistentes).
//   _PulsoPeriodo  segundos por latido (la zona forzada late más urgente que la antesala).
//
// El TINTE viene del color del SpriteRenderer (vertex color): un solo material sirve
// para ambas zonas, y el flash rojo de "cambio denegado" (ZonaDeCambio lerpeando
// overlay.color) atraviesa el shader sin contrato extra.
Shader "LaProyeccion/ZonaDeCambioOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ZoneSize ("Tamaño de la zona (u)", Vector) = (10, 6, 0, 0)
        _PulsoPeriodo ("Período del latido (s)", Float) = 1.6

        _FillBase ("Relleno base", Range(0, 1)) = 0.05
        _FillLatido ("Relleno extra en el latido", Range(0, 1)) = 0.07
        _BordeGrosor ("Grosor del borde (u)", Float) = 0.16
        _BordeAlpha ("Alpha del borde", Range(0, 1)) = 0.55

        _OndaAlpha ("Alpha de la onda expansiva", Range(0, 1)) = 0.4
        _OndaGrosor ("Grosor de la onda (u)", Float) = 0.55

        _MotasDensidad ("Motas: celdas por unidad", Float) = 0.9
        _MotasAlpha ("Alpha de las motas", Range(0, 1)) = 0.55
        _MotasVel ("Deriva de las motas (u/s)", Float) = 0.5

        _ScanDensity ("Scanlines por unidad", Float) = 2.5
        _ScanAlpha ("Alpha de scanlines", Range(0, 1)) = 0.08
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
            float4 _ZoneSize;
            float _PulsoPeriodo;
            float _FillBase;
            float _FillLatido;
            float _BordeGrosor;
            float _BordeAlpha;
            float _OndaAlpha;
            float _OndaGrosor;
            float _MotasDensidad;
            float _MotasAlpha;
            float _MotasVel;
            float _ScanDensity;
            float _ScanAlpha;

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
                // Unidades de zona: densidades iguales en zonas de distinto tamaño.
                float2 p = i.uv * _ZoneSize.xy;
                float2 centro = _ZoneSize.xy * 0.5;

                // ---- latido cardíaco: lub-dub y descanso (NO un seno) ----
                // c recorre 0..1 por ciclo; dos gaussianas cercanas = sístole doble.
                float c = frac(_Time.y / max(_PulsoPeriodo, 0.05));
                float lub = exp(-pow((c - 0.08) * 16.0, 2.0));
                float dub = 0.55 * exp(-pow((c - 0.30) * 16.0, 2.0));
                float beat = lub + dub;

                // ---- borde delimitado que late (la región se LEE, Pilar 3) ----
                float2 dEdge = min(p, _ZoneSize.xy - p);
                float edge = 1.0 - smoothstep(0.0, _BordeGrosor, min(dEdge.x, dEdge.y));
                float bordeA = edge * _BordeAlpha * (0.55 + 0.45 * beat);

                // ---- onda expansiva: nace en el centro con cada latido y muere
                //      al llegar al borde (el "pulso" del corazón viajando) ----
                float dist = length(p - centro);
                float maxR = length(centro);
                float r = c * maxR * 1.15;
                float onda = exp(-pow((dist - r) / max(_OndaGrosor, 0.05), 2.0));
                onda *= (1.0 - c) * (1.0 - c);          // se disipa al expandirse
                onda *= smoothstep(0.0, 0.04, c);       // sin flash en el frame c=0

                // ---- motas: partículas procedurales que derivan hacia arriba y
                //      titilan con el latido (cada celda decide si tiene mota) ----
                float2 q = float2(p.x, p.y - _Time.y * _MotasVel);
                float2 id = floor(q * _MotasDensidad);
                float2 fp = frac(q * _MotasDensidad);
                float2 rnd = lerp(0.2, 0.8, float2(hash21(id), hash21(id + 7.3)));
                float mota = 1.0 - smoothstep(0.0, 0.16, length(fp - rnd));
                mota *= step(0.72, hash21(id + 3.1));    // ~1 de cada 4 celdas
                mota *= 0.35 + 0.65 * beat;              // titilan al ritmo

                // ---- scanlines sutiles (familia Keplin) ----
                float scan = 0.5 + 0.5 * sin(p.y * _ScanDensity * 6.28318 + _Time.y * 1.4);
                scan *= scan;

                // ---- composición ----
                float a = _FillBase
                        + beat * _FillLatido
                        + bordeA
                        + onda * _OndaAlpha
                        + mota * _MotasAlpha
                        + scan * _ScanAlpha;

                // Blanco modulado: el tinte lo pone el vertex color (SpriteRenderer).
                float3 col = 1.0 + onda * 0.5 + beat * edge * 0.35;

                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 outCol = fixed4(col, saturate(a)) * i.color;
                outCol.a *= tex.a;
                return outCol;
            }
            ENDCG
        }
    }
}
