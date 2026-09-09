Shader "MukJump/InkGaugeTint"
{
    Properties
    {
        _MainTex ("Ink mask", 2D) = "white" {}
        _InkColor ("Golden pigment", Color) = (0.612,0.478,0.235,1)
        _Recolor ("Pigment blend", Range(0,1)) = 0
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
            #pragma target 2.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _InkColor;
            half _Recolor;
            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.uv);
                // 검은 안료를 금색으로 치환하되 한지 섬유의 밝기 차이와 알파는 보존한다.
                half fiber = saturate(dot(source.rgb, half3(0.299,0.587,0.114)));
                fixed3 gold = _InkColor.rgb * lerp(0.86, 1.08, fiber);
                return fixed4(lerp(source.rgb, gold, saturate(_Recolor)), source.a);
            }
            ENDCG
        }
    }
}
