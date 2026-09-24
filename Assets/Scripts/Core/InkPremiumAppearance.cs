using UnityEngine;

namespace MukJump.Core
{
    public interface IInkPremiumEntitlement
    {
        bool IsOwned { get; }
    }

    public interface IInkPremiumStore : IInkPremiumEntitlement
    {
        bool IsBusy { get; }
        bool CanPurchase { get; }
        string LocalizedPrice { get; }
        string StatusMessage { get; }
        void Purchase();
        void Restore();
    }

    /// 구매 권한은 StoreKit 공급자가 확인한다. 업적 마스크·클라우드 의상 ID에 구매 비트를 넣지 않는다.
    public static class InkPremiumAppearance
    {
        static IInkPremiumEntitlement entitlement;
        public static IInkPremiumStore Store => entitlement as IInkPremiumStore;
        public static event System.Action ShopRequested;
        public static void OpenShop() { if (IsSupported) ShopRequested?.Invoke(); }
        public static bool IsSupported
        {
            get
            {
#if UNITY_EDITOR
                if (testSupport.HasValue) return testSupport.Value;
#endif
#if UNITY_IOS && !UNITY_WEBGL
                return true;
#else
                return false;
#endif
            }
        }
        public static bool IsOwned => IsSupported && entitlement?.IsOwned == true;
        public static bool SuppressAds
        {
            get
            {
                if (!IsSupported) return false;
                if (IsOwned) return true;
                if (entitlement is IInkPremiumStoreState state) return !state.OwnershipResolved;
#if UNITY_IOS && !UNITY_EDITOR
                return entitlement == null;
#else
                return false;
#endif
            }
        }
        public static bool IsEquipped => IsOwned && InkSkinPreferences.GoldSelected &&
            InkSkinPreferences.Equipped == InkSkinId.Ink && !SkinDebugSession.Active;
        public static bool IsGoldInRun => IsOwned && (RunGrowthController.Instance != null &&
            GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby
                ? RunGrowthController.Instance.GoldSnapshot : IsEquipped);
        internal static void Register(IInkPremiumEntitlement value) => entitlement = value;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            entitlement = null;
            ShopRequested = null;
#if UNITY_EDITOR
            testSupport = null;
#endif
        }
#if UNITY_EDITOR
        static bool? testSupport;
        public static void UseForTests(bool supported, IInkPremiumEntitlement value)
        { testSupport = supported; entitlement = value; }
        public static void RestoreForTests() => Reset();
#endif
    }

    /// 표시 전용 64번째 칸. 저장용 InkSkinId와 63개 업적 비트는 확장하지 않는다.
    public readonly struct InkWardrobeEntry
    {
        public readonly InkSkinId Skin;
        public readonly bool Gold;
        public InkWardrobeEntry(InkSkinId skin, bool gold = false) { Skin = skin; Gold = gold; }
        public int CardKey => Gold ? InkSkinCatalog.Count : (int)Skin;
        public int Group => Gold ? 0 : InkSkinCatalog.BrowseGroup(Skin);
        public static int Count => InkSkinCatalog.BrowseCount + (InkPremiumAppearance.IsSupported ? 1 : 0);
        public static InkWardrobeEntry At(int index)
        {
            index = Mathf.Clamp(index, 0, Count - 1);
            if (InkPremiumAppearance.IsSupported && index == 1) return new(InkSkinId.Ink, true);
            return new(InkSkinCatalog.BrowseAt(index - (InkPremiumAppearance.IsSupported && index > 1 ? 1 : 0)));
        }
        public static int Index(InkSkinId skin, bool gold = false)
        {
            if (gold && InkPremiumAppearance.IsSupported) return 1;
            int index = InkSkinCatalog.BrowseIndex(skin);
            return index + (InkPremiumAppearance.IsSupported && index > 0 ? 1 : 0);
        }
    }
}
