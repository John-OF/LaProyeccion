// Sprite emisivo para el pase de arte: neón, ventanas encendidas, halos y motas de luz.
// No recibe luz 2D: pinta su propio color multiplicado por un color HDR, de modo que lo que
// pase de 1 lo recoge el Bloom del Volume del mundo y se convierte en resplandor.
//
// Dos usos, un solo shader (se elige en el material con _SrcBlend/_DstBlend):
//   Sólido   (One, OneMinusSrcAlpha): el sprite tapa lo que hay detrás -> tubo de neón, ventana.
//   Aditivo  (One, One):              suma luz sobre lo que hay detrás -> halo, mota, haz.
// La salida va premultiplicada por alfa para que los dos modos compartan fragment.
//
// El color del SpriteRenderer (o de la partícula) llega por el color de vértice y tiñe el brillo,
// así que un mismo material sirve para ámbar, violeta, etc.
Shader "LaProyeccion/SpriteBrillo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [HDR] _Brillo ("Brillo (HDR, multiplica al color del sprite)", Color) = (1, 1, 1, 1)
        _Parpadeo ("Parpadeo (0 = luz fija)", Range(0, 1)) = 0
        _VelParpadeo ("Velocidad del parpadeo", Float) = 9
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Mezcla origen", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Mezcla destino", Float) = 1
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
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Brillo;
            float _Parpadeo;
            float _VelParpadeo;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float Hash11(float p)
            {
                return frac(sin(p * 127.1) * 43758.5453);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 tex = tex2D(_MainTex, i.uv);
                float alfa = tex.a * i.color.a * _Brillo.a;

                // Parpadeo de tubo viejo: escalones bruscos, no una onda suave.
                float paso = floor(_Time.y * _VelParpadeo);
                float ruido = Hash11(paso) * 0.6 + Hash11(paso * 0.37 + 5.0) * 0.4;
                float luz = lerp(1.0, step(0.35, ruido) * (0.55 + 0.45 * ruido), _Parpadeo);

                float3 rgb = tex.rgb * i.color.rgb * _Brillo.rgb * luz;
                return float4(rgb * alfa, alfa);
            }
            ENDCG
        }
    }
}
