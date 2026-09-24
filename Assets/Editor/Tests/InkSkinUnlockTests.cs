using System.Linq;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MukJump.EditorTests
{
    [Category("SkinUnlockFlow")]
    public sealed class InkSkinUnlockTests
    {
        GameObject host, scoreHost;
        InkSkinUnlockView view;
        MemoryPermanentGrowthStore growth;
        MemoryLobbySettingsStore settings;
        static GameOverResult Saved => new(100, 100, true, 1, 1, true);
        static ulong Bit(InkSkinId id) => 1UL << (int)id;

        [Test] public void LobbyRevealConsumesOnlySavedNewRewardsAndCannotRepeatAfterAPlaySession()
        {
            object account = new object(); var state = new InkSkinUnlockState();
            ulong before = Bit(InkSkinId.Gat), reward = Bit(InkSkinId.NightBat);
            state.Initialize(account, before);
            state.QueueLobby(account, before, before); Assert.That(state.PeekLobby(account, before), Is.Zero);
            state.QueueLobby(account, reward, before); Assert.That(state.PeekLobby(account, before), Is.Zero, "저장 실패 보상은 예약하지 않는다.");
            state.QueueLobby(account, reward, before | reward); state.QueueLobby(account, reward, before | reward);
            Assert.That(state.PeekLobby(account, before | reward), Is.EqualTo(reward));
            Assert.That(state.PeekLobby(account, before | reward), Is.EqualTo(reward), "전환 시작/취소는 조회만 하며 소비하지 않는다.");
            Assert.That(state.TakeLobby(account, before | reward), Is.EqualTo(reward));
            Assert.That(state.TakeLobby(account, before | reward), Is.Zero);
            state.QueueLobby(account, reward, before | reward); Assert.That(state.PeekLobby(account, before | reward), Is.Zero);
            state.BeginRun("next", account, before | reward);
            Assert.That(state.Take("next", account, before | reward | Bit(InkSkinId.FirstRibbon), Saved), Is.EqualTo(Bit(InkSkinId.FirstRibbon)));
        }

        [Test] public void LobbyRewardCanWaitForGameOverButNeverLeaksAcrossAnAccountSwitch()
        {
            var account = new object(); var state = new InkSkinUnlockState(); ulong bit = Bit(InkSkinId.BondKnot);
            state.Initialize(account, 0); state.QueueLobby(account, bit, bit);
            state.BeginRun("run", account, bit);
            Assert.That(state.PeekLobby(new object(), bit), Is.Zero);
            var pending = new GameOverResult(10, 10, true, 1, 1, true, growthRewardSaved: false);
            Assert.That(state.Take("run", account, bit, pending), Is.Zero);
            Assert.That(state.PeekLobby(account, bit), Is.EqualTo(bit));
            var rollback = new object(); state.FollowSettlement(account, rollback);
            Assert.That(state.Take("run", rollback, bit, Saved), Is.EqualTo(bit));
            Assert.That(state.TakeLobby(rollback, bit), Is.Zero);
            state.Initialize(new object(), bit); Assert.That(state.PeekLobby(rollback, bit), Is.Zero);
        }

        [Test] public void LobbyQueueRejectsRetiredIdsAndUnownedOrFailedSecretGrants()
        {
            var state = new InkSkinUnlockState(); object account = PermanentGrowthProfile.SkinAccountToken;
            state.Initialize(account, InkSkinUnlockState.ReadUnlocked());
            growth.ThrowOnPrimarySave = true;
            Assert.That(PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat), Is.False);
            state.FollowSettlement(account, PermanentGrowthProfile.SkinAccountToken);
            state.QueueLobby(PermanentGrowthProfile.SkinAccountToken, Bit(InkSkinId.NightBat), InkSkinUnlockState.ReadUnlocked());
            Assert.That(state.PeekLobby(PermanentGrowthProfile.SkinAccountToken, ulong.MaxValue), Is.Zero);
            state.QueueLobby(PermanentGrowthProfile.SkinAccountToken, Bit(InkSkinId.StormScarf), ulong.MaxValue);
            Assert.That(state.PeekLobby(PermanentGrowthProfile.SkinAccountToken, ulong.MaxValue), Is.Zero);
        }
        [SetUp] public void Setup()
        {
            growth = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(growth);
            settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            ScoreManager.UseStoreForTests(new MemoryScoreStore { Best = 2000 });
            scoreHost = new GameObject("UnlockScore"); var score = scoreHost.AddComponent<ScoreManager>();
            Call(score,"OnEnable"); Call(score,"Awake");
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            host = new GameObject("UnlockTest"); view = host.AddComponent<InkSkinUnlockView>();
            view.BuildForTests(AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png")
                .OfType<Sprite>().First(s=>s.name=="idle"));
        }
        [TearDown] public void Teardown()
        {
            Object.DestroyImmediate(host); Object.DestroyImmediate(scoreHost);
            InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
            MobileApplicationLifecycle.SetPlatformVisibility(true);
        }
        [Test] public void NewRunAndLobbyDiscoveriesAreComparedBeforeTheEarlyBestCommit()
        {
            var owner = new object(); var state = new InkSkinUnlockState();
            state.Initialize(owner, Bit(InkSkinId.BambooHat));
            state.BeginRun("run",owner,Bit(InkSkinId.BambooHat)|Bit(InkSkinId.NightBat));
            ulong all = Bit(InkSkinId.BambooHat)|Bit(InkSkinId.Gat)|Bit(InkSkinId.NightBat);
            Assert.That(state.Take("run",owner,all,Saved),Is.EqualTo(Bit(InkSkinId.Gat)|Bit(InkSkinId.NightBat)));
            Assert.That(state.Take("run",owner,all,Saved),Is.Zero);
            state.BeginRun("next",owner,all);
            Assert.That(state.Take("next",owner,all,Saved),Is.Zero);
        }
        [TestCase(false,true,true,false)] [TestCase(true,false,true,false)]
        [TestCase(true,true,false,false)] [TestCase(true,true,true,true)]
        public void IneligiblePreviewOrFailedSaveNeverConsumesTheUnlock(bool eligible,bool growthSaved,bool recordSaved,bool preview)
        {
            var owner=new object(); var state=new InkSkinUnlockState(); state.BeginRun("run",owner,0);
            var pending=new GameOverResult(100,100,true,0,0,eligible,growthSaved,recordSaved,isGrowthPreview:preview);
            Assert.That(state.Take("run",owner,Bit(InkSkinId.Gat),pending),Is.Zero);
            Assert.That(state.Take("run",owner,Bit(InkSkinId.Gat),Saved),Is.EqualTo(Bit(InkSkinId.Gat)));
        }
        [Test] public void LateScoreInitializationDoesNotAnnounceAlreadyOwnedBestCostumes()
        {
            var owner=new object(); var state=new InkSkinUnlockState(); state.Initialize(owner,0);
            state.BeginRun("run",owner,Bit(InkSkinId.Gat));
            Assert.That(state.Take("run",owner,Bit(InkSkinId.Gat)|Bit(InkSkinId.FirstRibbon),Saved),Is.EqualTo(Bit(InkSkinId.FirstRibbon)));
        }
        [Test] public void AccountSwitchAndStaleRunCannotRevealAnOldReward()
        {
            var oldOwner=new object(); var newOwner=new object(); var state=new InkSkinUnlockState();
            state.BeginRun("old",oldOwner,0);
            Assert.That(state.Take("old",newOwner,Bit(InkSkinId.Gat),Saved),Is.Zero);
            state.BeginRun("new",newOwner,Bit(InkSkinId.Gat));
            Assert.That(state.Take("old",newOwner,Bit(InkSkinId.Gat)|Bit(InkSkinId.FirstRibbon),Saved),Is.Zero);
            Assert.That(state.Take("new",newOwner,Bit(InkSkinId.Gat),Saved),Is.Zero);
        }
        [Test] public void FailedGrowthRollbackStaysSilentUntilRecoveryAndANewRun()
        {
            PermanentGrowthProfile.SettleRun("base",1,1,0,10,true,SkinRunStats.Begin(false,InkSkinId.Ink));
            var stats=SkinRunStats.Begin(false,InkSkinId.Ink);
            stats.items=stats.inkDrops=InkSkinAchievements.Goal(InkSkinId.CatEars);
            var state=new InkSkinUnlockState(); object owner=PermanentGrowthProfile.SkinAccountToken;
            state.BeginRun("retry",owner,InkSkinUnlockState.ReadUnlocked());
            growth.ThrowOnPrimarySave=true;
            var failed=PermanentGrowthProfile.SettleRun("retry",10,10,0,10,true,stats);
            Assert.That(failed.Accepted,Is.False);
            state.FollowSettlement(owner,PermanentGrowthProfile.SkinAccountToken);
            Assert.That(state.Take("retry",PermanentGrowthProfile.SkinAccountToken,InkSkinUnlockState.ReadUnlocked(),
                new GameOverResult(10,2000,false,0,0,true,growthRewardSaved:false)),Is.Zero);
            growth.ThrowOnPrimarySave=false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(),Is.True);
            owner=PermanentGrowthProfile.SkinAccountToken;
            state.BeginRun("after-recovery",owner,InkSkinUnlockState.ReadUnlocked());
            Assert.That(PermanentGrowthProfile.SettleRun("after-recovery",10,10,0,10,true,stats).Accepted,Is.True);
            state.FollowSettlement(owner,PermanentGrowthProfile.SkinAccountToken);
            ulong newly=state.Take("after-recovery",PermanentGrowthProfile.SkinAccountToken,InkSkinUnlockState.ReadUnlocked(),Saved);
            Assert.That(newly & Bit(InkSkinId.CatEars),Is.Not.Zero);
            Assert.That(newly & Bit(InkSkinId.Gat),Is.Zero);
        }
        [Test] public void QueueFiltersUnownedRewardsAndAcknowledgesExactlyOnce()
        {
            int calls=0;
            Assert.That(view.Show(Bit(InkSkinId.Gat)|Bit(InkSkinId.NightBat)|1UL,PermanentGrowthProfile.SkinAccountToken,()=>calls++),Is.True);
            Assert.That(view.Count,Is.EqualTo(1)); Assert.That(view.Current,Is.EqualTo(InkSkinId.Gat));
            Assert.That(view.Show(Bit(InkSkinId.Gat),PermanentGrowthProfile.SkinAccountToken,()=>calls++),Is.False);
            view.Advance(); view.Advance(); view.AcknowledgeAll();
            Assert.That(calls,Is.EqualTo(1)); Assert.That(view.IsOpen,Is.False);
            Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(InkSkinId.Ink));
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat),Is.True);
        }
        [TestCase(GameLanguage.Korean)] [TestCase(GameLanguage.English)] [TestCase(GameLanguage.Japanese)]
        public void RevealTextAndArtworkFitAndOnlyOneSharedEffectIsBuilt(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat);
            PermanentGrowthProfile.SettleRun("paper-wings", InkSkinAchievements.Goal(InkSkinId.CloudDragon), InkSkinAchievements.Goal(InkSkinId.CloudDragon), 0,
                InkSkinAchievements.Goal(InkSkinId.OrigamiWings), true, SkinRunStats.Begin(false, InkSkinId.Ink));
            int objects=view.GetComponentsInChildren<Transform>(true).Length;
            foreach(var id in new[]{InkSkinId.Gat,InkSkinId.CloudDragon,InkSkinId.PhoenixCrown,InkSkinId.NightBat,InkSkinId.OrigamiWings})
            {
                Assert.That(view.Show(Bit(id),PermanentGrowthProfile.SkinAccountToken,null),Is.True,id.ToString()); Canvas.ForceUpdateCanvases();
                foreach(var label in view.Paper.GetComponentsInChildren<UnityEngine.UI.Text>())
                {
                    if(string.IsNullOrEmpty(label.text)) continue;
                    Assert.That(label.preferredHeight,Is.LessThanOrEqualTo(label.rectTransform.rect.height+.1f),label.name+": "+label.text);
                    Assert.That(label.font.HasCharacter(label.text[0]),Is.True,label.text);
                    AssertInside(view.Paper,label.rectTransform,24);
                }
                var stage=(RectTransform)view.Paper.Find("UnlockStage");
                foreach(var art in stage.Find("Entrance/Preview").GetComponentsInChildren<UnityEngine.UI.Image>().Where(i=>i.enabled))
                    AssertInside(stage,art.rectTransform,10);
                Assert.That(view.GetComponentsInChildren<InkShowroomPresentation>().Length,Is.EqualTo(1));
                view.AcknowledgeAll();
            }
            Assert.That(view.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(objects));
        }
        [Test] public void AccountDeletionDismissesTheRevealWithoutChangingSavedCostumes()
        {
            int calls=0, invalidated=0;
            view.Show(Bit(InkSkinId.Gat),PermanentGrowthProfile.SkinAccountToken,()=>calls++,()=>invalidated++);
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(),Is.True);
            view.Advance(); Assert.That(calls,Is.Zero);
            Call(view,"Update"); Assert.That(view.IsOpen,Is.False); Assert.That(calls,Is.Zero); Assert.That(invalidated,Is.EqualTo(1));
            Assert.That(host.transform.Find("SkinUnlockCanvas").GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
        }
        [Test] public void EquipButtonSavesOnlyDisplayedUnlockedCostumeAndKeepsRemainingRewards()
        {
            PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat);
            Assert.That(view.Show(Bit(InkSkinId.Gat) | Bit(InkSkinId.NightBat), PermanentGrowthProfile.SkinAccountToken, null), Is.True);
            var equip = view.Paper.Find("Equip").GetComponent<UnityEngine.UI.Button>();
            Assert.That(equip.interactable, Is.True);
            equip.onClick.Invoke();
            Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.Gat));
            Assert.That(view.IsOpen, Is.True);
            Assert.That(view.Count, Is.EqualTo(2));
            Assert.That(equip.interactable, Is.False);
            view.Advance();
            Assert.That(view.Current, Is.EqualTo(InkSkinId.NightBat));
            Assert.That(equip.interactable, Is.True);
            Assert.That(view.EquipCurrent(), Is.True);
            view.Advance();
            Assert.That(view.IsOpen, Is.False);
            Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.NightBat));
            Assert.That(view.EquipCurrent(), Is.False);
        }
        [Test] public void FailedEquipSaveLeavesPreviousCostumeAndAllowsRetry()
        {
            Assert.That(view.Show(Bit(InkSkinId.Gat), PermanentGrowthProfile.SkinAccountToken, null), Is.True);
            settings.ThrowOnSave = true;
            Assert.That(view.EquipCurrent(), Is.False);
            Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(view.IsOpen, Is.True);
            Assert.That(view.Paper.Find("EquipStatus").GetComponent<UnityEngine.UI.Text>().text, Is.Not.Empty);
            settings.ThrowOnSave = false;
            Assert.That(view.EquipCurrent(), Is.True);
            Assert.That(view.Paper.Find("EquipStatus").GetComponent<UnityEngine.UI.Text>().text, Is.Empty);
        }
        [Test] public void StaleAccountAndHiddenApplicationCannotEquip()
        {
            Assert.That(view.Show(Bit(InkSkinId.Gat), PermanentGrowthProfile.SkinAccountToken, null), Is.True);
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            Assert.That(view.EquipCurrent(), Is.False);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(), Is.True);
            Assert.That(view.EquipCurrent(), Is.False);
            Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.Ink));
        }
        [Test] public void DebugPreviewNeverOffersOrPersistsEquip()
        {
            Assert.That(view.ShowPreview(Bit(InkSkinId.PhoenixCrown), PermanentGrowthProfile.SkinAccountToken, null), Is.True);
            Assert.That(view.Paper.Find("Equip").gameObject.activeSelf, Is.False);
            Assert.That(view.EquipCurrent(), Is.False);
            Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.PhoenixCrown), Is.False);
        }
        static void AssertInside(RectTransform parent,RectTransform child,float margin)
        {
            var corners=new Vector3[4]; child.GetWorldCorners(corners);
            foreach(var corner in corners)
            {
                Vector3 p=parent.InverseTransformPoint(corner);
                Assert.That(p.x,Is.InRange(parent.rect.xMin+margin,parent.rect.xMax-margin),child.name);
                Assert.That(p.y,Is.InRange(parent.rect.yMin+margin,parent.rect.yMax-margin),child.name);
            }
        }
        static void Call(object target,string name) => target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    }
}
