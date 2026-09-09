using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 짧은 번짐으로 부드럽게 닫히는 한지 띠와 작은 사각 낙관. 글자·터치·반복 모션은 건드리지 않는다.
    public sealed class InkHudSurface : MaskableGraphic
    {
        public const float BandHeight = 104f;
        [SerializeField] bool seal;

        public static InkHudSurface Ensure(RectTransform parent, bool isSeal)
        {
            const string surfaceName = "HudInkSurface";
            var surface = parent.Find(surfaceName)?.GetComponent<InkHudSurface>();
            if (surface == null)
            {
                var go = new GameObject(surfaceName, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(InkHudSurface));
                go.transform.SetParent(parent, false);
                surface = go.GetComponent<InkHudSurface>();
            }
            surface.seal = isSeal;
            surface.raycastTarget = false;
            var rect = surface.rectTransform;
            rect.anchorMin = new Vector2(0f, isSeal ? 0f : 0.5f);
            rect.anchorMax = new Vector2(1f, isSeal ? 1f : 0.5f);
            rect.sizeDelta = new Vector2(0f, isSeal ? 0f : BandHeight);
            rect.anchoredPosition = Vector2.zero;
            rect.SetAsFirstSibling();
            surface.color = isSeal ? InkPalette.Red : new Color(0.73f, 0.69f, 0.59f, 0.78f);
            // 구형 띠는 호환 참조만 남기고 성장 카드와 같은 한지·갈필 프레임으로 교체한다.
            surface.enabled = isSeal;
            if (!isSeal)
            {
                var paper = parent.Find("HudHanjiCard")?.GetComponent<Image>();
                if (paper == null)
                {
                    var go = new GameObject("HudHanjiCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(parent, false);
                    paper = go.GetComponent<Image>();
                }
                paper.rectTransform.anchorMin = new Vector2(0f, .5f);
                paper.rectTransform.anchorMax = new Vector2(1f, .5f);
                paper.rectTransform.sizeDelta = new Vector2(0f, BandHeight);
                paper.rectTransform.anchoredPosition = Vector2.zero;
                paper.raycastTarget = false;
                InkUiStyle.ConfigureHanjiSurface(paper);
                paper.transform.SetAsFirstSibling();
            }
            surface.SetVerticesDirty();
            return surface;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (!seal)
            {
                PopulateSoftBand(mesh, rect);
                return;
            }
            int columns = seal ? 12 : 96;
            int rows = seal ? 12 : 4;
            float roughness = seal ? 0.9f : 1.7f;
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                float u = x / (float)columns;
                float v = y / (float)rows;
                float px = Mathf.Lerp(rect.xMin + roughness, rect.xMax - roughness, u);
                float py = Mathf.Lerp(rect.yMin + roughness, rect.yMax - roughness, v);
                // 양 끝도 중앙과 같은 높이를 유지하고, 테두리만 미세하게 흔든다.
                if (x == 0 || x == columns)
                    px += (Mathf.PerlinNoise(y * 1.37f, x + 0.41f) - 0.5f) * roughness;
                if (y == 0 || y == rows)
                    py += (Mathf.PerlinNoise(x * 1.73f, y + 0.29f) - 0.5f) * roughness;
                Color tint = color;
                float grain = Mathf.PerlinNoise(x * 1.19f + 0.7f, y * 1.63f + 0.2f);
                tint.a *= Mathf.Lerp(seal ? 0.78f : 0.93f, 1f, grain);
                mesh.AddVert(new Vector3(px, py), tint, new Vector2(u, v));
                if (x == 0 || y == 0) continue;
                int i = y * (columns + 1) + x;
                mesh.AddTriangle(i - columns - 2, i - 1, i);
                mesh.AddTriangle(i - columns - 2, i, i - columns - 1);
            }
        }

        void PopulateSoftBand(VertexHelper mesh, Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            const int columns = 100, rows = 8;
            float feather = Mathf.Min(2.2f, Mathf.Min(rect.width, rect.height) * 0.08f);
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                float u = EdgeCoordinate(x, columns, feather / rect.width);
                float v = EdgeCoordinate(y, rows, feather / rect.height);
                // 짧고 잦은 톱니 대신 긴 종이섬유를 따라 완만하게 굽는다.
                // 고정 좌표만 사용해 다시 그리거나 고도가 바뀌어도 윤곽이 흔들리지 않는다.
                float bottom = rect.yMin + 1.8f + BandContour(u, 0.29f);
                float top = rect.yMax - 1.8f + BandContour(u, 7.81f);
                float corner = Mathf.Pow(Mathf.Abs(v * 2f - 1f), 3f) * 1.8f;
                float left = rect.xMin + 1.6f + corner +
                    (Mathf.PerlinNoise(v * 3.2f, 0.41f) - 0.5f) * 1.4f;
                float right = rect.xMax - 1.6f - corner +
                    (Mathf.PerlinNoise(v * 3.2f, 9.73f) - 0.5f) * 1.4f;
                Color tint = color;
                float grain = Mathf.PerlinNoise(u * 29f + 0.7f, v * 7f + 0.2f);
                tint.a *= Mathf.Lerp(0.93f, 1f, grain) *
                    EdgeOpacity(x, columns) * EdgeOpacity(y, rows);
                mesh.AddVert(new Vector3(Mathf.Lerp(left, right, u), Mathf.Lerp(bottom, top, v)),
                    tint, new Vector2(u, v));
                if (x == 0 || y == 0) continue;
                int i = y * (columns + 1) + x;
                mesh.AddTriangle(i - columns - 2, i - 1, i);
                mesh.AddTriangle(i - columns - 2, i, i - columns - 1);
            }
        }

        static float BandContour(float u, float seed) =>
            (Mathf.PerlinNoise(u * 4.3f + 1.8f, seed) - 0.5f) * 4.6f +
            (Mathf.PerlinNoise(u * 20f + 0.7f, seed + 2f) - 0.5f) * 0.45f;

        static float EdgeCoordinate(int index, int count, float inset)
        {
            if (index == 0) return 0f;
            if (index == count) return 1f;
            if (index == 1) return inset * 0.42f;
            if (index == count - 1) return 1f - inset * 0.42f;
            return Mathf.Lerp(inset, 1f - inset, (index - 2f) / (count - 4f));
        }

        static float EdgeOpacity(int index, int count) =>
            index == 0 || index == count ? 0f :
            index == 1 || index == count - 1 ? 0.42f : 1f;
    }
}
