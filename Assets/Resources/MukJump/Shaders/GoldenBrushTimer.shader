Shader "MukJump/GoldenBrushTimer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FillColor ("Yellow remaining arc", Color) = (0.886,0.706,0.243,1)
        _TrackColor ("Ink track", Color) = (0.11,0.106,0.102,1)
        _Remaining ("Remaining time", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            fixed4 _FillColor, _TrackColor;
            float _Remaining;
            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 p = input.uv - 0.5;
                float radius = length(p);
                float aa = max(fwidth(radius), 0.001);
                float outline = (1 - smoothstep(0.46 - aa, 0.46 + aa, radius)) *
                    smoothstep(0.315 - aa, 0.315 + aa, radius);
                float ring = (1 - smoothstep(0.44 - aa, 0.44 + aa, radius)) *
                    smoothstep(0.335 - aa, 0.335 + aa, radius);
                // 12시에서 시계 방향으로 비운다. 회전하는 로딩 스피너가 아니다.
                float phase = frac(atan2(p.x, p.y) / 6.2831853 + 1);
                float remaining = saturate(_Remaining);
                float visible = step(1 - remaining, phase);
                if (remaining >= 0.99999) visible = 1;
                float fill = ring * visible;
                float alpha = outline * lerp(0.38, 0.8, visible);
                alpha = max(alpha, fill * _FillColor.a);
                if (remaining <= 0) alpha = 0;
                return fixed4(lerp(_TrackColor.rgb, _FillColor.rgb, fill), alpha);
            }
            ENDCG
        }
    }
}
