using System.Collections.Generic;
using System.Linq;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
namespace MukJump.EditorTests
{
    public sealed class LiveInkAchievementTests
    {
        MemoryPermanentGrowthStore store;
        [SetUp] public void Setup()
        { store = new(); PermanentGrowthProfile.UseStoreForTests(store); ScoreManager.UseStoreForTests(new MemoryScoreStore()); }
        [TearDown] public void Teardown()
        { PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests(); }
        [Test] public void ImmediateThresholdSavesOnceWithoutPrematureEndOfRunRewardsOrDoubleCounting()
        {
            var live = new LiveInkAchievements(); live.Begin(); var notices = new List<int>();
            var stats = SkinRunStats.Begin(true, InkSkinId.CatEars); stats.items = 5; stats.inkDrops = 1;
            live.Sample(stats, 777, 65, 3, notices.Add); live.Sample(stats, 777, 65, 3, notices.Add);
            Assert.That(notices.Distinct().Count(), Is.EqualTo(notices.Count));
            Assert.That(notices, Has.Member((int)InkSkinId.BellRibbon));
            Assert.That(notices, Has.Member(InkSkinCatalog.Count)); Assert.That(notices, Has.Member(InkSkinCatalog.Count+1));
            foreach (var id in new[]{InkSkinId.FirstRibbon, InkSkinId.LuckyDice, InkSkinId.ClockworkCat, InkSkinId.TigerMask, InkSkinId.PaperBoat})
                Assert.That(notices, Has.No.Member((int)id));
            var saved = PermanentGrowthProfile.AchievementSnapshot();
            Assert.That(saved.items, Is.Zero); Assert.That(saved.runs, Is.Zero);
            Assert.That(saved.Has(InkSkinId.BellRibbon), Is.True);
            Assert.That(PermanentGrowthProfile.SettleRun("live", 777, 777, 0, 65, true, stats).Accepted, Is.True);
            saved = PermanentGrowthProfile.AchievementSnapshot();
            Assert.That(saved.items, Is.EqualTo(5)); Assert.That(saved.runs, Is.EqualTo(1));
            Assert.That(saved.gameplayMask, Is.EqualTo(3)); Assert.That(saved.Has(InkSkinId.LuckyDice), Is.True);
        }
        [Test] public void FailedWriteAndSwitchedProfileNeverAnnounceOrSendCompletion()
        {
            var live = new LiveInkAchievements(); live.Begin(); var notices = new List<int>();
            store.ThrowOnPrimarySave = true;
            live.Sample(SkinRunStats.Begin(false, InkSkinId.Ink), 100, 10, 3, notices.Add);
            Assert.That(notices, Is.Empty); Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(InkAchievementCatalog.SavedProgress(PermanentGrowthProfile.AchievementSnapshot(), 0), Is.Empty);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            live.Sample(SkinRunStats.Begin(false, InkSkinId.Ink), 100, 10, 3, notices.Add);
            Assert.That(notices, Is.Empty); Assert.That(PermanentGrowthProfile.AchievementSnapshot().gameplayMask, Is.Zero);
        }
        [Test] public void LegacyStateLoadsButV2RequiresItsNewField()
        {
            var state = new SkinAchievementState { version = 1 };
            string legacy = JsonUtility.ToJson(state).Replace(",\"gameplayMask\":0", "");
            Assert.That(SkinAchievementState.HasCompletePayload("{\"skinAchievements\":"+legacy+"}"), Is.True);
            Assert.That(JsonUtility.FromJson<SkinAchievementState>(legacy).IsValid, Is.True);
            Assert.That(SkinAchievementState.HasCompletePayload("{\"skinAchievements\":"+legacy.Replace("\"version\":1", "\"version\":2")+"}"), Is.False);
        }
        [Test] public void GameCenterUsesOnlySavedProgressAndNeverRevealsLockedSecrets()
        {
            var state = new SkinAchievementState { items = 2, best = 50 };
            var progress = InkAchievementCatalog.SavedProgress(state, 0);
            Assert.That(progress.Single(x=>x.id == InkAchievementCatalog.Id((int)InkSkinId.BellRibbon)).percent, Is.EqualTo(40));
            for(int i=1;i<=InkAchievementCatalog.Count;i++) if(InkAchievementCatalog.Hidden(i))
                Assert.That(progress.Any(x=>x.id == InkAchievementCatalog.Id(i)), Is.False);
            state.Grant(InkSkinId.NightBat);
            Assert.That(InkAchievementCatalog.SavedProgress(state,0).Single(x=>x.id == InkAchievementCatalog.Id(31)).percent, Is.EqualTo(100));
            Assert.That(Enumerable.Range(1,InkAchievementCatalog.Count).Select(InkAchievementCatalog.Id).Distinct().Count(), Is.EqualTo(InkAchievementCatalog.Count));
        }
        [Test] public void ConfirmedLegacyBestIsReadForGameCenterWithoutMutatingGrowth()
        {
            ScoreManager.UseStoreForTests(new MemoryScoreStore { Best=237 });
            var host=new GameObject("LegacyScore");
            try
            {
                var score=host.AddComponent<ScoreManager>();
                foreach(var method in new[]{"OnEnable","Awake"}) typeof(ScoreManager).GetMethod(method,
                    System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(score,null);
                Assert.That(PermanentGrowthProfile.AchievementSnapshot().best,Is.Zero);
                Assert.That(PermanentGrowthProfile.AchievementSyncSnapshot().best,Is.EqualTo(237));
                Assert.That(PermanentGrowthProfile.AchievementSnapshot().best,Is.Zero);
            }
            finally { Object.DestroyImmediate(host); }
        }
        [Test] public void ToastSitsBelowHudAndPauseTargetOnPortraitAndTabletSafeAreas()
        {
            foreach(var size in new[]{new Vector2Int(1179,2556),new Vector2Int(1080,1920),new Vector2Int(1536,2048)})
            {
                var safe=new Rect(0,80,size.x,size.y-200);
                float top=InkAchievementToast.CalculateTop(safe,size.x,size.y)-MobileUiLayout.GetLogicalTopInset(safe,size.x,size.y);
                Assert.That(top,Is.LessThan(GameplayHudView.CalculatePauseTouchRect(safe,size.x,size.y).yMin));
                Assert.That(top,Is.LessThan(GameplayHudView.CalculateVisibleHudRect(safe,size.x,size.y).yMin));
            }
        }
        [Test] public void OldResponsesCannotCrossGameOrGameCenterAccountsAndNewProgressIsNotLostInFlight()
        {
            var sync = new AppleAchievementSyncState(); var owner = new object();
            sync.Bind("gc-A",owner); int load = sync.Begin();
            Assert.That(sync.Accept(load,"gc-A",true,null),Is.True);
            var state = new SkinAchievementState { items=2 };
            var batch = sync.Changes(state,0); int report = sync.Begin(batch);
            state.items=5; Assert.That(sync.Accept(report,"gc-A",true,null),Is.True);
            Assert.That(sync.Changes(state,0).Single(x=>x.id==InkAchievementCatalog.Id(10)).percent,Is.EqualTo(100));
            report=sync.Begin(sync.Changes(state,0)); sync.Bind("gc-A",new object());
            Assert.That(sync.Accept(report,"gc-A",true,null),Is.False); Assert.That(sync.Loaded,Is.False);
            load=sync.Begin(); sync.Bind("gc-B",owner);
            Assert.That(sync.Accept(load,"gc-A",true,null),Is.False);
            Assert.That(state.gameplayMask,Is.Zero,"Game Center 응답은 로컬 데이터를 바꾸지 않는다.");
        }
        [Test] public void NativeAchievementBridgeKeepsCallbackAndNeverResetsOrSubmitsLeaderboardScores()
        {
            string native=System.IO.File.ReadAllText("Assets/Plugins/iOS/MukJumpAchievements.mm");
            Assert.That(native,Does.Contain("a.showsCompletionBanner = NO"));
            Assert.That(native,Does.Not.Contain("resetAchievements"));
            Assert.That(native,Does.Not.Contain("GKLeaderboard"));
            string managed=System.IO.File.ReadAllText("Assets/Scripts/Core/AppleAchievementRuntime.cs");
            Assert.That(managed,Does.Contain("[UnityEngine.Scripting.Preserve]\n        public void OnAchievementReply"));
        }
    }
}
