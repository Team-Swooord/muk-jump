using System;
using System.Collections.Generic;
using UnityEngine;
using MukJump.Player;

namespace MukJump.Core
{
    /// 판 시작 순간의 영구 성장 소유 상태. 구 장착 ID는 저장 호환용으로만 받으며,
    /// 해금한 일반 노드와 비기를 모두 같은 판 스냅샷에서 적용한다.
    public sealed class PermanentGrowthRunSnapshot
    {
        readonly HashSet<string> ownedNodeIds;
        readonly Dictionary<PermanentGrowthBranch, string> activeKeystones;

        public static PermanentGrowthRunSnapshot Empty { get; } =
            new(Array.Empty<string>(), null);

        public PermanentGrowthRunSnapshot(
            IEnumerable<string> ownedNodeIds,
            IReadOnlyDictionary<PermanentGrowthBranch, string> activeKeystones)
        {
            this.ownedNodeIds = new HashSet<string>(
                ownedNodeIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            this.activeKeystones = new Dictionary<PermanentGrowthBranch, string>();
            if (activeKeystones == null)
                return;

            foreach (KeyValuePair<PermanentGrowthBranch, string> pair
                     in activeKeystones)
            {
                if (!string.IsNullOrEmpty(pair.Value))
                    this.activeKeystones[pair.Key] = pair.Value;
            }
        }

        public int OwnedNodeCount => ownedNodeIds.Count;
        public float InkCapacityMultiplier => 1f + EffectTotal(PermanentGrowthType.InkCapacity);
        public float InkBudgetCostMultiplier => Mathf.Max(
            0.55f,
            1f - EffectTotal(PermanentGrowthType.InkBudgetEfficiency));
        /// 먹 게이지는 오래된 획이 사라진 길이만큼 돌아온다. 회복 성장은
        /// 자연 소멸의 대기시간이 아니라 실제 페이드 속도만 빠르게 한다.
        public float InkRecoverySpeedMultiplier =>
            1f + EffectTotal(PermanentGrowthType.InkRecovery);
        // 이전 성장 저장·도구 호환용. 새 단일 능력치 트리에서는 사용하지 않는다.
        public float InkEvictionFadeBonusSeconds => 0f;
        public float InkEvictionDelaySeconds => 0f;
        public float ShortStrokeBudgetCostMultiplier => Mathf.Max(
            0.55f,
            1f - EffectTotal(PermanentGrowthType.ShortStrokeEfficiency));
        public float JumpChargeMultiplier =>
            Mathf.Max(0.5f, 1f - EffectTotal(PermanentGrowthType.JumpCharge));
        public int MaxHealthBonus => Mathf.Clamp(
            Mathf.RoundToInt(EffectTotal(PermanentGrowthType.Vitality)),
            0,
            PlayerController.MaximumHealth - PlayerController.DefaultMaxHealth);
        /// 먹피 IV는 본체의 마지막 체력 1칸과 함께 모든 런타임 분신의
        /// 최대 체력을 기본 1칸에서 2칸으로 올린다.
        public int InkCloneMaxHealthBonus => HasNode("S-KA") ? 1 : 0;
        public float DamageGraceBonusSeconds =>
            EffectTotal(PermanentGrowthType.DamageGrace);
        public bool HasPostHitShield =>
            EffectTotal(PermanentGrowthType.PostHitShield) >= 1f;
        public int InkCloneItemExtraCount => Mathf.Clamp(
            Mathf.RoundToInt(EffectTotal(PermanentGrowthType.InkCloneItemExtraCount)),
            0,
            1);
        public float JumpPowerMultiplier =>
            1f + EffectTotal(PermanentGrowthType.JumpPower);
        /// 노드 수치는 정점 높이 기준이므로 실제 이륙 속도에는 제곱근으로 적용한다.
        public float JumpHeightMultiplier =>
            1f + EffectTotal(PermanentGrowthType.JumpHeight);
        public float JumpVerticalSpeedMultiplier =>
            Mathf.Sqrt(Mathf.Max(1f, JumpHeightMultiplier));
        public float DrawnPlatformLeapMultiplier => 1f;
        public float HitHorizontalRetention => Mathf.Clamp(
            0.82f - EffectTotal(PermanentGrowthType.HitHorizontalStability),
            0.64f,
            0.82f);
        public float MinimumHitRebound => 1.6f;
        public float MinimumPlatformPowerMultiplier => 0.85f;
        public float WindInfluenceMultiplier => 1f;
        public float MaximumFallSpeedMultiplier => 1f;
        public float WallClingDuration => HasWallCling
            ? EffectTotal(PermanentGrowthType.WallCling)
            : 0f;
        public float DoubleJumpVerticalSpeedRatio => HasDoubleJump
            ? EffectTotal(PermanentGrowthType.DoubleJump)
            : 0f;
        public bool HasShortStrokeDiscount => false;
        public bool HasDrawnChargeRhythm => false;
        public bool HasApexHang => false;
        public bool HasLastBreath => false;
        public bool HasStableHit => false;
        public bool HasWallCling => false;
        public bool HasSafetyPlatform => false;
        public bool HasDoubleJump => false;
        // v3 코드 호환용. 도약 v4에서는 세 효과를 새 구조 패시브로 교체했다.
        public bool HasConsecutiveLandingRhythm => false;
        public bool HasShortPlatformKeystone => false;
        public bool HasLastFallBrake => false;

        public bool HasNode(string nodeId) =>
            !string.IsNullOrEmpty(nodeId) && ownedNodeIds.Contains(nodeId);

        public bool IsKeystoneActive(string nodeId)
        {
            PermanentGrowthNodeDefinition node =
                PermanentGrowthCatalog.GetNode(nodeId);
            // 비기는 장비가 아니라 영구 성장의 마지막 열매다. 한 번 해금하면
            // 다른 비기와 교체하지 않고 소유한 모든 비기가 판마다 함께 적용된다.
            return node != null && node.IsKeystone && HasNode(node.Id);
        }

        public string GetActiveKeystoneId(PermanentGrowthBranch branch) =>
            activeKeystones.TryGetValue(branch, out string nodeId)
                ? nodeId
                : string.Empty;

        float EffectTotal(PermanentGrowthType effectId)
        {
            float total = 0f;
            IReadOnlyList<PermanentGrowthNodeDefinition> nodes =
                PermanentGrowthCatalog.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                PermanentGrowthNodeDefinition node = nodes[i];
                if (node.EffectId != effectId || !HasNode(node.Id))
                    continue;
                total += node.EffectValue;
            }
            return total;
        }
    }
}
