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

    /// 구매 권한은 각 플랫폼의 스토어 공급자가 확인한다. 업적 마스크·의상 ID에 구매 비트를 넣지 않는다.
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
#if UNITY_IOS || UNITY_WEBGL
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
#if (UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
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

    /// 금색·새 외형은 표시 항목만 추가한다. 저장용 InkSkinId와 63개 업적 비트는 확장하지 않는다.
    public readonly struct InkWardrobeEntry
    {
        public readonly InkSkinId Skin;
        public readonly bool Gold, Nest;
        public InkWardrobeEntry(InkSkinId skin, bool gold = false, bool nest = false) { Skin = skin; Gold = gold; Nest = nest; }
        public int CardKey => Gold ? InkSkinCatalog.Count : Nest ? InkSkinCatalog.Count + 1 : (int)Skin;
        public int Group => Gold || Nest ? 0 : InkSkinCatalog.BrowseGroup(Skin);
        static int Extras => (InkPremiumAppearance.IsSupported ? 1 : 0) +
            (NextUpdateScope.DeferredWardrobeEnabled ? 1 : 0);
        public static int Count => InkSkinCatalog.BrowseCount + Extras;
        public static InkWardrobeEntry At(int index)
        {
            index = Mathf.Clamp(index, 0, Count - 1);
            if (index == 0) return new(InkSkinId.Ink);
            if (InkPremiumAppearance.IsSupported && index == 1) return new(InkSkinId.Ink, true);
            if (NextUpdateScope.DeferredWardrobeEnabled && index == Extras) return new(InkSkinId.Ink, false, true);
            return new(InkSkinCatalog.BrowseAt(index - Extras));
        }
        public static int Index(InkSkinId skin, bool gold = false, bool nest = false)
        {
            if (gold && InkPremiumAppearance.IsSupported) return 1;
            if (nest) return NextUpdateScope.DeferredWardrobeEnabled ? Extras : 0;
            int index = InkSkinCatalog.BrowseIndex(skin);
            return index + (index > 0 ? Extras : 0);
        }
    }
}
