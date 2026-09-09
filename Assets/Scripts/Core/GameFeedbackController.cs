using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using MukJump.AI;
using MukJump.Items;
using MukJump.Core.Pooling;
using AppsInToss;

namespace MukJump.Core
{
    /// 점프·착지·드로잉·아이템 등 순간 피드백을 한곳에서 관리한다.
    /// 내장 효과음을 우선 사용하고, 누락된 소리는 런타임 합성 폴백으로 보완한다.
    public class GameFeedbackController : MonoBehaviour
    {
        const int LineVfxCapacity = 8;
        const int SpriteVfxCapacity = 16;
        const float DeathPopDuration = 0.17f;

        public static GameFeedbackController Instance { get; private set; }

        [SerializeField] Texture2D contactDropletAtlas;
        [SerializeField] Texture2D contactSplash;
        InkContactParticles contactParticles;
        public int ActiveContactParticleCount => contactParticles?.ActiveCount ?? 0;

        AudioClip jumpClip;
        AudioClip landingClip;
        AudioClip drawClip;
        AudioClip invalidClip;
        AudioClip itemClip;
        AudioClip milestoneClip;
        AudioClip brushLoopClip;
        AudioClip brushTransitionClip;
        AudioClip wallHitClip;
        AudioClip damageHitClip;
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
        float lastDamageSoundTime = -10f;
        bool jumpPending;
        bool landingPending;
        Vector3 pendingJumpPosition;
        Vector2 pendingJumpDirection;
        bool pendingAirJump;
        Vector3 pendingLandingPosition;
        float pendingLandingStrength;
        Camera feedbackCamera;
        float lastLandingStrength;
        bool lastJumpWasAirborne;
        InkWakeSampler wakeSampler;
        MukJump.Player.PlayerController wakeLeader;
        uint visualSeed = 0xA35139F1;

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
                return (deathSqueakClip != null ? deathSqueakClip.length : DeathPopDuration) + 0.04f;
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
            if (contactParticles == null && contactDropletAtlas != null && contactSplash != null)
            {
                var shader = Resources.Load<Shader>("MukJump/Shaders/InkContactParticle");
                if (shader != null && shader.isSupported)
                    contactParticles = new InkContactParticles(transform, contactDropletAtlas, contactSplash, shader);
            }
        }

        void OnDisable()
        {
            // 코루틴을 먼저 멈춘 뒤 모두 반납해야, 재활성화 후 같은 요소를 다시 빌렸을 때
            // 이전 코루틴이 새 연출의 위치·색을 덮어쓰지 않는다.
            StopAllCoroutines();
            jumpPending = landingPending = false;
            wakeSampler.Reset();
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
            contactParticles?.Advance(Time.deltaTime,
                GameManager.Instance == null || GameManager.Instance.IsGameplayTicking);
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
                contactParticles?.Clear();
            if (bannerText == null) return;
            if (lastOverlayScreenWidth != Screen.width ||
                lastOverlayScreenHeight != Screen.height ||
                lastOverlaySafeArea != MobileUiLayout.CurrentSafeArea)
                ApplyOverlayLayout();
        }

        void OnDestroy()
        {
            DisposeRuntimeAssets();
        }

