using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 한지 원화의 UV를 보존하며 위에서부터 펼치는 가벼운 UI 메시.
    /// 종이만 변형하고 글자·버튼·터치 영역은 이 메시의 자식으로 두지 않는다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HanjiScrollPaperGraphic : MaskableGraphic
    {
        public const int Columns = 18;
        public const int Rows = 28;
        public const float ClosedFraction = 0.028f;
        Texture paperTexture;
        float reveal;
        float phaseSeconds;
        float motionStrength;

        public HanjiScrollPaperGraphic()
        {
            // Graphic 기본값은 구형 Mesh 오버로드를 호출한다. VertexHelper 경로를 명시한다.
            useLegacyMeshGeneration = false;
        }

        public override Texture mainTexture => paperTexture != null ? paperTexture : Texture2D.whiteTexture;
        public float RevealedFraction => Mathf.Lerp(ClosedFraction, 1f, reveal);
        public float PhaseSeconds => phaseSeconds;

        public void Configure(Texture texture, Color tint)
        {
            paperTexture = texture;
            color = tint;
            raycastTarget = false;
            SetAllDirty();
        }

        public void SetPose(float opening, float seconds, float strength)
        {
            opening = Mathf.Clamp01(opening);
            strength = Mathf.Clamp01(strength);
            if (Mathf.Approximately(reveal, opening) &&
                Mathf.Approximately(phaseSeconds, seconds) &&
                Mathf.Approximately(motionStrength, strength)) return;
            reveal = opening;
            phaseSeconds = seconds;
            motionStrength = strength;
            SetVerticesDirty();
        }

        public static Vector2 BottomDrift(float seconds) =>
            new Vector2(Mathf.Sin(seconds * 0.85f) * 2.4f,
                Mathf.Sin(seconds * 1.1f) * 1.8f);

        public static Vector2 PaperPoint(Vector2 size, float u, float v,
            float seconds, float strength)
        {
            float edge = Mathf.Pow(Mathf.Abs(u * 2f - 1f), 5f);
            float suspended = Mathf.Sin(v * Mathf.PI);
            // 좌우를 서로 다르게 다듬어 딱딱한 사각 테두리를 없앤다.
            float left = (Mathf.Sin(v * 9f) * 8f + Mathf.Sin(v * 23f) * 2f) * suspended;
            float right = (Mathf.Sin(v * 8f + 1f) * 7f + Mathf.Sin(v * 19f) * 2f) * suspended;
            float x = (u - 0.5f) * size.x + Mathf.Lerp(left, right, u) * edge;
            float y = (0.5f - v) * size.y;
            float flutter = Mathf.Sin(seconds * 1.25f) * Mathf.Sin(v * 8f + u * 3f) * 5f +
                            Mathf.Sin(seconds * 0.73f) * Mathf.Sin(v * 15f - u * 2f) * 2f;
            Vector2 drift = BottomDrift(seconds) * (v * v);
            x += strength * (flutter * suspended * edge + drift.x);
            y += strength * (Mathf.Sin(seconds * 1.05f) * Mathf.Sin(u * 7f + v * 5f) *
                             3f * suspended * edge + drift.y);
            return new Vector2(x, y);
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = rectTransform.rect;
            float fraction = RevealedFraction;
            for (int row = 0; row <= Rows; row++)
            {
                float v = row / (float)Rows * fraction;
                for (int column = 0; column <= Columns; column++)
                {
                    // 가장자리 3px만 알파로 풀고 중앙 한지의 젖은 결은 그대로 보존한다.
                    float u = column == 0 ? 0f : column == Columns ? 1f :
                        Mathf.Lerp(0.004f, 0.996f, (column - 1f) / (Columns - 2f));
                    Vector2 point = PaperPoint(rect.size, u, v, phaseSeconds, motionStrength);
                    float edge = Mathf.Pow(Mathf.Abs(u * 2f - 1f), 5f);
                    float shade = 1f - edge * (0.035f + 0.02f * motionStrength *
                        Mathf.Sin(phaseSeconds * 1.25f + v * 8f));
                    Color tint = color;
                    tint.r *= shade;
                    tint.g *= shade;
                    tint.b *= shade;
                    if (column == 0 || column == Columns) tint.a = 0f;
                    UIVertex vertex = UIVertex.simpleVert;
                    vertex.position = point + rect.center;
                    vertex.uv0 = new Vector2(u, 1f - v);
                    vertex.color = tint;
                    vertices.AddVert(vertex);
                }
            }
            for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                int a = row * (Columns + 1) + column;
                int b = a + Columns + 1;
                vertices.AddTriangle(a, a + 1, b + 1);
                vertices.AddTriangle(a, b + 1, b);
            }
        }
    }
}
