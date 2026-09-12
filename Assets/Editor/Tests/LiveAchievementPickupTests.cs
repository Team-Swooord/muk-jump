using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using MukJump.Items;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;
namespace MukJump.EditorTests
{
    public sealed class LiveAchievementPickupTests
    {
        readonly List<GameObject> objects = new();
        GameManager game;
        PlayerController player;
        GameObject Root(string name) { var go = new GameObject(name); objects.Add(go); return go; }
        [SetUp] public void Setup()
        {
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            var settings = new MemoryLobbySettingsStore(); LobbySettingsProfile.UseStoreForTests(settings); InkSkinPreferences.UseStoreForTests(settings);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var score=Root("Score").AddComponent<ScoreManager>(); Call(score,"OnEnable"); Call(score,"Awake");
            game=Root("Game").AddComponent<GameManager>(); Call(game,"OnEnable");
            typeof(GameManager).GetProperty("State").SetValue(game,GameState.Playing);
            Field(game,"currentRunId","pickup-test"); Field(game,"skinRunStats",SkinRunStats.Begin(false,InkSkinId.Ink));
            ((LiveInkAchievements)Get(game,"liveAchievements")).Begin();
            var body=Root("Player"); body.AddComponent<SpriteRenderer>(); body.AddComponent<Rigidbody2D>(); body.AddComponent<CircleCollider2D>();
            player=body.AddComponent<PlayerController>(); Call(player,"Awake"); game.RegisterPlayer(player);
        }
        [TearDown] public void Teardown()
        {
            foreach(var go in objects) if(go!=null) Object.DestroyImmediate(go); objects.Clear();
            PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
            MobileApplicationLifecycle.SetPlatformVisibility(true);
        }
        ItemPickup Pick(ItemType type)
        {
            var item=Root("Pickup").AddComponent<ItemPickup>(); item.Configure(type,0);
            Call(item,"OnTriggerEnter2D",player.GetComponent<CircleCollider2D>()); return item;
        }
        void Shield() { if(player.HasShield) Call(player,"ConsumeShield"); Pick(ItemType.InkShield); }
        int Extra => PermanentGrowthProfile.AchievementSnapshot().gameplayMask;
        [TestCase(false)] [TestCase(true)]
        public void ThirdAdditionalPickupUnlocksAndDuplicateOrRejectedPickupDoesNotCount(bool thirdIsInkDrop)
        {
            Pick(ItemType.InkDrop); Assert.That(player.AchievementItemFlight,Is.GreaterThan(0));
            var first=Pick(ItemType.InkShield);
            Call(first,"OnTriggerEnter2D",player.GetComponent<CircleCollider2D>());
            var rejected=Pick(ItemType.InkShield);
            Assert.That(rejected.GetComponent<CircleCollider2D>().enabled,Is.True);
            Assert.That((int)Get(game,"itemFlightCount"),Is.EqualTo(1));
            Shield(); Assert.That(Extra&2,Is.Zero);
            if(thirdIsInkDrop) Pick(ItemType.InkDrop); else Shield();
            Assert.That(Extra&2,Is.EqualTo(2));
            if(thirdIsInkDrop) Assert.That((int)Get(game,"itemFlightCount"),Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void WindOrReviveLaunchCancelsTheItemFlight(bool reviveHeight)
        {
            Pick(ItemType.InkDrop); Shield();
            player.LaunchInkDrop(reviveHeight?50:36,false);
            Assert.That(player.AchievementItemFlight,Is.Zero);
            Shield(); Shield(); Shield(); Assert.That(Extra&2,Is.Zero);
        }
        [Test] public void FallingAndBackgroundDoNotCompleteAnAscentAchievement()
        {
            Pick(ItemType.InkDrop); Shield(); Shield();
            player.Body.linearVelocity=Vector2.down;
            Shield(); Assert.That(Extra&2,Is.Zero);
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            game.NotifySkinWallCling(); Assert.That(Extra&1,Is.Zero);
        }
        [Test] public void ActualWallClingGrantsOnceAndStayCannotRetrigger()
        {
            PermanentGrowthProfile.TryGrantLiveAchievements(PermanentGrowthProfile.SkinAccountToken,1UL<<(int)InkSkinId.SpiderMask,0);
            typeof(GameManager).GetProperty("State").SetValue(game,GameState.Lobby);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiderMask),Is.True);
            // 실제 플레이어는 판 시작 시 외형 능력을 고정한다.
            var growth=Root("RunGrowth").AddComponent<RunGrowthController>(); Call(growth,"OnEnable");
            Field(growth,"skinSnapshot",InkSkinId.SpiderMask);
            typeof(GameManager).GetProperty("State").SetValue(game,GameState.Playing);
            var wall=Root("Wall").AddComponent<ScreenSideWall>();
            Assert.That(Call(player,"TryBeginWallCling",wall,1f),Is.True);
            Assert.That(Extra&1,Is.EqualTo(1));
            Assert.That(Call(player,"TryBeginWallCling",wall,1f),Is.False);
        }
        static object Get(object o,string key)=>o.GetType().GetField(key,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
        static void Field(object o,string key,object value)=>o.GetType().GetField(key,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
        static object Call(object o,string key,params object[] args)=>o.GetType().GetMethod(key,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);
    }
}
