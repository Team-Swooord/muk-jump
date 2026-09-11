using System.IO;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class AppleGameCenterRemovalTests
    {
        GameObject host;
        GameLanguage previousLanguage;

        [SetUp] public void SetUp()
        {
            previousLanguage = GameLocalization.Language;
            MukJumpIdentityProfile.UseStoreForTests(new MemoryIdentityStore());
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        }

        [TearDown] public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            GameLocalization.SetLanguage(previousLanguage);
            MukJumpIdentityProfile.UseStoreForTests(null);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }

        [Test] public void BackendRankingAndAppleAccountLoginArePreserved()
        {
            Assert.That(MukJumpBackendSettings.Load().AllTimeRankUuid,
                Is.EqualTo(MukJumpBackendReleaseValidator.ExpectedAllTimeRankUuid));
            Assert.That(LobbyOptionsView.ShouldOfferAppleSignIn(RuntimePlatform.IPhonePlayer), Is.True);
            Assert.That(MukJumpBackendReleaseValidator.CollectIssues(BackendReleasePlatform.IOS), Is.Empty);
        }

        [Test] public void NoAutomaticGameCenterRuntimeOrNativeBridgeRemains()
        {
            Assert.That(typeof(GameManager).Assembly.GetType("MukJump.Core.AppleGameCenterRuntime"), Is.Null);
            Assert.That(File.Exists("Assets/Plugins/iOS/MukJumpGameCenter.mm"), Is.False);
            Assert.That(typeof(MukJumpBackendSettings).GetProperty("AppleGameCenterLeaderboardId"), Is.Null);
            string manager = File.ReadAllText("Assets/Scripts/Core/GameManager.cs");
            Assert.That(manager, Does.Not.Contain("AppleGameCenterRuntime"));
            Assert.That(manager, Does.Contain("MukJumpAccountRuntime.Instance?.NotifyRunSettled(result)"));
            Assert.That(manager, Does.Contain("AppsInTossGameCenterRuntime.SubmitCompletedRun(result)"));
        }

        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        public void SingleLeaderboardHasNoTabsOrSubtitleAndKeepsReadableRefresh(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            host = new GameObject("SingleLeaderboardUiTests");
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            var page = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage");
            Assert.That(page, Is.Not.Null);
            Assert.That(page.Find("GlobalLeaderboardTab"), Is.Null);
            Assert.That(page.Find("AppleLeaderboardTab"), Is.Null);
            Assert.That(page.Find("LeaderboardStatus"), Is.Null);
            var refresh = page.Find("LeaderboardRefresh").GetComponent<Button>();
            Assert.That(refresh.interactable, Is.True);
            Canvas.ForceUpdateCanvases();
            var text = refresh.GetComponentInChildren<Text>(true);
            Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), text.text);
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.text);
            var title = (RectTransform)page.Find("LeaderboardTitle");
            var heading = (RectTransform)page.Find("RankHeading");
            Assert.That(title.anchoredPosition.y + title.rect.yMin,
                Is.GreaterThan(heading.anchoredPosition.y + heading.rect.yMax));
            Assert.That(page.Find("RegionFlag1"), Is.Not.Null);
            Assert.That(page.Find("NameCell10/Name10"), Is.Not.Null);
        }
    }
}
