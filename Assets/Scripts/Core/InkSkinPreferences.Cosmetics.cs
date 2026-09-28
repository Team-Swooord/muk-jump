using System;
using UnityEngine;

namespace MukJump.Core
{
    [Serializable]
    public sealed class InkCosmeticSelection
    {
        public int version = 1;
        // 빈 skin은 기존 정수 장착값을 따른다. 조회만으로 저장하지 않는다.
        public string skin = string.Empty;
        public string landing = InkCosmeticCatalog.DefaultLanding;
        public string brush = InkCosmeticCatalog.DefaultBrush;
        public string clone = InkCosmeticCatalog.FollowBody;
        public InkCosmeticSelection Copy() => new() { version = version, skin = skin, landing = landing, brush = brush, clone = clone };
        public bool IsValid => version == 1 && skin != null &&
            (skin.Length == 0 || InkCosmeticCatalog.ValidId(skin)) &&
            InkCosmeticCatalog.ValidId(landing) && InkCosmeticCatalog.ValidId(brush) && InkCosmeticCatalog.ValidId(clone);
        public string Get(InkCosmeticSlot slot) => slot switch
        { InkCosmeticSlot.Skin => skin, InkCosmeticSlot.Landing => landing, InkCosmeticSlot.Brush => brush, _ => clone };
        public void Set(InkCosmeticSlot slot, string id)
        {
            switch (slot)
            {
                case InkCosmeticSlot.Skin: skin = id; break;
                case InkCosmeticSlot.Landing: landing = id; break;
                case InkCosmeticSlot.Brush: brush = id; break;
                case InkCosmeticSlot.Clone: clone = id; break;
            }
        }
    }

    public static partial class InkSkinPreferences
    {
        public const string CosmeticsKey = "MukJump.Settings.Cosmetics.v1";
        static string cachedCosmeticsJson;
        static InkCosmeticSelection cachedCosmetics;
        static bool cachedCosmeticsValid;
        static bool TryReadCosmetics(out InkCosmeticSelection selection)
        {
            string json = store.GetString(CosmeticsKey, string.Empty);
            if (cachedCosmetics != null && json == cachedCosmeticsJson)
            { selection = cachedCosmetics; return cachedCosmeticsValid; }
            cachedCosmeticsJson = json;
            cachedCosmeticsValid = ReadCosmetics(json, out cachedCosmetics);
            selection = cachedCosmetics;
            return cachedCosmeticsValid;
        }
        static bool ReadCosmetics(string json, out InkCosmeticSelection selection)
        {
            selection = new InkCosmeticSelection();
            if (string.IsNullOrEmpty(json)) return true;
            try
            {
                // 누락 필드를 기본값으로 덮어쓰지 않고 기존 선택을 보호한다.
                foreach (string field in new[] { "version", "skin", "landing", "brush", "clone" })
                    if (!System.Text.RegularExpressions.Regex.IsMatch(json, "\"" + field + "\"\\s*:")) return false;
                var parsed = JsonUtility.FromJson<InkCosmeticSelection>(json);
                if (parsed == null || !parsed.IsValid) return false;
                selection = parsed;
                return true;
            }
            catch (Exception) { return false; }
        }
        public static string PreferredCosmetic(InkCosmeticSlot slot)
        {
            TryReadCosmetics(out var selection);
            string id = selection.Get(slot);
            return slot == InkCosmeticSlot.Skin && string.IsNullOrEmpty(id)
                ? store.GetInt(GoldSelectionKey, 0) == 1 ? InkCosmeticCatalog.Gold
                    : InkCosmeticCatalog.Legacy(InkSkinCatalog.Normalize((InkSkinId)store.GetInt(StorageKey, 0)))
                : id;
        }
        public static bool CanUseCosmetic(string id)
        {
            if (id == InkCosmeticCatalog.Gold) return InkPremiumAppearance.IsOwned;
            if (InkCosmeticCatalog.TryLegacy(id, out var legacy)) return InkSkinAchievements.IsUnlocked(legacy);
            if (id is InkCosmeticCatalog.DefaultLanding or InkCosmeticCatalog.DefaultBrush or InkCosmeticCatalog.FollowBody) return true;
            bool known = id is InkCosmeticCatalog.BambooLeaf or InkCosmeticCatalog.MagpieNest or InkCosmeticCatalog.Tadpole;
            return NextUpdateScope.DeferredWardrobeEnabled && known && PermanentGrowthProfile.OwnsCosmetic(id);
        }
        public static string EquippedCosmetic(InkCosmeticSlot slot)
        {
            string preferred = PreferredCosmetic(slot);
            return InkCosmeticCatalog.BelongsTo(preferred, slot) && CanUseCosmetic(preferred)
                ? preferred : InkCosmeticCatalog.Default(slot);
        }
        public static bool TryEquipCosmetic(InkCosmeticSlot slot, string id)
        {
            if (slot == InkCosmeticSlot.Skin && InkCosmeticCatalog.TryLegacy(id, out var legacy)) return TryEquip(legacy);
            if (slot == InkCosmeticSlot.Skin && id == InkCosmeticCatalog.Gold) return TryEquipGold();
            if (!CanChangeCosmetics() || SkinDebugSession.Active || !InkCosmeticCatalog.BelongsTo(id, slot) ||
                !CanUseCosmetic(id) || !TryReadCosmetics(out var selection)) return false;
            selection = selection.Copy();
            selection.Set(slot, id);
            // 새 스킨은 기본 먹과 같은 능력이다. 다른 갈래는 유지한다.
            return SaveCosmeticSelection(selection, slot == InkCosmeticSlot.Skin ? InkSkinId.Ink : (InkSkinId?)null, false);
        }
        static bool CanChangeCosmetics() =>
            (GameManager.Instance == null || GameManager.Instance.State == GameState.Lobby) &&
            (MukJumpAccountRuntime.Instance == null || !MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync);

        static bool SaveCosmeticSelection(InkCosmeticSelection selection, InkSkinId? skin, bool gold)
        {
            string previousJson = store.GetString(CosmeticsKey, string.Empty);
            int previous = store.GetInt(StorageKey, 0), previousGold = store.GetInt(GoldSelectionKey, 0);
            string json = JsonUtility.ToJson(selection);
            if (json == previousJson && (!skin.HasValue || previous == (int)skin.Value && previousGold == (gold ? 1 : 0))) return true;
            try
            {
                store.SetString(CosmeticsKey, json);
                if (skin.HasValue)
                {
                    store.SetInt(StorageKey, (int)skin.Value);
                    store.SetInt(GoldSelectionKey, gold ? 1 : 0);
                }
                store.Save();
            }
            catch (Exception)
            {
                try
                {
                    store.SetString(CosmeticsKey, previousJson);
                    store.SetInt(StorageKey, previous);
                    store.SetInt(GoldSelectionKey, previousGold);
                }
                catch (Exception) { }
                return false;
            }
            if (Changed != null)
                foreach (Action listener in Changed.GetInvocationList())
                    try { listener(); }
                    catch (Exception error) { Debug.LogWarning("[MukJump] 착용 변경 알림 실패: " + error.Message); }
            return true;
        }
    }
}
