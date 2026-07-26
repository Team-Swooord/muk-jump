using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using MukJump.AI;
using MukJump.Items;
using MukJump.Core.Pooling;

namespace MukJump.Core
{
    /// 점프·착지·드로잉·아이템 등 순간 피드백을 한곳에서 관리한다.
    /// 외부 음원 없이 짧은 효과음을 런타임에 합성해 API 키나 추가 에셋 없이 동작한다.
    public class GameFeedbackController : MonoBehaviour
    {
        const int LineVfxCapacity = 8;
        const int SpriteVfxCapacity = 16;

        public static GameFeedbackController Instance { get; private set; }

        AudioClip jumpClip;
        AudioClip landingClip;
        AudioClip drawClip;
        AudioClip invalidClip;
        AudioClip itemClip;
        AudioClip milestoneClip;
        AudioClip brushLoopClip;
        AudioClip brushTransitionClip;
        AudioClip wallHitClip;
        AudioClip deathSqueakClip;
        AudioClip gameOverClip;
        AudioSource brushSource;
        AudioSource accentSource;
        Coroutine gameOverSoundRoutine;
        Coroutine hitStopRoutine;
        Coroutine gamepadHapticRoutine;
        float hitStopPreviousScale = 1f;
        float hitStopPreviousFixedDelta;
        float lastLandingHapticTime = -10f;

        enum HapticPattern
        {
            Landing,
            ShieldBreak,
            Death,
        }

        public float GameOverRevealDelay
        {
            get
            {
                EnsureInitialized();
                return (deathSqueakClip != null ? deathSqueakClip.length : 0.58f) + 0.04f;
            }
        }
        Sprite dotSprite;
        Canvas overlayCanvas;
        Text bannerText;
        Coroutine bannerRoutine;
        Transform transientPoolRoot;
        ComponentPool<TransientVfxElement> lineVfxPool;
        ComponentPool<TransientVfxElement> spriteVfxPool;
        readonly HashSet<TransientVfxElement> leasedLineVfx = new();
        readonly HashSet<TransientVfxElement> leasedSpriteVfx = new();

        void OnEnable()
        {
            Instance = this;
            EnsureInitialized();
        }

        void OnDisable()
        {
            // 코루틴을 먼저 멈춘 뒤 모두 반납해야, 재활성화 후 같은 요소를 다시 빌렸을 때
            // 이전 코루틴이 새 연출의 위치·색을 덮어쓰지 않는다.
            StopAllCoroutines();
            gameOverSoundRoutine = null;
            hitStopRoutine = null;
            gamepadHapticRoutine = null;
            bannerRoutine = null;
            ReturnAllTransientVfx();
            if (Instance == this) Instance = null;
            RestoreTimeScale();
            if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
            if (bannerText != null) bannerText.color = Color.clear;
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (jumpClip != null && brushSource != null && accentSource != null) return;

            jumpClip = CreateTone("JumpBrush", 0.16f, 240f, 520f, 0.18f, 0.04f);
            landingClip = CreateTone("LandingInk", 0.13f, 150f, 82f, 0.24f, 0.18f);
            drawClip = CreateTone("DrawSet", 0.1f, 390f, 320f, 0.12f, 0.08f);
            invalidClip = CreateTone("InvalidStroke", 0.12f, 170f, 125f, 0.16f, 0.2f);
            itemClip = CreateTone("ItemPickup", 0.22f, 420f, 760f, 0.16f, 0.03f);
            milestoneClip = CreateTone("MilestoneSeal", 0.34f, 220f, 440f, 0.2f, 0.08f);
            brushLoopClip = LoadSfx("SFX_Brush_Community") ??
                            LoadSfx("SFX_Brush_Draw_Loop") ??
                            CreateBrushNoise("BrushDrawing", 0.42f, 0.16f);
            brushTransitionClip = LoadSfx("SFX_Brush_Community") ??
                                  LoadSfx("SFX_Brush_Transition") ??
                                  CreateBrushNoise("BrushTransition", 1.15f, 0.3f, true);
            wallHitClip = LoadSfx("SFX_Wall_Hit") ??
                          CreateTone("WallHit", 0.11f, 120f, 72f, 0.28f, 0.32f);
            deathSqueakClip = LoadSfx("SFX_Character_Death_Slime") ??
                              LoadSfx("SFX_Character_Death") ??
                              CreateTone("DeathSqueak", 0.32f, 1080f, 185f, 0.68f, 0.025f);
            gameOverClip = LoadSfx("SFX_Game_Over_Ink_Spill") ??
                           LoadSfx("SFX_Game_Over") ??
                           CreateTone("GameOver", 0.58f, 310f, 92f, 0.42f, 0.08f);
            CreateDedicatedAudioSources();
            if (dotSprite == null) dotSprite = CreateDotSprite();
            if (bannerText == null)
            {
                var existingBanner = transform.Find("FeedbackOverlay/ZoneBanner");
                if (existingBanner != null)
                {
                    bannerText = existingBanner.GetComponent<Text>();
                    overlayCanvas = existingBanner.GetComponentInParent<Canvas>();
                }
            }
        }

