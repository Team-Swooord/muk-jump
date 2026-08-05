using System.Collections.Generic;
using MukJump.Core;
using UnityEngine;

namespace MukJump.Player
{
    /// 각 먹방울이의 체력을 머리 위에 직접 표시한다. 월드 Canvas를 만들지 않고
    /// 상태별로 공유하는 작은 Sprite 한 장만 사용해 분신 24마리에서도 비용을 제한한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    [DefaultExecutionOrder(200)]
    public sealed class PlayerHealthBillboard : MonoBehaviour
    {
        const string RendererObjectName = "PlayerHealthBillboard";
        const int TextureWidth = 96;
        const int TextureHeight = 14;
        const float TexturePixelsPerUnit = 100f;
        const float VerticalGap = 0.12f;

        static readonly Dictionary<int, Sprite> sharedSprites = new();
        static readonly List<Object> sharedAssets = new();

        PlayerController player;
        SpriteRenderer bodyRenderer;
        Collider2D bodyCollider;
        SpriteRenderer billboardRenderer;
        int displayedHealth = int.MinValue;
        int displayedMaximum = int.MinValue;

        /// 테스트와 런타임 호환 계층이 같은 렌더러를 재사용하는지 확인하는 읽기 전용 값.
        public SpriteRenderer HealthRenderer => billboardRenderer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSharedSprites()
        {
            for (int i = 0; i < sharedAssets.Count; i++)
            {
                Object asset = sharedAssets[i];
                if (asset == null) continue;
                if (Application.isPlaying)
                    Object.Destroy(asset);
                else
                    Object.DestroyImmediate(asset);
            }
            sharedAssets.Clear();
            sharedSprites.Clear();
        }

        void Awake()
        {
            CacheOwnerComponents();
            EnsureRenderer();
            RefreshHealthSprite(true);
            SetRendererVisible(false);
        }

        void OnEnable()
        {
            CacheOwnerComponents();
            EnsureRenderer();
            player.HealthChanged -= HandleHealthChanged;
            player.HealthChanged += HandleHealthChanged;
            RefreshHealthSprite(true);
        }

        void OnDisable()
        {
            if (player != null)
                player.HealthChanged -= HandleHealthChanged;
            SetRendererVisible(false);
        }

        void LateUpdate()
        {
            CacheOwnerComponents();
            EnsureRenderer();
            RefreshHealthSprite(false);

            GameState state = GameManager.Instance != null
                ? GameManager.Instance.State
                : GameState.Lobby;
            bool visible = ShouldDisplay(
                Application.isPlaying,
                state,
                player != null && player.IsDead,
                bodyRenderer != null && bodyRenderer.enabled &&
                bodyRenderer.sprite != null);
            SetRendererVisible(visible);
            if (!visible) return;

            Bounds ownerBounds = ResolveOwnerBounds();
            Vector3 position = ResolveWorldPosition(
                ownerBounds,
                transform.position,
                VerticalGap);
            billboardRenderer.transform.SetPositionAndRotation(
                position,
                Quaternion.identity);
            billboardRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            billboardRenderer.sortingOrder = bodyRenderer.sortingOrder + 3;
        }

        void HandleHealthChanged(int current, int maximum)
        {
            RefreshHealthSprite(true, current, maximum);
        }

        void CacheOwnerComponents()
        {
            if (player == null)
                player = GetComponent<PlayerController>();
            if (bodyRenderer == null)
                bodyRenderer = GetComponent<SpriteRenderer>();
            if (bodyCollider == null)
                bodyCollider = GetComponent<Collider2D>();
        }

        void EnsureRenderer()
        {
            if (billboardRenderer != null) return;

            Transform existing = transform.Find(RendererObjectName);
            if (existing != null)
                billboardRenderer = existing.GetComponent<SpriteRenderer>();
            if (billboardRenderer != null) return;

            var rendererObject = new GameObject(
                RendererObjectName,
                typeof(SpriteRenderer));
            rendererObject.transform.SetParent(transform, false);
            billboardRenderer = rendererObject.GetComponent<SpriteRenderer>();
            billboardRenderer.color = Color.white;
            billboardRenderer.enabled = false;
        }

