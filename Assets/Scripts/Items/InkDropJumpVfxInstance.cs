using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;
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

        struct SprayMotion
        {
            public Vector3 Velocity;
            public float Age;
            public float Duration;
            public bool Active;
        }

        struct ResidualMotion
        {
            public Vector3 Velocity;
            public Vector3 LocalPosition;
            public Sprite Sprite;
            public float Width;
            public float Height;
            public float StartTime;
            public float Age;
            public float Duration;
            public bool Active;
            public bool Started;
        }

        struct AfterimageMotion
        {
            public float StartTime;
            public float Age;
            public bool Active;
            public bool Started;
        }

        readonly List<SpriteRenderer> allRenderers = new();
        SpriteRenderer[] sprays = Array.Empty<SpriteRenderer>();
        SpriteRenderer[] residualDrops = Array.Empty<SpriteRenderer>();
        SpriteRenderer[] afterimages = Array.Empty<SpriteRenderer>();
        SprayMotion[] sprayMotions = Array.Empty<SprayMotion>();
        ResidualMotion[] residualMotions = Array.Empty<ResidualMotion>();
        AfterimageMotion[] afterimageMotions = Array.Empty<AfterimageMotion>();
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
            StartCoroutine(PlaySequence());
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
            sprayMotions = new SprayMotion[sprayCount];
            for (int i = 0; i < sprays.Length; i++)
                sprays[i] = CreateSprite($"InkSpray_{i:00}");

            residualDrops = new SpriteRenderer[residualDropCount];
            residualMotions = new ResidualMotion[residualDropCount];
            for (int i = 0; i < residualDrops.Length; i++)
                residualDrops[i] = CreateSprite($"ResidualDrop_{i:00}");

            afterimages = new SpriteRenderer[AfterimageCount];
            afterimageMotions = new AfterimageMotion[AfterimageCount];
            for (int i = 0; i < afterimages.Length; i++)
                afterimages[i] = CreateSprite($"Afterimage_{i + 1}");
        }

        IEnumerator PlaySequence()
        {
            ConfigureRenderer(flash, assets.SoftFlash, Paper, 8);
            ConfigureRenderer(blob, assets.GroundBlob, Ink, 5);
            ConfigureRenderer(splash, assets.InkSplash, Ink, 6);
            ConfigureRenderer(inkRing, assets.ShockRing, Ink, 5);
            ConfigureRenderer(paperRing,
                VfxQualityRuntime.Tier >= VfxQualityTier.Medium
                    ? assets.ShockRing
                    : null,
                new Color(Paper.r, Paper.g, Paper.b, 0.4f), 4);
            ConfigureRenderer(brush, assets.VerticalBrush, Ink, 3);
            ConfigureRenderer(fibers,
                VfxQualityRuntime.Tier >= VfxQualityTier.Medium
                    ? assets.BrushFibers
                    : null,
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

            PrepareMotionStates();

            float elapsed = 0f;
            while (elapsed < SequenceDuration)
            {
                float delta = Time.deltaTime;
                elapsed += delta;
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
                UpdateSprays(delta);
                UpdateResidualDrops(elapsed, delta);
                UpdateAfterimages(elapsed, delta);
                yield return null;
            }

            ReleaseRequested?.Invoke(this);
        }

        void PrepareMotionStates()
        {
            var profile = VfxQualityRuntime.Profile;
            int activeSprayCount = profile.ScaleDecorativeCount(
                sprays.Length,
                Mathf.Min(8, sprays.Length));
            for (int i = 0; i < sprays.Length; i++)
            {
                var sprite = sprays[i];
                if (i >= activeSprayCount)
                {
                    sprite.enabled = false;
                    sprayMotions[i] = default;
                    continue;
                }

                ConfigureRenderer(sprite, assets.InkStreak, Ink, 6);
                float speed = UnityEngine.Random.Range(1.8f, 5.5f) * height;
                float angle = UnityEngine.Random.Range(-28f, 28f) * Mathf.Deg2Rad;
                float size = UnityEngine.Random.Range(0.025f, 0.075f) * height;
                sprite.transform.localPosition =
                    new Vector3((i % 3 - 1) * 0.04f * height, 0f, 0f);
                SetScale(sprite, size, size * UnityEngine.Random.Range(2.5f, 5f));
                sprayMotions[i] = new SprayMotion
                {
                    Velocity = new Vector3(
                        Mathf.Sin(angle) * speed * 0.35f,
                        Mathf.Cos(angle) * speed,
                        0f),
                    Duration = UnityEngine.Random.Range(0.55f, 1.25f),
                    Active = true,
                };
            }

            int activeResidualCount = profile.ScaleDecorativeCount(
                residualDrops.Length,
                Mathf.Min(6, residualDrops.Length));
            for (int i = 0; i < residualDrops.Length; i++)
            {
                var sprite = residualDrops[i];
                if (i >= activeResidualCount)
                {
                    sprite.enabled = false;
                    residualMotions[i] = default;
                    continue;
                }

                Sprite selectedSprite = assets.InkDrop;
                if (assets.DropletFrames != null && assets.DropletFrames.Length > 0)
                {
                    selectedSprite =
                        assets.DropletFrames[
                            UnityEngine.Random.Range(0, assets.DropletFrames.Length)];
                }

                float angle = UnityEngine.Random.Range(55f, 125f) * Mathf.Deg2Rad;
                float speed = UnityEngine.Random.Range(0.45f, 1.6f) * height;
                float size = UnityEngine.Random.Range(0.018f, 0.055f) * height;
                residualMotions[i] = new ResidualMotion
                {
                    Velocity = new Vector3(
                        Mathf.Cos(angle) * speed,
                        Mathf.Sin(angle) * speed,
                        0f),
                    LocalPosition =
                        new Vector3((i % 5 - 2) * 0.035f * height, 0f, 0f),
                    Sprite = selectedSprite,
                    Width = size,
                    Height = size * UnityEngine.Random.Range(0.8f, 1.5f),
                    StartTime = UnityEngine.Random.Range(0.03f, 0.22f),
                    Duration = UnityEngine.Random.Range(1f, 2.3f),
                    Active = true,
                };
            }

            int activeAfterimageCount = VfxQualityRuntime.Tier switch
            {
                VfxQualityTier.Low => 1,
                VfxQualityTier.Medium => 2,
                _ => afterimages.Length,
            };
            for (int i = 0; i < afterimages.Length; i++)
            {
                afterimages[i].enabled = false;
                afterimageMotions[i] = i < activeAfterimageCount
                    ? new AfterimageMotion
                    {
                        StartTime = 0.1f + i * 0.13f,
                        Active = true,
                    }
                    : default;
            }
        }

        void UpdateSprays(float delta)
        {
            for (int i = 0; i < sprays.Length; i++)
            {
                var motion = sprayMotions[i];
                if (!motion.Active) continue;
                motion.Age += delta;
                motion.Velocity *= Mathf.Exp(-3.9f * delta);
                motion.Velocity += Vector3.down * (1.4f * height * delta);
                sprays[i].transform.position += motion.Velocity * delta;
                SetAlpha(sprays[i], 1f - Mathf.Clamp01(motion.Age / motion.Duration));
                if (motion.Age >= motion.Duration)
                {
                    motion.Active = false;
                    sprays[i].enabled = false;
                }
                sprayMotions[i] = motion;
            }
        }

        void UpdateResidualDrops(float sequenceElapsed, float delta)
        {
            for (int i = 0; i < residualDrops.Length; i++)
            {
                var motion = residualMotions[i];
                if (!motion.Active) continue;
                var sprite = residualDrops[i];
                if (!motion.Started)
                {
                    if (sequenceElapsed < motion.StartTime) continue;
                    motion.Started = true;
                    ConfigureRenderer(sprite, motion.Sprite, Ink, 5);
                    sprite.transform.localPosition = motion.LocalPosition;
                    SetScale(sprite, motion.Width, motion.Height);
                }

                motion.Age += delta;
                motion.Velocity *= Mathf.Exp(-2.7f * delta);
                motion.Velocity += Vector3.down * (0.18f * height * delta);
                sprite.transform.position += motion.Velocity * delta;
                SetAlpha(
                    sprite,
                    Mathf.Sin(Mathf.Clamp01(motion.Age / motion.Duration) * Mathf.PI));
                if (motion.Age >= motion.Duration)
                {
                    motion.Active = false;
                    sprite.enabled = false;
                }
                residualMotions[i] = motion;
            }
        }

        void UpdateAfterimages(float sequenceElapsed, float delta)
        {
            for (int i = 0; i < afterimages.Length; i++)
            {
                var motion = afterimageMotions[i];
                if (!motion.Active) continue;
                var afterimage = afterimages[i];
                if (!motion.Started)
                {
                    if (sequenceElapsed < motion.StartTime) continue;
                    if (trackedPlayer == null || playerRenderer == null)
                    {
                        motion.Active = false;
                        afterimageMotions[i] = motion;
                        continue;
                    }

                    motion.Started = true;
                    ConfigureRenderer(
                        afterimage,
                        playerRenderer.sprite,
                        new Color(Ink.r, Ink.g, Ink.b, 0.13f),
                        playerRenderer.sortingOrder - 1);
                    afterimage.flipX = playerRenderer.flipX;
                    afterimage.flipY = playerRenderer.flipY;
                    afterimage.transform.position = trackedPlayer.position;
                    afterimage.transform.localScale = trackedPlayer.localScale;
                }

                motion.Age += delta;
                SetAlpha(
                    afterimage,
                    0.13f * (1f - Mathf.Clamp01(motion.Age / 0.42f)));
                if (motion.Age >= 0.42f)
                {
                    motion.Active = false;
                    afterimage.enabled = false;
                }
                afterimageMotions[i] = motion;
            }
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
        }
    }
}
