using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class MonetizationPolicyTests
    {
        [TearDown]
        public void TearDown()
        {
            MonetizationAds.ResetProvider();
        }

        [Test]
        public void PreRunShieldIsAnOptionalFirstRunOfferOnly()
        {
            Assert.That(MonetizationPolicy.CanOfferPreRunShield(0, false, true),
                Is.True);
            Assert.That(MonetizationPolicy.CanOfferPreRunShield(0, true, true),
                Is.False);
            Assert.That(MonetizationPolicy.CanOfferPreRunShield(1, false, true),
                Is.False);
        }

        [Test]
        public void GameOverReviveRequiresReadyAdAndUnusedRunReward()
        {
            Assert.That(MonetizationPolicy.CanOfferGameOverRevive(true, false, true),
                Is.True);
            Assert.That(MonetizationPolicy.CanOfferGameOverRevive(false, false, true),
                Is.False);
            Assert.That(MonetizationPolicy.CanOfferGameOverRevive(true, true, true),
                Is.False);
            Assert.That(MonetizationPolicy.CanOfferGameOverRevive(true, false, false),
                Is.False);
        }

        [Test]
        public void InterstitialUsesThreeRunAndTwoMinuteCaps()
        {
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(
                3, 60f, 180f, false, true), Is.True);
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(
                2, 60f, 180f, false, true), Is.False);
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(
                3, 20f, 180f, false, true), Is.False);
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(
                3, 60f, 30f, false, true), Is.False);
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(
                3, 60f, 180f, true, true), Is.False);
        }

        [Test]
        public void MissingProviderNeverExposesAnAdButton()
        {
            Assert.That(MonetizationAds.HasProvider, Is.False);
            Assert.That(MonetizationAds.Provider.IsReady(
                FullScreenAdPlacement.PreRunShieldReward), Is.False);
            Assert.That(MonetizationAds.Provider.IsReady(
                FullScreenAdPlacement.GameOverReviveReward), Is.False);
        }
    }
}