        void RefreshHealthSprite(
            bool force,
            int current = int.MinValue,
            int maximum = int.MinValue)
        {
            if (player == null || billboardRenderer == null) return;
            if (current == int.MinValue) current = player.CurrentHealth;
            if (maximum == int.MinValue) maximum = player.MaxHealth;
            maximum = Mathf.Max(1, maximum);
            current = Mathf.Clamp(current, 0, maximum);
            if (!force && current == displayedHealth &&
                maximum == displayedMaximum)
                return;

            displayedHealth = current;
            displayedMaximum = maximum;
            billboardRenderer.sprite = GetOrCreateHealthSprite(current, maximum);
        }

        Bounds ResolveOwnerBounds()
        {
            if (bodyRenderer != null && bodyRenderer.sprite != null)
                return bodyRenderer.bounds;
            if (bodyCollider != null && bodyCollider.enabled)
                return bodyCollider.bounds;
            return new Bounds(transform.position, Vector3.one * 0.8f);
        }

        void SetRendererVisible(bool visible)
        {
            if (billboardRenderer != null)
                billboardRenderer.enabled = visible;
        }

        static Sprite GetOrCreateHealthSprite(int current, int maximum)
        {
            int key = maximum * 100 + current;
            if (sharedSprites.TryGetValue(key, out Sprite sprite) && sprite != null)
                return sprite;

            var texture = new Texture2D(
                TextureWidth,
                TextureHeight,
                TextureFormat.RGBA32,
                false)
            {
                name = $"PlayerHealth_{current}_{maximum}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            Color[] pixels = new Color[TextureWidth * TextureHeight];
            PaintHealthPixels(pixels, TextureWidth, TextureHeight, current, maximum);
            texture.SetPixels(pixels);
            texture.Apply(false, true);

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureWidth, TextureHeight),
                new Vector2(0.5f, 0.5f),
                TexturePixelsPerUnit,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sharedAssets.Add(sprite);
            sharedAssets.Add(texture);
            sharedSprites[key] = sprite;
            return sprite;
        }

        static void PaintHealthPixels(
            Color[] pixels,
            int width,
            int height,
            int current,
            int maximum)
        {
            Color ink = InkPalette.Ink;
            Color paper = InkPalette.Paper;
            Color wounded = InkPalette.Red;
            maximum = Mathf.Max(1, maximum);
            current = Mathf.Clamp(current, 0, maximum);

            // 검은 채움 위에 검은 구분선을 얹으면 모바일 화면에서 한 줄처럼 합쳐진다.
            // 각 체력을 독립된 먹 테두리 셀로 그리고 사이를 투명하게 비워 3/3, 2/3,
            // 1/3 상태를 캐릭터가 작게 보일 때도 즉시 읽을 수 있게 한다.
            const int outerMargin = 1;
            int gap = maximum <= 6 ? 3 : 2;
            int drawableWidth = Mathf.Max(
                maximum * 3,
                width - outerMargin * 2 - gap * (maximum - 1));
            for (int segment = 0; segment < maximum; segment++)
            {
                int start = outerMargin +
                            Mathf.RoundToInt(drawableWidth * segment /
                                             (float)maximum) +
                            gap * segment;
                int end = outerMargin +
                          Mathf.RoundToInt(drawableWidth * (segment + 1) /
                                           (float)maximum) +
                          gap * segment;
                bool filled = segment < current;
                Color fill = current <= 1 ? wounded : ink;
                for (int y = outerMargin; y < height - outerMargin; y++)
                {
                    for (int x = start; x < end; x++)
                    {
                        bool border = x <= start + 1 || x >= end - 2 ||
                                      y <= outerMargin + 1 ||
                                      y >= height - outerMargin - 2;
                        pixels[y * width + x] = border
                            ? ink
                            : filled
                                ? fill
                                : paper;
                    }
                }
            }
        }

        static bool ShouldDisplay(
            bool applicationPlaying,
            GameState state,
            bool isDead,
            bool bodyVisible)
        {
            return applicationPlaying && state == GameState.Playing &&
                   !isDead && bodyVisible;
        }

        static Vector3 ResolveWorldPosition(
            Bounds ownerBounds,
            Vector3 fallbackPosition,
            float verticalGap)
        {
            float centerX = ownerBounds.size.x > 0f
                ? ownerBounds.center.x
                : fallbackPosition.x;
            float topY = ownerBounds.size.y > 0f
                ? ownerBounds.max.y
                : fallbackPosition.y;
            return new Vector3(
                centerX,
                topY + Mathf.Max(0f, verticalGap),
                fallbackPosition.z);
        }
    }
}