        void LateUpdate()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayTicking)
            {
                jumpPending = landingPending = false;
                wakeSampler.Reset();
                return;
            }
            // 충돌 순서가 아니라 같은 프레임의 화면 중심에 가까운 실제 접촉을 선택한다.
            // 공중의 대표 개체로 착지 위치를 옮기지는 않는다.
            if (landingPending)
            {
                EmitLanding(pendingLandingPosition, pendingLandingStrength);
                landingPending = false;
            }
            if (jumpPending)
            {
                EmitJump(pendingJumpPosition, pendingJumpDirection, pendingAirJump);
                jumpPending = false;
            }
            EmitAirWake();
        }

        void EmitAirWake()
        {
            var leader = GameManager.Instance != null ? GameManager.Instance.HighestLivingPlayer : null;
            if (contactParticles == null || leader == null || leader.Body == null || leader.IsGrounded ||
                leader.IsInkDropBoosted || LobbySettingsProfile.ReducedMotionEnabled ||
                Mathf.Abs(leader.Body.linearVelocity.y) < 3f)
            {
                wakeSampler.Reset();
                return;
            }
            Vector3 position = leader.transform.position;
            if (wakeLeader != leader)
            {
                wakeLeader = leader;
                wakeSampler.Reset();
            }
            if (wakeSampler.Sample(1, position, Time.deltaTime) &&
                !float.IsPositiveInfinity(FeedbackDistance(position)))
                contactParticles.EmitAirSlip(position - Vector3.up * 0.3f, leader.Body.linearVelocity);
        }

        float FeedbackDistance(Vector3 position)
        {
            if (feedbackCamera == null) feedbackCamera = Camera.main;
            if (feedbackCamera == null) return position.sqrMagnitude;
            Vector3 viewport = feedbackCamera.WorldToViewportPoint(position);
            if (viewport.z <= 0f || viewport.x < -0.1f || viewport.x > 1.1f ||
                viewport.y < -0.1f || viewport.y > 1.1f) return float.PositiveInfinity;
            float outsidePenalty = viewport.x < 0f || viewport.x > 1f ||
                viewport.y < 0f || viewport.y > 1f ? 10f : 0f;
            return outsidePenalty +
                (new Vector2(viewport.x, viewport.y) - Vector2.one * 0.5f).sqrMagnitude;
        }

        void DisposeRuntimeAssets()
        {
            contactParticles?.Dispose();
            contactParticles = null;
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
            damageHitClip = null;
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
            damageHitClip = CreateOwnedDamageHit();
            deathSqueakClip = LoadSfx("SFX_Character_Death") ??
                              CreateOwnedDeathPop();
            gameOverClip = CreateOwnedGameOverSound();
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
            if (contactParticles != null)
                contactParticles.EmitImpact(position, Vector2.right * Mathf.Sign(inwardDirection), 0.65f);
            else SpawnDroplets(
                position,
                5,
                InkPalette.Ink,
                VfxImportance.Normal,
                2);
        }

        /// 실제 체력이 줄어든 순간만 짧은 붉은 링·충돌음·약한 진동으로 알린다.
        /// 먹떼가 동시에 맞아도 공용 풀과 오디오 채널을 분신 수만큼 소모하지 않는다.
        public void PlayDamageHit(Vector3 position, bool playSound = true)
        {
            EnsureInitialized();
            if (playSound) PlayDamageSound();
            if (Time.unscaledTime - lastDamageHitFeedbackTime < 0.07f) return;
            lastDamageHitFeedbackTime = Time.unscaledTime;
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
            => PlayDirectionalJump(position, Vector2.up);

        public void PlayDirectionalJump(Vector3 position, Vector2 direction, bool airborne = false)
        {
            if (float.IsPositiveInfinity(FeedbackDistance(position))) return;
            if (!jumpPending || FeedbackDistance(position) < FeedbackDistance(pendingJumpPosition))
            {
                pendingJumpPosition = position;
                pendingJumpDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
                pendingAirJump = airborne;
            }
            jumpPending = true;
        }

        void EmitJump(Vector3 position, Vector2 direction, bool airborne)
        {
            EnsureInitialized();
            // 먹떼가 거의 동시에 점프할 때 동일 피드백을 한 번으로 묶어
            // 소리 채널과 순간 풀을 분신 수만큼 소모하지 않는다.
            if (Time.unscaledTime - lastJumpFeedbackTime < 0.1f &&
                (!airborne || lastJumpWasAirborne)) return;
            lastJumpFeedbackTime = Time.unscaledTime;
            lastJumpWasAirborne = airborne;
            VfxAudioManager.Instance?.PlayOneShot(jumpClip, airborne ? 0.64f : 0.48f);
            if (!airborne)
                StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.12f, 0.62f,
                    0.22f, 0.055f, 0.36f, 0.24f, VfxImportance.Decorative));
            if (!LobbySettingsProfile.ReducedMotionEnabled)
                StartCoroutine(AnimateBrushStreak(position, direction, airborne));
            contactParticles?.EmitJump(position, direction, airborne);
        }

        public void PlayLanding(Vector3 position, float impactSpeed)
        {
            if (float.IsPositiveInfinity(FeedbackDistance(position))) return;
            if (!landingPending || FeedbackDistance(position) < FeedbackDistance(pendingLandingPosition))
                pendingLandingPosition = position;
            pendingLandingStrength = landingPending
                ? Mathf.Max(pendingLandingStrength, impactSpeed) : impactSpeed;
            landingPending = true;
        }

        void EmitLanding(Vector3 position, float impactSpeed)
        {
            EnsureInitialized();
            float strength = Mathf.InverseLerp(2f, 14f, impactSpeed);
            // 짧은 반복 착지는 억제하되 뒤늦게 들어온 강한 충격까지 버리지는 않는다.
            if (Time.unscaledTime - lastLandingFeedbackTime < 0.12f &&
                strength < lastLandingStrength + 0.3f) return;
            lastLandingFeedbackTime = Time.unscaledTime;
            lastLandingStrength = strength;
            VfxAudioManager.Instance?.PlayOneShot(landingClip, Mathf.Lerp(0.45f, 0.9f, strength));
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.12f,
                Mathf.Lerp(0.42f, 0.88f, strength), 0.26f, 0.07f, 0.48f, 0.2f));
            contactParticles?.EmitLanding(position, strength);
            if (contactParticles == null && strength >= 0.45f)
                SpawnDroplets(position, 3 + Mathf.RoundToInt(strength * 2f),
                    InkPalette.Ink, VfxImportance.Decorative);
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
                contactParticles?.EmitStrokeSettle(position);
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
            // 분신 본체의 팝과 공용 도착 링이 이미 실루엣을 담당한다.
            if (type == ItemType.InkClone) return;
            StartCoroutine(AnimateRing(position, color, 0.2f, 1.15f,
                0.38f, 0.08f, 0.15f, 1f, VfxImportance.Important));
            StartCoroutine(AnimateItemSignature(position, type, color));
            SpawnDroplets(position, 3, color, VfxImportance.Decorative);
        }

        public void PlayItemTelegraph(Vector3 position, ItemType type)
        {
            EnsureInitialized();
            Color color = ItemColor(type);
            StartCoroutine(AnimateRing(position, color, 0.12f, 0.72f,
                0.42f, 0.035f, 0.22f, 1f, VfxImportance.Important));
            if (VfxQualityRuntime.Tier >= VfxQualityTier.Medium &&
                !LobbySettingsProfile.ReducedMotionEnabled)
                StartCoroutine(AnimateRing(position, color, 0.32f, 1.02f,
                    0.55f, 0.025f, 0.13f, 1f, VfxImportance.Decorative));
        }

        static Color ItemColor(ItemType type)
        {
            return ItemFeedbackPalette.For(type);
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
            // 본체 ItemEffectView의 파열 고리·파편이 핵심 시각을 소유한다.
            PlayHaptic(HapticPattern.ShieldBreak, 1f);
        }

        /// 분신 본체의 몸통→완성 팝이 핵심 실루엣을 담당한다. 공용 풀에서는 짧은
        /// 응집 링과 안쪽으로 모이는 먹만 보조해 일반 획득과 구분한다.
        public void PlayCloneArrival(Vector3 position, Vector2 carrierVelocity = default)
        {
            EnsureInitialized();
            StartCoroutine(AnimateRing(position, InkPalette.Ink, 0.85f, 0.14f,
                0.24f, 0.045f, 0.38f, 1f, VfxImportance.Important));
            if (contactParticles != null)
                contactParticles.EmitCloneConvergence(position, carrierVelocity);
            else
                SpawnDroplets(position, 3, InkPalette.Ink, VfxImportance.Decorative);
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
            if (contactParticles != null)
                contactParticles.EmitImpact(position, Vector2.up, 1f);
            else SpawnDroplets(
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
            bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
            if (reduced) endRadius = Mathf.Lerp(startRadius, endRadius, 0.25f);
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
                elapsed += Mathf.Min(Time.deltaTime, 0.05f);
                float t = Mathf.Clamp01(elapsed / duration);
                float radius = Mathf.Lerp(startRadius, endRadius, 1f - Mathf.Pow(1f - t, 3f));
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / line.positionCount;
                    // 먹 테두리가 매 프레임 끓는 대신 같은 결을 유지하며 얇아진다.
                    float wobble = 1f + Mathf.Sin(angle * 5f) * 0.027f + Mathf.Sin(angle * 9f) * 0.014f;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                        radius * wobble);
                }
                color.a = startAlpha * InkVfxMotion.TailAlpha(t, 0.08f);
                line.startWidth = line.endWidth = width * Mathf.Lerp(1f, 0.12f, t * t);
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
            if (!LobbySettingsProfile.HapticsEnabled)
                return;

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

