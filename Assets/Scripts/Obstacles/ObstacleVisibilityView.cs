using UnityEngine;
using MukJump.AI;
using MukJump.Core;

namespace MukJump.Obstacles
{
    /// 어두운 장애물이 수채화 산과 겹쳐도 읽히도록 얇은 붉은 위험선만 덧댄다.
    /// 실제 스프라이트와 콜라이더 크기는 바꾸지 않는 순수 시각 레이어다.
    public sealed class ObstacleVisibilityView : MonoBehaviour
    {
        SpriteRenderer paperHalo;
        LineRenderer dangerRing;
        FallingInkRock fallingRock;

        public void Configure(float localRadius, int spriteSortingOrder)
        {
            fallingRock = GetComponent<FallingInkRock>();
            DisableLegacyPaperHalo();
            ConfigureDangerRing(localRadius, spriteSortingOrder - 1);
            SetVisible(true);
        }

        void Update()
        {
            if (dangerRing == null) return;

            bool warning = fallingRock != null &&
                           fallingRock.State == FallingInkRockState.Warning;
            float pulse = warning
                ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f)
                : 0f;
            dangerRing.transform.localScale = warning
                ? Vector3.one * Mathf.Lerp(1f, 1.015f, pulse)
                : Vector3.one;

            Color danger = InkPalette.Red;
            danger.a = warning
                ? Mathf.Lerp(0.58f, 0.9f, pulse)
                : 0.62f;
            dangerRing.startColor = dangerRing.endColor = danger;
        }

        public void SetVisible(bool visible)
        {
            if (paperHalo != null) paperHalo.enabled = false;
            if (dangerRing != null) dangerRing.enabled = visible;
        }

        void DisableLegacyPaperHalo()
        {
            var existing = transform.Find("PaperHalo");
            paperHalo = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
            if (paperHalo != null) paperHalo.enabled = false;
        }

        void ConfigureDangerRing(float localRadius, int sortingOrder)
        {
            if (dangerRing == null)
            {
                var existing = transform.Find("DangerRing");
                var go = existing != null ? existing.gameObject : new GameObject("DangerRing");
                if (existing == null) go.transform.SetParent(transform, false);
                dangerRing = go.GetComponent<LineRenderer>();
                if (dangerRing == null) dangerRing = go.AddComponent<LineRenderer>();
            }

            dangerRing.transform.localPosition = Vector3.zero;
            dangerRing.transform.localRotation = Quaternion.identity;
            dangerRing.transform.localScale = Vector3.one;
            dangerRing.useWorldSpace = false;
            dangerRing.loop = true;
            dangerRing.positionCount = 36;
            dangerRing.sharedMaterial = FallbackInkStyle.SharedTintableBrushMaterial;
            dangerRing.textureMode = LineTextureMode.Stretch;
            dangerRing.numCapVertices = 3;
            dangerRing.numCornerVertices = 3;
            dangerRing.startWidth = dangerRing.endWidth = 0.014f;
            dangerRing.sortingOrder = sortingOrder;

            Color danger = InkPalette.Red;
            danger.a = 0.62f;
            dangerRing.startColor = dangerRing.endColor = danger;

            float radius = Mathf.Max(0.05f, localRadius + 0.025f);
            for (int i = 0; i < dangerRing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / dangerRing.positionCount;
                float wobble = 1f + Mathf.Sin(angle * 5f + 0.4f) * 0.015f;
                dangerRing.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                    radius * wobble);
            }
        }
    }
}
