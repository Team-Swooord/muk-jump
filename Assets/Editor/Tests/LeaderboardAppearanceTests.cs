using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class LeaderboardAppearanceTests
    {
        [TestCase(null, "", 0)]
        [TestCase("kr", "KR", 0)]
        [TestCase("US|1|7", "US", 7)]
        [TestCase("JP|1|39", "JP", 39)]
        [TestCase("|1|36", "", 36)]
        [TestCase("KR|1|-1", "KR", 0)]
        [TestCase("KR|1|999999999999999999", "KR", 0)]
        [TestCase("US|2|7", "US", 0)]
        [TestCase("JP|1|999", "JP", 0)]
        [TestCase("US|1|7|bad", "US", 0)]
        public void DecodeOldOrMalformedAppearance(string data, string region, int skin)
        {
            LeaderboardAppearance.Decode(data, out string decodedRegion, out InkSkinId decodedSkin);
            Assert.That(decodedRegion, Is.EqualTo(region));
            Assert.That(decodedSkin, Is.EqualTo((InkSkinId)skin));
        }

        [Test]
        public void AllCostumesRoundTripAndFitFlagSizedCellWithoutViewerUnlocks()
        {
            var host = new GameObject("LeaderboardAvatarFixture", typeof(RectTransform));
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            try
            {
                Sprite idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png")
                    .OfType<Sprite>().First(s => s.name == "idle");
                var avatar = new InkLeaderboardAvatar(host.transform, "Avatar", Vector2.zero, 44, idle);
                for (int i = 0; i < InkSkinCatalog.Count; i++)
                {
                    string payload = LeaderboardAppearance.Encode("jp", (InkSkinId)i);
                    Assert.That(System.Text.Encoding.UTF8.GetByteCount(payload), Is.LessThan(256));
                    LeaderboardAppearance.Decode(payload, out var region, out var skin);
                    Assert.That(skin, Is.EqualTo(InkSkinCatalog.Normalize((InkSkinId)i)));
                    var entry = new MukJumpLeaderboardEntry(10, 237, "未解放の衣装", regionCode: region, equippedSkin: skin);
                    avatar.Set(entry);
                    Assert.That(avatar.Root.sizeDelta, Is.EqualTo(new Vector2(44, 44)));
                    var body = avatar.Root.Find("Artwork/OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                    var art = avatar.Root.Find("Artwork/Accessory").GetComponent<InkAccessoryImage>();
                    Assert.That(body.sprite, Is.SameAs(idle));
                    Assert.That(art.sprite == InkSkinCatalog.Sprite(skin), Is.True);
                    Assert.That(avatar.Root.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g => !g.raycastTarget), Is.True);
                    var bounds = LobbySkinPreview.CostumeBounds(skin, art.sprite);
                    float size = body.rectTransform.rect.width;
                    Vector2 min = body.rectTransform.anchoredPosition + (Vector2)body.transform.parent.localPosition + bounds.min * size;
                    Vector2 max = body.rectTransform.anchoredPosition + (Vector2)body.transform.parent.localPosition + bounds.max * size;
                    Assert.That(avatar.Root.rect.Contains(min), Is.True, skin + " min");
                    Assert.That(avatar.Root.rect.Contains(max), Is.True, skin + " max");
                    Assert.That(art.transform.GetSiblingIndex(), Is.EqualTo(InkSkinCatalog.BehindBody(skin) ? 0 : 1));
                }
                avatar.Set(null);
                Assert.That(avatar.Root.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(host); PermanentGrowthProfile.RestoreDefaultStoreForTests(); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RenderLeaderboardCostumesWithNoNetworkWrites()
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings); InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var oldLanguage = GameLocalization.Language;
            MukJumpBackendSettings legacySettings = null;
            try
            {
                Assert.That(MukJumpAccountRuntime.ShouldEnableBackendForRuntime(RuntimePlatform.OSXEditor, true, true), Is.False);
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var account = MukJumpAccountRuntime.Instance;
                Assert.That(account.IsOnlineAuthenticated, Is.False);
                // 이 fixture는 구버전 전체 기간 보드의 순위 아바타를 검증한다.
                legacySettings = Object.Instantiate(MukJumpBackendSettings.Load());
                typeof(MukJumpBackendSettings).GetField("monthlyRankUuid", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(legacySettings, "");
                typeof(MukJumpAccountRuntime).GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(account, legacySettings);
                var entries = (List<MukJumpLeaderboardEntry>)typeof(MukJumpAccountRuntime)
                    .GetField("leaderboardEntries", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(account);
                entries.Clear();
                var skins = new[] { InkSkinId.Gat, InkSkinId.CraneWings, InkSkinId.RedHeadband, InkSkinId.Astronaut,
                    InkSkinId.CloudBoots, InkSkinId.SpiderMask, InkSkinId.ButterflyWings, InkSkinId.Ink, InkSkinId.FoxMask, InkSkinId.CloudDragon };
                for (int i = 0; i < 10; i++) entries.Add(new MukJumpLeaderboardEntry(i + 1, 2370 - i * 100,
                    new[] { "guest12345", "먹방울친구", "月の旅人", "InkJumper" }[i % 4],
                    regionCode: new[] { "KR", "US", "JP" }[i % 3], equippedSkin: skins[i], timeSeconds: 90 + i * 17));
                var view = Object.FindAnyObjectByType<LobbyOptionsView>();
                int refreshes = 0;
                typeof(LobbyOptionsView).GetField("refreshLeaderboardForTests", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(view, new System.Action(() => refreshes++));
                view.OpenLeaderboard(); yield return Wait(1);
                var page = view.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage");
                Assert.That(page.Find("LeaderboardRefresh"), Is.Null);
                Assert.That(refreshes, Is.EqualTo(1));
                // Update에서 타이머가 실제로 호출되는지 확인하되 검증을 위해 1분을 기다리지는 않는다.
                typeof(LobbyOptionsView).GetField("nextLeaderboardRefreshRealtime", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(view, Time.realtimeSinceStartupAsDouble - 1d);
                yield return null;
                yield return null;
                Assert.That(refreshes, Is.EqualTo(2));
                for (int i = 1; i <= 10; i++)
                {
                    var rank = (RectTransform)page.Find("Rank" + i);
                    var avatar = (RectTransform)page.Find("SkinAvatar" + i);
                    var flag = (RectTransform)page.Find("RegionFlag" + i);
                    var name = (RectTransform)page.Find("NameCell" + i);
                    var score = (RectTransform)page.Find("LeaderboardRow" + i);
                    Assert.That(avatar.gameObject.activeSelf, Is.True);
                    Assert.That(avatar.sizeDelta, Is.EqualTo(flag.sizeDelta));
                    var columns = new[] { rank, avatar, flag, name, score };
                    for (int c = 0; c < columns.Length - 1; c++)
                        Assert.That(columns[c].anchoredPosition.x + columns[c].rect.xMax,
                            Is.LessThan(columns[c + 1].anchoredPosition.x + columns[c + 1].rect.xMin));
                }
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language); yield return Wait(.3f);
                    string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
                    float until = Time.realtimeSinceStartup + 5;
                    while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
                    Assert.That(File.Exists(path), Is.True);
                    Directory.CreateDirectory("output/qa/leaderboard-appearance");
                    File.Copy(path, "output/qa/leaderboard-appearance/leaderboard-" + language + ".png", true);
                    File.Delete(path);
                }
                entries.Clear(); account.RefreshLeaderboard(); yield return Wait(.2f);
                Assert.That(page.Find("SkinAvatar1").gameObject.activeSelf, Is.False);
                Assert.That(account.IsOnlineAuthenticated, Is.False);
                view.Close();
            }
            finally
            {
                if (legacySettings != null) Object.Destroy(legacySettings);
                GameLocalization.SetLanguage(oldLanguage); Application.runInBackground = oldBackground;
                LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
