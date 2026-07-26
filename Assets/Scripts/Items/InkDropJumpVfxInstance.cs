using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MukJump.Core.Pooling;

namespace MukJump.Items
{
    /// 먹물방울 점프 연출 한 묶음. 모든 자식 렌더러를 최초 한 번만 만든 뒤 통째로 재사용한다.
    public sealed class InkDropJumpVfxInstance : MonoBehaviour, IPoolableEntity
    {
        public readonly struct AssetSet
        {
            public readonly Sprite InkDrop;
            public readonly Sprite GroundBlob;
            public readonly Sprite InkSplash;
            public readonly Sprite ShockRing;
            public readonly Sprite VerticalBrush;
            public readonly Sprite BrushFibers;
            public readonly Sprite SoftFlash;
            public readonly Sprite InkStreak;
            public readonly Sprite[] DropletFrames;

            public AssetSet(Sprite inkDrop, Sprite groundBlob, Sprite inkSplash, Sprite shockRing,
                Sprite verticalBrush, Sprite brushFibers, Sprite softFlash, Sprite inkStreak,
                Sprite[] dropletFrames)
            {
                InkDrop = inkDrop;
                GroundBlob = groundBlob;
                InkSplash = inkSplash;
                ShockRing = shockRing;
                VerticalBrush = verticalBrush;
                BrushFibers = brushFibers;
                SoftFlash = softFlash;
                InkStreak = inkStreak;
                DropletFrames = dropletFrames;
            }
        }

        const int AfterimageCount = 3;
        const float SequenceDuration = 3.55f;
        static readonly Color Ink = new(0.09f, 0.086f, 0.071f, 1f);
        static readonly Color Paper = new(0.933f, 0.894f, 0.804f, 1f);

        readonly List<SpriteRenderer> allRenderers = new();
        SpriteRenderer[] sprays = Array.Empty<SpriteRenderer>();
        SpriteRenderer[] residualDrops = Array.Empty<SpriteRenderer>();
        SpriteRenderer[] afterimages = Array.Empty<SpriteRenderer>();
        SpriteRenderer flash;
        SpriteRenderer blob;
        SpriteRenderer splash;
        SpriteRenderer inkRing;
        SpriteRenderer paperRing;
        SpriteRenderer brush;
        SpriteRenderer fibers;
        AssetSet assets;
        Transform poolParent;
        Transform trackedPlayer;
        SpriteRenderer playerRenderer;
        Vector3 ground;
        float height;
        float maximumStrokeLength;
        Coroutine sequence;
        bool built;

        public event Action<InkDropJumpVfxInstance> ReleaseRequested;
        public int BuiltChildCount => transform.childCount;

        /// 풀 팩토리에서 한 번 호출한다. 재호출되어도 기존 자식 구성을 보존한다.
        public void Initialize(Transform newPoolParent, AssetSet newAssets, int sprayCount,
            int residualDropCount)
        {
            poolParent = newPoolParent;
            if (built) return;

            assets = newAssets;
            BuildComposite(Mathf.Max(0, sprayCount), Mathf.Max(0, residualDropCount));
            built = true;
            ResetPlaybackState();
        }

        /// 현재 플레이어와 지면 위치를 기준으로 3.55초 연출을 시작한다.
        public void Play(Transform player, SpriteRenderer sourceRenderer, Vector3 groundPosition,
            float effectHeight, float strokeLength)
        {
            if (!built)
                throw new InvalidOperationException("InkDropJumpVfxInstance.Initialize를 먼저 호출해야 합니다.");

            StopAllCoroutines();
            ResetRenderers();
            trackedPlayer = player;
            playerRenderer = sourceRenderer;
            ground = groundPosition;
            height = Mathf.Max(0.25f, effectHeight);
            maximumStrokeLength = Mathf.Max(0.5f, strokeLength);

            // 활성 연출도 공유 서비스 아래에 둬 Play 중 스크립트 리로드가 발생해도
            // Configure가 멈춘 합성 인스턴스를 찾아 정리할 수 있게 한다.
            transform.SetParent(poolParent, false);
            transform.position = ground;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            sequence = StartCoroutine(PlaySequence());
        }

        public void OnPoolAcquire()
        {
            ResetPlaybackState();
        }

        public void OnPoolRelease()
        {
            ResetPlaybackState();
        }

        void BuildComposite(int sprayCount, int residualDropCount)
        {
            flash = CreateSprite("Impact_CreamFlash");
            blob = CreateSprite("Impact_GroundBlob");
            splash = CreateSprite("Impact_InkSplash");
            inkRing = CreateSprite("ShockRing_Ink");
            paperRing = CreateSprite("ShockRing_Paper");
            brush = CreateSprite("Vertical_Brush");
            fibers = CreateSprite("Vertical_Fibers");

            sprays = new SpriteRenderer[sprayCount];
            for (int i = 0; i < sprays.Length; i++)
                sprays[i] = CreateSprite($"InkSpray_{i:00}");

            residualDrops = new SpriteRenderer[residualDropCount];
            for (int i = 0; i < residualDrops.Length; i++)
                residualDrops[i] = CreateSprite($"ResidualDrop_{i:00}");

            afterimages = new SpriteRenderer[AfterimageCount];
            for (int i = 0; i < afterimages.Length; i++)
                afterimages[i] = CreateSprite($"Afterimage_{i + 1}");
        }

