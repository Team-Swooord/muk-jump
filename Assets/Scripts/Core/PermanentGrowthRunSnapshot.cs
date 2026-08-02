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
        public float InkRecoveryMultiplier => 1f + EffectTotal(PermanentGrowthType.InkRecovery);
        public float PlatformLifetimeMultiplier =>
            1f + EffectTotal(PermanentGrowthType.PlatformLifetime);
        public float JumpChargeMultiplier =>
            Mathf.Max(0.5f, 1f - EffectTotal(PermanentGrowthType.JumpCharge));
        public int MaxHealthBonus => HasNode("S-A3") ? 1 : 0;
        public float DamageGraceBonusSeconds =>
            EffectTotal(PermanentGrowthType.DamageGrace);
        public float CloneSpawnGraceBonusSeconds =>
            HasNode("S-C1") ? 0.15f : 0f;
        public float JumpPowerMultiplier => HasNode("J-B1") ? 1.02f : 1f;
        public float DrawnPlatformLeapMultiplier => HasNode("J-B3") ? 1.03f : 1f;
        public float HitHorizontalRetention => HasNode("S-B1") ? 0.90f : 0.82f;
        public float MinimumHitRebound => HasNode("S-B2") ? 1.3f : 1.6f;
        public float MinimumPlatformPowerMultiplier => HasNode("J-B2") ? 0.90f : 0.85f;
        public float WindInfluenceMultiplier => HasNode("J-C3") ? 0.90f : 1f;
        public float MaximumFallSpeedMultiplier => HasNode("J-C2") ? 0.96f : 1f;
        public bool HasShortStrokeDiscount => HasNode("I-A2");
        public bool HasIdleStrokeDiscount => HasNode("I-A3");
        public bool HasDrawnChargeRhythm => HasNode("J-A3");
        public bool HasApexHang => HasNode("J-C1");
        public bool HasFirstLandingPause => HasNode("I-C3");
        public bool HasCloneSourceGrace => HasNode("S-C2");
        public bool HasCloneDeathHeal => HasNode("S-C3");
        public bool HasHitInkRecovery => HasNode("S-B3");
        public bool HasDrawnLandingInk => HasNode("I-B3");
        public bool HasLastBreath => IsKeystoneActive("S-KA");
        public bool HasStableHit => IsKeystoneActive("S-KB");
        public bool HasCloneBond => IsKeystoneActive("S-KC");
        public bool HasConsecutiveLandingRhythm => IsKeystoneActive("J-KA");
        public bool HasShortPlatformKeystone => IsKeystoneActive("J-KB");
        public bool HasLastFallBrake => IsKeystoneActive("J-KC");
        public bool HasNaturalExpiryRefund => IsKeystoneActive("I-KA");
        public bool HasLowInkRecovery => IsKeystoneActive("I-KB");
        public bool HasSharedStrokeGuard => IsKeystoneActive("I-KC");

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
