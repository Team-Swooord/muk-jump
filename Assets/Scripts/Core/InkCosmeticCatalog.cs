using System;
using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Core
{
    public enum InkCosmeticSlot { Skin, Landing, Brush, Clone }

    /// 기존 0~62 의상 번호와 해금 비트에 새 외형을 끼워 넣지 않는다.
    public static class InkCosmeticCatalog
    {
        public const string MagpieNest = "skin.magpie_nest.v1";
        public const string BambooLeaf = "landing.bamboo_leaf.v1";
        public const string Tadpole = "clone.tadpole.v1";
        public const string DefaultLanding = "landing.default";
        public const string DefaultBrush = "brush.default";
        public const string FollowBody = "clone.follow";
        public const string Gold = "skin.gold";
        public static readonly IReadOnlyList<string> NewItems = Array.AsReadOnly(new[] { BambooLeaf, MagpieNest, Tadpole });
        static readonly Dictionary<string, Sprite> sprites = new(StringComparer.Ordinal);

        public static string Legacy(InkSkinId id) => "skin.legacy." + (int)id;
        public static bool TryLegacy(string id, out InkSkinId skin)
        {
            skin = InkSkinId.Ink;
            if (id == null || !id.StartsWith("skin.legacy.", StringComparison.Ordinal) ||
                !int.TryParse(id.Substring(12), out int value) || !InkSkinCatalog.IsValid((InkSkinId)value)) return false;
            skin = (InkSkinId)value;
            return id == Legacy(skin);
        }
        public static string Default(InkCosmeticSlot slot) => slot switch
        {
            InkCosmeticSlot.Skin => Legacy(InkSkinId.Ink), InkCosmeticSlot.Landing => DefaultLanding,
            InkCosmeticSlot.Brush => DefaultBrush, _ => FollowBody
        };
        public static bool BelongsTo(string id, InkCosmeticSlot slot) => slot switch
        {
            InkCosmeticSlot.Skin => id is MagpieNest or Gold || TryLegacy(id, out _),
            InkCosmeticSlot.Landing => id is DefaultLanding or BambooLeaf,
            InkCosmeticSlot.Brush => id == DefaultBrush,
            InkCosmeticSlot.Clone => id is FollowBody or Tadpole,
            _ => false
        };
        public static int Goal(string id) => id switch { BambooLeaf => 200, MagpieNest => 1000, Tadpole => 12, _ => 0 };
        public static int Progress(string id, SkinAchievementState state) => state == null ? 0 : id switch
        { BambooLeaf => state.drawnLandings, MagpieNest => state.items, Tadpole => state.clones, _ => 0 };
        public static string Name(string id) => id switch
        {
            BambooLeaf => "댓잎", MagpieNest => "까치 둥지", Tadpole => "먹 올챙이",
            DefaultLanding => "먹 번짐", DefaultBrush => "기본 붓", FollowBody => "본체 따르기", _ => "먹"
        };
        public static string ResourcePath(string id) => id switch
        {
            BambooLeaf => "MukJump/Cosmetics/landing_bamboo_leaf_v1",
            MagpieNest => "MukJump/Cosmetics/skin_magpie_nest_v1",
            Tadpole => "MukJump/Cosmetics/clone_tadpole_v1", _ => null
        };
        public static Sprite Sprite(string id)
        {
            string path = ResourcePath(id);
            if (path == null) return null;
            if (!sprites.TryGetValue(id, out var sprite) || sprite == null)
                sprites[id] = sprite = Resources.Load<Sprite>(path);
            return sprite;
        }
        internal static bool ValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 128 &&
            System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z0-9._-]+$");
    }

    /// 알 수 없는 식별자도 보존한다. 새 저장 구조는 상위 버전 검사로 구 앱의 덮어쓰기를 막는다.
    [Serializable]
    public sealed class InkCosmeticOwnership
    {
        public int version = 1;
        public List<string> owned = new();
        public bool IsValid
        {
            get
            {
                if (version != 1 || owned == null || owned.Count > 2048) return false;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in owned) if (!InkCosmeticCatalog.ValidId(id) || !seen.Add(id)) return false;
                return true;
            }
        }
        internal void GrantEarned(SkinAchievementState state)
        {
            foreach (string id in InkCosmeticCatalog.NewItems)
                if (InkCosmeticCatalog.Progress(id, state) >= InkCosmeticCatalog.Goal(id) && !owned.Contains(id)) owned.Add(id);
        }
        internal void Merge(InkCosmeticOwnership local)
        {
            if (local == null) return;
            foreach (string id in local.owned) if (!owned.Contains(id)) owned.Add(id);
        }
        public static bool HasCompletePayload(string json)
        {
            var match = System.Text.RegularExpressions.Regex.Match(json, "\"cosmetics\"\\s*:\\s*(\\{[^{}]*\\})");
            return match.Success && System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"version\"\\s*:") &&
                System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"owned\"\\s*:\\s*\\[");
        }
    }
}