        IEnumerator PlaySequence()
        {
            ConfigureRenderer(flash, assets.SoftFlash, Paper, 8);
            ConfigureRenderer(blob, assets.GroundBlob, Ink, 5);
            ConfigureRenderer(splash, assets.InkSplash, Ink, 6);
            ConfigureRenderer(inkRing, assets.ShockRing, Ink, 5);
            ConfigureRenderer(paperRing, assets.ShockRing,
                new Color(Paper.r, Paper.g, Paper.b, 0.4f), 4);
            ConfigureRenderer(brush, assets.VerticalBrush, Ink, 3);
            ConfigureRenderer(fibers, assets.BrushFibers,
                new Color(Paper.r, Paper.g, Paper.b, 0.24f), 4);

            SetScale(flash, height * 0.15f, height * 0.15f);
            SetScale(blob, height, height * 0.42f);
            SetScale(splash, height * 1.05f, height * 0.48f);
            SetScale(inkRing, height * 0.2f, height * 0.06f);
            SetScale(paperRing, height * 0.18f, height * 0.05f);
            splash.transform.localRotation =
                Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-12f, 12f));
            inkRing.transform.localRotation =
                Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-7f, 7f));

            for (int i = 0; i < sprays.Length; i++)
                StartCoroutine(AnimateSpray(sprays[i], height, i));
            for (int i = 0; i < residualDrops.Length; i++)
                StartCoroutine(AnimateResidualDrop(residualDrops[i], height, i));
            StartCoroutine(AnimateAfterimages());

            float elapsed = 0f;
            while (elapsed < SequenceDuration)
            {
                elapsed += Time.deltaTime;
                float impactT = Mathf.Clamp01(elapsed / 0.6f);
                float quickT = Mathf.Clamp01(elapsed / 0.28f);

                SetScale(flash, height * Mathf.Lerp(0.15f, 1.55f, EaseOut(impactT)),
                    height * Mathf.Lerp(0.15f, 1.55f, EaseOut(impactT)));
                SetAlpha(flash, 1f - Mathf.Clamp01(elapsed / 0.16f));
                SetScale(blob, height * Mathf.Lerp(0.2f, 1f, EaseOut(quickT)), height * 0.42f);
                SetAlpha(blob, 1f - quickT);
                SetScale(splash, height * Mathf.Lerp(0.25f, 1.15f, EaseOut(quickT)),
                    height * 0.48f);
                SetAlpha(splash, 1f - Mathf.Clamp01(elapsed / 0.26f));
                SetScale(inkRing, height * Mathf.Lerp(0.2f, 2.45f, EaseOut(impactT)),
                    height * 0.27f);
                SetScale(paperRing, height * Mathf.Lerp(0.18f, 2.15f, EaseOut(impactT)),
                    height * 0.24f);
                SetAlpha(inkRing, 1f - impactT);
                SetAlpha(paperRing, 0.4f * (1f - impactT));

                UpdateVerticalStroke(elapsed);
                yield return null;
            }

            sequence = null;
            ReleaseRequested?.Invoke(this);
        }

        IEnumerator AnimateSpray(SpriteRenderer sprite, float effectHeight, int index)
        {
            ConfigureRenderer(sprite, assets.InkStreak, Ink, 6);
            float duration = UnityEngine.Random.Range(0.55f, 1.25f);
            float speed = UnityEngine.Random.Range(1.8f, 5.5f) * effectHeight;
            float angle = UnityEngine.Random.Range(-28f, 28f) * Mathf.Deg2Rad;
            Vector3 velocity = new(Mathf.Sin(angle) * speed * 0.35f,
                Mathf.Cos(angle) * speed, 0f);
            float size = UnityEngine.Random.Range(0.025f, 0.075f) * effectHeight;
            sprite.transform.localPosition =
                new Vector3((index % 3 - 1) * 0.04f * effectHeight, 0f, 0f);
            SetScale(sprite, size, size * UnityEngine.Random.Range(2.5f, 5f));

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                velocity *= Mathf.Exp(-3.9f * dt);
                velocity += Vector3.down * (1.4f * effectHeight * dt);
                sprite.transform.position += velocity * dt;
                SetAlpha(sprite, 1f - Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            sprite.enabled = false;
        }

        IEnumerator AnimateResidualDrop(SpriteRenderer sprite, float effectHeight, int index)
        {
            float delay = UnityEngine.Random.Range(0.03f, 0.22f);
            yield return new WaitForSeconds(delay);

            Sprite selectedSprite = assets.InkDrop;
            if (assets.DropletFrames != null && assets.DropletFrames.Length > 0)
            {
                selectedSprite =
                    assets.DropletFrames[UnityEngine.Random.Range(0, assets.DropletFrames.Length)];
            }
            ConfigureRenderer(sprite, selectedSprite, Ink, 5);

            float duration = UnityEngine.Random.Range(1f, 2.3f);
            float angle = UnityEngine.Random.Range(55f, 125f) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(0.45f, 1.6f) * effectHeight;
            Vector3 velocity = new(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);
            float size = UnityEngine.Random.Range(0.018f, 0.055f) * effectHeight;
            sprite.transform.localPosition =
                new Vector3((index % 5 - 2) * 0.035f * effectHeight, 0f, 0f);
            SetScale(sprite, size, size * UnityEngine.Random.Range(0.8f, 1.5f));

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                velocity *= Mathf.Exp(-2.7f * dt);
                velocity += Vector3.down * (0.18f * effectHeight * dt);
                sprite.transform.position += velocity * dt;
                SetAlpha(sprite, Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI));
                yield return null;
            }
            sprite.enabled = false;
        }

        IEnumerator AnimateAfterimages()
        {
            float[] delays = { 0.1f, 0.12f, 0.14f };
            for (int i = 0; i < afterimages.Length; i++)
            {
                yield return new WaitForSeconds(delays[i]);
                if (trackedPlayer == null || playerRenderer == null) yield break;

                var afterimage = afterimages[i];
                ConfigureRenderer(afterimage, playerRenderer.sprite,
                    new Color(Ink.r, Ink.g, Ink.b, 0.13f), playerRenderer.sortingOrder - 1);
                afterimage.flipX = playerRenderer.flipX;
                afterimage.flipY = playerRenderer.flipY;
                afterimage.transform.position = trackedPlayer.position;
                afterimage.transform.localScale = trackedPlayer.localScale;
                StartCoroutine(FadeAfterimage(afterimage, 0.42f));
            }
        }

        static IEnumerator FadeAfterimage(SpriteRenderer sprite, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(sprite, 0.13f * (1f - Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            sprite.enabled = false;
        }

        void UpdateVerticalStroke(float elapsed)
        {
            if (brush == null || fibers == null) return;
            float playerY = trackedPlayer != null ? trackedPlayer.position.y : ground.y;
            float targetY = Mathf.Max(ground.y + 0.05f, playerY - height * 0.45f);
            float length = Mathf.Min(maximumStrokeLength, targetY - ground.y);
            float grow = EaseOut(Mathf.Clamp01(elapsed / 0.43f));
            float shownLength = Mathf.Max(0.01f, length * grow);
            float alpha = elapsed < 1.45f
                ? 1f
                : 1f - Mathf.Clamp01((elapsed - 1.45f) / 2.1f);
            Vector3 center = new(ground.x, ground.y + shownLength * 0.5f, ground.z);

            brush.transform.position = center;
            fibers.transform.position = center;
            SetScale(brush, height * 0.28f, shownLength);
            SetScale(fibers, height * 0.28f, shownLength);
            SetAlpha(brush, alpha);
            SetAlpha(fibers, alpha * 0.24f);
        }

        SpriteRenderer CreateSprite(string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            allRenderers.Add(renderer);
            return renderer;
        }

        static void ConfigureRenderer(SpriteRenderer renderer, Sprite sprite, Color color,
            int sortingOrder)
        {
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = sprite != null;
        }

        void ResetPlaybackState()
        {
            StopAllCoroutines();
            sequence = null;
            trackedPlayer = null;
            playerRenderer = null;
            ground = Vector3.zero;
            height = 1f;
            maximumStrokeLength = 1f;
            ResetRenderers();

            transform.SetParent(poolParent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        void ResetRenderers()
        {
            for (int i = 0; i < allRenderers.Count; i++)
            {
                var renderer = allRenderers[i];
                if (renderer == null) continue;
                renderer.enabled = false;
                renderer.sprite = null;
                renderer.color = Color.clear;
                renderer.sortingOrder = 0;
                renderer.flipX = false;
                renderer.flipY = false;
                renderer.transform.localPosition = Vector3.zero;
                renderer.transform.localRotation = Quaternion.identity;
                renderer.transform.localScale = Vector3.one;
            }
        }

        static void SetScale(SpriteRenderer renderer, float worldWidth, float worldHeight)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector2 size = renderer.sprite.bounds.size;
            if (size.x <= 0f || size.y <= 0f) return;
            renderer.transform.localScale =
                new Vector3(worldWidth / size.x, worldHeight / size.y, 1f);
        }

        static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color color = renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }

        static float EaseOut(float value) => 1f - (1f - value) * (1f - value);

        void OnDisable()
        {
            StopAllCoroutines();
            sequence = null;
        }
    }
}
