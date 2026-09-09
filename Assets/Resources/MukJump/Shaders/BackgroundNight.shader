Shader "MukJump/BackgroundNight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original painting", 2D) = "white" {}
        _CleanSkyTex ("Sunless sky plate", 2D) = "white" {}
        _Night ("Night blend", Range(0,1)) = 0
        _EraseSun ("Separate sun from painting", Range(0,1)) = 0
        _SplitForeground ("Separate foreground mountains", Float) = 0
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            sampler2D _CleanSkyTex;
            half _Night, _EraseSun, _SplitForeground;
            float4 _RidgeSamples[33];
            float RidgeSample(int index)
            {
                return _RidgeSamples[index / 4][index % 4];
            }
            fixed4 Frag(v2f input) : SV_Target
            {
                if (_SplitForeground > .5)
                {
                    // 앞산 Mesh와 같은 128개 구간을 사용한다. 원화 픽셀은 별도 앞산 레이어가 그린다.
                    float x = saturate(input.texcoord.x) * 128;
                    int index = min((int)floor(x), 127);
                    float ridge = lerp(RidgeSample(index), RidgeSample(index + 1), x - index);
                    clip(input.texcoord.y - ridge);
                }
                fixed4 source = SampleSpriteTexture(input.texcoord);
                // 해 주변만 깨끗한 하늘로 보강한다. 원래 산·소나무·붓결은 그대로 둔다.
                half2 delta = (input.texcoord - half2(0.68333,0.90156)) * half2(1,1.77778);
                half patch = (1 - smoothstep(0.047,0.065,length(delta))) * _EraseSun;
                source.rgb = lerp(source.rgb, tex2D(_CleanSkyTex,input.texcoord).rgb, patch);
                // 위쪽 하늘은 짙은 먹청색, 아래 플레이 공간은 달빛 한지 명암을 남긴다.
                half sky = smoothstep(0.54,0.94,input.texcoord.y);
                half3 nightTint = lerp(half3(0.40,0.53,0.72),half3(0.075,0.14,0.26),sky);
                source.rgb *= lerp(half3(1,1,1),nightTint,saturate(_Night));
                return source * input.color;
            }
            ENDCG
        }
    }
}
