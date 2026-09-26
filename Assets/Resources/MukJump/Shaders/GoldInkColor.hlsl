// 원본 알파·흰 눈·가장 어두운 동공은 보존하고 먹색의 명암만 금색으로 치환한다.
half4 GoldInkColor(half4 source, float2 bodyPosition, float seconds)
{
    half3 gammaColor = source.rgb;
    #ifndef UNITY_COLORSPACE_GAMMA
        gammaColor = max(gammaColor, 0.0);
        gammaColor = lerp(1.055 * pow(gammaColor, 1.0 / 2.4) - 0.055,
            gammaColor * 12.92, step(gammaColor, 0.0031308));
    #endif
    half lightness = max(gammaColor.r, max(gammaColor.g, gammaColor.b));
    half mask = smoothstep(0.048, 0.074, lightness) * (1.0 - smoothstep(0.24, 0.80, lightness));
    // 한 번의 텍스처 읽기로 몸체 안에서만 느린 금빛 결을 만든다. 눈·알파는 원본 그대로다.
    half wave = sin(bodyPosition.x * 2.1 + bodyPosition.y * 1.6 - seconds * 1.65);
    half glow = saturate((wave - 0.62) / 0.38);
    glow *= glow;
    half shade = saturate((lightness - 0.055) * 6.2 +
        _GoldShimmer * (0.12 * (wave + 1.0) + 0.24 * glow));
    half3 gold = lerp(half3(0.55, 0.32, 0.055), half3(1.0, 0.84, 0.35), shade);
    #ifndef UNITY_COLORSPACE_GAMMA
        gold = pow((gold + 0.055) / 1.055, 2.4);
    #endif
    return half4(lerp(source.rgb, gold, mask), source.a);
}
