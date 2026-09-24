// 원본 알파·흰 눈·가장 어두운 동공은 보존하고 먹색의 명암만 금색으로 치환한다.
half4 GoldInkColor(half4 source)
{
    half3 gammaColor = source.rgb;
    #ifndef UNITY_COLORSPACE_GAMMA
        gammaColor = max(gammaColor, 0.0);
        gammaColor = lerp(1.055 * pow(gammaColor, 1.0 / 2.4) - 0.055,
            gammaColor * 12.92, step(gammaColor, 0.0031308));
    #endif
    half lightness = max(gammaColor.r, max(gammaColor.g, gammaColor.b));
    half mask = smoothstep(0.048, 0.074, lightness) * (1.0 - smoothstep(0.24, 0.80, lightness));
    half shade = saturate((lightness - 0.055) * 6.2);
    half3 gold = lerp(half3(0.55, 0.32, 0.055), half3(1.0, 0.84, 0.35), shade);
    #ifndef UNITY_COLORSPACE_GAMMA
        gold = pow((gold + 0.055) / 1.055, 2.4);
    #endif
    return half4(lerp(source.rgb, gold, mask), source.a);
}
