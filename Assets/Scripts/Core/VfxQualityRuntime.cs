using System;
using UnityEngine;

namespace MukJump.Core
{
    /// VFX가 게임 정보에 얼마나 중요한지 나타낸다.
    /// 품질 예산이 부족해도 Important 이상은 핵심 실루엣을 유지한다.
    public enum VfxImportance
    {
        Decorative = 0,
        Normal = 1,
        Important = 2,
        Critical = 3,
    }

    public enum VfxQualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    public enum VfxQualityChangeReason
    {
        InitialRecommendation,
        DebugOverride,
        SustainedFrameTime,
        LowMemory,
    }

    /// 현재 프로젝트의 절차적 Line/Sprite VFX에 맞춘 품질별 소프트 예산.
    /// 실제 풀의 하드 상한은 유지하고, 장식 레이어만 이 값에서 먼저 생략한다.
    public readonly struct VfxQualityProfile
    {
        public readonly int TransientLineLimit;
        public readonly int TransientSpriteLimit;
        public readonly int TransientRingSegments;
        public readonly int PersistentRingSegments;
        public readonly int WeatherLineCount;
        public readonly int CompositeConcurrentLimit;
        public readonly float DecorativeScale;

        public VfxQualityProfile(
            int transientLineLimit,
            int transientSpriteLimit,
            int transientRingSegments,
            int persistentRingSegments,
            int weatherLineCount,
            int compositeConcurrentLimit,
            float decorativeScale)
        {
            TransientLineLimit = transientLineLimit;
            TransientSpriteLimit = transientSpriteLimit;
            TransientRingSegments = transientRingSegments;
            PersistentRingSegments = persistentRingSegments;
            WeatherLineCount = weatherLineCount;
            CompositeConcurrentLimit = compositeConcurrentLimit;
            DecorativeScale = decorativeScale;
        }

        public int ScaleDecorativeCount(int requested, int minimum = 0)
        {
            if (requested <= 0) return 0;
            return Mathf.Clamp(
                Mathf.CeilToInt(requested * DecorativeScale),
                Mathf.Clamp(minimum, 0, requested),
                requested);
        }
    }

    /// 프로젝트 전체가 공유하는 VFX 품질 상태.
    /// Domain Reload 비활성 환경에서도 정적 이벤트가 누적되지 않도록 세션 시작에 초기화한다.
    public static class VfxQualityRuntime
    {
        static readonly VfxQualityProfile LowProfile = new(
            transientLineLimit: 4,
            transientSpriteLimit: 6,
            transientRingSegments: 20,
            persistentRingSegments: 24,
            weatherLineCount: 4,
            compositeConcurrentLimit: 1,
            decorativeScale: 0.42f);

        static readonly VfxQualityProfile MediumProfile = new(
            transientLineLimit: 5,
            transientSpriteLimit: 10,
            transientRingSegments: 26,
            persistentRingSegments: 36,
            weatherLineCount: 7,
            compositeConcurrentLimit: 2,
            decorativeScale: 0.7f);

        static readonly VfxQualityProfile HighProfile = new(
            transientLineLimit: 6,
            transientSpriteLimit: 12,
            transientRingSegments: 32,
            persistentRingSegments: 48,
            weatherLineCount: 10,
            compositeConcurrentLimit: 3,
            decorativeScale: 1f);

        static VfxQualityTier tier = VfxQualityTier.Medium;
        static bool initialRecommendationApplied;

        public static event Action<VfxQualityTier, VfxQualityChangeReason> Changed;

        public static VfxQualityTier Tier => tier;
        public static VfxQualityProfile Profile => GetProfile(tier);
        public static VfxQualityChangeReason LastChangeReason { get; private set; } =
            VfxQualityChangeReason.InitialRecommendation;

        public static VfxQualityProfile GetProfile(VfxQualityTier value)
        {
            return value switch
            {
                VfxQualityTier.Low => LowProfile,
                VfxQualityTier.High => HighProfile,
                _ => MediumProfile,
            };
        }

        /// 하드웨어 문자열 화이트리스트 대신 보수적인 메모리·셰이더 기준만 사용한다.
        /// 0은 일부 기기에서 "알 수 없음"이므로 저사양으로 단정하지 않는다.
        public static VfxQualityTier RecommendInitialTier(
            int systemMemoryMb,
            int graphicsMemoryMb,
            int shaderLevel)
        {
            bool lowSystemMemory = systemMemoryMb > 0 && systemMemoryMb <= 3500;
            bool lowGraphicsMemory = graphicsMemoryMb > 0 && graphicsMemoryMb <= 512;
            bool lowShaderLevel = shaderLevel > 0 && shaderLevel < 45;
            if (lowSystemMemory || lowGraphicsMemory || lowShaderLevel)
                return VfxQualityTier.Low;

            bool mediumSystemMemory = systemMemoryMb > 0 && systemMemoryMb <= 6000;
            bool mediumGraphicsMemory = graphicsMemoryMb > 0 && graphicsMemoryMb <= 1500;
            if (mediumSystemMemory || mediumGraphicsMemory)
                return VfxQualityTier.Medium;

            return VfxQualityTier.High;
        }

        public static void ApplyInitialRecommendation()
        {
            if (initialRecommendationApplied) return;
            initialRecommendationApplied = true;
            SetTier(
                RecommendInitialTier(
                    SystemInfo.systemMemorySize,
                    SystemInfo.graphicsMemorySize,
                    SystemInfo.graphicsShaderLevel),
                VfxQualityChangeReason.InitialRecommendation);
        }

        public static void SetTier(
            VfxQualityTier value,
            VfxQualityChangeReason reason)
        {
            value = (VfxQualityTier)Mathf.Clamp(
                (int)value,
                (int)VfxQualityTier.Low,
                (int)VfxQualityTier.High);
            LastChangeReason = reason;
            if (tier == value) return;

            tier = value;
            Changed?.Invoke(tier, reason);
        }

        public static bool DowngradeOneStep(VfxQualityChangeReason reason)
        {
            if (tier == VfxQualityTier.Low) return false;
            SetTier((VfxQualityTier)((int)tier - 1), reason);
            return true;
        }

        public static void CycleForDebug()
        {
            var next = tier switch
            {
                VfxQualityTier.Low => VfxQualityTier.Medium,
                VfxQualityTier.Medium => VfxQualityTier.High,
                _ => VfxQualityTier.Low,
            };
            SetTier(next, VfxQualityChangeReason.DebugOverride);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            tier = VfxQualityTier.Medium;
            initialRecommendationApplied = false;
            LastChangeReason = VfxQualityChangeReason.InitialRecommendation;
            Changed = null;
        }
    }
}
