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
    /// 내장 효과음을 우선 사용하고, 누락된 소리는 런타임 합성 폴백으로 보완한다.
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
        float lastLandingFeedbackTime = -10f;
        float lastJumpFeedbackTime = -10f;
        float lastDeathFeedbackTime = -10f;
        float lastHitStopRequestTime = -10f;
        float lastWallHitFeedbackTime = -10f;
        float lastDamageHitFeedbackTime = -10f;

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
        Text bannerText;
        RectTransform bannerSafeAreaRoot;
        RectTransform bannerRect;
        Coroutine bannerRoutine;
        int lastOverlayScreenWidth;
        int lastOverlayScreenHeight;
        Rect lastOverlaySafeArea;
        Transform transientPoolRoot;
        ComponentPool<TransientVfxElement> lineVfxPool;
        ComponentPool<TransientVfxElement> spriteVfxPool;
        readonly HashSet<TransientVfxElement> leasedLineVfx = new();
        readonly HashSet<TransientVfxElement> leasedSpriteVfx = new();
        readonly List<AudioClip> ownedRuntimeClips = new();

        public int ActiveLineVfxCount => lineVfxPool?.LeasedCount ?? 0;
        public int ActiveSpriteVfxCount => spriteVfxPool?.LeasedCount ?? 0;

        void OnEnable()
        {
            Instance = this;
            EnsureInitialized();
            PrewarmTransientPools();
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
            DisposeRuntimeAssets();
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void Update()
        {
            if (bannerText == null) return;
            if (lastOverlayScreenWidth != Screen.width ||
                lastOverlayScreenHeight != Screen.height ||
                lastOverlaySafeArea != Screen.safeArea)
                ApplyOverlayLayout();
        }

        void OnDestroy()
        {
            DisposeRuntimeAssets();
        }

        void DisposeRuntimeAssets()
        {
            if (brushSource != null)
            {
                brushSource.Stop();
                brushSource.clip = null;
            }
            accentSource?.Stop();

            for (int i = 0; i < ownedRuntimeClips.Count; i++)
                DestroyOwnedObject(ownedRuntimeClips[i]);
            ownedRuntimeClips.Clear();

            if (dotSprite != null)
            {
                Texture2D texture = dotSprite.texture;
                DestroyOwnedObject(dotSprite);
                DestroyOwnedObject(texture);
                dotSprite = null;
            }

            jumpClip = null;
            landingClip = null;
            drawClip = null;
            invalidClip = null;
            itemClip = null;
            milestoneClip = null;
            brushLoopClip = null;
            brushTransitionClip = null;
            wallHitClip = null;
            deathSqueakClip = null;
            gameOverClip = null;
        }

        void EnsureInitialized()
        {
            if (jumpClip != null && brushSource != null && accentSource != null) return;

            jumpClip = CreateOwnedTone("JumpBrush", 0.16f, 240f, 520f, 0.18f, 0.04f);
            landingClip = CreateOwnedTone("LandingInk", 0.13f, 150f, 82f, 0.24f, 0.18f);
            drawClip = CreateOwnedTone("DrawSet", 0.1f, 390f, 320f, 0.12f, 0.08f);
            invalidClip = CreateOwnedTone("InvalidStroke", 0.12f, 170f, 125f, 0.16f, 0.2f);
            itemClip = CreateOwnedTone("ItemPickup", 0.22f, 420f, 760f, 0.16f, 0.03f);
            milestoneClip = CreateOwnedTone(
                "MilestoneSeal", 0.34f, 220f, 440f, 0.2f, 0.08f);
            brushLoopClip = LoadSfx("SFX_Brush_Community") ??
                            LoadSfx("SFX_Brush_Draw_Loop") ??
                            CreateOwnedBrushNoise("BrushDrawing", 0.42f, 0.16f);
            brushTransitionClip = LoadSfx("SFX_Brush_Community") ??
                                  LoadSfx("SFX_Brush_Transition") ??
                                  CreateOwnedBrushNoise(
                                      "BrushTransition", 1.15f, 0.3f, true);
            wallHitClip = LoadSfx("SFX_Wall_Hit") ??
                          CreateOwnedTone("WallHit", 0.11f, 120f, 72f, 0.28f, 0.32f);
            deathSqueakClip = LoadSfx("SFX_Character_Death_Slime") ??
                              LoadSfx("SFX_Character_Death") ??
                              CreateOwnedTone(
                                  "DeathSqueak", 0.32f, 1080f, 185f, 0.68f, 0.025f);
            gameOverClip = LoadSfx("SFX_Game_Over_Ink_Spill") ??
                           LoadSfx("SFX_Game_Over") ??
                           CreateOwnedTone(
                               "GameOver", 0.58f, 310f, 92f, 0.42f, 0.08f);
            CreateDedicatedAudioSources();
            if (dotSprite == null) dotSprite = CreateDotSprite();
            if (bannerText == null)
            {
                var existingBanner =
                    transform.Find("FeedbackOverlay/SafeAreaRoot/ZoneBanner") ??
                    transform.Find("FeedbackOverlay/ZoneBanner");
                if (existingBanner != null)
                {
                    bannerText = existingBanner.GetComponent<Text>();
                }
            }
        }

        public void StartBrushDrawing()
        {
            EnsureInitialized();
            if (brushSource == null || brushLoopClip == null || brushSource.isPlaying) return;
            brushSource.volume = 0.28f * LobbySettingsProfile.SfxVolume;
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
            brushSource.volume =
                Mathf.Lerp(0.24f, 0.4f, Mathf.Clamp01(movement / 0.5f)) *
                LobbySettingsProfile.SfxVolume;
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

        public void PlayWallHit(Vector3 position, float inwardDirection)
        {
            EnsureInitialized();
            if (Time.unscaledTime - lastWallHitFeedbackTime < 0.08f) return;
            lastWallHitFeedbackTime = Time.unscaledTime;
            VfxAudioManager.Instance?.PlayOneShot(wallHitClip, 0.78f);
            StartCoroutine(AnimateWallImpact(position, Mathf.Sign(inwardDirection)));
            SpawnDroplets(
                position,
                5,
                InkPalette.Ink,
                VfxImportance.Normal,
                2);
        }

        /// 실제 체력이 줄어든 순간만 짧은 붉은 링·충돌음·약한 진동으로 알린다.
        /// 먹떼가 동시에 맞아도 공용 풀과 오디오 채널을 분신 수만큼 소모하지 않는다.
        public void PlayDamageHit(Vector3 position)
        {
            EnsureInitialized();
            if (Time.unscaledTime - lastDamageHitFeedbackTime < 0.07f) return;
            lastDamageHitFeedbackTime = Time.unscaledTime;
            VfxAudioManager.Instance?.PlayOneShot(wallHitClip, 0.56f);
            StartCoroutine(AnimateRing(
                position,
                InkPalette.Red,
                0.1f,
                0.76f,
                0.22f,
                0.075f,
                0.82f,
                0.62f,
                VfxImportance.Important));
            SpawnDroplets(
                position,
                2,
                InkPalette.Red,
                VfxImportance.Decorative,
                1);
            PlayHaptic(HapticPattern.Landing, 0.42f);
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
            // 먹떼가 거의 동시에 점프할 때 동일 피드백을 한 번으로 묶어
            // 소리 채널과 순간 풀을 분신 수만큼 소모하지 않는다.
            if (Time.unscaledTime - lastJumpFeedbackTime < 0.045f) return;
            lastJumpFeedbackTime = Time.unscaledTime;
            VfxAudioManager.Instance?.PlayOneShot(jumpClip, 0.72f);
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.18f, 0.78f,
                0.24f, 0.07f, 0.2f));
            StartCoroutine(AnimateBrushStreak(position + Vector3.down * 0.25f));
        }

        public void PlayLanding(Vector3 position, float impactSpeed)
        {
            EnsureInitialized();
            if (Time.unscaledTime - lastLandingFeedbackTime < 0.06f) return;
            lastLandingFeedbackTime = Time.unscaledTime;
            float strength = Mathf.InverseLerp(2f, 14f, impactSpeed);
            VfxAudioManager.Instance?.PlayOneShot(landingClip, Mathf.Lerp(0.45f, 0.9f, strength));
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.12f,
                Mathf.Lerp(0.55f, 1.05f, strength), 0.28f, 0.09f, 0.12f, 0.35f));
            SpawnDroplets(
                position,
                5 + Mathf.RoundToInt(strength * 4f),
                InkPalette.Ink,
                VfxImportance.Decorative);
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
                StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.08f, 0.48f,
                    0.2f, 0.05f, 0.2f, 1f, VfxImportance.Decorative));
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
            StartCoroutine(AnimateRing(position, color, 0.2f, 1.15f,
                0.38f, 0.08f, 0.15f, 1f, VfxImportance.Important));
            StartCoroutine(AnimateItemSignature(position, type, color));
            SpawnDroplets(position, 9, color, VfxImportance.Normal, 4);
        }

        public void PlayItemTelegraph(Vector3 position, ItemType type)
        {
            EnsureInitialized();
            Color color = ItemColor(type);
            VfxAudioManager.Instance?.PlayOneShot(itemClip, 0.2f);
            StartCoroutine(AnimateRing(position, color, 0.12f, 0.72f,
                0.42f, 0.035f, 0.22f, 1f, VfxImportance.Important));
            if (VfxQualityRuntime.Tier >= VfxQualityTier.Medium)
                StartCoroutine(AnimateRing(position, color, 0.32f, 1.02f,
                    0.55f, 0.025f, 0.13f, 1f, VfxImportance.Decorative));
        }

        static Color ItemColor(ItemType type)
        {
            return type switch
            {
                ItemType.GoldenBrush => InkPalette.Gold,
                _ => InkPalette.Ink,
            };
        }

        public void PlayDeath(Vector3 position, bool force = false)
        {
            EnsureInitialized();
            // 같은 장애물에 먹떼가 한 물리 프레임에 닿아도 소리·진동·VFX는 한 번만 낸다.
            // 단, 마지막 목숨은 결과창 전에 반드시 사망 피드백을 들려준다.
            if (!force && Time.unscaledTime - lastDeathFeedbackTime < 0.14f) return;
            lastDeathFeedbackTime = Time.unscaledTime;
            StopBrushDrawing();
            PlayAccent(deathSqueakClip, 1f);
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.1f, 1.35f,
                0.42f, 0.12f, 0.75f, 1f, VfxImportance.Critical));
            SpawnDroplets(
                position,
                4,
                InkPalette.Ink,
                VfxImportance.Critical,
                4);
            SpawnDroplets(
                position,
                10,
                InkPalette.Ink,
                VfxImportance.Decorative);
            PlayHaptic(HapticPattern.Death, 1f);
        }

        public void PlayShieldBreak(Vector3 position)
        {
            EnsureInitialized();
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.5f, 1.42f,
                0.36f, 0.09f, 0.72f, 0.9f, VfxImportance.Important));
            SpawnDroplets(
                position,
                8,
                InkPalette.Ink,
                VfxImportance.Important,
                4);
            PlayHaptic(HapticPattern.ShieldBreak, 1f);
        }

        /// 분신 본체의 몸통→완성 팝이 핵심 실루엣을 담당한다. 공용 풀에서는 짧은
        /// 도착 링과 먹방울만 보조해 일반 획득 연출과의 중복·풀 경합을 줄인다.
        public void PlayCloneArrival(Vector3 position)
        {
            EnsureInitialized();
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.12f, 0.82f,
                0.3f, 0.065f, 0.46f, 1f, VfxImportance.Important));
            SpawnDroplets(
                position,
                3,
                InkPalette.Ink,
                VfxImportance.Decorative);
        }

        public void PlayRecordStamp()
        {
            EnsureInitialized();
            VfxAudioManager.Instance?.PlayOneShot(milestoneClip, 0.82f);
            PlayHaptic(HapticPattern.Landing, 0.7f);
        }

        /// 낙묵석이 실제로 충돌한 순간의 붉은 낙관형 결과 피드백.
        public void PlayHazardImpact(Vector3 position)
        {
            EnsureInitialized();
            StartCoroutine(AnimateRing(position, InkPalette.Red, 0.08f, 0.88f,
                0.28f, 0.085f, 0.82f, 0.56f, VfxImportance.Important));
            SpawnDroplets(
                position,
                7,
                InkPalette.Ink,
                VfxImportance.Normal,
                3);
        }

        public void PlayHitStop(float duration = 0.055f)
        {
            if (!isActiveAndEnabled ||
                (GameManager.Instance != null && GameManager.Instance.IsPaused))
                return;
            if (Time.unscaledTime - lastHitStopRequestTime < 0.04f) return;
            lastHitStopRequestTime = Time.unscaledTime;
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
            float duration, float width, float startAlpha, float yScale = 1f,
            VfxImportance importance = VfxImportance.Normal)
        {
            var element = TryAcquireLineVfx("FeedbackRing", importance);
            if (element == null) yield break;
            element.transform.position = position;
            element.transform.localScale = new Vector3(1f, yScale, 1f);
            var line = element.UseLine();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = VfxQualityRuntime.Profile.TransientRingSegments;
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
            var element = TryAcquireLineVfx(
                "JumpBrushStreak",
                VfxImportance.Decorative);
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
                var element = TryAcquireLineVfx(
                    $"InvalidStrokeSlash_{i + 1}",
                    VfxImportance.Important);
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

        IEnumerator AnimateItemSignature(Vector3 position, ItemType type, Color color)
        {
            var element = TryAcquireLineVfx(
                $"ItemSignature_{type}",
                VfxImportance.Important);
            if (element == null) yield break;

            element.transform.position = position;
            var line = element.UseLine();
            line.useWorldSpace = false;
            line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 14;
            line.startWidth = line.endWidth = 0.055f;
            ConfigureItemSignature(line, type);

            float elapsed = 0f;
            const float Duration = 0.44f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Duration);
                float strike = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.58f), 3f);
                float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1f, t));
                float scale = t < 0.58f
                    ? Mathf.Lerp(0.42f, 1.16f, strike)
                    : Mathf.Lerp(1.16f, 1f, settle);
                element.transform.localScale = Vector3.one * scale;
                element.transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(-8f, 3f, t));
                color.a = Mathf.Min(
                    Mathf.InverseLerp(0f, 0.12f, t),
                    1f - Mathf.InverseLerp(0.72f, 1f, t));
                line.startColor = line.endColor = color;
                yield return null;
            }

            ReleaseLineVfx(element);
        }

        static void ConfigureItemSignature(LineRenderer line, ItemType type)
        {
            switch (type)
            {
                case ItemType.InkDrop:
                    line.loop = true;
                    line.positionCount = 12;
                    for (int i = 0; i < line.positionCount; i++)
                    {
                        float t = i / (float)line.positionCount;
                        float angle = t * Mathf.PI * 2f;
                        float width = 0.18f + 0.16f * Mathf.Clamp01(-Mathf.Cos(angle));
                        line.SetPosition(i, new Vector3(
                            Mathf.Sin(angle) * width,
                            Mathf.Cos(angle) * 0.34f - 0.03f,
                            0f));
                    }
                    break;
                case ItemType.GoldenBrush:
                    line.loop = true;
                    line.positionCount = 8;
                    for (int i = 0; i < line.positionCount; i++)
                    {
                        float angle = i * Mathf.PI * 2f / line.positionCount;
                        float radius = i % 2 == 0 ? 0.38f : 0.12f;
                        line.SetPosition(i, new Vector3(
                            Mathf.Cos(angle) * radius,
                            Mathf.Sin(angle) * radius,
                            0f));
                    }
                    break;
                case ItemType.InkShield:
                    line.loop = true;
                    line.positionCount = 7;
                    line.SetPosition(0, new Vector3(-0.28f, 0.24f));
                    line.SetPosition(1, new Vector3(0f, 0.34f));
                    line.SetPosition(2, new Vector3(0.28f, 0.24f));
                    line.SetPosition(3, new Vector3(0.24f, -0.08f));
                    line.SetPosition(4, new Vector3(0f, -0.36f));
                    line.SetPosition(5, new Vector3(-0.24f, -0.08f));
                    line.SetPosition(6, new Vector3(-0.28f, 0.24f));
                    break;
                case ItemType.InkClone:
                    line.loop = true;
                    line.positionCount = 16;
                    for (int i = 0; i < line.positionCount; i++)
                    {
                        float angle = i * Mathf.PI * 2f / line.positionCount;
                        line.SetPosition(i, new Vector3(
                            Mathf.Sin(angle) * 0.38f,
                            Mathf.Sin(angle * 2f) * 0.22f,
                            0f));
                    }
                    break;
                default:
                    line.loop = false;
                    line.positionCount = 5;
                    line.SetPosition(0, new Vector3(-0.34f, -0.2f));
                    line.SetPosition(1, new Vector3(-0.12f, -0.2f));
                    line.SetPosition(2, new Vector3(-0.12f, 0f));
                    line.SetPosition(3, new Vector3(0.12f, 0f));
                    line.SetPosition(4, new Vector3(0.34f, 0.26f));
                    break;
            }
        }

        IEnumerator AnimateWallImpact(Vector3 position, float inwardDirection)
        {
            var element = TryAcquireLineVfx(
                "WallInkImpact",
                VfxImportance.Normal);
            if (element == null) yield break;

            element.transform.position = position;
            element.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                inwardDirection >= 0f ? -8f : 8f);
            var line = element.UseLine();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = 5;
            line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 13;
            line.startWidth = 0.12f;
            line.endWidth = 0.018f;
            line.SetPosition(0, new Vector3(0f, -0.42f));
            line.SetPosition(1, new Vector3(inwardDirection * 0.12f, -0.16f));
            line.SetPosition(2, new Vector3(0f, 0.02f));
            line.SetPosition(3, new Vector3(inwardDirection * 0.16f, 0.2f));
            line.SetPosition(4, new Vector3(inwardDirection * 0.05f, 0.48f));

            float elapsed = 0f;
            const float Duration = 0.24f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Duration);
                Color color = InkPalette.Ink;
                color.a = 1f - t;
                line.startColor = line.endColor = color;
                element.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.08f, t);
                yield return null;
            }
            ReleaseLineVfx(element);
        }

        void SpawnDroplets(
            Vector3 position,
            int count,
            Color color,
            VfxImportance importance,
            int minimumCount = 0)
        {
            int scaledCount = VfxQualityRuntime.Profile.ScaleDecorativeCount(
                count,
                minimumCount);
            for (int i = 0; i < scaledCount; i++)
            {
                var element = TryAcquireSpriteVfx("FeedbackDroplet", importance);
                if (element == null) break;
                StartCoroutine(AnimateDroplet(
                    element,
                    position,
                    color,
                    i,
                    scaledCount));
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

        TransientVfxElement TryAcquireLineVfx(
            string objectName,
            VfxImportance importance = VfxImportance.Normal)
        {
            EnsureTransientPools();
            int active = lineVfxPool.LeasedCount;
            int softLimit = VfxQualityRuntime.Profile.TransientLineLimit;
            int allowed = importance switch
            {
                VfxImportance.Critical => LineVfxCapacity,
                VfxImportance.Important => LineVfxCapacity - 1,
                _ => softLimit,
            };
            if (active >= allowed)
            {
                VfxRuntimeMonitor.Instance?.RecordDropped(importance);
                return null;
            }
            var element = lineVfxPool.Acquire();
            element.gameObject.name = objectName;
            leasedLineVfx.Add(element);
            ReportTransientUsage();
            return element;
        }

        TransientVfxElement TryAcquireSpriteVfx(
            string objectName,
            VfxImportance importance = VfxImportance.Decorative)
        {
            EnsureTransientPools();
            int active = spriteVfxPool.LeasedCount;
            int softLimit = VfxQualityRuntime.Profile.TransientSpriteLimit;
            int allowed = importance switch
            {
                VfxImportance.Critical => SpriteVfxCapacity,
                VfxImportance.Important => SpriteVfxCapacity - 4,
                // Normal이 High 소프트 예산 12개를 모두 차지해 Important를
                // 굶기지 않도록 2개를 추가 예약한다.
                _ => Mathf.Min(softLimit, SpriteVfxCapacity - 6),
            };
            if (active >= allowed)
            {
                VfxRuntimeMonitor.Instance?.RecordDropped(importance);
                return null;
            }
            var element = spriteVfxPool.Acquire();
            element.gameObject.name = objectName;
            leasedSpriteVfx.Add(element);
            ReportTransientUsage();
            return element;
        }

        void ReleaseLineVfx(TransientVfxElement element)
        {
            if (element == null || !leasedLineVfx.Remove(element)) return;
            lineVfxPool?.Release(element);
            ReportTransientUsage();
        }

        void ReleaseSpriteVfx(TransientVfxElement element)
        {
            if (element == null || !leasedSpriteVfx.Remove(element)) return;
            spriteVfxPool?.Release(element);
            ReportTransientUsage();
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

        /// 로비에서 현재 품질 예산과 Critical 예약 슬롯까지 구성해 첫 사망·피격
        /// 프레임에 GameObject와 Renderer를 몰아서 추가하지 않는다.
        void PrewarmTransientPools()
        {
            EnsureTransientPools();
            int lineCount = Mathf.Min(
                LineVfxCapacity,
                VfxQualityRuntime.Profile.TransientLineLimit + 2);
            int spriteCount = Mathf.Min(
                SpriteVfxCapacity,
                VfxQualityRuntime.Profile.TransientSpriteLimit + 4);
            PrewarmTransientPool(lineVfxPool, lineCount, useSprite: false);
            PrewarmTransientPool(spriteVfxPool, spriteCount, useSprite: true);
            ReportTransientUsage();
        }

        static void PrewarmTransientPool(
            ComponentPool<TransientVfxElement> pool,
            int count,
            bool useSprite)
        {
            if (pool == null || count <= 0) return;
            var borrowed = new TransientVfxElement[count];
            for (int i = 0; i < count; i++)
            {
                borrowed[i] = pool.Acquire();
                if (useSprite)
                    borrowed[i].UseSprite();
                else
                    borrowed[i].UseLine();
            }
            for (int i = borrowed.Length - 1; i >= 0; i--)
                pool.Release(borrowed[i]);
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
            ReportTransientUsage();
        }

        void ReportTransientUsage()
        {
            VfxRuntimeMonitor.Instance?.ReportTransientUsage(
                lineVfxPool?.LeasedCount ?? 0,
                spriteVfxPool?.LeasedCount ?? 0);
        }

        void CreateOverlay()
        {
            var canvasObject = new GameObject("FeedbackOverlay", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 140;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            MobileUiLayout.ConfigurePortraitScaler(scaler);

            var safeObject = new GameObject(
                "SafeAreaRoot",
                typeof(RectTransform));
            bannerSafeAreaRoot = safeObject.GetComponent<RectTransform>();
            bannerSafeAreaRoot.SetParent(canvasObject.transform, false);
            bannerSafeAreaRoot.anchorMin = Vector2.zero;
            bannerSafeAreaRoot.anchorMax = Vector2.one;
            bannerSafeAreaRoot.offsetMin = Vector2.zero;
            bannerSafeAreaRoot.offsetMax = Vector2.zero;

            var textObject = new GameObject("ZoneBanner", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(bannerSafeAreaRoot, false);
            bannerRect = textObject.GetComponent<RectTransform>();
            bannerText = textObject.GetComponent<Text>();
            ConfigureBannerText();
            ApplyOverlayLayout();
            bannerText.color = Color.clear;
        }

        void EnsureOverlay()
        {
            if (bannerText == null)
            {
                var existingBanner =
                    transform.Find("FeedbackOverlay/SafeAreaRoot/ZoneBanner") ??
                    transform.Find("FeedbackOverlay/ZoneBanner");
                if (existingBanner != null)
                    bannerText = existingBanner.GetComponent<Text>();
            }
            if (bannerText == null)
            {
                CreateOverlay();
                return;
            }

            Transform overlay = transform.Find("FeedbackOverlay");
            if (overlay == null)
            {
                CreateOverlay();
                return;
            }
            bannerSafeAreaRoot = overlay.Find("SafeAreaRoot") as RectTransform;
            if (bannerSafeAreaRoot == null)
            {
                var safeObject = new GameObject(
                    "SafeAreaRoot",
                    typeof(RectTransform));
                bannerSafeAreaRoot = safeObject.GetComponent<RectTransform>();
                bannerSafeAreaRoot.SetParent(overlay, false);
            }
            bannerSafeAreaRoot.anchorMin = Vector2.zero;
            bannerSafeAreaRoot.anchorMax = Vector2.one;
            bannerSafeAreaRoot.offsetMin = Vector2.zero;
            bannerSafeAreaRoot.offsetMax = Vector2.zero;
            bannerRect = bannerText.rectTransform;
            if (bannerRect.parent != bannerSafeAreaRoot)
                bannerRect.SetParent(bannerSafeAreaRoot, false);
            ConfigureBannerText();
            ApplyOverlayLayout();
        }

        void ApplyOverlayLayout()
        {
            if (bannerSafeAreaRoot == null || bannerRect == null ||
                Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(
                bannerSafeAreaRoot,
                safe,
                Screen.width,
                Screen.height);
            bannerRect.anchorMin = bannerRect.anchorMax =
                new Vector2(0.5f, 0.78f);
            bannerRect.anchoredPosition = Vector2.zero;
            bannerRect.localScale = Vector3.one *
                MobileUiLayout.CalculateWidthFitScale(
                    860f,
                    safe,
                    Screen.width,
                    Screen.height,
                    24f);
            lastOverlayScreenWidth = Screen.width;
            lastOverlayScreenHeight = Screen.height;
            lastOverlaySafeArea = Screen.safeArea;
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
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.78f);
            rect.anchoredPosition = Vector2.zero;
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

        AudioClip CreateOwnedTone(string name, float duration, float startFrequency,
            float endFrequency, float volume, float noiseAmount)
        {
            AudioClip clip = CreateTone(
                name, duration, startFrequency, endFrequency, volume, noiseAmount);
            ownedRuntimeClips.Add(clip);
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
            accentSource.PlayOneShot(
                clip,
                Mathf.Clamp01(volume) * LobbySettingsProfile.SfxVolume);
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

        AudioClip CreateOwnedBrushNoise(string name, float duration, float volume,
            bool fadeOut = false)
        {
            AudioClip clip = CreateBrushNoise(name, duration, volume, fadeOut);
            ownedRuntimeClips.Add(clip);
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

        static void DestroyOwnedObject(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null) return;
            if (Application.isPlaying)
                Destroy(ownedObject);
            else
                DestroyImmediate(ownedObject);
        }
    }
}
