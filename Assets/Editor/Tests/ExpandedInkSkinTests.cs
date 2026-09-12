using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class ExpandedInkSkinTests
    {
        MemoryPermanentGrowthStore store;
        readonly List<Object> objects = new();
        [SetUp] public void Setup()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.UseStoreForTests(settings);
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            var score = Track(new GameObject("ExpandedSkinScore")).AddComponent<ScoreManager>();
            Call(score,"Awake"); Call(score,"OnEnable");
        }
        [TearDown] public void Teardown()
        {
            foreach (var o in objects) if(o != null) Object.DestroyImmediate(o);
            objects.Clear();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            InkSkinPreferences.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
        }
        public static IEnumerable<InkSkinId> Rewards => Enumerable.Range(1,35).Select(i=>(InkSkinId)i);
        [Test] public void CatalogHasRequestedCountsAndOnlyThreeSignatureCostumes()
        {
            Assert.That(InkSkinCatalog.Count,Is.EqualTo(36));
            var all=Rewards.Select(InkSkinCatalog.Get).ToArray();
            Assert.That(all.Count(x=>x.Tier is SkinTier.Basic or SkinTier.Hidden),Is.EqualTo(5));
            Assert.That(all.Count(x=>x.Tier==SkinTier.Easy),Is.EqualTo(10));
            Assert.That(all.Count(x=>x.Tier==SkinTier.Medium),Is.EqualTo(10));
            Assert.That(all.Count(x=>x.Tier==SkinTier.Hard),Is.EqualTo(5));
            Assert.That(all.Count(x=>x.Tier==SkinTier.UltraHidden),Is.EqualTo(5));
            Assert.That(Rewards.Count(InkSkinCatalog.IsSignatureCostume),Is.EqualTo(3));
            Assert.That(all.Select(x=>x.Asset).Distinct().Count(),Is.EqualTo(35));
            Assert.That(InkSkinAchievements.IsUnlocked((InkSkinId)36),Is.False);
        }
        [TestCaseSource(nameof(Rewards))]
        public void EveryRewardStartsLockedHasArtAndTranslations(InkSkinId id)
        {
            var d=InkSkinCatalog.Get(id);
            Assert.That(d.Id,Is.EqualTo(id));
            Assert.That(d.Goal,Is.GreaterThan(0));
            Assert.That(InkSkinAchievements.IsUnlocked(id),Is.False);
            Assert.That(InkSkinCatalog.Sprite(id),Is.Not.Null,id.ToString());
            Assert.That(InkSkinTranslations.TryTranslate(d.Korean,false,out string english),Is.True);
            Assert.That(english,Is.EqualTo(d.English));
            Assert.That(InkSkinTranslations.TryTranslate(d.Korean,true,out string japanese),Is.True);
            Assert.That(japanese,Is.EqualTo(d.Japanese));
            Assert.That(InkSkinTranslations.TryTranslate(InkSkinAchievements.Requirement(id),false,out _),Is.True);
            Assert.That(InkSkinTranslations.TryTranslate(InkSkinAchievements.Requirement(id),true,out _),Is.True);
        }
        [TestCaseSource(nameof(Rewards))]
        public void EachThresholdIsExclusiveBelowAndInclusiveAtGoal(InkSkinId id)
        {
            var d=InkSkinCatalog.Get(id); var state=new SkinAchievementState();
            var fields=new Dictionary<SkinMetric,string> {
                [SkinMetric.Best]="best",[SkinMetric.Runs]="runs",[SkinMetric.DrawnLandings]="drawnLandings",
                [SkinMetric.Items]="items",[SkinMetric.InkDrops]="inkDrops",[SkinMetric.Survival]="survival",
                [SkinMetric.Clones]="clones",[SkinMetric.NightBest]="nightBest",[SkinMetric.NoItemBest]="noItemBest",[SkinMetric.Growth]="growth" };
            if (fields.TryGetValue(d.Metric,out var field))
            {
                typeof(SkinAchievementState).GetField(field).SetValue(state,d.Goal-1);
                Assert.That(state.Progress(id,0,0),Is.LessThan(d.Goal));
                typeof(SkinAchievementState).GetField(field).SetValue(state,d.Goal);
                Assert.That(state.Progress(id,0,0),Is.EqualTo(d.Goal));
            }
            else if(d.Metric==SkinMetric.Distance)
            {
                Assert.That(state.Progress(id,d.Goal-1,0),Is.LessThan(d.Goal));
                Assert.That(state.Progress(id,d.Goal,0),Is.EqualTo(d.Goal));
            }
            else
            {
                Assert.That(state.Progress(id,0,0),Is.Zero);
                state.Grant(id); Assert.That(state.Progress(id,0,0),Is.EqualTo(d.Goal));
            }
        }
        [Test] public void LifetimeRunsContinueBeyondBoundedDedupeHistoryAndSurviveReload()
        {
            for(int i=0;i<100;i++) Settle("lifetime-"+i,1);
            Assert.That(PermanentGrowthProfile.CompletedRunHistoryCount,Is.EqualTo(64));
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.InkMecha),Is.True);
            Settle("lifetime-99",1);
            Assert.That(JObject.Parse(store.Json)["skinAchievements"]["runs"].Value<int>(),Is.EqualTo(100));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.InkMecha),Is.True);
        }
        [Test] public void MeasuredEventsAreAtomicAndMissingOldSnapshotDoesNotAwardSecrets()
        {
            Settle("old",0,default);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.InkGhost),Is.False);
            var stats=SkinRunStats.Begin(true,InkSkinId.CatEars);
            stats.drawnLandings=100;stats.maxLivingPlayers=3;
            Settle("cat",777,stats,60);
            foreach(var id in new[]{InkSkinId.ClockworkCat,InkSkinId.LuckyDice,InkSkinId.TigerMask,InkSkinId.TurtleShell,InkSkinId.BrushMaster,InkSkinId.MoonCrown})
                Assert.That(InkSkinAchievements.IsUnlocked(id),Is.True,id.ToString());
            Settle("zero",0,SkinRunStats.Begin(false,InkSkinId.Ink));
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.InkGhost),Is.True);
        }
        [TestCase(59,false,0)] [TestCase(60,true,1)] [TestCase(60,false,1)]
        public void CatSecretRequiresNightSixtySecondsAndNoItems(int seconds,bool day,int items)
        {
            var stats=SkinRunStats.Begin(!day,InkSkinId.CatEars);stats.items=items;
            Settle("no-cat",777,stats,seconds);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.ClockworkCat),Is.False);
        }
        [Test] public void FailedSettlementDoesNotUnlockAndRetryDoesNotDuplicate()
        {
            Settle("base",1);
            var stats=SkinRunStats.Begin(false,InkSkinId.Ink);stats.items=5;stats.inkDrops=1;
            store.ThrowOnPrimarySave=true;
            Assert.That(Settle("failed",100,stats).Accepted,Is.False);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.CatEars),Is.False);
            store.ThrowOnPrimarySave=false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(),Is.True);
            Settle("failed",100,stats);Settle("failed",100,stats);
            Assert.That(PermanentGrowthProfile.SkinProgress(InkSkinId.SpiritHalo),Is.EqualTo(5));
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.CatEars),Is.True);
        }
        [Test] public void LegacyMigrationUsesKnownLowerBoundAndDamagedNewPayloadIsReadOnly()
        {
            for(int i=0;i<70;i++) Settle("legacy-"+i,1);
            var old=JObject.Parse(store.Json);old["balanceVersion"]=11;old.Remove("skinAchievements");
            store.Json=old.ToString(Newtonsoft.Json.Formatting.None);store.BackupJson="";
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.SkinProgress(InkSkinId.InkMecha),Is.EqualTo(64));
            var damaged=JObject.Parse(store.Json);((JObject)damaged["skinAchievements"]).Remove("items");
            store.Json=damaged.ToString();store.BackupJson="";
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery,Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask),Is.False);
        }
        [Test] public void LegacyGrowthCostumeSurvivesImmediateRefundBeforeAnyNewRunOrPurchase()
        {
            Settle("legacy-growth-fund",20000);
            int purchased=0;
            foreach(var node in PermanentGrowthCatalog.Nodes)
                if(purchased<5 && PermanentGrowthProfile.TryPurchaseNode(node)) purchased++;
            Assert.That(purchased,Is.EqualTo(5));
            var legacy=JObject.Parse(store.Json);
            legacy["balanceVersion"]=11;
            legacy.Remove("skinAchievements");
            store.Json=legacy.ToString(Newtonsoft.Json.Formatting.None);
            store.BackupJson="";
            PermanentGrowthProfile.ResetCacheForTests();
            // 이전 이후 추가 판 정산·구매 없이 바로 환불해도 이미 달성한 의상은 남는다.
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.KoiMask),Is.True);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.KoiMask),Is.True);
            Assert.That(PermanentGrowthProfile.TryResetPurchasedNodes(),Is.True);
            Assert.That(JObject.Parse(store.Json)["ownedNodeIds"].Count(),Is.Zero);
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.KoiMask),Is.True);
            Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(InkSkinId.KoiMask));
            Assert.That(PermanentGrowthProfile.CompletedRunHistoryCount,Is.EqualTo(1));
        }
        [Test] public void SecretCountersResetAcrossAccountReplacementAndDeletion()
        {
            for(int i=0;i<5;i++) SkinSecrets.AcceptedNightChange(i%2==0);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NightBat),Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Astronaut),Is.False);
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string first),Is.True);
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(),Is.True);
            SkinSecrets.AcceptedNightChange(false);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Astronaut),Is.False);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NightBat),Is.False);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(first),Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NightBat),Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Astronaut),Is.False);
        }
        [Test] public void WardrobeSecretRequiresOrderedExplicitSelection()
        {
            SkinSecrets.SelectedByUser(InkSkinId.Gat);SkinSecrets.SelectedByUser(InkSkinId.SpiderMask);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.OrigamiWings),Is.False);
            foreach(var id in new[]{InkSkinId.Ink,InkSkinId.Gat,InkSkinId.Ink,InkSkinId.SpiderMask,InkSkinId.Ink}) SkinSecrets.SelectedByUser(id);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.OrigamiWings),Is.True);
        }
        [Test] public void PendingSnapshotPreservesRunStatsAndOldSnapshotsAreNotMeasured()
        {
            var old=JsonUtility.FromJson<PendingGameOverSettlementSnapshot>("{\"version\":1,\"runId\":\"old\",\"eligible\":true}");
            Assert.That(old.skinStats.IsMeasured,Is.False);
            var stats=SkinRunStats.Begin(true,InkSkinId.CatEars);stats.drawnLandings=29;stats.items=4;stats.inkDrops=1;
            var next=JsonUtility.FromJson<PendingGameOverSettlementSnapshot>(JsonUtility.ToJson(new PendingGameOverSettlementSnapshot { skinStats=stats }));
            Assert.That(next.skinStats.IsMeasured,Is.True);Assert.That(next.skinStats.drawnLandings,Is.EqualTo(29));
            Assert.That(next.skinStats.startingSkin,Is.EqualTo(InkSkinId.CatEars));
        }
        [TestCase("drawnLandings")] [TestCase("items")] [TestCase("version")]
        public void IncompletePendingStatsCannotBeMistakenForMeasuredZero(string missing)
        {
            var pending = new PendingGameOverSettlementSnapshot { runId=Guid.NewGuid().ToString("N"),
                scoreHeight=777, swarmProgressHeight=777, activeGameplaySeconds=60, eligible=true,
                skinStats=SkinRunStats.Begin(true,InkSkinId.CatEars) };
            var json=JObject.Parse(JsonUtility.ToJson(pending));((JObject)json["skinStats"]).Remove(missing);
            Assert.That(SkinRunStats.HasCompletePayload(json.ToString()),Is.False);
            var pendingStore=new MemoryPendingGameOverSettlementStore();pendingStore.Save(json.ToString());
            GameManager.UsePendingGameOverSettlementStoreForTests(pendingStore);
            try
            {
                Assert.That(GameManager.TryRecoverPendingGameOverSettlement(),Is.True);
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.InkGhost),Is.False);
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.ClockworkCat),Is.False);
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.TigerMask),Is.False);
            }
            finally {GameManager.RestorePendingGameOverSettlementStoreForTests();}
        }
        [TestCase(InkSkinId.CloudDragon)] [TestCase(InkSkinId.InkMecha)] [TestCase(InkSkinId.ClockworkCat)]
        public void SignatureCostumesAreOverlaysAndNeverReplaceTheOriginalInk(InkSkinId id)
        {
            var sprites=Resources.LoadAll<Sprite>(InkSkinCatalog.ResourcePath(id));
            Assert.That(sprites.Length,Is.EqualTo(1));
            var host=Track(new GameObject("PreservedInk",typeof(SpriteRenderer),typeof(Rigidbody2D),typeof(CircleCollider2D)));
            var player=host.AddComponent<PlayerController>(); Call(player,"Awake");
            var renderer=host.GetComponent<SpriteRenderer>();
            var original=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().First(s=>s.name=="idle");
            renderer.sprite=original;
            var view=host.AddComponent<InkSkinView>();
            Call(view,"OnEnable");
            var systems=Track(new GameObject("SignatureSystems"));
            var growth=systems.AddComponent<RunGrowthController>();Call(growth,"OnEnable");
            typeof(RunGrowthController).GetField("skinSnapshot",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(growth,id);
            Call(view,"LateUpdate");
            Assert.That(renderer.sprite,Is.SameAs(original));
            Assert.That(renderer.enabled,Is.True);
            Assert.That(view.AccessoryRenderer.sprite,Is.SameAs(sprites[0]));
        }
        [Test] public void WardrobeScrollsAllCardsAndFullyConcealsUnfoundRewards()
        {
            var host=Track(new GameObject("ExpandedWardrobe"));var view=host.AddComponent<InkWardrobeView>();
            view.BuildForTests();view.SetNavigationPresentation(true,true);
            for(int page=0;page<6;page++)
            {
                Assert.That(view.Paper.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name.StartsWith("SkinCard")),Is.EqualTo(36));
                view.ChangePage(1);
            }
            view.Select(InkSkinId.ClockworkCat);
            Assert.That(view.Paper.Find("SkinName").GetComponent<UnityEngine.UI.Text>().text,Is.Empty);
            Assert.That(view.Paper.Find("ShowroomStage/Preview/QuestionMark").GetComponent<UnityEngine.UI.Text>().enabled,Is.True);
            Assert.That(view.Paper.Find("ShowroomStage/Preview/Accessory").GetComponent<UnityEngine.UI.Image>().enabled,Is.False);
            Assert.That(view.Paper.Find("AchievementProgress").GetComponent<UnityEngine.UI.Text>().text,Is.Empty);
            Assert.That(view.EquipButton.interactable,Is.False);
        }
        PermanentGrowthSettlement Settle(string id,int height,SkinRunStats stats=default,float seconds=2) =>
            PermanentGrowthProfile.SettleRun(id,height,height,0,seconds,true,stats);
        T Track<T>(T value) where T:Object {objects.Add(value);return value;}
        static void Call(object o,string name) => o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.Invoke(o,null);
    }
}
