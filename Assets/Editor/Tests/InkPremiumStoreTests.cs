using System;
using System.Collections.Generic;
using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class InkPremiumStoreTests
    {
        sealed class Gateway : IInkPremiumGateway
        {
            public event Action Connected, Disconnected, PurchasesFailed, Deferred, AccountChanged;
            public event Action<string> Price, Revoked;
            public event Action<InkPremiumPurchase[]> Purchases;
            public event Action<InkPremiumPurchase> Pending;
            public event Action<bool> PurchaseFailed;
            public event Action<string, bool> Confirmed;
            public int Connections, Fetches, ProductFetches, PurchasesStarted, Restores;
            public readonly List<string> Confirmations = new();
            public Action<bool> RestoreCallback;
            public Action BeforeConfirm;
            public void Connect() => Connections++;
            public void FetchProduct() => ProductFetches++;
            public void FetchPurchases() => Fetches++;
            public void Purchase() => PurchasesStarted++;
            public void Restore(Action<bool> completed) { Restores++; RestoreCallback = completed; }
            public void Confirm(InkPremiumPurchase purchase) { BeforeConfirm?.Invoke(); Confirmations.Add(purchase.TransactionId); }
            public void Dispose() { }
            public void Online() => Connected?.Invoke();
            public void Offline() => Disconnected?.Invoke();
            public void Product(string value) => Price?.Invoke(value);
            public void Snapshot(params InkPremiumPurchase[] values) => Purchases?.Invoke(values);
            public void FetchFailed() => PurchasesFailed?.Invoke();
            public void Paid(InkPremiumPurchase value) => Pending?.Invoke(value);
            public void Fail(bool cancel) => PurchaseFailed?.Invoke(cancel);
            public void AskToBuy() => Deferred?.Invoke();
            public void Finish(string transaction, bool ok) => Confirmed?.Invoke(transaction, ok);
            public void Revoke(string product = InkPremiumStore.ProductId) => Revoked?.Invoke(product);
            public void ChangeAccount() => AccountChanged?.Invoke();
        }
        Gateway gateway;
        InkPremiumStore store;
        double time;
        bool saveFailure;
        List<string> saves;
        static InkPremiumPurchase Order(string id = "123", bool pending = true, bool verified = true,
            string product = InkPremiumStore.ProductId) => new(product, id, verified, pending ? new object() : null);
        [SetUp] public void Setup()
        {
            time = 0; saveFailure = false; saves = new(); gateway = new();
            store = NewStore(gateway);
            InkPremiumAppearance.UseForTests(true, store);
            store.Start(); gateway.Online();
        }
        InkPremiumStore NewStore(Gateway bridge) => new(bridge, id =>
            { if (saveFailure) throw new InvalidOperationException(); saves.Add(id); }, () => time);
        [TearDown] public void Teardown() { store.Dispose(); InkPremiumAppearance.RestoreForTests(); MonetizationAds.ResetProvider(); }
        void Ready() { gateway.Snapshot(); gateway.Product("₩4,900"); }

        [Test] public void InitialOwnershipWaitSuppressesEveryAdWithoutGrantingRevival()
        {
            Assert.That(store.IsOwned, Is.False); Assert.That(store.OwnershipResolved, Is.False);
            Assert.That(InkPremiumAppearance.SuppressAds, Is.True);
            Assert.That(MonetizationAds.HasProvider, Is.False);
            Assert.That(MonetizationAds.Provider.IsReady(FullScreenAdPlacement.GameOverReviveReward), Is.False);
            Assert.That(MonetizationPolicy.ShouldShowTopBanner(GameState.Playing, false, false, true, false), Is.False);
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(3, 99, 200, false, true), Is.False);
            gateway.FetchFailed(); Assert.That(store.OwnershipResolved, Is.False);
            Assert.That(InkPremiumAppearance.SuppressAds, Is.True);
            time = 31; store.Tick(); Assert.That(gateway.Fetches, Is.EqualTo(2));
            gateway.Snapshot(); Assert.That(store.OwnershipResolved, Is.True);
            Assert.That(InkPremiumAppearance.SuppressAds, Is.False);
        }
        [Test] public void PendingSuccessNeedsMatchingVerifiedStoreKitTransactionBeforeGrantAndConfirm()
        {
            Ready(); store.Purchase(); Assert.That(gateway.PurchasesStarted, Is.EqualTo(1));
            gateway.Paid(Order(verified: false));
            Assert.That(store.IsOwned, Is.False); Assert.That(saves, Is.Empty); Assert.That(gateway.Confirmations, Is.Empty);
            gateway.BeforeConfirm = () => { Assert.That(store.IsOwned, Is.True); Assert.That(saves, Does.Contain("123")); };
            gateway.Snapshot(Order());
            Assert.That(store.IsOwned, Is.True); Assert.That(gateway.Confirmations, Is.EqualTo(new[] { "123" }));
            Assert.That(MonetizationAds.Provider.IsReady(FullScreenAdPlacement.GameOverReviveReward), Is.True);
        }
        [Test] public void InvalidProofOtherProductAndEmptyTransactionNeverGrant()
        {
            Ready(); gateway.Paid(Order(verified: false));
            gateway.Snapshot(Order(verified: false), Order(product: "other.product"), Order(id: ""));
            Assert.That(store.IsOwned, Is.False); Assert.That(gateway.Confirmations, Is.Empty); Assert.That(saves, Is.Empty);
        }
        [Test] public void PriceFailureDoesNotEraseOfflineVerifiedOwnership()
        {
            gateway.Product(""); gateway.Snapshot(Order(pending: false));
            Assert.That(store.IsOwned, Is.True); Assert.That(store.LocalizedPrice, Is.Empty);
            Assert.That(store.CanPurchase, Is.False); Assert.That(gateway.Confirmations, Is.Empty);
            gateway.Offline(); gateway.FetchFailed(); Assert.That(store.IsOwned, Is.True);
        }
        [Test] public void DuplicatePendingAndSnapshotGrantAndConfirmOnlyOnce()
        {
            Ready(); gateway.Paid(Order()); gateway.Snapshot(Order(), Order());
            gateway.Finish("123", true);
            gateway.Paid(Order()); gateway.Snapshot(Order());
            Assert.That(saves.Count, Is.EqualTo(1)); Assert.That(gateway.Confirmations.Count, Is.EqualTo(1));
        }
        [Test] public void RestartBeforeConfirmationRequiresFreshVerificationAndRecoversUnfinishedPurchase()
        {
            Ready(); gateway.Paid(Order()); gateway.Snapshot(Order());
            store.Dispose(); gateway = new(); store = NewStore(gateway);
            Assert.That(store.IsOwned, Is.False, "저장 표식으로 권한을 추정하지 않는다");
            store.Start(); gateway.Online(); gateway.Snapshot(Order());
            Assert.That(store.IsOwned, Is.True); Assert.That(gateway.Confirmations.Count, Is.EqualTo(1));
        }
        [Test] public void DurableSaveFailureLeavesOrderUnconfirmedAndCanRetry()
        {
            Ready(); saveFailure = true; gateway.Paid(Order()); gateway.Snapshot(Order());
            Assert.That(gateway.Confirmations, Is.Empty); Assert.That(store.IsOwned, Is.True, "OS의 유효한 구매는 유지한다");
            saveFailure = false; time = 31; store.Tick(); gateway.Snapshot(Order());
            Assert.That(saves.Count, Is.EqualTo(1)); Assert.That(gateway.Confirmations.Count, Is.EqualTo(1));
        }
        [Test] public void ConfirmationFailureRetriesWithoutDuplicateGrant()
        {
            Ready(); gateway.Paid(Order()); gateway.Snapshot(Order()); gateway.Finish("123", false);
            time = 31; store.Tick(); gateway.Snapshot(Order());
            Assert.That(saves.Count, Is.EqualTo(1)); Assert.That(gateway.Confirmations.Count, Is.EqualTo(2));
            Assert.That(store.IsOwned, Is.True);
        }
        [TestCase(true)] [TestCase(false)] public void CancelAndFailureNeverGrant(bool cancel)
        {
            Ready(); store.Purchase(); gateway.Fail(cancel); gateway.Snapshot();
            Assert.That(store.IsOwned, Is.False); Assert.That(gateway.Confirmations, Is.Empty);
            Assert.That(store.CanPurchase, Is.True);
        }
        [Test] public void AskToBuyWaitsForVerifiedApprovalAndDoesNotStartAnotherPurchase()
        {
            Ready(); store.Purchase(); gateway.AskToBuy(); store.Purchase();
            Assert.That(store.IsOwned, Is.False); Assert.That(gateway.PurchasesStarted, Is.EqualTo(1));
            gateway.Paid(Order()); gateway.Snapshot(Order()); Assert.That(store.IsOwned, Is.True);
        }
        [Test] public void RestoreSuccessAloneIsNotOwnershipAndEmptyRestoreIsReported()
        {
            Ready(); store.Restore(); gateway.RestoreCallback(true);
            Assert.That(store.IsOwned, Is.False); Assert.That(store.IsBusy, Is.True);
            gateway.Snapshot(); Assert.That(store.IsOwned, Is.False); Assert.That(store.IsBusy, Is.False);
            Assert.That(store.StatusMessage, Does.Contain("구매 내역이 없어요"));
            store.Restore(); gateway.RestoreCallback(true); gateway.Snapshot(Order(pending: false));
            Assert.That(store.IsOwned, Is.True);
        }
        [Test] public void FailedRestorePreservesPreviousVerifiedOwnership()
        {
            gateway.Snapshot(Order(pending: false)); store.Restore(); gateway.RestoreCallback(false);
            Assert.That(store.IsOwned, Is.True); Assert.That(store.IsBusy, Is.False);
        }
        [Test] public void RevocationWinsOverInFlightOldSnapshotAndAdPolicyResumes()
        {
            gateway.Snapshot(Order(pending: false)); store.RefreshOwnership();
            gateway.Revoke(); Assert.That(store.IsOwned, Is.False);
            gateway.Snapshot(Order(pending: false)); Assert.That(store.IsOwned, Is.False, "회수 전 시작된 조회를 버린다");
            gateway.Snapshot(); Assert.That(InkPremiumAppearance.SuppressAds, Is.False);
            Assert.That(MonetizationPolicy.ShouldShowTopBanner(GameState.Playing, false, false, true, false), Is.True);
        }
        [Test] public void AccountChangeRefetchesCatalogAndDoesNotMergeOldOwnership()
        {
            gateway.Snapshot(Order(pending: false)); gateway.Product("$2.99");
            gateway.ChangeAccount(); Assert.That(store.LocalizedPrice, Is.Empty);
            Assert.That(gateway.ProductFetches, Is.EqualTo(2));
            gateway.Snapshot(); Assert.That(store.IsOwned, Is.False);
            gateway.Product("¥500"); Assert.That(store.LocalizedPrice, Is.EqualTo("¥500"));
        }
        [Test] public void PendingDuringOlderSnapshotForcesFreshReadBeforeGrant()
        {
            gateway.Paid(Order()); gateway.Snapshot(Order()); Assert.That(store.IsOwned, Is.False);
            gateway.Snapshot(Order()); Assert.That(store.IsOwned, Is.True);
        }
        [Test] public void AccountChangeDuringCatalogFetchNeverDisplaysPreviousStorefrontPrice()
        {
            gateway.Snapshot(); gateway.ChangeAccount();
            gateway.Product("$2.99");
            Assert.That(store.LocalizedPrice, Is.Empty);
            Assert.That(gateway.ProductFetches, Is.EqualTo(2));
            gateway.Product("¥500");
            Assert.That(store.LocalizedPrice, Is.EqualTo("¥500"));
        }
        [Test] public void GoldChoiceSurvivesRestartAndUnknownOwnershipThenReturnsAfterVerification()
        {
            var preferences = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(preferences);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            try
            {
                gateway.Snapshot(Order(pending: false));
                Assert.That(InkSkinPreferences.TryEquipGold(), Is.True);
                store.Dispose(); gateway = new(); store = NewStore(gateway);
                InkPremiumAppearance.UseForTests(true, store);
                store.Start(); gateway.Online(); gateway.FetchFailed();
                Assert.That(InkSkinPreferences.GoldSelected, Is.True);
                Assert.That(InkPremiumAppearance.IsEquipped, Is.False);
                time = 31; store.Tick(); gateway.Snapshot(Order(pending: false));
                Assert.That(InkPremiumAppearance.IsEquipped, Is.True);
                gateway.Revoke(); gateway.Snapshot();
                Assert.That(InkPremiumAppearance.IsEquipped, Is.False);
                Assert.That(InkSkinPreferences.GoldSelected, Is.True);
            }
            finally
            {
                InkSkinPreferences.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests();
            }
        }
        [Test] public void DisabledPlatformNeverSuppressesAdsOrGrantsGold()
        {
            InkPremiumAppearance.UseForTests(false, store); gateway.Snapshot(Order(pending: false));
            Assert.That(InkPremiumAppearance.IsOwned, Is.False); Assert.That(InkPremiumAppearance.SuppressAds, Is.False);
        }
    }
}
