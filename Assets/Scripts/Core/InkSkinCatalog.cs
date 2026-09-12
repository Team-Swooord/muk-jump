using System;
using UnityEngine;

namespace MukJump.Core
{
    public enum InkSkinId { Ink = 0, Gat = 1, SpiderMask = 2 }

    /// 먹 몸체는 교체하지 않는다. 무료 장식 한 개와 자동 발동 능력 한 개를 묶는다.
    public static class InkSkinCatalog
    {
        public const int Count = 3;
        public const float AirJumpRatio = 0.4f;
        public static bool IsValid(InkSkinId id) => (int)id >= 0 && (int)id < Count;
        public static InkSkinId Normalize(InkSkinId id) => IsValid(id) ? id : InkSkinId.Ink;
        public static string Name(InkSkinId id) => id switch
        {
            InkSkinId.Gat => "바람 갓",
            InkSkinId.SpiderMask => "거미탈",
            _ => "먹방울",
        };
        public static string Ability(InkSkinId id) => id switch
        {
            InkSkinId.Gat => "이단 도약",
            InkSkinId.SpiderMask => "거미 걸음",
            _ => "기본 도약",
        };
        public static string Description(InkSkinId id) => id switch
        {
            InkSkinId.Gat => "점프 정점에서 한 번 더 도약해요.\n첫 도약의 40% 힘으로 자동 발동해요.",
            InkSkinId.SpiderMask => "양옆 벽에 붙었다가\n화면 안쪽으로 자동 도약해요.",
            _ => "장식 없이 원래 먹의 모습으로.\n기존 점프와 성장 효과를 유지해요.",
        };
        public static string ResourcePath(InkSkinId id) => id switch
        {
            InkSkinId.Gat => "MukJump/Skins/skin_gat_v1",
            InkSkinId.SpiderMask => "MukJump/Skins/skin_spider_mask_v1",
            _ => string.Empty,
        };
        // 전체 1024px 캐릭터 프레임에 대한 비율. UI와 월드 장착이 같은 기준을 쓴다.
        public static float Width(InkSkinId id) => id == InkSkinId.Gat ? 0.9f : 0.6f;
        public static Vector2 Offset(InkSkinId id) => id == InkSkinId.Gat
            ? new Vector2(0, 0.4f) : new Vector2(0, 0.085f);
    }

    /// 무료 스킨의 착용 선택은 기기 설정이다. 계정/점수/성장 클라우드 저장을 변경하지 않는다.
    public static class InkSkinPreferences
    {
        public const string StorageKey = "MukJump.Settings.EquippedSkin";
        static ILobbySettingsStore store = new PlayerPrefsLobbySettingsStore();
        public static InkSkinId Equipped => InkSkinCatalog.Normalize(
            (InkSkinId)store.GetInt(StorageKey, 0));

        public static bool TryEquip(InkSkinId id)
        {
            if (!InkSkinCatalog.IsValid(id) ||
                GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby ||
                MukJumpAccountRuntime.Instance != null && MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
                return false;
            int previous = store.GetInt(StorageKey, 0);
            if (previous == (int)id) return true;
            try
            {
                store.SetInt(StorageKey, (int)id);
                store.Save();
                return true;
            }
            catch (Exception)
            {
                // Save 실패 뒤 종료 시 새 값이 뒤늦게 저장되지 않도록 메모리 값도 되돌린다.
                try { store.SetInt(StorageKey, previous); } catch (Exception) { }
                return false;
            }
        }

#if UNITY_EDITOR
        public static void UseStoreForTests(ILobbySettingsStore value) => store = value;
        public static void RestoreDefaultStoreForTests() => store = new PlayerPrefsLobbySettingsStore();
#endif
    }
}
