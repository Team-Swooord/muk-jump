Shader "MukJump/ObstaclePaperRed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
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
            #pragma vertex SpriteVert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 Frag(v2f input) : SV_Target
            {
                fixed4 source = SampleSpriteTexture(input.texcoord);
                fixed luminance = dot(source.rgb, fixed3(0.299, 0.587, 0.114));
                fixed paperFiber = saturate((luminance - 0.015) * 4.2);
                fixed3 paperRed = input.color.rgb;
                fixed3 darkRed = paperRed * 0.52;
                fixed3 paleRed = lerp(paperRed, fixed3(0.94, 0.78, 0.68), 0.22);
                fixed3 remapped = lerp(darkRed, paleRed, paperFiber);
                return fixed4(remapped, source.a * input.color.a);
            }
            ENDCG
        }
    }
}
