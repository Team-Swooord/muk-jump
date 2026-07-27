using UnityEngine;

namespace MukJump.Core
{
    /// VFX 품질 초기화, 완만한 프레임 기반 강등, Low Memory 대응과
    /// Development Build용 경량 통계를 담당한다.
    [DefaultExecutionOrder(-700)]
    [DisallowMultipleComponent]
    public sealed class VfxRuntimeMonitor : MonoBehaviour
    {
        const float WarmupSeconds = 5f;
        const float SampleWindowSeconds = 12f;
        const float DowngradeCooldownSeconds = 30f;
        const float MaximumSampleDeltaSeconds = 0.5f;

        public static VfxRuntimeMonitor Instance { get; private set; }

        public float MeasuredFps { get; private set; } = 60f;
        public int ActiveLineVfx { get; private set; }
        public int ActiveSpriteVfx { get; private set; }
        public int ActiveCompositeVfx { get; private set; }
        public int PeakActiveVfx { get; private set; }
        public int DroppedDecorativeVfx { get; private set; }
        public int DroppedNormalVfx { get; private set; }
        public int DroppedImportantVfx { get; private set; }
        public int DroppedCriticalVfx { get; private set; }
        public int ReclaimedCompositeVfx { get; private set; }
        public bool AutomaticQualityEnabled { get; private set; } = true;

        float warmupRemaining = WarmupSeconds;
        float sampledTime;
        int sampledFrames;
        float nextDowngradeTime;

        void OnEnable()
        {
            if (Instance != null && Instance != this && Instance.isActiveAndEnabled)
            {
                enabled = false;
                return;
            }

            Instance = this;
            Application.lowMemory += HandleLowMemory;
            VfxQualityRuntime.ApplyInitialRecommendation();
            ResetFrameWindow(WarmupSeconds);
        }

        void OnDisable()
        {
            Application.lowMemory -= HandleLowMemory;
            if (Instance == this) Instance = null;
            ResetFrameWindow(WarmupSeconds);
        }

        void Update()
        {
            var manager = GameManager.Instance;
            ProcessFrameSample(
                Time.unscaledDeltaTime,
                manager != null && manager.IsGameplayTicking,
                Time.unscaledTime,
                Application.targetFrameRate);
        }

        void ProcessFrameSample(
            float delta,
            bool gameplayTicking,
            float unscaledTime,
            int targetFrameRate)
        {
            if (delta <= 0f) return;

            if (warmupRemaining > 0f)
            {
                warmupRemaining -= Mathf.Min(delta, MaximumSampleDeltaSeconds);
                return;
            }

            if (!gameplayTicking)
            {
                ResetFrameWindow(1f);
                return;
            }

            // 일시적인 긴 프레임 하나로 전체 표본을 버리지 않는다. 0.5초까지만
            // 반영하면 실제 2~3FPS 지속 저하도 감지하면서 앱 복귀 공백은 과대평가하지 않는다.
            sampledTime += Mathf.Min(delta, MaximumSampleDeltaSeconds);
            sampledFrames++;
            if (sampledTime < SampleWindowSeconds) return;

            MeasuredFps = sampledFrames / Mathf.Max(0.001f, sampledTime);
            float lowFpsThreshold = LowFpsThresholdForTarget(targetFrameRate);
            if (AutomaticQualityEnabled &&
                MeasuredFps < lowFpsThreshold &&
                unscaledTime >= nextDowngradeTime &&
                VfxQualityRuntime.DowngradeOneStep(
                    VfxQualityChangeReason.SustainedFrameTime))
            {
                nextDowngradeTime = unscaledTime + DowngradeCooldownSeconds;
            }

            ResetFrameWindow(0f);
        }

        public static float LowFpsThresholdForTarget(int targetFrameRate)
        {
            return targetFrameRate <= 0 || targetFrameRate >= 55
                ? 47f
                : targetFrameRate >= 40
                    ? 35f
                    : 26f;
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused)
                ResetFrameWindow(2f);
        }

        public void CycleQualityForDebug()
        {
            if (!GameManager.DebugToolsAvailable) return;
            if (AutomaticQualityEnabled)
            {
                AutomaticQualityEnabled = false;
                VfxQualityRuntime.SetTier(
                    VfxQualityTier.Low,
                    VfxQualityChangeReason.DebugOverride);
            }
            else if (VfxQualityRuntime.Tier == VfxQualityTier.Low)
            {
                VfxQualityRuntime.SetTier(
                    VfxQualityTier.Medium,
                    VfxQualityChangeReason.DebugOverride);
            }
            else if (VfxQualityRuntime.Tier == VfxQualityTier.Medium)
            {
                VfxQualityRuntime.SetTier(
                    VfxQualityTier.High,
                    VfxQualityChangeReason.DebugOverride);
            }
            else
            {
                RestoreAutomaticQuality();
                return;
            }
            ResetFrameWindow(1f);
        }

        public void RestoreAutomaticQuality()
        {
            AutomaticQualityEnabled = true;
            VfxQualityRuntime.SetTier(
                VfxQualityRuntime.RecommendInitialTier(
                    SystemInfo.systemMemorySize,
                    SystemInfo.graphicsMemorySize,
                    SystemInfo.graphicsShaderLevel),
                VfxQualityChangeReason.InitialRecommendation);
            ResetFrameWindow(2f);
        }

        public void ReportTransientUsage(int lineCount, int spriteCount)
        {
            ActiveLineVfx = Mathf.Max(0, lineCount);
            ActiveSpriteVfx = Mathf.Max(0, spriteCount);
            RefreshPeak();
        }

        public void ReportCompositeUsage(int activeCount)
        {
            ActiveCompositeVfx = Mathf.Max(0, activeCount);
            RefreshPeak();
        }

        public void RecordDropped(VfxImportance importance)
        {
            if (importance <= VfxImportance.Decorative)
                DroppedDecorativeVfx++;
            else if (importance == VfxImportance.Normal)
                DroppedNormalVfx++;
            else if (importance == VfxImportance.Important)
                DroppedImportantVfx++;
            else
                DroppedCriticalVfx++;
        }

        public void RecordCompositeReclaimed()
        {
            ReclaimedCompositeVfx++;
        }

        void RefreshPeak()
        {
            PeakActiveVfx = Mathf.Max(
                PeakActiveVfx,
                ActiveLineVfx + ActiveSpriteVfx + ActiveCompositeVfx);
        }

        void HandleLowMemory()
        {
            VfxQualityRuntime.SetTier(
                VfxQualityTier.Low,
                VfxQualityChangeReason.LowMemory);
            ResetFrameWindow(3f);
        }

        void ResetFrameWindow(float warmup)
        {
            sampledTime = 0f;
            sampledFrames = 0;
            warmupRemaining = Mathf.Max(warmupRemaining, warmup);
        }
    }
}