#if UNITY_WEBGL && !UNITY_EDITOR
            PlayAppsInTossHaptic(pattern);
#elif UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(durationMs, amplitude);
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS 기본 진동은 경량 착지에도 강한 알림 진동을 내므로 반복 점프에서는 생략.
            if (pattern != HapticPattern.Landing) Handheld.Vibrate();
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

#if UNITY_WEBGL && !UNITY_EDITOR
        static async void PlayAppsInTossHaptic(HapticPattern pattern)
        {
            HapticFeedbackType type = pattern switch
            {
                HapticPattern.Landing => HapticFeedbackType.Tap,
                HapticPattern.ShieldBreak => HapticFeedbackType.BasicMedium,
                _ => HapticFeedbackType.Error,
            };

            try
            {
                await AIT.GenerateHapticFeedback(
                    new HapticFeedbackOptions { Type = type },
                    1500);
            }
            catch (AITException)
            {
                // 햅틱 미지원·타임아웃은 게임 진행을 막지 않는다.
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] Apps in Toss 햅틱 호출 예외를 격리했습니다: " +
                    exception.Message);
            }
        }
#endif

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

        IEnumerator AnimateBrushStreak(Vector3 position, Vector2 direction, bool airborne)
        {
            var element = TryAcquireLineVfx(
                "JumpBrushStreak",
                VfxImportance.Decorative);
            if (element == null) yield break;
            element.transform.position = position;
            element.transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            var line = element.UseLine();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = 4;
            line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 11;
            line.startWidth = airborne ? 0.14f : 0.11f;
            line.endWidth = 0.012f;
            line.SetPosition(0, new Vector3(airborne ? -0.35f : -0.04f, 0f));
            line.SetPosition(1, new Vector3(airborne ? -0.18f : 0.02f, airborne ? -0.12f : 0.14f));
            line.SetPosition(2, new Vector3(airborne ? 0.18f : -0.02f, airborne ? -0.12f : 0.38f));
            line.SetPosition(3, new Vector3(airborne ? 0.35f : 0.04f, airborne ? 0f : 0.62f));
            float elapsed = 0f;
            while (elapsed < 0.24f)
            {
                elapsed += Time.deltaTime;
                Color color = InkPalette.Ink;
                float t = Mathf.Clamp01(elapsed / 0.24f);
                color.a = 0.74f * (1f - t) * (1f - t);
                element.transform.localScale = new Vector3(1f, Mathf.Lerp(0.45f, 1.12f, t), 1f);
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
                    {
                        slashes[i].transform.localScale = Vector3.one * scale;
                        Color color = InkPalette.Red;
                        color.a = InkVfxMotion.TailAlpha(t, 0.38f);
                        var line = slashes[i].UseLine();
                        line.startColor = line.endColor = color;
                    }
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
                if (LobbySettingsProfile.ReducedMotionEnabled) scale = 1f;
                element.transform.localScale = Vector3.one * scale;
                element.transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    LobbySettingsProfile.ReducedMotionEnabled ? 0f : Mathf.Lerp(-8f, 3f, t));
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
            if (LobbySettingsProfile.ReducedMotionEnabled && importance == VfxImportance.Decorative)
                return;
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
            float speed = VisualRange(1.1f, 2.5f);
            Vector3 velocity = new(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);
            float scale = VisualRange(0.035f, 0.085f);
            element.transform.localScale = Vector3.one * scale;
            Vector3 current = position;
            float startAlpha = color.a;
            float elapsed = 0f;
            while (elapsed < 0.45f)
            {
                float delta = Mathf.Min(Time.deltaTime, 0.05f);
                elapsed += delta;
                float t = Mathf.Clamp01(elapsed / 0.45f);
                bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
                if (!reduced)
                {
                    InkVfxMotion.Integrate(ref current, ref velocity, Vector3.down * 4.5f, 2.2f, delta);
                    element.transform.position = current;
                    element.transform.rotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f);
                    float taper = Mathf.Lerp(1f, 0.25f, t * t);
                    element.transform.localScale = new Vector3(scale * taper,
                        scale * taper * Mathf.Lerp(1.65f, 1f, t), 1f);
                }
                color.a = startAlpha * InkVfxMotion.TailAlpha(t);
                renderer.color = color;
                yield return null;
            }
            ReleaseSpriteVfx(element);
        }

        float VisualRange(float min, float max)
        {
            visualSeed ^= visualSeed << 13;
            visualSeed ^= visualSeed >> 17;
            visualSeed ^= visualSeed << 5;
            return Mathf.Lerp(min, max, (visualSeed & 0xFFFFFF) / 16777216f);
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
            lastOverlaySafeArea = safe;
        }

        IEnumerator AnimateBanner(string title, string subtitle)
        {
            InkLocalizedText.SetSource(bannerText, $"{title}\n<size=30>{subtitle}</size>");
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
            InkLocalizedText.Bind(bannerText);
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

        /// 체력 감소/치명타에서 공통 호출한다. 방어막·무적 접촉은 호출하지 않는다.
        public void PlayDamageSound()
        {
            EnsureInitialized();
            if (Time.unscaledTime - lastDamageSoundTime < .07f) return;
            lastDamageSoundTime = Time.unscaledTime;
            VfxAudioManager.Instance?.PlayOneShot(damageHitClip, .9f);
        }

        AudioClip CreateOwnedGameOverSound()
        {
            float[] samples = GameOverSound.BuildSamples();
            var clip = AudioClip.Create("GameOverWoodStringsBell", samples.Length, 1,
                GameOverSound.SampleRate, false);
            clip.SetData(samples, 0);
            ownedRuntimeClips.Add(clip);
            return clip;
        }

        AudioClip CreateOwnedDamageHit()
        {
            float[] samples = BuildDamageHitSamples();
            var clip = AudioClip.Create("ObstaclePunchImpact", samples.Length, 1, 44100, false);
            clip.SetData(samples, 0);
            ownedRuntimeClips.Add(clip);
            return clip;
        }

        // 순간적인 파열음 + 급히 낮아지는 둔탁한 몸통 + 짧은 마찰음으로 찰진 피격감을 만든다.
        static float[] BuildDamageHitSamples()
        {
            const int sampleRate = 44100;
            var samples = new float[7056];
            uint noiseState = 0x7516A9u;
            float lowNoise = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)sampleRate;
                noiseState = unchecked(noiseState * 1664525u + 1013904223u);
                float noise = (noiseState >> 8) / 8388607.5f - 1f;
                lowNoise += .24f * (noise - lowNoise);
                // 210→85Hz의 짧은 충격: 음을 길게 울리지 않고 두께만 남긴다.
                float phase = 2f * Mathf.PI * (85f * t + 125f * (1f - Mathf.Exp(-65f * t)) / 65f);
                float body = Mathf.Sin(phase) * Mathf.Exp(-30f * t) * .74f;
                float crack = (noise - lowNoise) * .48f * Mathf.Exp(-145f * t);
                float crunch = lowNoise * .8f * Mathf.Exp(-43f * t);
                float attack = Mathf.Min(1f, t / .0007f);
                float tail = Mathf.Clamp01((.16f - t) / .03f);
                float hit = (body + crack + crunch) * attack;
                // 부드러운 포화로 작은 스피커에서도 충격이 들리되 클리핑은 막는다.
                samples[i] = hit / (1f + Mathf.Abs(hit) * .65f) * 1.18f * tail;
            }
            return samples;
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

        AudioClip CreateOwnedDeathPop()
        {
            float[] samples = BuildDeathPopSamples();
            var clip = AudioClip.Create("DeathInkPop", samples.Length, 1, 44100, false);
            clip.SetData(samples, 0);
            ownedRuntimeClips.Add(clip);
            return clip;
        }

        // tools/generate_sfx.mjs의 사망 WAV와 같은 파형. 폴백도 긴 전자음으로 바뀌지 않는다.
        // 지역 난수만 써서 음원 생성이 발판·아이템 난수에 영향을 주지 않게 한다.
        static float[] BuildDeathPopSamples()
        {
            const int sampleRate = 44100;
            int count = (int)System.Math.Round(DeathPopDuration * sampleRate);
            var samples = new float[count];
            uint seed = 0x4d554b;
            double phase = 0, bodyPhase = 0, filtered = 0;
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)sampleRate;
                double p = i / (double)(count - 1);
                seed = unchecked(seed * 1664525u + 1013904223u);
                double grain = seed / (double)uint.MaxValue * 2 - 1;
                filtered += (grain - filtered) * 0.28;
                double frequency = 330 + 1550 * System.Math.Exp(-t / 0.011);
                phase += frequency / sampleRate * System.Math.PI * 2;
                bodyPhase += (130 + 170 * System.Math.Exp(-t / 0.018)) /
                             sampleRate * System.Math.PI * 2;
                double squeak = System.Math.Sin(phase) * System.Math.Exp(-t / 0.016) * 0.38;
                double body = System.Math.Sin(bodyPhase) * System.Math.Exp(-t / 0.023) * 0.48;
                double crack = (grain - filtered) * System.Math.Exp(-t / 0.012) * 0.43;
                double inkTexture = filtered * System.Math.Exp(-t / 0.032) * 0.3;
                double envelope = System.Math.Min(1, t / 0.0015) *
                                  System.Math.Min(1, (1 - p) / 0.12);
                samples[i] = (float)((squeak + body + crack + inkTexture) * envelope);
            }
            return samples;
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
