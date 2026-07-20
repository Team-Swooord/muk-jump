Shader "MukJump/FallbackInk"
{
    // AI 수묵 변환이 지연/실패할 때 즉시 대체되는 먹 텍스처 셰이더.
    // 외부 텍스처 의존 없이 절차적 노이즈로 먹 번짐 느낌을 낸다 (끊김 없는 플레이 보장 목적).
    Properties
    {
        _Color ("Ink Color", Color) = (0.11, 0.106, 0.101, 1)
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.35
        _NoiseScale ("Noise Scale", Float) = 18
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;
            float _EdgeSoftness;
            float _NoiseScale;
            float _NoiseStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 간단한 해시 기반 procedural 노이즈 (외부 텍스처 불필요)
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 스트로크 폭 방향(v)으로 가장자리를 부드럽게, 길이 방향(u)으로 먹 번짐 노이즈를 얹는다
                float edge = smoothstep(0.0, _EdgeSoftness, i.uv.y) *
                             smoothstep(1.0, 1.0 - _EdgeSoftness, i.uv.y);

                float n = noise(i.uv * _NoiseScale);
                float ink = lerp(1.0, n, _NoiseStrength);

                float alpha = saturate(edge * ink);
                return fixed4(_Color.rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }
}
