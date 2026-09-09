Shader "MukJump/GoldenGaugeParticle"
{
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        // 밝은 한지에서도 금색 외곽이 남도록 순수 가산 대신 알파 혼합을 쓴다.
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4x4 _GuiProjection;
            struct Input { float4 vertex : POSITION; float3 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Output { float4 vertex : SV_POSITION; float3 uv : TEXCOORD0; fixed4 color : COLOR; };
            Output vert(Input input)
            {
                Output output;
                output.vertex = mul(_GuiProjection, input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            fixed4 frag(Output input) : SV_Target
            {
                float2 p = abs(input.uv.xy * 2 - 1);
                float radius = length(p);
                float aa = max(fwidth(radius), .015);
                float mask, core;
                if (input.uv.z > 2.5)
                {
                    // 넓고 아주 옅은 금분 후광 한 장. 가장자리를 완전히 비운다.
                    mask = saturate(1 - radius);
                    mask = mask * mask * (3 - 2 * mask);
                    core = 0;
                }
                else if (input.uv.z < .5)
                {
                    // 사방으로 길게 뻗는 별과 따뜻한 금박 테두리.
                    float star = sqrt(p.x) + sqrt(p.y);
                    mask = 1 - smoothstep(.9 - aa, 1.04 + aa, star);
                    mask = max(mask, saturate(1 - radius * 1.4) * .16);
                    core = 1 - smoothstep(.22, .66, star);
                }
                else
                {
                    mask = 1 - smoothstep(.48 - aa, .96 + aa, radius);
                    core = 1 - smoothstep(.08, .46, radius);
                }
                fixed3 color = lerp(input.color.rgb, fixed3(1, .96, .76), core * .9);
                return fixed4(color, mask * input.color.a);
            }
            ENDCG
        }
    }
}
