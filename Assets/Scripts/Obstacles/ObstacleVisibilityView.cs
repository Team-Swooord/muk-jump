using UnityEngine;
using MukJump.AI;
using MukJump.Core;

namespace MukJump.Obstacles
{
    /// 어두운 장애물이 수채화 산과 겹쳐도 읽히도록 한지 받침과 붉은 위험선을 덧댄다.
    /// 실제 스프라이트와 콜라이더 크기는 바꾸지 않는 순수 시각 레이어다.
    public sealed class ObstacleVisibilityView : MonoBehaviour
    {
        SpriteRenderer paperHalo;
        LineRenderer dangerRing;
        FallingInkRock fallingRock;
        Vector3 haloBaseScale;

        public void Configure(float localRadius, int spriteSortingOrder)
        {
            fallingRock = GetComponent<FallingInkRock>();
            ConfigurePaperHalo(localRadius, spriteSortingOrder - 2);
            ConfigureDangerRing(localRadius, spriteSortingOrder + 1);
            SetVisible(true);
        }

        void Update()
        {
            if (paperHalo == null || dangerRing == null) return;

            bool warning = fallingRock != null &&
                           fallingRock.State == FallingInkRockState.Warning;
            float speed = warning ? 8f : 3.2f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
            float scale = warning
                ? Mathf.Lerp(0.94f, 1.12f, pulse)
                : Mathf.Lerp(1f, 1.035f, pulse);

            dangerRing.transform.localScale = Vector3.one * scale;
            paperHalo.transform.localScale = haloBaseScale *
                                             Mathf.Lerp(1f, 1.025f, pulse);

            Color danger = InkPalette.Red;
            danger.a = warning
                ? Mathf.Lerp(0.58f, 0.98f, pulse)
                : Mathf.Lerp(0.48f, 0.72f, pulse);
            dangerRing.startColor = dangerRing.endColor = danger;

            Color paper = InkPalette.Paper;
            paper.a = warning
                ? Mathf.Lerp(0.76f, 0.94f, pulse)
                : 0.86f;
            paperHalo.color = paper;
        }

        public void SetVisible(bool visible)
        {
            if (paperHalo != null) paperHalo.enabled = visible;
            if (dangerRing != null) dangerRing.enabled = visible;
        }

        void ConfigurePaperHalo(float localRadius, int sortingOrder)
        {
            if (paperHalo == null)
            {
                var existing = transform.Find("PaperHalo");
                var go = existing != null ? existing.gameObject : new GameObject("PaperHalo");
                if (existing == null) go.transform.SetParent(transform, false);
                paperHalo = go.GetComponent<SpriteRenderer>();
                if (paperHalo == null) paperHalo = go.AddComponent<SpriteRenderer>();
            }

            paperHalo.transform.localPosition = Vector3.zero;
            paperHalo.transform.localRotation = Quaternion.identity;
            paperHalo.sprite = InkUiTextureFactory.CreateBlobSprite();
            paperHalo.sortingOrder = sortingOrder;
            Color paper = InkPalette.Paper;
            paper.a = 0.86f;
            paperHalo.color = paper;

            float diameter = Mathf.Max(0.1f, localRadius * 2f + 0.18f);
            float spriteWidth = paperHalo.sprite.bounds.size.x;
            // Blob 마스크는 텍스처 바깥 약 18%가 투명하므로 실제 불투명 폭을 기준으로 맞춘다.
            float visibleSpriteWidth = spriteWidth * 0.82f;
            float scale = visibleSpriteWidth > 0f ? diameter / visibleSpriteWidth : 1f;
            haloBaseScale = Vector3.one * scale;
            paperHalo.transform.localScale = haloBaseScale;
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
            dangerRing.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            dangerRing.textureMode = LineTextureMode.Stretch;
            dangerRing.numCapVertices = 3;
            dangerRing.numCornerVertices = 3;
            dangerRing.startWidth = dangerRing.endWidth = 0.035f;
            dangerRing.sortingOrder = sortingOrder;

            float radius = Mathf.Max(0.05f, localRadius + 0.08f);
            for (int i = 0; i < dangerRing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / dangerRing.positionCount;
                float wobble = 1f + Mathf.Sin(angle * 5f + 0.4f) * 0.025f;
                dangerRing.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                    radius * wobble);
            }
        }
    }
}
