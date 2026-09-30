using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 기존 붓글씨 메시에 호박색→금빛 결을 입힌다. 전용 재질·텍스처·파티클은 만들지 않는다.
    [DisallowMultipleComponent, RequireComponent(typeof(Text))]
    public sealed class GoldInkNameGradient : BaseMeshEffect
    {
        static readonly Color Amber = new(.46f, .20f, .025f);
        static readonly Color Gold = new(.77f, .46f, .055f);
        static readonly Color LightGold = new(.94f, .69f, .19f);
        CanvasGroup[] groups;
        float phase, refreshAt;
        public float Phase => phase;

        protected override void OnEnable()
        {
            base.OnEnable();
            groups = GetComponentsInParent<CanvasGroup>(true);
            phase = 0f; refreshAt = 0f;
        }
        void Update()
        {
            if (!Application.isPlaying || !MobileApplicationLifecycle.IsApplicationActive ||
                graphic == null || graphic.canvasRenderer.cull || Time.unscaledTime < refreshAt) return;
            if (groups != null)
                foreach (var group in groups) if (group != null && group.alpha <= .001f) return;
            refreshAt = Time.unscaledTime + 1f / 15f;
            float next = LobbySettingsProfile.ReducedMotionEnabled ? 0f : Mathf.Repeat(phase + .066667f / 4.8f, 1f);
            if (Mathf.Approximately(phase, next)) return;
            phase = next;
            graphic.SetVerticesDirty();
        }
        public static Color TintAt(float horizontal, float vertical, float time)
        {
            float wave = .5f + .5f * Mathf.Cos((horizontal * .85f - time + vertical * .13f) * Mathf.PI * 2f);
            return wave < .58f ? Color.Lerp(Amber, Gold, wave / .58f) : Color.Lerp(Gold, LightGold, (wave - .58f) / .42f);
        }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;
            var vertex = new UIVertex();
            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                min = Vector2.Min(min, vertex.position); max = Vector2.Max(max, vertex.position);
            }
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                var tint = (Color32)TintAt(Mathf.InverseLerp(min.x, max.x, vertex.position.x),
                    Mathf.InverseLerp(min.y, max.y, vertex.position.y), phase);
                tint.a = vertex.color.a;
                vertex.color = tint;
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
