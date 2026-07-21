using UnityEngine;
using MukJump.Core;

namespace MukJump.AI
{
    /// 폴백 수묵 스타일: API 없이도 발판이 붓질처럼 보이게 하는 절차적 잉크 렌더링.
    /// 마른 붓 질감 텍스처를 런타임에 생성해 LineRenderer에 입힌다.
    /// (제출 요건: API 키 없이도 게임이 정상 동작해야 함 — 이 폴백이 기본 동작)
    public static class FallbackInkStyle
    {
        static Material inkMaterial;
        static Texture2D brushTexture;

        public static Material SharedInkMaterial
        {
            get
            {
                if (inkMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    inkMaterial = new Material(shader) { mainTexture = BrushTexture };
                }
                return inkMaterial;
            }
        }

        /// 발판 LineRenderer에 붓선 스타일을 적용한다
        public static void Apply(LineRenderer line, float strokeLength)
        {
            line.material = SharedInkMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 5;
            line.numCornerVertices = 5;
            line.sortingOrder = 0;

            // 붓 획: 시작은 가늘게 눌러 들어가고, 끝은 스치듯 빠진다
            float baseWidth = Mathf.Lerp(0.5f, 0.62f, Mathf.InverseLerp(1f, 6f, strokeLength));
            var taper = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.18f, 1f),
                new Keyframe(0.75f, 0.9f),
                new Keyframe(1f, 0.2f));

            int count = Mathf.Max(line.positionCount, 2);
            var widths = new float[count];
            for (int i = 0; i < count; i++)
                widths[i] = baseWidth * taper.Evaluate(i / (float)(count - 1));
            ApplyWidths(line, widths);

            var ink = InkPalette.Ink;
            ink.a = 0.96f;
            line.startColor = line.endColor = ink;
        }

        /// 정점별 두께 배열을 widthCurve로 변환해 적용한다 (LineRenderer에는 정점별
        /// 두께 API가 없어 커브의 키를 정점 위치마다 찍는 방식으로 굽는다)
        static void ApplyWidths(LineRenderer line, float[] widths)
        {
            var keys = new Keyframe[widths.Length];
            for (int i = 0; i < widths.Length; i++)
                keys[i] = new Keyframe(i / (float)(widths.Length - 1), widths[i]);
            line.widthCurve = new AnimationCurve(keys);
            line.widthMultiplier = 1f;
        }

        /// 마른 붓(갈필) 질감: 세로 방향 가장자리가 노이즈로 거칠게 끊기는 잉크 띠
        static Texture2D BrushTexture
        {
            get
            {
                if (brushTexture != null) return brushTexture;

                const int w = 256, h = 64;
                brushTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };

                for (int y = 0; y < h; y++)
                {
                    // 중심(0.5)에서 멀수록 옅어지는 기본 농도
                    float edge = 1f - Mathf.Abs(y / (float)(h - 1) - 0.5f) * 2f;
                    for (int x = 0; x < w; x++)
                    {
                        float u = x / (float)w;
                        // 결 방향 노이즈: 붓털이 갈라진 자국
                        float streak = Mathf.PerlinNoise(u * 6f, y * 0.55f);
                        float grain = Mathf.PerlinNoise(u * 40f, y * 0.15f) * 0.25f;
                        float a = Mathf.Clamp01(edge * 1.4f - (1f - streak) * 0.7f - grain);
                        a = Mathf.SmoothStep(0f, 1f, a);
                        brushTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                brushTexture.Apply();
                return brushTexture;
            }
        }
    }
}