        public void StartBrushDrawing()
        {
            EnsureInitialized();
            if (brushSource == null || brushLoopClip == null || brushSource.isPlaying) return;
            brushSource.volume = 0.28f;
            brushSource.pitch = Random.Range(0.94f, 1.04f);
            if (brushSource.timeSamples > 0)
                brushSource.UnPause();
            else
                brushSource.Play();
        }

        public void PlayBrushMovement(float movement)
        {
            EnsureInitialized();
            if (brushSource == null || brushLoopClip == null) return;
            brushSource.volume = Mathf.Lerp(0.24f, 0.4f, Mathf.Clamp01(movement / 0.5f));
            brushSource.pitch = Mathf.Lerp(0.9f, 1.12f, Mathf.Clamp01(movement / 0.5f));
        }

        public void StopBrushDrawing()
        {
            if (brushSource != null && brushSource.isPlaying)
                brushSource.Pause();
        }

        public void PlayBrushTransition()
        {
            EnsureInitialized();
            StopBrushDrawing();
            VfxAudioManager.Instance?.PlayOneShot(brushTransitionClip, 0.78f);
        }

        public void PlayWallHit()
        {
            EnsureInitialized();
            VfxAudioManager.Instance?.PlayOneShot(wallHitClip, 0.78f);
        }

        public void PlayGameOver()
        {
            EnsureInitialized();
            if (gameOverSoundRoutine != null) StopCoroutine(gameOverSoundRoutine);
            gameOverSoundRoutine = StartCoroutine(PlayGameOverAfterDeath());
        }

