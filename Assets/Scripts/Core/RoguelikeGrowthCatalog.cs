using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MukJump.Core
{
    /// 로그라이크 성장 계보의 주된 플레이 영역.
    public enum GrowthCatalogCategory
    {
        Survival,
        Jump,
        InkResource,
        Platform,
        PlatformDefense,
        Item,
        Weather,
        Swarm,
        Shield,
        Hazard,
        Drawing,
        Mastery,
        AirControl,
        Pact,
    }

    /// 한 계보 안에서 노드가 차지하는 단계.
    public enum NodeTier
    {
        Root,
        Branch,
        Completion,
    }

    /// 희귀도는 UI 색상보다 한지 결·붓 테두리 횟수로 표현한다.
    public enum NodeRarity
    {
        Common,
        Rare,
        Epic,
        Legendary,
        Covenant,
    }

    /// Planned 노드는 도감에는 보이지만 런타임 추첨에는 들어가지 않는다.
    public enum ImplementationStatus
    {
        Planned,
        RuntimeReady,
    }

    /// UI와 추첨기가 공유하는 불변 성장 노드 데이터.
    public sealed class RoguelikeGrowthDefinition
    {
        readonly ReadOnlyCollection<string> prerequisiteIds;
        readonly ReadOnlyCollection<string> requiredPrerequisiteIds;
        readonly ReadOnlyCollection<string> alternativePrerequisiteIds;
        readonly ReadOnlyCollection<string> exclusionIds;

        internal RoguelikeGrowthDefinition(
            string id,
            string familyId,
            string familyName,
            string name,
            string description,
            string synergy,
            string unlockHint,
            GrowthCatalogCategory category,
            NodeTier tier,
            NodeRarity rarity,
            ImplementationStatus status,
            int maxLevel,
            IEnumerable<string> requiredPrerequisites,
            IEnumerable<string> alternativePrerequisites,
            IEnumerable<string> exclusions,
            GrowthUpgradeType? runtimeType)
        {
            Id = id;
            FamilyId = familyId;
            FamilyName = familyName;
            Name = name;
            Description = description;
            Synergy = synergy;
            UnlockHint = unlockHint;
            Category = category;
            Tier = tier;
            Rarity = rarity;
            Status = status;
            MaxLevel = maxLevel;
            RuntimeType = runtimeType;

            var required = CopyDistinct(requiredPrerequisites);
            var alternatives = CopyDistinct(alternativePrerequisites);
            var flattened = new List<string>(required.Count + alternatives.Count);
            AppendDistinct(flattened, required);
            AppendDistinct(flattened, alternatives);

            requiredPrerequisiteIds = required.AsReadOnly();
            alternativePrerequisiteIds = alternatives.AsReadOnly();
            prerequisiteIds = flattened.AsReadOnly();
            exclusionIds = CopyDistinct(exclusions).AsReadOnly();
        }

        public string Id { get; }
        public string FamilyId { get; }
        public string FamilyName { get; }
        public string Name { get; }
        public string Description { get; }
        public string Effect => Description;
        public string Synergy { get; }
        public string UnlockHint { get; }
        public GrowthCatalogCategory Category { get; }
        public NodeTier Tier { get; }
        public NodeRarity Rarity { get; }
        public ImplementationStatus Status { get; }
        public int MaxLevel { get; }
        public IReadOnlyList<string> PrerequisiteIds => prerequisiteIds;
        public IReadOnlyList<string> RequiredPrerequisiteIds =>
            requiredPrerequisiteIds;
        public IReadOnlyList<string> AlternativePrerequisiteIds =>
            alternativePrerequisiteIds;
        public IReadOnlyList<string> ExclusionIds => exclusionIds;
        public GrowthUpgradeType? RuntimeType { get; }

        static List<string> CopyDistinct(IEnumerable<string> source)
        {
            var result = new List<string>();
            AppendDistinct(result, source);
            return result;
        }

        static void AppendDistinct(List<string> destination, IEnumerable<string> source)
        {
            if (source == null) return;
            foreach (string value in source)
            {
                if (string.IsNullOrWhiteSpace(value) || destination.Contains(value))
                    continue;
                destination.Add(value);
            }
        }
    }

    /// 카탈로그 무결성 감사 결과.
    public sealed class RoguelikeGrowthCatalogValidation
    {
        internal RoguelikeGrowthCatalogValidation(List<string> errors)
        {
            Errors = errors.AsReadOnly();
        }

        public bool IsValid => Errors.Count == 0;
        public IReadOnlyList<string> Errors { get; }
    }

    /// 25계보 × 4노드의 단일 진실 공급원.
    /// 현재 RunGrowthController가 지원하는 여덟 뿌리만 RuntimeReady다.
    public static class RoguelikeGrowthCatalog
    {
        const int ExpectedFamilyCount = 25;
        const int ExpectedDefinitionCount = ExpectedFamilyCount * 4;

        static readonly ReadOnlyCollection<RoguelikeGrowthDefinition> all;
        static readonly ReadOnlyCollection<RoguelikeGrowthDefinition> runtimeReady;
        static readonly Dictionary<string, RoguelikeGrowthDefinition> byId;
        static readonly Dictionary<string, int> indexById;
        static readonly Dictionary<GrowthUpgradeType, RoguelikeGrowthDefinition>
            byRuntimeType;

        static RoguelikeGrowthCatalog()
        {
            var built = BuildDefinitions();
            all = built.AsReadOnly();
            byId = new Dictionary<string, RoguelikeGrowthDefinition>(
                built.Count, StringComparer.Ordinal);
            indexById = new Dictionary<string, int>(
                built.Count, StringComparer.Ordinal);
            byRuntimeType =
                new Dictionary<GrowthUpgradeType, RoguelikeGrowthDefinition>();
            var ready = new List<RoguelikeGrowthDefinition>();

            for (int i = 0; i < built.Count; i++)
            {
                RoguelikeGrowthDefinition definition = built[i];
                if (!byId.ContainsKey(definition.Id))
                {
                    byId.Add(definition.Id, definition);
                    indexById.Add(definition.Id, i);
                }
                if (definition.Status == ImplementationStatus.RuntimeReady)
                    ready.Add(definition);
                if (definition.RuntimeType.HasValue &&
                    !byRuntimeType.ContainsKey(definition.RuntimeType.Value))
                {
                    byRuntimeType.Add(definition.RuntimeType.Value, definition);
                }
            }

            runtimeReady = ready.AsReadOnly();
        }

        public static IReadOnlyList<RoguelikeGrowthDefinition> All => all;
        public static IReadOnlyList<RoguelikeGrowthDefinition> RuntimeReady =>
            runtimeReady;

        public static bool TryGet(
            string id,
            out RoguelikeGrowthDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }
            return byId.TryGetValue(id, out definition);
        }

        public static bool TryGetRuntimeType(
            string id,
            out GrowthUpgradeType runtimeType)
        {
            if (TryGet(id, out RoguelikeGrowthDefinition definition) &&
                definition.RuntimeType.HasValue)
            {
                runtimeType = definition.RuntimeType.Value;
                return true;
            }

            runtimeType = default;
            return false;
        }

        /// 도감 가상화 목록에서 stable ID의 고정 정렬 위치를 O(1)로 찾는다.
        /// 알 수 없는 ID는 예외 대신 -1을 반환한다.
        public static int IndexOf(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return -1;
            return indexById.TryGetValue(id, out int index) ? index : -1;
        }

        public static bool TryGetDefinition(
            GrowthUpgradeType runtimeType,
            out RoguelikeGrowthDefinition definition)
        {
            return byRuntimeType.TryGetValue(runtimeType, out definition);
        }

        /// ID·계보 형태·선행·상충·런타임 어댑터를 한 번에 감사한다.
        public static RoguelikeGrowthCatalogValidation Validate()
        {
            var errors = new List<string>();
            ValidateCounts(errors);
            ValidateDefinitions(errors);
            ValidateFamilies(errors);
            ValidateRuntimeAdapters(errors);
            return new RoguelikeGrowthCatalogValidation(errors);
        }

        public static bool TryValidate(out IReadOnlyList<string> errors)
        {
            RoguelikeGrowthCatalogValidation validation = Validate();
            errors = validation.Errors;
            return validation.IsValid;
        }

        static void ValidateCounts(List<string> errors)
        {
            if (all.Count != ExpectedDefinitionCount)
            {
                errors.Add(
                    $"성장 노드는 정확히 {ExpectedDefinitionCount}개여야 하지만 " +
                    $"{all.Count}개입니다.");
            }
        }

        static void ValidateDefinitions(List<string> errors)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < all.Count; i++)
            {
                RoguelikeGrowthDefinition definition = all[i];
                if (string.IsNullOrWhiteSpace(definition.Id))
                    errors.Add($"인덱스 {i}의 ID가 비어 있습니다.");
                else if (!ids.Add(definition.Id))
                    errors.Add($"중복 ID: {definition.Id}");

                if (string.IsNullOrWhiteSpace(definition.FamilyId))
                    errors.Add($"{definition.Id}: FamilyId가 비어 있습니다.");
                if (string.IsNullOrWhiteSpace(definition.FamilyName))
                    errors.Add($"{definition.Id}: FamilyName이 비어 있습니다.");
                if (string.IsNullOrWhiteSpace(definition.Name))
                    errors.Add($"{definition.Id}: Name이 비어 있습니다.");
                if (string.IsNullOrWhiteSpace(definition.Description))
                    errors.Add($"{definition.Id}: Description이 비어 있습니다.");
                if (string.IsNullOrWhiteSpace(definition.Synergy))
                    errors.Add($"{definition.Id}: Synergy가 비어 있습니다.");
                if (string.IsNullOrWhiteSpace(definition.UnlockHint))
                    errors.Add($"{definition.Id}: UnlockHint가 비어 있습니다.");
                if (definition.MaxLevel < 1)
                    errors.Add($"{definition.Id}: MaxLevel은 1 이상이어야 합니다.");
            }

            for (int i = 0; i < all.Count; i++)
            {
                RoguelikeGrowthDefinition definition = all[i];
                ValidateReferences(
                    definition, definition.PrerequisiteIds, "선행", ids, errors);
                ValidateReferences(
                    definition, definition.ExclusionIds, "상충", ids, errors);
            }
        }

        static void ValidateReferences(
            RoguelikeGrowthDefinition owner,
            IReadOnlyList<string> references,
            string label,
            HashSet<string> ids,
            List<string> errors)
        {
            for (int i = 0; i < references.Count; i++)
            {
                string referencedId = references[i];
                if (referencedId == owner.Id)
                    errors.Add($"{owner.Id}: 자기 자신을 {label} 참조합니다.");
                else if (!ids.Contains(referencedId))
                    errors.Add($"{owner.Id}: 없는 {label} ID {referencedId}");
            }
        }

        static void ValidateFamilies(List<string> errors)
        {
            var families =
                new Dictionary<string, List<RoguelikeGrowthDefinition>>(
                    StringComparer.Ordinal);
            for (int i = 0; i < all.Count; i++)
            {
                RoguelikeGrowthDefinition definition = all[i];
                if (!families.TryGetValue(
                        definition.FamilyId,
                        out List<RoguelikeGrowthDefinition> family))
                {
                    family = new List<RoguelikeGrowthDefinition>(4);
                    families.Add(definition.FamilyId, family);
                }
                family.Add(definition);
            }

            if (families.Count != ExpectedFamilyCount)
            {
                errors.Add(
                    $"성장 계보는 정확히 {ExpectedFamilyCount}개여야 하지만 " +
                    $"{families.Count}개입니다.");
            }

            foreach (KeyValuePair<string, List<RoguelikeGrowthDefinition>> pair
                     in families)
            {
                List<RoguelikeGrowthDefinition> family = pair.Value;
                if (family.Count != 4)
                {
                    errors.Add($"{pair.Key}: 노드가 {family.Count}개입니다.");
                    continue;
                }

                var roots = FindTier(family, NodeTier.Root);
                var branches = FindTier(family, NodeTier.Branch);
                var completions = FindTier(family, NodeTier.Completion);
                if (roots.Count != 1 || branches.Count != 2 ||
                    completions.Count != 1)
                {
                    errors.Add(
                        $"{pair.Key}: 뿌리 1·가지 2·완성 1 구조가 아닙니다.");
                    continue;
                }

                RoguelikeGrowthDefinition root = roots[0];
                RoguelikeGrowthDefinition firstBranch = branches[0];
                RoguelikeGrowthDefinition secondBranch = branches[1];
                RoguelikeGrowthDefinition completion = completions[0];

                ValidateBranch(
                    firstBranch, root, secondBranch, errors);
                ValidateBranch(
                    secondBranch, root, firstBranch, errors);

                if (!Contains(
                        completion.RequiredPrerequisiteIds, root.Id))
                {
                    errors.Add(
                        $"{completion.Id}: 완성 노드가 뿌리를 필수 선행으로 " +
                        "갖지 않습니다.");
                }
                if (completion.AlternativePrerequisiteIds.Count != 2 ||
                    !Contains(
                        completion.AlternativePrerequisiteIds, firstBranch.Id) ||
                    !Contains(
                        completion.AlternativePrerequisiteIds, secondBranch.Id))
                {
                    errors.Add(
                        $"{completion.Id}: 두 가지 중 하나를 대안 선행으로 " +
                        "가져야 합니다.");
                }
            }
        }

        static void ValidateBranch(
            RoguelikeGrowthDefinition branch,
            RoguelikeGrowthDefinition root,
            RoguelikeGrowthDefinition sibling,
            List<string> errors)
        {
            if (branch.RequiredPrerequisiteIds.Count != 1 ||
                !Contains(branch.RequiredPrerequisiteIds, root.Id))
            {
                errors.Add(
                    $"{branch.Id}: 같은 계보의 뿌리 하나만 필수 선행이어야 합니다.");
            }
            if (branch.ExclusionIds.Count != 1 ||
                !Contains(branch.ExclusionIds, sibling.Id))
            {
                errors.Add(
                    $"{branch.Id}: 반대 가지 {sibling.Id}와 상호 배타가 아닙니다.");
            }
        }

        static void ValidateRuntimeAdapters(List<string> errors)
        {
            Array values = Enum.GetValues(typeof(GrowthUpgradeType));
            if (runtimeReady.Count != values.Length)
            {
                errors.Add(
                    $"RuntimeReady는 {values.Length}개여야 하지만 " +
                    $"{runtimeReady.Count}개입니다.");
            }

            for (int i = 0; i < all.Count; i++)
            {
                RoguelikeGrowthDefinition definition = all[i];
                bool hasRuntimeType = definition.RuntimeType.HasValue;
                bool isReady =
                    definition.Status == ImplementationStatus.RuntimeReady;
                if (hasRuntimeType != isReady)
                {
                    errors.Add(
                        $"{definition.Id}: RuntimeType과 RuntimeReady 상태가 " +
                        "일치하지 않습니다.");
                }
                if (isReady && definition.Tier != NodeTier.Root)
                {
                    errors.Add($"{definition.Id}: RuntimeReady는 뿌리여야 합니다.");
                }
            }

            foreach (GrowthUpgradeType runtimeType in values)
            {
                if (!byRuntimeType.ContainsKey(runtimeType))
                    errors.Add($"{runtimeType}: 카탈로그 어댑터가 없습니다.");
            }
        }

        static List<RoguelikeGrowthDefinition> FindTier(
            List<RoguelikeGrowthDefinition> family,
            NodeTier tier)
        {
            var matches = new List<RoguelikeGrowthDefinition>();
            for (int i = 0; i < family.Count; i++)
                if (family[i].Tier == tier)
                    matches.Add(family[i]);
            return matches;
        }

        static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return true;
            return false;
        }

        static List<RoguelikeGrowthDefinition> BuildDefinitions()
        {
            var result =
                new List<RoguelikeGrowthDefinition>(ExpectedDefinitionCount);

            AddFamily(
                result, "family.ink_armor", "먹갑옷",
                GrowthCatalogCategory.Survival,
                Root("growth.vitality", "먹두께",
                    "최대 Lv.3. 단계마다 먹떼 공용 장애물 완충 +1.",
                    3, "처음부터 해금", "방패울림·먹떼호흡",
                    GrowthUpgradeType.Vitality),
                Branch("growth.vitality.afterecho", "먹울림",
                    "최대 Lv.2. 방패·완충 소모 뒤 피격 유예 +0.2초/단계.",
                    2, NodeRarity.Rare, "먹두께 획득",
                    "방패울림"),
                Branch("growth.vitality.quiet_mend", "고요한 아묾",
                    "최대 Lv.2. 무피격 40/35m마다 완충 1회 회복.",
                    2, NodeRarity.Epic, "먹두께 획득",
                    "긴 생존·길운"),
                Completion("growth.vitality.ink_armor", "먹갑옷",
                    "완충 시 겹친 위험 판정을 밀어내고 먹 15% 회복.",
                    "먹갑옷 가지 완성", "방패·먹떼 빌드"));

            AddFamily(
                result, "family.mountain_bird_leap", "산새도약",
                GrowthCatalogCategory.Jump,
                Root("growth.jump_power", "도약",
                    "최대 Lv.5. 일반 자동 점프력 +4%/단계.",
                    5, "처음부터 해금", "정점숨·중심잡기",
                    GrowthUpgradeType.JumpPower),
                Branch("growth.jump.slope_reader", "비탈읽기",
                    "최대 Lv.2. 발판 기울기의 수평 추진 영향 +10%/단계.",
                    2, NodeRarity.Rare, "도약 2회 선택",
                    "엇갈린 산길"),
                Branch("growth.jump.upright", "올곧은 뜀",
                    "최대 Lv.2. 기울기 수평 힘의 18%/단계를 수직 힘으로 전환.",
                    2, NodeRarity.Rare, "도약 2회 선택",
                    "고른 발"),
                Completion("growth.jump.mountain_bird", "산새의 큰뜀",
                    "매 세 번째 착지 점프가 25% 강해지고 횡풍 영향을 받지 않음.",
                    "산새도약 가지 완성", "한붓산맥·바람읽기"));

            AddFamily(
                result, "family.bottomless_inkstone", "끝없는 벼루",
                GrowthCatalogCategory.InkResource,
                Root("growth.ink_capacity", "큰 벼루",
                    "최대 Lv.4. 최대 먹 +10%/단계.",
                    4, "처음부터 해금", "먹샘·여분먹",
                    GrowthUpgradeType.InkCapacity),
                Branch("growth.ink_capacity.overflow", "넘친 벼루",
                    "최대 Lv.2. 자연 회복 초과분을 최대 먹의 15%/단계까지 저장.",
                    2, NodeRarity.Epic, "큰 벼루 2회 선택",
                    "먹샘·여분먹"),
                Branch("growth.ink_capacity.weighted", "묵직한 벼루",
                    "최대 Lv.2. 먹 80% 이상일 때 발판 수명 +15%/단계, 비용 +5%.",
                    2, NodeRarity.Rare, "큰 벼루 2회 선택",
                    "진먹·긴 여운"),
                Completion("growth.ink_capacity.bottomless", "끝없는 벼루",
                    "먹이 2초간 가득 차면 다음 유효 획의 첫 2m가 무료.",
                    "끝없는 벼루 가지 완성", "담묵·진먹"));

            AddFamily(
                result, "family.mountain_ink_spring", "산속 먹샘",
                GrowthCatalogCategory.InkResource,
                Root("growth.ink_recovery", "먹샘",
                    "최대 Lv.4. 먹 회복 +12%/단계.",
                    4, "처음부터 해금", "큰 벼루·빗물받이",
                    GrowthUpgradeType.InkRecovery),
                Branch("growth.ink_recovery.falling", "낙하샘",
                    "최대 Lv.2. 모든 생존자가 하강 중이면 회복 +25%/단계.",
                    2, NodeRarity.Rare, "먹샘 2회 선택",
                    "정점숨"),
                Branch("growth.ink_recovery.landing", "착지샘",
                    "최대 Lv.2. 접지 중 회복 +30%/단계, 점프 대기 +0.08초.",
                    2, NodeRarity.Rare, "먹샘 2회 선택",
                    "중심잡기"),
                Completion("growth.ink_recovery.mountain_spring", "산속 먹샘",
                    "착지할 때 최대 먹 8% 즉시 회복, 재사용 2.5초.",
                    "산속 먹샘 가지 완성", "중심잡기"));

            AddFamily(
                result, "family.thousand_year_stroke", "천년 먹선",
                GrowthCatalogCategory.Platform,
                Root("growth.platform_lifetime", "긴 여운",
                    "최대 Lv.3. 임시 발판 수명 +10%/단계.",
                    3, "처음부터 해금", "진먹·이은획",
                    GrowthUpgradeType.PlatformLifetime),
                Branch("growth.platform_lifetime.afterglow", "남은 여운",
                    "최대 Lv.2. 수명 종료 뒤 희미한 충돌이 0.35초/단계 남음.",
                    2, NodeRarity.Rare, "긴 여운 획득",
                    "담묵"),
                Branch("growth.platform_lifetime.first_anchor", "첫 먹자리",
                    "최대 Lv.1. 성장 선택 뒤 첫 발판은 최초 착지 때 타이머 시작.",
                    1, NodeRarity.Epic, "긴 여운 획득",
                    "이은획"),
                Completion("growth.platform_lifetime.thousand_year", "천년 먹선",
                    "캐릭터가 서 있는 동안 타이머 정지, 발판당 최대 5초.",
                    "천년 먹선 가지 완성", "먹떼호흡"));

            AddFamily(
                result, "family.five_peaks", "다섯 봉우리",
                GrowthCatalogCategory.Platform,
                Root("growth.platform_slots", "겹친 획",
                    "최대 Lv.1. 임시 발판 상한 4→5.",
                    1, "처음부터 해금", "이은획·긴 여운",
                    GrowthUpgradeType.PlatformSlots),
                Branch("growth.platform_slots.joined_peak", "맞댄 봉우리",
                    "최대 Lv.2. 기존 끝점과의 연결 범위 +0.15m/단계.",
                    2, NodeRarity.Rare, "겹친 획 획득",
                    "이은획"),
                Branch("growth.platform_slots.reclaim", "되찾은 먹",
                    "최대 Lv.2. 상한 초과로 밀린 발판 비용의 15%/단계 환급.",
                    2, NodeRarity.Rare, "겹친 획 획득",
                    "큰 벼루"),
                Completion("growth.platform_slots.five_peaks", "다섯 봉우리",
                    "서로 다른 발판 3개 연속 착지 시 가장 오래된 발판 +1초, 먹 15% 회복.",
                    "다섯 봉우리 가지 완성", "중심잡기"));

            AddFamily(
                result, "family.diamond_ink_stroke", "금강 먹선",
                GrowthCatalogCategory.PlatformDefense,
                Root("growth.stroke_guard", "굳은 획",
                    "최대 Lv.1. 새 임시 발판이 낙묵석 1회 방어.",
                    1, "처음부터 해금", "낙묵갈이",
                    GrowthUpgradeType.StrokeGuard),
                Branch("growth.stroke_guard.grind", "돌을 간 획",
                    "최대 Lv.2. 낙묵석 방어 시 최대 먹 10%/단계 회복.",
                    2, NodeRarity.Rare, "굳은 획 획득",
                    "낙묵갈이"),
                Branch("growth.stroke_guard.polished", "다듬은 획",
                    "최대 Lv.1. 방어 충전이 2회가 되지만 수명 -15%.",
                    1, NodeRarity.Epic, "굳은 획 획득",
                    "진먹"),
                Completion("growth.stroke_guard.diamond_ink", "금강 먹선",
                    "매 네 번째 유효 발판이 용·해태·낙묵석 중 하나를 1회 막음.",
                    "금강 먹선 가지 완성", "장애물 계보"));

            AddFamily(
                result, "family.sevenfold_fortune", "일곱 겹 길운",
                GrowthCatalogCategory.Item,
                Root("growth.item_fortune", "길운",
                    "최대 Lv.3. 다음 일반 아이템 간격 -7%/단계.",
                    3, "처음부터 해금", "여분먹·분신싹",
                    GrowthUpgradeType.ItemFortune),
                Branch("growth.item_fortune.returning_fate", "돌아온 인연",
                    "최대 Lv.2. 놓친 아이템 종류의 가중치 +25%/단계, 획득 시 초기화.",
                    2, NodeRarity.Rare, "길운 획득",
                    "아이템 집중 빌드"),
                Branch("growth.item_fortune.paired", "나란한 복",
                    "최대 Lv.1. 매 세 번째 스폰이 둘 중 하나만 고르는 아이템 쌍으로 등장.",
                    1, NodeRarity.Epic, "길운 획득",
                    "아이템 선택 빌드"),
                Completion("growth.item_fortune.sevenfold", "일곱 겹 길운",
                    "매 일곱 번째 일반 아이템 효과가 2초 뒤 60%로 한 번 반복.",
                    "일곱 겹 길운 가지 완성", "모든 아이템 빌드"));

            AddFamily(
                result, "family.wind_eye", "바람의 눈",
                GrowthCatalogCategory.Weather,
                Root("growth.wind.reading", "바람읽기",
                    "최대 Lv.3. 바람 방향으로 오르는 발판 수명 +10%/단계.",
                    3, "풍향을 따라 발판 3회 착지", "엇갈린 산길"),
                Branch("growth.wind.reed", "바람갈대",
                    "최대 Lv.2. 일반 횡풍 가속 -18%/단계.",
                    2, NodeRarity.Rare, "바람읽기 획득",
                    "안정형 도약"),
                Branch("growth.wind.sail", "순풍돛",
                    "최대 Lv.2. 횡풍 +20%/단계, 순풍 점프 수평력 +8%/단계.",
                    2, NodeRarity.Epic, "바람읽기 획득",
                    "비탈읽기"),
                Completion("growth.wind.eye", "바람의 눈",
                    "풍향 반전을 2.5초 미리 표시하고 반전 뒤 첫 순풍 획 무료.",
                    "바람의 눈 가지 완성", "풍맥디딤"));

            AddFamily(
                result, "family.rain_landscape", "우중산수",
                GrowthCatalogCategory.Weather,
                Root("growth.rain.collector", "빗물받이",
                    "최대 Lv.3. 먹비 구간 먹 회복 +15%/단계.",
                    3, "먹비 계곡 500m 발견", "먹샘"),
                Branch("growth.rain.lacquer", "먹비옻칠",
                    "최대 Lv.2. 먹비 수명 감소를 절반씩 완화, 획 비용 +10%.",
                    2, NodeRarity.Epic, "빗물받이 획득",
                    "긴 여운"),
                Branch("growth.rain.washed_ink", "씻긴 먹",
                    "최대 Lv.2. 먹비로 빨리 마른 발판 비용 20%/단계 환급.",
                    2, NodeRarity.Rare, "빗물받이 획득",
                    "큰 벼루"),
                Completion("growth.rain.landscape", "우중산수",
                    "먹비 구간의 매 세 번째 유효 획이 비용 절반·낙묵석 방어 1회 획득.",
                    "우중산수 가지 완성", "굳은 획"));

            AddFamily(
                result, "family.swarm_painting", "한 폭 먹떼",
                GrowthCatalogCategory.Swarm,
                Root("growth.swarm.breath", "먹떼호흡",
                    "최대 Lv.3. 생존 분신 4마리마다 최대 먹 +3%/단계.",
                    3, "한 판에서 분신 2마리 보유", "큰 벼루"),
                Branch("growth.swarm.spacing", "벌어진 먹떼",
                    "최대 Lv.2. 가까운 분신 사이 약한 수평 벌림 +0.2m/s/단계.",
                    2, NodeRarity.Rare, "먹떼호흡 획득",
                    "넓은 발판"),
                Branch("growth.swarm.stagger", "나란한 뜀",
                    "최대 Lv.2. 같은 발판의 분신 점프를 0.08초/단계씩 분산.",
                    2, NodeRarity.Rare, "먹떼호흡 획득",
                    "좁은 발판"),
                Completion("growth.swarm.one_painting", "한 폭 먹떼",
                    "서로 다른 분신 3마리가 1.2초 안에 착지하면 먹 12%와 발판 수명 0.5초 회복.",
                    "한 폭 먹떼 가지 완성", "중심잡기"));

            AddFamily(
                result, "family.full_swarm", "가득 찬 먹떼",
                GrowthCatalogCategory.Swarm,
                Root("growth.clone.sprout", "분신싹",
                    "최대 Lv.2. 8마리 미만에서 분신 아이템의 추가 분신 확률 +20%/단계.",
                    2, "한 판에서 분신 아이템 3회 획득", "길운"),
                Branch("growth.clone.twin_sprout", "쌍싹",
                    "최대 Lv.1. 6마리 미만에서는 분신 아이템이 항상 2마리 생성.",
                    1, NodeRarity.Epic, "분신싹 최대", "초반 먹떼"),
                Branch("growth.clone.rebloom", "되살이먹",
                    "최대 Lv.1. 매 세 번째 분신 사망이 다음 아이템 획득 때 1마리로 부활.",
                    1, NodeRarity.Epic, "분신싹 최대", "후반 생존"),
                Completion("growth.clone.full_swarm", "가득 찬 먹떼",
                    "12마리 이상에서 분신 아이템은 분신 +1과 먹 20%·완충 1회 부여.",
                    "가득 찬 먹떼 가지 완성", "먹갑옷"));

            AddFamily(
                result, "family.roof_tile_shield", "기와겹방패",
                GrowthCatalogCategory.Shield,
                Root("growth.shield.echo", "방패울림",
                    "최대 Lv.3. 방패 파괴 시 최대 먹 12%/단계 회복.",
                    3, "방패 아이템으로 피해 3회 방어", "먹두께"),
                Branch("growth.shield.neighbor", "이웃 방패",
                    "최대 Lv.2. 2m/단계 안의 방패 보유 분신이 대신 방어 가능.",
                    2, NodeRarity.Rare, "방패울림 획득", "먹떼호흡"),
                Branch("growth.shield.rebound", "되튄 방패",
                    "최대 Lv.2. 방패 파괴 반등 목표 높이 +4m/단계.",
                    2, NodeRarity.Rare, "방패울림 획득", "도약"),
                Completion("growth.shield.roof_tile", "기와겹방패",
                    "60m마다 처음 얻는 방패가 가장 낮은 생존 분신 한 마리에도 복제.",
                    "기와겹방패 가지 완성", "먹떼호흡"));

            AddFamily(
                result, "family.sky_current", "하늘풍맥",
                GrowthCatalogCategory.Weather,
                Root("growth.current.step", "풍맥디딤",
                    "최대 Lv.3. 풍맥 발판 상승 높이 +10%/단계.",
                    3, "풍맥 발판 5회 사용", "바람읽기"),
                Branch("growth.current.cloud_foot", "구름발",
                    "최대 Lv.1. 상승기류 경고 중 첫 획은 기류 종료까지 슬롯 미사용.",
                    1, NodeRarity.Epic, "풍맥디딤 획득", "겹친 획"),
                Branch("growth.current.wind_ladder", "바람사다리",
                    "최대 Lv.2. 풍맥 뒤 다음 순풍 발판이 추가로 3m/단계 상승.",
                    2, NodeRarity.Rare, "풍맥디딤 획득", "바람읽기"),
                Completion("growth.current.sky_path", "하늘풍맥",
                    "풍맥 발판 3회 사용 시 다음 상승기류가 24m 빨라지고 먹 20% 회복.",
                    "하늘풍맥 가지 완성", "바람의 눈"));

            AddFamily(
                result, "family.rock_inkstone", "벼루가 된 돌",
                GrowthCatalogCategory.Hazard,
                Root("growth.rock.grinding", "낙묵갈이",
                    "최대 Lv.3. 발판으로 낙묵석 파괴 시 먹 5%/단계 회복.",
                    3, "낙묵석 5개를 발판으로 파괴", "굳은 획"),
                Branch("growth.rock.distant_echo", "먼 울림",
                    "최대 Lv.2. 낙묵석 경고 시간 +0.25초/단계.",
                    2, NodeRarity.Rare, "낙묵갈이 획득", "안정형 방어"),
                Branch("growth.rock.bait", "먹돌 미끼",
                    "최대 Lv.1. 경고 구역에 그린 획의 중앙으로 낙하 목표 변경.",
                    1, NodeRarity.Epic, "낙묵갈이 획득", "숙련형 방어"),
                Completion("growth.rock.inkstone", "벼루가 된 돌",
                    "세 번째로 파괴한 낙묵석이 붓 여유 35%를 남김.",
                    "벼루가 된 돌 가지 완성", "여분먹"));

            AddFamily(
                result, "family.ink_dragon_path", "먹룡의 길",
                GrowthCatalogCategory.Hazard,
                Root("growth.dragon.trace", "용길읽기",
                    "최대 Lv.3. 용 뒤 1m 안에서 획 완성 시 용 이동 -20%, 0.6초/단계.",
                    3, "어린 용 뒤에서 발판 3회 완성", "바람읽기"),
                Branch("growth.dragon.tailwind", "용꼬리바람",
                    "최대 Lv.2. 용의 뒤를 통과하면 횡풍 면역 1초/단계.",
                    2, NodeRarity.Rare, "용길읽기 획득", "순풍돛"),
                Branch("growth.dragon.scale_bounce", "비늘튕김",
                    "최대 Lv.1. 용이 굳은 획과 충돌하면 방향을 한 번 반전.",
                    1, NodeRarity.Epic, "용길읽기 획득", "굳은 획"),
                Completion("growth.dragon.ink_path", "먹룡의 길",
                    "용과 상호작용한 뒤 지나온 궤적이 2초간 희미한 풍맥 발판으로 변함.",
                    "먹룡의 길 가지 완성", "풍맥디딤"));

            AddFamily(
                result, "family.haetae_permission", "해태의 허락",
                GrowthCatalogCategory.Hazard,
                Root("growth.haetae.footprint", "해태발자국",
                    "최대 Lv.2. 해태를 막은 자리의 흔적이 1.5초/단계 풍맥 발판으로 남음.",
                    2, "먹해태를 발판으로 2회 방어", "풍맥디딤"),
                Branch("growth.haetae.guardian_eye", "문지기 눈",
                    "최대 Lv.2. 해태 예고 +0.2초/단계, 표적 경로 가독성 증가.",
                    2, NodeRarity.Rare, "해태발자국 획득", "안정형 방어"),
                Branch("growth.haetae.tile_claw", "기와발톱",
                    "최대 Lv.2. 해태 방어 시 해당 발판 수명 +0.8초·먹 8%/단계.",
                    2, NodeRarity.Epic, "해태발자국 획득", "숙련형 방어"),
                Completion("growth.haetae.permission", "해태의 허락",
                    "250m 구간당 첫 해태 방어 후 다음 공격 장애물 예약을 한 번 건너뜀.",
                    "해태의 허락 가지 완성", "생존 계보"));

            AddFamily(
                result, "family.endless_mountain_range", "끝없는 산맥",
                GrowthCatalogCategory.Drawing,
                Root("growth.stroke.connected_breath", "이은획",
                    "최대 Lv.3. 이전 끝점 0.4m 안에서 시작한 획의 첫 0.8m 비용 -15%/단계.",
                    3, "연결 획 3회 생성", "겹친 획"),
                Branch("growth.stroke.touching", "맞댄 획",
                    "최대 Lv.2. 연결 판정 거리 +0.15m/단계.",
                    2, NodeRarity.Rare, "이은획 획득", "맞댄 봉우리"),
                Branch("growth.stroke.one_brush_range", "한붓산맥",
                    "최대 Lv.2. 연결 3회째 획 비용 -25%/단계.",
                    2, NodeRarity.Rare, "이은획 획득", "큰 벼루"),
                Completion("growth.stroke.endless_range", "끝없는 산맥",
                    "새 연결 획 생성 때 같은 연결망의 가장 오래된 발판 수명 +0.8초.",
                    "끝없는 산맥 가지 완성", "긴 여운"));

            AddFamily(
                result, "family.dawn_light_ink", "새벽 담묵",
                GrowthCatalogCategory.Drawing,
                Root("growth.light_ink.root", "담묵",
                    "최대 Lv.2. 획 비용 -18%/단계, 발판 수명 -15%/단계.",
                    2, "짧은 획만으로 50m 상승", "빠른 도약"),
                Branch("growth.light_ink.short_poem", "짧은 시",
                    "최대 Lv.2. 2.2m 이하 획 비용 추가 -12%/단계.",
                    2, NodeRarity.Rare, "담묵 획득", "짧은 발판"),
                Branch("growth.light_ink.mist_bridge", "안개다리",
                    "최대 Lv.2. 사라진 담묵 발판이 0.3초/단계 단방향 잔상 유지.",
                    2, NodeRarity.Epic, "담묵 획득", "남은 여운"),
                Completion("growth.light_ink.dawn", "새벽 담묵",
                    "매 세 번째 담묵 발판은 무료이며 기본 수명으로 생성.",
                    "새벽 담묵 가지 완성", "빠른 도약"));

            AddFamily(
                result, "family.iron_ink_mountain", "철묵산",
                GrowthCatalogCategory.Drawing,
                Root("growth.dense_ink.root", "진먹",
                    "최대 Lv.2. 획 비용 +15%/단계, 발판 수명 +25%/단계.",
                    2, "긴 획 하나로 30m 상승", "큰 벼루"),
                Branch("growth.dense_ink.stone", "돌먹",
                    "최대 Lv.1. 진먹 발판마다 낙묵석 방어 1회.",
                    1, NodeRarity.Epic, "진먹 획득", "굳은 획"),
                Branch("growth.dense_ink.cliff", "절벽먹",
                    "최대 Lv.2. 35도 이상 진먹 발판 접착력 +20%/단계.",
                    2, NodeRarity.Rare, "진먹 획득", "올곧은 뜀"),
                Completion("growth.dense_ink.iron_mountain", "철묵산",
                    "진먹 발판은 두 번째 착지까지 남지만 활성 발판 상한 -1.",
                    "철묵산 가지 완성", "도약 방향 빌드"));

            AddFamily(
                result, "family.crane_landing", "학의 착지",
                GrowthCatalogCategory.Mastery,
                Root("growth.landing.center", "중심잡기",
                    "최대 Lv.3. 발판 중앙 1/3 착지 시 먹 4%/단계 회복.",
                    3, "중앙 착지 5회", "먹샘"),
                Branch("growth.landing.even_foot", "고른 발",
                    "최대 Lv.2. 중앙 착지에서 기울기 수평 영향 -20%/단계.",
                    2, NodeRarity.Rare, "중심잡기 획득", "올곧은 뜀"),
                Branch("growth.landing.strong_foot", "힘찬 발",
                    "최대 Lv.2. 중앙 착지 다음 점프 +8%/단계, 대기 -0.05초.",
                    2, NodeRarity.Rare, "중심잡기 획득", "도약"),
                Completion("growth.landing.crane", "학의 착지",
                    "중앙 착지 3회 연속 성공 시 다음 획 무료·낙묵석 방어 1회.",
                    "학의 착지 가지 완성", "굳은 획"));

            AddFamily(
                result, "family.winding_mountain_path", "굽이산길",
                GrowthCatalogCategory.Mastery,
                Root("growth.zigzag.root", "엇갈린 산길",
                    "최대 Lv.3. 좌우 기울기를 번갈아 3회 착지하면 먹 6%/단계 회복.",
                    3, "반대 기울기 착지 4회", "비탈읽기"),
                Branch("growth.zigzag.mountain_bird", "좌우 산새",
                    "최대 Lv.2. 엇갈림 연속 중 수평 점프력 +8%/단계.",
                    2, NodeRarity.Rare, "엇갈린 산길 획득", "비탈읽기"),
                Branch("growth.zigzag.wind_bird", "바람 산새",
                    "최대 Lv.2. 풍향과 엇갈림이 맞으면 발판 수명 +15%/단계.",
                    2, NodeRarity.Rare, "엇갈린 산길 획득", "바람읽기"),
                Completion("growth.zigzag.winding_path", "굽이산길",
                    "엇갈림 한 번 실패를 보존하고 다섯 번째 성공에 먹 25% 회복.",
                    "굽이산길 가지 완성", "바람의 눈"));

            AddFamily(
                result, "family.double_inkstone", "두 겹 벼루",
                GrowthCatalogCategory.InkResource,
                Root("growth.reserve.root", "여분먹",
                    "최대 Lv.2. 붓 여유 아이템 충전량 +기본 용량 10%/단계.",
                    2, "붓 여유 아이템 3회 획득", "큰 벼루"),
                Branch("growth.reserve.sealed", "봉한 먹",
                    "최대 Lv.1. 일반 먹을 먼저 소비하고 여분먹은 마지막에 사용.",
                    1, NodeRarity.Rare, "여분먹 획득", "긴 획 빌드"),
                Branch("growth.reserve.cushion", "넘침막",
                    "최대 Lv.2. 다른 방어가 없을 때 여분먹 35/25%로 장애물 1회 방어.",
                    2, NodeRarity.Epic, "여분먹 획득", "먹갑옷"),
                Completion("growth.reserve.double_inkstone", "두 겹 벼루",
                    "일반 먹이 가득 찬 동안 여분먹이 초당 기본 용량 1% 회복.",
                    "두 겹 벼루 가지 완성", "먹샘"));

            AddFamily(
                result, "family.crane_wing_breath", "학날개숨",
                GrowthCatalogCategory.AirControl,
                Root("growth.air.apex_breath", "정점숨",
                    "최대 Lv.3. 점프 정점의 완만한 체공 +0.08초/단계.",
                    3, "정점 중 획 완성 3회", "도약"),
                Branch("growth.air.feather", "가벼운 먹",
                    "최대 Lv.2. 최대 낙하 속도 -8%/단계.",
                    2, NodeRarity.Rare, "정점숨 획득", "안정형 공중제어"),
                Branch("growth.air.falling_star", "낙성",
                    "최대 Lv.2. 3m 이상 낙하 뒤 다음 점프 +10%/단계.",
                    2, NodeRarity.Epic, "정점숨 획득", "위험형 공중제어"),
                Completion("growth.air.crane_wing", "학날개숨",
                    "정점 뒤 1초간 횡풍 면역, 그동안 그린 첫 상승 획 비용 -30%.",
                    "학날개숨 가지 완성", "바람·도약 계보"));

            AddFamily(
                result, "family.unbroken_pact", "깨지지 않는 맹세",
                GrowthCatalogCategory.Pact,
                Root("growth.pact.rough_hanji", "거친 한지",
                    "최대 Lv.1. 성장 두루마리 간격 -10m, 발판 수명 -10%.",
                    1, "한 판 최고 750m 도달", "고위험 기록",
                    null, NodeRarity.Covenant),
                Branch("growth.pact.dry_inkstone", "마른 벼루 맹세",
                    "최대 Lv.1. 먹 자연 회복 0, 아이템·환급·착지 회복량 2배.",
                    1, NodeRarity.Covenant, "거친 한지 획득",
                    "아이템·숙련 회복"),
                Branch("growth.pact.storm", "폭풍 맹세",
                    "최대 Lv.1. 횡풍·위험 빈도 +30%, 두루마리 간격 추가 -20m.",
                    1, NodeRarity.Covenant, "거친 한지 획득",
                    "바람 숙련"),
                Completion("growth.pact.unbroken", "깨지지 않는 맹세",
                    "가지 선택 후 120m 생존 시 서약의 불이익 절반, 이익 유지.",
                    "맹세를 지키며 120m 생존", "고위험 기록"));

            return result;
        }

        static void AddFamily(
            List<RoguelikeGrowthDefinition> result,
            string familyId,
            string familyName,
            GrowthCatalogCategory category,
            NodeSeed root,
            NodeSeed firstBranch,
            NodeSeed secondBranch,
            NodeSeed completion)
        {
            result.Add(CreateDefinition(
                familyId, familyName, category, NodeTier.Root, root,
                null, null, null));
            result.Add(CreateDefinition(
                familyId, familyName, category, NodeTier.Branch, firstBranch,
                new[] { root.Id }, null, new[] { secondBranch.Id }));
            result.Add(CreateDefinition(
                familyId, familyName, category, NodeTier.Branch, secondBranch,
                new[] { root.Id }, null, new[] { firstBranch.Id }));
            result.Add(CreateDefinition(
                familyId, familyName, category, NodeTier.Completion, completion,
                new[] { root.Id },
                new[] { firstBranch.Id, secondBranch.Id },
                null));
        }

        static RoguelikeGrowthDefinition CreateDefinition(
            string familyId,
            string familyName,
            GrowthCatalogCategory category,
            NodeTier tier,
            NodeSeed seed,
            IEnumerable<string> requiredPrerequisites,
            IEnumerable<string> alternativePrerequisites,
            IEnumerable<string> exclusions)
        {
            var status = seed.RuntimeType.HasValue
                ? ImplementationStatus.RuntimeReady
                : ImplementationStatus.Planned;
            return new RoguelikeGrowthDefinition(
                seed.Id,
                familyId,
                familyName,
                seed.Name,
                seed.Description,
                seed.Synergy,
                seed.UnlockHint,
                category,
                tier,
                seed.Rarity,
                status,
                seed.MaxLevel,
                requiredPrerequisites,
                alternativePrerequisites,
                exclusions,
                seed.RuntimeType);
        }

        static NodeSeed Root(
            string id,
            string name,
            string description,
            int maxLevel,
            string unlockHint,
            string synergy,
            GrowthUpgradeType? runtimeType = null,
            NodeRarity rarity = NodeRarity.Common)
        {
            return new NodeSeed(
                id, name, description, maxLevel, rarity,
                unlockHint, synergy, runtimeType);
        }

        static NodeSeed Branch(
            string id,
            string name,
            string description,
            int maxLevel,
            NodeRarity rarity,
            string unlockHint,
            string synergy)
        {
            return new NodeSeed(
                id, name, description, maxLevel, rarity,
                unlockHint, synergy, null);
        }

        static NodeSeed Completion(
            string id,
            string name,
            string description,
            string unlockHint,
            string synergy)
        {
            return new NodeSeed(
                id, name, description, 1, NodeRarity.Legendary,
                unlockHint, synergy, null);
        }

        sealed class NodeSeed
        {
            public NodeSeed(
                string id,
                string name,
                string description,
                int maxLevel,
                NodeRarity rarity,
                string unlockHint,
                string synergy,
                GrowthUpgradeType? runtimeType)
            {
                Id = id;
                Name = name;
                Description = description;
                MaxLevel = maxLevel;
                Rarity = rarity;
                UnlockHint = unlockHint;
                Synergy = synergy;
                RuntimeType = runtimeType;
            }

            public string Id { get; }
            public string Name { get; }
            public string Description { get; }
            public int MaxLevel { get; }
            public NodeRarity Rarity { get; }
            public string UnlockHint { get; }
            public string Synergy { get; }
            public GrowthUpgradeType? RuntimeType { get; }
        }
    }
}
