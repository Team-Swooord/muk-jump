// 빌더가 고정한 먹 원본 4×2 시트 전용. 모든 프레임은 같은 눈 위치를 쓴다.
// 어두운 색만으로 동공을 판별하면 몸체의 먹색까지 빠지므로 프레임 안의 눈 위치를 함께 구분한다.
half4 GoldInkColor(half4 source, float2 sheetUv, float seconds)
{
    half3 gammaColor = source.rgb;
    #ifndef UNITY_COLORSPACE_GAMMA
        gammaColor = max(gammaColor, 0.0);
        gammaColor = lerp(1.055 * pow(gammaColor, 1.0 / 2.4) - 0.055,
            gammaColor * 12.92, step(gammaColor, 0.0031308));
    #endif
    half lightness = max(gammaColor.r, max(gammaColor.g, gammaColor.b));
    float2 frameUv = frac(sheetUv * float2(4.0, 2.0));
    float eyeDistance = min(length(frameUv - float2(0.4004, 0.5195)),
                            length(frameUv - float2(0.6035, 0.5195)));
    half mask = smoothstep(0.036, 0.044, eyeDistance) * (1.0 - smoothstep(0.24, 0.80, lightness));
    // 크기와 무관하게 한 번의 텍스처 읽기만 사용한다. 좁은 광택 대신 넓은 샴페인 금빛이 8초에 걸쳐 흐른다.
    // 움직임 줄이기에서는 시간만 멈추고 같은 정적 그라데이션을 유지한다.
    float phase = dot(frameUv, float2(0.75, 0.32)) * 6.2831853 - seconds * 0.7853982 * _GoldShimmer;
    half flow = 0.5 + 0.5 * sin(phase);
    half3 gold = lerp(half3(0.84, 0.57, 0.20), half3(1.0, 0.88, 0.53), flow);
    gold *= 0.95 + 0.05 * saturate(lightness * 6.0);
    #ifndef UNITY_COLORSPACE_GAMMA
        gold = pow((gold + 0.055) / 1.055, 2.4);
    #endif
    return half4(lerp(source.rgb, gold, mask), source.a);
}