        public void PlayJump(Vector3 position)
        {
            EnsureInitialized();
            VfxAudioManager.Instance?.PlayOneShot(jumpClip, 0.72f);
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.18f, 0.78f, 0.24f, 0.07f, 0.2f));
            StartCoroutine(AnimateBrushStreak(position + Vector3.down * 0.25f));
        }

        public void PlayLanding(Vector3 position, float impactSpeed)
        {
            EnsureInitialized();
            float strength = Mathf.InverseLerp(2f, 14f, impactSpeed);
            VfxAudioManager.Instance?.PlayOneShot(landingClip, Mathf.Lerp(0.45f, 0.9f, strength));
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.12f,
                Mathf.Lerp(0.55f, 1.05f, strength), 0.28f, 0.09f, 0.12f, 0.35f));
            SpawnDroplets(position, 5 + Mathf.RoundToInt(strength * 4f), InkPalette.Ink);
            if (strength >= 0.34f && Time.unscaledTime - lastLandingHapticTime >= 0.18f)
            {
                lastLandingHapticTime = Time.unscaledTime;
                PlayHaptic(HapticPattern.Landing, strength);
            }
        }

        public void PlayStrokeResolved(Vector3 position, bool valid)
        {
            EnsureInitialized();
            if (valid)
            {
                VfxAudioManager.Instance?.PlayOneShot(drawClip, 0.55f);
                StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.08f, 0.48f, 0.2f, 0.05f, 0.2f));
            }
            else
            {
                VfxAudioManager.Instance?.PlayOneShot(invalidClip, 0.65f);
                StartCoroutine(AnimateInvalidSeal(position));
            }
        }

        public void PlayItemPickup(Vector3 position, ItemType type)
        {
            EnsureInitialized();
            Color color = ItemColor(type);
            VfxAudioManager.Instance?.PlayOneShot(itemClip, 0.72f);
            StartCoroutine(AnimateRing(position, color, 0.2f, 1.15f, 0.38f, 0.08f, 0.15f));
            SpawnDroplets(position, 9, color);
        }

        public void PlayItemTelegraph(Vector3 position, ItemType type)
        {
            EnsureInitialized();
            Color color = ItemColor(type);
            VfxAudioManager.Instance?.PlayOneShot(itemClip, 0.2f);
            StartCoroutine(AnimateRing(position, color, 0.12f, 0.72f, 0.42f, 0.035f, 0.22f));
            StartCoroutine(AnimateRing(position, color, 0.32f, 1.02f, 0.55f, 0.025f, 0.13f));
        }

        static Color ItemColor(ItemType type)
        {
            return type switch
            {
                ItemType.GoldenBrush => InkPalette.Gold,
                ItemType.InkReserve => new Color(0.18f, 0.5f, 0.42f),
                _ => InkPalette.Ink,
            };
        }

        public void PlayDeath(Vector3 position)
        {
            EnsureInitialized();
            StopBrushDrawing();
            PlayAccent(deathSqueakClip, 1f);
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.1f, 1.35f,
                0.42f, 0.12f, 0.75f));
            SpawnDroplets(position, 14, InkPalette.Ink);
            PlayHaptic(HapticPattern.Death, 1f);
        }

        public void PlayShieldBreak()
        {
            PlayHaptic(HapticPattern.ShieldBreak, 1f);
        }

        public void PlayHitStop(float duration = 0.055f)
        {
            if (!isActiveAndEnabled ||
                (GameManager.Instance != null && GameManager.Instance.IsPaused))
                return;
            if (hitStopRoutine != null)
            {
                StopCoroutine(hitStopRoutine);
                RestoreTimeScale();
            }
            hitStopRoutine = StartCoroutine(HitStopRoutine(Mathf.Clamp(duration, 0.02f, 0.09f)));
        }

        /// 일시정지 직전에 실시간 코루틴이 뒤늦게 timeScale을 되살리지 않도록 정리한다.
        public void PrepareForPause()
        {
            StopBrushDrawing();
            if (accentSource != null) accentSource.Stop();
            VfxAudioManager.Instance?.StopAll();
            if (hitStopRoutine != null)
            {
                StopCoroutine(hitStopRoutine);
                hitStopRoutine = null;
                RestoreTimeScale();
            }
            if (gamepadHapticRoutine != null)
            {
                StopCoroutine(gamepadHapticRoutine);
                gamepadHapticRoutine = null;
            }
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }

        public void ShowZone(string title, string subtitle)
        {
            EnsureInitialized();
            EnsureOverlay();
            VfxAudioManager.Instance?.PlayOneShot(milestoneClip, 0.72f);
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(AnimateBanner(title, subtitle));
        }

        IEnumerator AnimateRing(Vector3 position, Color color, float startRadius, float endRadius,
            float duration, float width, float startAlpha, float yScale = 1f)
        {
            var element = TryAcquireLineVfx("FeedbackRing");
            if (element == null) yield break;
            element.transform.position = position;
            element.transform.localScale = new Vector3(1f, yScale, 1f);
            var line = element.UseLine();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 32;
            line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 12;
            line.startWidth = line.endWidth = width;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float radius = Mathf.Lerp(startRadius, endRadius, 1f - Mathf.Pow(1f - t, 3f));
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / line.positionCount;
                    float wobble = 1f + Mathf.Sin(angle * 5f + t * 8f) * 0.035f;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                        radius * wobble);
                }
                color.a = Mathf.Lerp(startAlpha, 0f, t);
                line.startColor = line.endColor = color;
                yield return null;
            }
            ReleaseLineVfx(element);
        }

        IEnumerator HitStopRoutine(float duration)
        {
            hitStopPreviousScale = Mathf.Max(0.01f, Time.timeScale);
            hitStopPreviousFixedDelta = Time.fixedDeltaTime;
            Time.timeScale = 0.05f;
            Time.fixedDeltaTime = hitStopPreviousFixedDelta * Time.timeScale /
                                  hitStopPreviousScale;
            yield return new WaitForSecondsRealtime(duration);
            RestoreTimeScale();
            hitStopRoutine = null;
        }

        void RestoreTimeScale()
        {
            if (hitStopPreviousFixedDelta > 0f)
                Time.fixedDeltaTime = hitStopPreviousFixedDelta;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
                Time.timeScale = 0f;
            else if (Time.timeScale <= 0.051f)
                Time.timeScale = Mathf.Max(0.01f, hitStopPreviousScale);
            hitStopPreviousFixedDelta = 0f;
        }

        void PlayHaptic(HapticPattern pattern, float strength)
        {
            int durationMs = pattern switch
            {
                HapticPattern.Landing => Mathf.RoundToInt(Mathf.Lerp(18f, 34f, strength)),
                HapticPattern.ShieldBreak => 82,
                _ => 165,
            };
            int amplitude = pattern switch
            {
                HapticPattern.Landing => Mathf.RoundToInt(Mathf.Lerp(45f, 85f, strength)),
                HapticPattern.ShieldBreak => 145,
                _ => 220,
            };

#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(durationMs, amplitude);
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif

            var gamepad = Gamepad.current;
            if (gamepad == null) return;
            if (gamepadHapticRoutine != null) StopCoroutine(gamepadHapticRoutine);
            float low = pattern == HapticPattern.Death ? 0.78f :
                pattern == HapticPattern.ShieldBreak ? 0.5f : 0.18f + strength * 0.18f;
            float high = pattern == HapticPattern.Death ? 0.34f :
                pattern == HapticPattern.ShieldBreak ? 0.72f : 0.12f + strength * 0.16f;
            gamepad.SetMotorSpeeds(low, high);
            gamepadHapticRoutine = StartCoroutine(StopGamepadHaptic(gamepad, durationMs / 1000f));
        }

        IEnumerator StopGamepadHaptic(Gamepad gamepad, float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            gamepad?.SetMotorSpeeds(0f, 0f);
            gamepadHapticRoutine = null;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void VibrateAndroid(int durationMs, int amplitude)
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var vibrator =
                    activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                int sdk = version.GetStatic<int>("SDK_INT");
                if (sdk >= 26)
                {
                    using var vibrationEffect =
                        new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                        "createOneShot", (long)durationMs, Mathf.Clamp(amplitude, 1, 255));
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", (long)durationMs);
                }
            }
            catch (System.Exception)
            {
                Handheld.Vibrate();
            }
        }
