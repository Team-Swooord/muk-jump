using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthTreeProfileTests
    {
        MemoryPermanentGrowthStore store;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
        }

        [TearDown]
        public void TearDown()
        {
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void Profile_CanPurchase와_TryPurchase가_같은_선행조건을_사용한다()
        {
            Seed(
                PermanentGrowthCatalog.TotalCost,
                0,
                string.Empty);

            Assert.That(
                PermanentGrowthProfile.MeetsRequirements(
                    PermanentGrowthType.InkRecovery),
                Is.False);
            Assert.That(
                PermanentGrowthProfile.CanPurchase(
                    PermanentGrowthType.InkRecovery),
                Is.False);
            Assert.That(
                PermanentGrowthProfile.TryPurchase(
                    PermanentGrowthType.InkRecovery),
                Is.False);
            Assert.That(
                PermanentGrowthProfile.GetLockReason(
                    PermanentGrowthType.InkRecovery),
                Does.Contain("먹그릇"));

            Assert.That(
                PermanentGrowthProfile.TryPurchase(
                    PermanentGrowthType.InkCapacity),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.TryPurchase(
                    PermanentGrowthType.InkCapacity),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.MeetsRequirements(
                    PermanentGrowthType.InkRecovery),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.CanPurchase(
                    PermanentGrowthType.InkRecovery),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.TryPurchase(
                    PermanentGrowthType.InkRecovery),
                Is.True);
        }

        [Test]
        public void Profile_기존에_구매한_노드는_새_선행조건을_소급하지_않는다()
        {
            Seed(
                PermanentGrowthCatalog.TotalCost,
                6,
                "{\"id\":\"permanent.ink_recovery\",\"level\":1}");

            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkCapacity),
                Is.Zero);
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkRecovery),
                Is.EqualTo(1));
            Assert.That(
                PermanentGrowthProfile.MeetsRequirements(
                    PermanentGrowthType.InkRecovery),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.GetLockReason(
                    PermanentGrowthType.InkRecovery),
                Is.Empty);
            Assert.That(
                PermanentGrowthProfile.TryPurchase(
                    PermanentGrowthType.InkRecovery),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkRecovery),
                Is.EqualTo(2));
        }

        [Test]
        public void Profile_schema1_기존_JSON의_레벨과_효과를_그대로_보존한다()
        {
            const string ranks =
                "{\"id\":\"permanent.ink_capacity\",\"level\":2}," +
                "{\"id\":\"permanent.ink_recovery\",\"level\":1}," +
                "{\"id\":\"permanent.platform_lifetime\",\"level\":3}," +
                "{\"id\":\"permanent.jump_charge\",\"level\":4}";
            Seed(17, 120, ranks);

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(17));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(120));
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkCapacity),
                Is.EqualTo(2));
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkRecovery),
                Is.EqualTo(1));
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.PlatformLifetime),
                Is.EqualTo(3));
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.JumpCharge),
                Is.EqualTo(4));
            Assert.That(
                PermanentGrowthProfile.InkCapacityMultiplier,
                Is.EqualTo(1.03f).Within(0.0001f));
            Assert.That(
                PermanentGrowthProfile.InkRecoveryMultiplier,
                Is.EqualTo(1.02f).Within(0.0001f));
            Assert.That(
                PermanentGrowthProfile.PlatformLifetimeMultiplier,
                Is.EqualTo(1.0375f).Within(0.0001f));
            Assert.That(
                PermanentGrowthProfile.JumpChargeMultiplier,
                Is.EqualTo(0.97f).Within(0.0001f));
        }

        [Test]
        public void Profile_신규_수치와_최종패시브를_노출한다()
        {
            const string ranks =
                "{\"id\":\"permanent.vitality\",\"level\":1}," +
                "{\"id\":\"permanent.damage_grace\",\"level\":3}," +
                "{\"id\":\"permanent.last_breath\",\"level\":1}," +
                "{\"id\":\"permanent.jump_power\",\"level\":5}," +
                "{\"id\":\"permanent.drawn_platform_leap\",\"level\":1}," +
                "{\"id\":\"permanent.stroke_guard\",\"level\":1}";
            Seed(0, 0, ranks);

            Assert.That(PermanentGrowthProfile.MaxHealthBonus, Is.EqualTo(1));
            Assert.That(
                PermanentGrowthProfile.DamageGraceBonusSeconds,
                Is.EqualTo(0.24f).Within(0.0001f));
            Assert.That(PermanentGrowthProfile.HasLastBreath, Is.True);
            Assert.That(
                PermanentGrowthProfile.JumpPowerMultiplier,
                Is.EqualTo(1.05f).Within(0.0001f));
            Assert.That(
                PermanentGrowthProfile.DrawnPlatformLeapMultiplier,
                Is.EqualTo(1.10f).Within(0.0001f));
            Assert.That(
                PermanentGrowthProfile.NewPlatformsHaveStrokeGuard,
                Is.True);
        }

        void Seed(int wallet, int spent, string rankObjects)
        {
            string ranks = string.IsNullOrEmpty(rankObjects)
                ? string.Empty
                : rankObjects;
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":1," +
                $"\"wallet\":{wallet},\"spent\":{spent}," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"legacy-run\"," +
                $"\"ranks\":[{ranks}]}}";
            PermanentGrowthProfile.ResetCacheForTests();
            _ = PermanentGrowthProfile.Currency;
        }
    }
}
