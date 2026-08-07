using System;
using System.Collections.Generic;
using UnityEngine;
using MukJump.Player;

namespace MukJump.Core
{
    /// 판 시작 순간의 영구 성장 스냅샷. 해금 기록은 모두 보존하되,
    /// 각 계보에서 선택한 A·B·C 한 줄기와 공용 뿌리만 한 판에 적용한다.
    public sealed class PermanentGrowthRunSnapshot
    {
        readonly HashSet<string> ownedNodeIds;
        readonly Dictionary<PermanentGrowthBranch, string> activeKeystones;
        readonly bool applyAllOwnedPaths;

        public static PermanentGrowthRunSnapshot Empty { get; } =
            new(Array.Empty<string>(), null);

        public PermanentGrowthRunSnapshot(
            IEnumerable<string> ownedNodeIds,
            IReadOnlyDictionary<PermanentGrowthBranch, string> activeKeystones,
            bool applyAllOwnedPaths = false)
        {
            this.applyAllOwnedPaths = applyAllOwnedPaths;
            this.ownedNodeIds = new HashSet<string>(
                ownedNodeIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            this.activeKeystones = new Dictionary<PermanentGrowthBranch, string>();
            if (activeKeystones != null)
            {
                foreach (KeyValuePair<PermanentGrowthBranch, string> pair
                         in activeKeystones)
                {
                    if (!string.IsNullOrEmpty(pair.Value))
                        this.activeKeystones[pair.Key] = pair.Value;
                }
            }

            InferMissingActivePath(PermanentGrowthBranch.Survival);
            InferMissingActivePath(PermanentGrowthBranch.Leap);
            InferMissingActivePath(PermanentGrowthBranch.InkHandling);
        }

        void InferMissingActivePath(PermanentGrowthBranch branch)
        {
            if (activeKeystones.ContainsKey(branch))
                return;
            PermanentGrowthPath selectedPath = PermanentGrowthPath.None;
            int selectedCount = 0;
            for (PermanentGrowthPath path = PermanentGrowthPath.A;
                 path <= PermanentGrowthPath.C;
                 path++)
            {
                int count = 0;
                foreach (string nodeId in ownedNodeIds)
                {
                    PermanentGrowthNodeDefinition node =
                        PermanentGrowthCatalog.GetNode(nodeId);
                    if (node != null && node.Branch == branch &&
                        PermanentGrowthCatalog.GetPath(node) == path)
                        count++;
                }
                if (count <= selectedCount)
                    continue;
                selectedCount = count;
                selectedPath = path;
            }
            string keystoneId =
                PermanentGrowthCatalog.GetKeystoneId(branch, selectedPath);
            if (!string.IsNullOrEmpty(keystoneId))
                activeKeystones[branch] = keystoneId;
        }

        public int OwnedNodeCount => ownedNodeIds.Count;
        public float InkCapacityMultiplier =>
            (1f + EffectTotal(PermanentGrowthType.InkCapacity)) *
            Mathf.Max(1f, EffectTotal(PermanentGrowthType.InkCapacityDouble));
        public float InkBudgetCostMultiplier => Mathf.Max(
            0.55f,
            1f - EffectTotal(PermanentGrowthType.InkBudgetEfficiency));
        /// 먹 게이지는 오래된 획이 사라진 길이만큼 돌아온다. 회복 성장은
        /// 자연 소멸의 대기시간이 아니라 실제 페이드 속도만 빠르게 한다.
        public float InkRecoverySpeedMultiplier =>
            1f + EffectTotal(PermanentGrowthType.InkRecovery);
        public float NaturalInkHoldBonusSeconds => 0f;
        // 이전 성장 저장·도구 호환용. 새 단일 능력치 트리에서는 사용하지 않는다.
        public float InkEvictionFadeBonusSeconds => 0f;
        public float InkEvictionDelaySeconds => 0f;
        public float ShortStrokeBudgetCostMultiplier => 1f;
        public float JumpChargeMultiplier =>
            Mathf.Max(0.5f, 1f - EffectTotal(PermanentGrowthType.JumpCharge));
        public int MaxHealthBonus => Mathf.Clamp(
            Mathf.RoundToInt(EffectTotal(PermanentGrowthType.Vitality)),
            0,
            PlayerController.MaximumHealth - PlayerController.DefaultMaxHealth);
        /// 먹피 결실은 모든 런타임 분신의 최대 체력을 1칸 늘린다.
        public int InkCloneMaxHealthBonus => Mathf.Clamp(
            Mathf.RoundToInt(EffectTotal(PermanentGrowthType.CloneMaxHealth)),
            0,
            PlayerController.MaximumRuntimeCloneHealth -
            PlayerController.RuntimeCloneMaxHealth);
        public float DamageGraceBonusSeconds =>
            EffectTotal(PermanentGrowthType.DamageGrace);
        public bool HasPostHitShield => false;
        public bool HasGoldenBrushShield =>
            EffectTotal(PermanentGrowthType.GoldenBrushShield) >= 1f;
        public bool HasInkDropEndShield =>
            EffectTotal(PermanentGrowthType.InkDropEndShield) >= 1f;
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
        public float DrawnPlatformChargeMultiplier => 1f;
        public float HitHorizontalRetention => Mathf.Clamp(
            0.82f - EffectTotal(PermanentGrowthType.HitHorizontalStability),
            0.64f,
            0.82f);
        public float MinimumHitRebound => 1.6f;
        public float MinimumPlatformPowerMultiplier => 0.85f;
        public float WindInfluenceMultiplier => 1f;
        public float MaximumFallSpeedMultiplier => 1f;
        public float DoubleJumpVerticalSpeedRatio => HasDoubleJump
            ? EffectTotal(PermanentGrowthType.DoubleJump)
            : 0f;
        public bool HasShortStrokeDiscount => false;
        public bool HasDrawnChargeRhythm => false;
        public bool HasApexHang => false;
        public bool HasLastBreath =>
            EffectTotal(PermanentGrowthType.LastBreath) >= 1f;
        public bool HasStableHit => false;
        public bool HasWallCling =>
            EffectTotal(PermanentGrowthType.WallCling) > 0f;
        public bool HasSafetyPlatform => false;
        public bool HasDoubleJump =>
            EffectTotal(PermanentGrowthType.DoubleJump) > 0f;
        // v3 코드 호환용. 도약 v4에서는 세 효과를 새 구조 패시브로 교체했다.
        public bool HasConsecutiveLandingRhythm => false;
        public bool HasShortPlatformKeystone => false;
        public bool HasLastFallBrake => false;

        public bool HasNode(string nodeId) =>
            !string.IsNullOrEmpty(nodeId) && ownedNodeIds.Contains(nodeId);

        public bool IsNodeApplied(string nodeId)
        {
            if (!HasNode(nodeId))
                return false;
            if (applyAllOwnedPaths)
                return true;
            PermanentGrowthNodeDefinition node =
                PermanentGrowthCatalog.GetNode(nodeId);
            if (node == null)
                return false;
            PermanentGrowthPath path = PermanentGrowthCatalog.GetPath(node);
            if (path == PermanentGrowthPath.None)
                return true;
            return string.Equals(
                GetActiveKeystoneId(node.Branch),
                PermanentGrowthCatalog.GetKeystoneId(node.Branch, path),
                StringComparison.Ordinal);
        }

        public bool IsKeystoneActive(string nodeId)
        {
            PermanentGrowthNodeDefinition node =
                PermanentGrowthCatalog.GetNode(nodeId);
            return node != null && node.IsKeystone && IsNodeApplied(node.Id);
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
                if (node.EffectId != effectId || !IsNodeApplied(node.Id))
                    continue;
                total += node.EffectValue;
            }
            return total;
        }
    }
}