#endif

        IEnumerator AnimateBrushStreak(Vector3 position)
        {
            var element = TryAcquireLineVfx("JumpBrushStreak");
            if (element == null) yield break;
            element.transform.position = position;
            var line = element.UseLine();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = 4;
            line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 11;
            line.startWidth = 0.16f;
            line.endWidth = 0.025f;
            line.SetPosition(0, new Vector3(-0.12f, -0.35f));
            line.SetPosition(1, new Vector3(0.05f, -0.08f));
            line.SetPosition(2, new Vector3(-0.04f, 0.24f));
            line.SetPosition(3, new Vector3(0.08f, 0.55f));
            float elapsed = 0f;
            while (elapsed < 0.24f)
            {
                elapsed += Time.deltaTime;
                Color color = InkPalette.Ink;
                color.a = 1f - elapsed / 0.24f;
                line.startColor = line.endColor = color;
                yield return null;
            }
            ReleaseLineVfx(element);
        }

        IEnumerator AnimateInvalidSeal(Vector3 position)
        {
            var slashes = new TransientVfxElement[2];
            int acquiredCount = 0;
            for (int i = 0; i < 2; i++)
            {
                var element = TryAcquireLineVfx($"InvalidStrokeSlash_{i + 1}");
                if (element == null) continue;
                slashes[i] = element;
                acquiredCount++;
                element.transform.position = position;
                var line = element.UseLine();
                line.useWorldSpace = false;
                line.loop = false;
                line.positionCount = 2;
                line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
                line.sortingOrder = 13;
                line.startWidth = line.endWidth = 0.1f;
                float sign = i == 0 ? 1f : -1f;
                line.SetPosition(0, new Vector3(-0.28f, -0.28f * sign));
                line.SetPosition(1, new Vector3(0.28f, 0.28f * sign));
                line.startColor = line.endColor = InkPalette.Red;
            }
            if (acquiredCount == 0) yield break;
            float elapsed = 0f;
            while (elapsed < 0.38f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.38f);
                float scale = Mathf.Lerp(0.55f, 1f, t);
                for (int i = 0; i < slashes.Length; i++)
                    if (slashes[i] != null)
                        slashes[i].transform.localScale = Vector3.one * scale;
                yield return null;
            }
            for (int i = 0; i < slashes.Length; i++)
                ReleaseLineVfx(slashes[i]);
        }

        void SpawnDroplets(Vector3 position, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                var element = TryAcquireSpriteVfx("FeedbackDroplet");
                if (element == null) break;
                StartCoroutine(AnimateDroplet(element, position, color, i, count));
            }
        }

        IEnumerator AnimateDroplet(TransientVfxElement element, Vector3 position, Color color,
            int index, int count)
        {
            element.transform.position = position;
            var renderer = element.UseSprite();
            renderer.sprite = dotSprite;
            renderer.sortingOrder = 12;
            renderer.color = color;
            float angle = Mathf.Lerp(20f, 160f, (index + 0.5f) / count) * Mathf.Deg2Rad;
            float speed = Random.Range(1.1f, 2.5f);
            Vector3 velocity = new(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);
            float scale = Random.Range(0.035f, 0.085f);
            element.transform.localScale = Vector3.one * scale;
            float elapsed = 0f;
            while (elapsed < 0.45f)
            {
                elapsed += Time.deltaTime;
                velocity += Vector3.down * (4.5f * Time.deltaTime);
                element.transform.position += velocity * Time.deltaTime;
                color.a = 1f - elapsed / 0.45f;
                renderer.color = color;
                yield return null;
            }
            ReleaseSpriteVfx(element);
        }

        TransientVfxElement TryAcquireLineVfx(string objectName)
        {
            EnsureTransientPools();
            if (lineVfxPool.LeasedCount >= LineVfxCapacity) return null;
            var element = lineVfxPool.Acquire();
            element.gameObject.name = objectName;
            leasedLineVfx.Add(element);
            return element;
        }

        TransientVfxElement TryAcquireSpriteVfx(string objectName)
        {
            EnsureTransientPools();
            if (spriteVfxPool.LeasedCount >= SpriteVfxCapacity) return null;
            var element = spriteVfxPool.Acquire();
            element.gameObject.name = objectName;
            leasedSpriteVfx.Add(element);
            return element;
        }

        void ReleaseLineVfx(TransientVfxElement element)
        {
            if (element == null || !leasedLineVfx.Remove(element)) return;
            lineVfxPool?.Release(element);
        }

        void ReleaseSpriteVfx(TransientVfxElement element)
        {
            if (element == null || !leasedSpriteVfx.Remove(element)) return;
            spriteVfxPool?.Release(element);
        }

        void EnsureTransientPools()
        {
            if (transientPoolRoot == null)
            {
                var existing = transform.Find("TransientFeedbackPool");
                if (existing != null)
                    transientPoolRoot = existing;
                else
                {
                    var root = new GameObject("TransientFeedbackPool");
                    root.transform.SetParent(transform, false);
                    transientPoolRoot = root.transform;
                }
            }

            bool rebuildPools = lineVfxPool == null || spriteVfxPool == null;
            lineVfxPool ??= new ComponentPool<TransientVfxElement>(
                () => CreateTransientElement("TransientLineVfx"), LineVfxCapacity);
            spriteVfxPool ??= new ComponentPool<TransientVfxElement>(
                () => CreateTransientElement("TransientSpriteVfx"), SpriteVfxCapacity);

            if (!rebuildPools) return;
            var existingElements =
                transientPoolRoot.GetComponentsInChildren<TransientVfxElement>(true);
            for (int i = 0; i < existingElements.Length; i++)
            {
                var element = existingElements[i];
                if (element.GetComponent<SpriteRenderer>() != null)
                    spriteVfxPool.Adopt(element);
                else
                    lineVfxPool.Adopt(element);
            }
        }

        TransientVfxElement CreateTransientElement(string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transientPoolRoot, false);
            return go.AddComponent<TransientVfxElement>();
        }

        void ReturnAllTransientVfx()
        {
            if (lineVfxPool != null)
                foreach (var element in leasedLineVfx)
                    lineVfxPool.Release(element);
            leasedLineVfx.Clear();

            if (spriteVfxPool != null)
                foreach (var element in leasedSpriteVfx)
                    spriteVfxPool.Release(element);
            leasedSpriteVfx.Clear();
        }

        void CreateOverlay()
        {
            var canvasObject = new GameObject("FeedbackOverlay", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            overlayCanvas = canvasObject.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 140;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            var textObject = new GameObject("ZoneBanner", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(canvasObject.transform, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.78f);
            rect.sizeDelta = new Vector2(860f, 180f);
            bannerText = textObject.GetComponent<Text>();
            ConfigureBannerText();
            bannerText.color = Color.clear;
        }

        void EnsureOverlay()
        {
            if (bannerText != null) return;
            var existingBanner = transform.Find("FeedbackOverlay/ZoneBanner");
            if (existingBanner != null)
            {
                bannerText = existingBanner.GetComponent<Text>();
                overlayCanvas = existingBanner.GetComponentInParent<Canvas>();
                ConfigureBannerText();
                return;
            }
            CreateOverlay();
        }

        IEnumerator AnimateBanner(string title, string subtitle)
        {
            bannerText.text = $"{title}\n<size=30>{subtitle}</size>";
            float duration = 2.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Min(Mathf.InverseLerp(0f, 0.16f, t),
                    1f - Mathf.InverseLerp(0.72f, 1f, t));
                Color color = InkPalette.Ink;
                color.a = alpha;
                bannerText.color = color;
                yield return null;
            }
            bannerText.color = Color.clear;
            bannerRoutine = null;
        }

        void ConfigureBannerText()
        {
            if (bannerText == null) return;
            bannerText.font = InkPalette.UiFont;
            bannerText.fontSize = 48;
            bannerText.fontStyle = FontStyle.Bold;
            bannerText.alignment = TextAnchor.MiddleCenter;
            bannerText.resizeTextForBestFit = false;
            bannerText.alignByGeometry = true;
            bannerText.raycastTarget = false;
            var rect = bannerText.rectTransform;
            rect.sizeDelta = new Vector2(860f, 180f);
        }

        static AudioClip CreateTone(string name, float duration, float startFrequency,
            float endFrequency, float volume, float noiseAmount)
        {
            const int sampleRate = 44100;
            int count = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += frequency / sampleRate * Mathf.PI * 2f;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) *
                                 Mathf.Pow(1f - t, 0.7f);
                float tonal = Mathf.Sin(phase) * (1f - noiseAmount);
                float noise = Random.Range(-1f, 1f) * noiseAmount;
                samples[i] = (tonal + noise) * envelope * volume;
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static AudioClip LoadSfx(string fileName)
        {
            return Resources.Load<AudioClip>($"MukJump/Audio/SFX/{fileName}");
        }

        void CreateDedicatedAudioSources()
        {
            var brushTransform = transform.Find("BrushDrawingAudio");
            var sourceObject = brushTransform != null
                ? brushTransform.gameObject
                : new GameObject("BrushDrawingAudio");
            if (brushTransform == null) sourceObject.transform.SetParent(transform, false);
            brushSource = sourceObject.GetComponent<AudioSource>();
            if (brushSource == null) brushSource = sourceObject.AddComponent<AudioSource>();
            brushSource.playOnAwake = false;
            brushSource.loop = true;
            brushSource.spatialBlend = 0f;
            brushSource.clip = brushLoopClip;

            var accentTransform = transform.Find("PriorityAccentAudio");
            var accentObject = accentTransform != null
                ? accentTransform.gameObject
                : new GameObject("PriorityAccentAudio");
            if (accentTransform == null) accentObject.transform.SetParent(transform, false);
            accentSource = accentObject.GetComponent<AudioSource>();
            if (accentSource == null) accentSource = accentObject.AddComponent<AudioSource>();
            accentSource.playOnAwake = false;
            accentSource.loop = false;
            accentSource.spatialBlend = 0f;
            accentSource.priority = 32;
        }

        void PlayAccent(AudioClip clip, float volume)
        {
            if (accentSource == null || clip == null) return;
            accentSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        IEnumerator PlayGameOverAfterDeath()
        {
            // 마지막 캐릭터의 짧은 "찍" 사망음이 먼저 온전히 들린 뒤 종료음을 붙인다.
            yield return new WaitForSecondsRealtime(GameOverRevealDelay);
            PlayAccent(gameOverClip, 0.74f);
            gameOverSoundRoutine = null;
        }

        static AudioClip CreateBrushNoise(string name, float duration, float volume,
            bool fadeOut = false)
        {
            const int sampleRate = 44100;
            int count = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[count];
            float filtered = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float grain = Random.Range(-1f, 1f);
                filtered = Mathf.Lerp(filtered, grain, 0.18f);
                float bristle = Mathf.Sin(t * Mathf.PI * 2f * 23f) * 0.12f;
                float envelope = fadeOut
                    ? Mathf.Sin(Mathf.PI * t) * Mathf.Pow(1f - t, 0.28f)
                    : 0.72f + Mathf.Sin(t * Mathf.PI * 2f * 3f) * 0.18f;
                samples[i] = (filtered * 0.88f + bristle) * envelope * volume;
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static Sprite CreateDotSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeInkDot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                byte alpha = (byte)(Mathf.Clamp01(1f - Mathf.InverseLerp(0.72f, 1f, distance)) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size);
        }
    }
}
