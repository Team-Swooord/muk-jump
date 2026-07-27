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
        static Material tintableBrushMaterial;
        static Texture2D brushTexture;
        static Texture2D tintableBrushTexture;
        static bool ownsBrushTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReleaseRuntimeAssets()
        {
            DestroyRuntimeObject(inkMaterial);
            DestroyRuntimeObject(tintableBrushMaterial);
            if (ownsBrushTexture)
                DestroyRuntimeObject(brushTexture);
            DestroyRuntimeObject(tintableBrushTexture);
            inkMaterial = null;
            tintableBrushMaterial = null;
            brushTexture = null;
            tintableBrushTexture = null;
            ownsBrushTexture = false;
        }

        public static Material SharedInkMaterial
        {
            get
            {
                if (inkMaterial == null)
                    inkMaterial = CreateMaterial(BrushTexture);
                return inkMaterial;
            }
        }

        /// 원본 LineSprite의 RGB가 검정이어도 효과색이 보이도록 흰색+알파 붓결을 쓰는 재질.
        public static Material SharedTintableBrushMaterial
        {
            get
            {
                if (tintableBrushMaterial == null)
                    tintableBrushMaterial = CreateMaterial(TintableBrushTexture);
                return tintableBrushMaterial;
            }
        }

        /// Main UI의 LineSprite 텍스처를 실제 드로잉 발판 붓결로 교체한다.
        public static void SetBrushTexture(Texture2D texture)
        {
            if (texture == null || texture == brushTexture) return;
            Texture2D previousTexture = brushTexture;
            bool destroyPrevious = ownsBrushTexture;
            brushTexture = texture;
            ownsBrushTexture = false;
            if (inkMaterial != null) inkMaterial.mainTexture = texture;
            if (destroyPrevious)
                DestroyRuntimeObject(previousTexture);
        }

        /// 발판 LineRenderer에 붓선 스타일을 적용한다
        public static void Apply(LineRenderer line, float strokeLength)
        {
            // 개별 Renderer.material 인스턴스를 만들지 않고 모든 붓선을 공유 재질로 묶는다.
            line.sharedMaterial = SharedInkMaterial;
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
            // 정점 수만큼 배열과 Keyframe을 매번 만들 필요 없이 동일한 4키 테이퍼를
            // 기준 곡선으로 사용하고 전체 두께만 multiplier로 조절한다.
            line.widthCurve = taper;
            line.widthMultiplier = baseWidth;

            var ink = InkPalette.Ink;
            ink.a = 0.96f;
            line.startColor = line.endColor = ink;
        }

        /// 마른 붓(갈필) 질감: 세로 방향 가장자리가 노이즈로 거칠게 끊기는 잉크 띠
        static Texture2D BrushTexture
        {
            get
            {
                if (brushTexture != null) return brushTexture;
                brushTexture = CreateProceduralBrushTexture("MukJump_InkBrushTexture");
                ownsBrushTexture = true;
                return brushTexture;
            }
        }

        static Texture2D TintableBrushTexture
        {
            get
            {
                if (tintableBrushTexture == null)
                    tintableBrushTexture =
                        CreateProceduralBrushTexture("MukJump_TintableBrushTexture");
                return tintableBrushTexture;
            }
        }

        static Texture2D CreateProceduralBrushTexture(string textureName)
        {
            const int w = 256, h = 64;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[w * h];

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
                    pixels[y * w + x] =
                        new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Material CreateMaterial(Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return new Material(shader) { mainTexture = texture };
        }

        static void DestroyRuntimeObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }
    }
}
