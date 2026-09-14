using UnityEngine;

namespace MukJump.Core
{
    /// 내부 TestFlight의 시험 착용만 보관한다. 저장/업적 모델에는 절대 합치지 않는다.
    public static class SkinDebugSession
    {
        public const string BuildDefine = "MUKJUMP_SKIN_DEBUG";
        public static bool Available
        {
            get
            {
#if UNITY_EDITOR || (UNITY_IOS && MUKJUMP_SKIN_DEBUG)
                return true;
#else
                return false;
#endif
            }
        }
        static object owner;
        static bool allUnlocked, menuOpen;
        static InkSkinId equipped;
        static bool hasEquipped;
        public static bool Active { get { ValidateOwner(); return Available && allUnlocked; } }
        public static bool MenuOpen { get { ValidateOwner(); return Available && menuOpen; } }
        public static bool CanEquip(InkSkinId id) => InkSkinCatalog.IsAvailable(id) &&
            (Active || InkSkinAchievements.IsUnlocked(id));
        public static bool IsConcealed(InkSkinId id) => !Active && InkSkinCatalog.IsConcealed(id);
        static void ValidateOwner()
        {
            var current = PermanentGrowthProfile.SkinAccountToken;
            if (!ReferenceEquals(owner, current) || PermanentGrowthProfile.RequiresRecovery ||
                MukJumpAccountRuntime.Instance != null && MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
            { Reset(); owner = current; }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { owner = null; allUnlocked = menuOpen = hasEquipped = false; equipped = InkSkinId.Ink; }
        public static void SetMenuOpen(bool value) { ValidateOwner(); menuOpen = Available && value; }
        public static bool SetAllUnlocked(bool value)
        {
            ValidateOwner();
            if (!Available || PermanentGrowthProfile.RequiresRecovery ||
                GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby ||
                MukJumpAccountRuntime.Instance != null && MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync) return false;
            allUnlocked = value; hasEquipped = false; return true;
        }
        public static bool TryGetEquipped(out InkSkinId id)
        { id = equipped; return Active && hasEquipped; }
        public static bool TryEquip(InkSkinId id)
        {
            if (!Active || !InkSkinCatalog.IsAvailable(id) ||
                GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby) return false;
            equipped = id; hasEquipped = true; return true;
        }
        public static void ExcludeTestRun()
        {
            if (Active) ScoreManager.Instance?.InvalidateCurrentRunForRecords();
        }
    }
}
