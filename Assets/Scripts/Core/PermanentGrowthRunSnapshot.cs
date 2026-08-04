using System;
using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Core
{
    /// 판 시작 순간의 영구 성장 소유·비기 장착 상태.
    /// 로비에서 장착을 바꿔도 이미 시작한 판의 규칙은 변하지 않는다.
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
        public float InkEvictionFadeBonusSeconds =>
            EffectTotal(PermanentGrowthType.InkEvictionFade);
        public float InkEvictionDelaySeconds =>
            EffectTotal(PermanentGrowthType.InkEvictionDelay);
        public float ShortStrokeBudgetCostMultiplier => Mathf.Max(
            0.55f,
            1f - EffectTotal(PermanentGrowthType.ShortStrokeEfficiency));
        public float JumpChargeMultiplier =>
            Mathf.Max(0.5f, 1f - EffectTotal(PermanentGrowthType.JumpCharge));
        public int MaxHealthBonus => HasNode("S-A3") ? 1 : 0;
        public float DamageGraceBonusSeconds =>
            EffectTotal(PermanentGrowthType.DamageGrace);
        public float CloneSpawnGraceBonusSeconds =>
            HasNode("S-C1") ? 0.15f : 0f;
        public float JumpPowerMultiplier =>
            1f + EffectTotal(PermanentGrowthType.JumpPower);
        /// 노드 수치는 정점 높이 기준이므로 실제 이륙 속도에는 제곱근으로 적용한다.
        public float JumpHeightMultiplier =>
            1f + EffectTotal(PermanentGrowthType.JumpHeight);
        public float JumpVerticalSpeedMultiplier =>
            Mathf.Sqrt(Mathf.Max(1f, JumpHeightMultiplier));
        public float DrawnPlatformLeapMultiplier => 1f;
        public float HitHorizontalRetention => HasNode("S-B1") ? 0.90f : 0.82f;
        public float MinimumHitRebound => HasNode("S-B2") ? 1.3f : 1.6f;
        public float MinimumPlatformPowerMultiplier => 0.85f;
        public float WindInfluenceMultiplier => 1f;
        public float MaximumFallSpeedMultiplier => 1f;
        public float WallClingDuration => HasWallCling
            ? EffectTotal(PermanentGrowthType.WallCling)
            : 0f;
        public float DoubleJumpVerticalSpeedRatio => HasDoubleJump
            ? EffectTotal(PermanentGrowthType.DoubleJump)
            : 0f;
        public bool HasShortStrokeDiscount => ShortStrokeBudgetCostMultiplier < 0.9999f;
        public bool HasDrawnChargeRhythm => false;
        public bool HasApexHang => false;
        public bool HasCloneSourceGrace => HasNode("S-C2");
        public bool HasCloneDeathHeal => HasNode("S-C3");
        public bool HasLastBreath => IsKeystoneActive("S-KA");
        public bool HasStableHit => IsKeystoneActive("S-KB");
        public bool HasCloneBond => IsKeystoneActive("S-KC");
        public bool HasWallCling => IsKeystoneActive("J-KA");
        public bool HasSafetyPlatform => IsKeystoneActive("J-KB");
        public bool HasDoubleJump => IsKeystoneActive("J-KC");
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
            return node != null &&
                   node.IsKeystone &&
                   activeKeystones.TryGetValue(node.Branch, out string active) &&
                   string.Equals(active, node.Id, StringComparison.Ordinal);
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
                if (node.IsKeystone && !IsKeystoneActive(node.Id))
                    continue;
                total += node.EffectValue;
            }
            return total;
        }
    }
}
