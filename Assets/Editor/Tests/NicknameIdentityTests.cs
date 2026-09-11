using System;
using System.Collections.Generic;
using System.Reflection;
using BackEnd;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class MemoryIdentityStore : MukJumpIdentityProfile.IStore
    {
        readonly Dictionary<string, string> values = new();
        public string Read(string key) => values.TryGetValue(key, out string value) ? value : string.Empty;
        public void Write(string key, string value) => values[key] = value;
        public void Save() { }
    }

    public sealed class NicknameIdentityTests
    {
        GameObject host;
        MukJumpAccountRuntime account;
        [SetUp] public void SetUp()
        {
            MukJumpIdentityProfile.UseStoreForTests(new MemoryIdentityStore());
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        }
        [TearDown] public void TearDown()
        {
            if (account != null) Call(account, "OnDisable");
            if (host != null) Object.DestroyImmediate(host);
            MukJumpIdentityProfile.UseStoreForTests(null);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }
        [Test] public void LocalGuestAlwaysHasStableUidAndRandomDefaultName()
        {
            string uid = MukJumpIdentityProfile.LocalUid;
            string name = MukJumpIdentityProfile.GuestNickname;
            Assert.That(uid, Does.StartWith("local-"));
            Assert.That(Guid.TryParseExact(uid.Substring(6), "N", out _), Is.True);
            Assert.That(MukJumpIdentityProfile.LocalUid, Is.EqualTo(uid));
            Assert.That(MukJumpIdentityProfile.GuestNickname, Is.EqualTo(name));
            Assert.That(MukJumpIdentityProfile.IsGeneratedNickname(name), Is.True);
            Assert.That(name, Does.Match(@"^guest\d{5}$"));
            Assert.That(name.Length, Is.EqualTo(10));
        }

        [TestCase("ㅇㅇㄹㄹ", "ㅇㅇㄹㄹ")]
        [TestCase("ㄱㅏㄴㅏ", "가나")]
        [TestCase("ㅏㅓ", "ㅏㅓ")]
        [TestCase("ㄳㅄ", "ㄳㅄ")]
        [TestCase("\u110b\u110b\u1105\u1105", "ㅇㅇㄹㄹ")]
        [TestCase("\u11a8\u11ab", "ㄱㄴ")]
        [TestCase("\u1100\u1161\u1102\u1161", "가나")]
        [TestCase(" Ｍｕｋ１２ ", "Muk12")]
        [TestCase("ｿﾗ", "ソラ")]
        [TestCase("먹방울_Muk", "먹방울_Muk")]
        public void NicknameGlyphsStayStableThroughStorageNormalization(string input, string expected)
        {
            Assert.That(MukJumpIdentityProfile.TryNormalizeNickname(input, out var value, out var error), Is.True, error);
            Assert.That(value, Is.EqualTo(input.Trim().Normalize(System.Text.NormalizationForm.FormKC)),
                "서버 이름 중복 판정에 사용하는 기존 정규화는 유지합니다.");
            Assert.That(MukJumpIdentityProfile.TryNormalizeNickname(value, out var again, out _), Is.True);
            Assert.That(again, Is.EqualTo(value), "중복 정규화가 표시 문자를 다시 바꾸면 안 됩니다.");
            Assert.That(MukJumpIdentityProfile.FormatNicknameForDisplay(value), Is.EqualTo(expected));
        }

        [Test] public void NicknameGlyphsCoverCompatibilityJamoIncludingCompoundConsonants()
        {
            for (char c = '\u3131'; c <= '\u318e'; c++)
            {
                string original = c.ToString();
                string stored = original.Normalize(System.Text.NormalizationForm.FormKC);
                Assert.That(MukJumpIdentityProfile.FormatNicknameForDisplay(stored), Is.EqualTo(original),
                    "U+" + ((int)c).ToString("X4"));
            }
        }

        [Test] public void NicknameGlyphsSentToServerKeepCanonicalIdentityButDisplayLikeFirstInput()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            string submitted = null;
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((name, cb) =>
            { submitted = name; cb(Result(204)); }));
            bool? success = null;
            account.ChangeNickname("ㅇㅇㄹㄹ", (ok, _) => success = ok);
            Assert.That(success, Is.True);
            Assert.That(submitted, Is.EqualTo("\u110b\u110b\u1105\u1105"));
            Assert.That(account.Nickname, Is.EqualTo(submitted));
            Assert.That(MukJumpIdentityProfile.ReadNickname("apple-a"), Is.EqualTo(submitted));
            Assert.That(MukJumpIdentityProfile.FormatNicknameForDisplay(submitted), Is.EqualTo("ㅇㅇㄹㄹ"));
        }

        [TestCase(false)] [TestCase(true)]
        public void NicknameGlyphsFromOldSaveMatchRetypingWithoutChangingServerIdentity(bool firstAppleLogin)
        {
            const string legacy = "\u110b\u110b\u1105\u1105";
            CreateAccount(MukJumpAccountKind.Apple);
            Set(account, "identityLoaded", true);
            Set(account, "identityLoadedScope", "apple-a");
            Set(account, "identityNickname", legacy);
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb => cb(UserInfo(legacy))));
            MukJumpIdentityProfile.SaveNickname("apple-a", legacy);
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            Call(view, "RefreshSettingsUuid");
            var settings = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage/Nickname").GetComponent<Text>();
            Assert.That(settings.text, Is.EqualTo("ㅇㅇㄹㄹ"));
            typeof(LobbyOptionsView).GetMethod("OpenNicknamePopup", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(view, new object[] { firstAppleLogin });
            var paper = host.transform.Find("NicknameCanvas/SafeAreaRoot/NicknameScroll");
            var input = paper.GetComponentInChildren<InputField>();
            paper.GetComponent<CanvasGroup>().interactable = true;
            input.ForceLabelUpdate();
            Assert.That(input.textComponent.text, Is.EqualTo("ㅇㅇㄹㄹ"));
            Assert.That(input.textComponent.font, Is.EqualTo(InkPalette.UiFont));
            string reopened = input.text;
            input.text = string.Empty;
            foreach (char c in "ㅇㅇㄹㄹ") input.ProcessEvent(new Event { type = EventType.KeyDown, character = c });
            input.ForceLabelUpdate();
            Assert.That(input.textComponent.text, Is.EqualTo(reopened));
            Assert.That(input.textComponent.font, Is.EqualTo(InkPalette.UiFont));
            var name = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage/NameCell1/Name1").GetComponent<Text>();
            LobbyOptionsView.FitLeaderboardName(name, legacy);
            Assert.That(name.text, Is.EqualTo(reopened));
            Assert.That(account.Nickname, Is.EqualTo(legacy), "표시 보정은 서버 이름 변경이 아닙니다.");
            Assert.That(MukJumpIdentityProfile.ReadNickname("apple-a"), Is.EqualTo(legacy));
        }

        [TestCase("deleted-owner")]
        [TestCase("")]
        [TestCase(MukJumpIdentityProfile.LocalScope)]
        public void DeletionRotatesLocalGuestAndOnlyClearsDeletedOwnersIdentity(string deletedScope)
        {
            var store = new MemoryIdentityStore();
            MukJumpIdentityProfile.UseStoreForTests(store);
            string oldUid = MukJumpIdentityProfile.LocalUid;
            string oldNickname = MukJumpIdentityProfile.GuestNickname;
            MukJumpIdentityProfile.SaveUid("deleted-owner", "123456");
            MukJumpIdentityProfile.SaveNickname("deleted-owner", "삭제할이름");
            MukJumpIdentityProfile.SaveUid("other-owner", "987654");
            MukJumpIdentityProfile.SaveNickname("other-owner", "보존할이름");
            Assert.That(MukJumpIdentityProfile.TryResetForAccountDeletion(deletedScope), Is.True);
            string newUid = MukJumpIdentityProfile.LocalUid;
            string newNickname = MukJumpIdentityProfile.GuestNickname;
            Assert.That(newUid, Is.Not.EqualTo(oldUid));
            Assert.That(newNickname, Is.Not.EqualTo(oldNickname));
            Assert.That(newNickname, Does.Match(@"^guest\d{5}$"));
            if (deletedScope == "deleted-owner")
            {
                Assert.That(MukJumpIdentityProfile.ReadUid(deletedScope), Is.Empty);
                Assert.That(MukJumpIdentityProfile.ReadNickname(deletedScope), Is.Empty);
            }
            Assert.That(MukJumpIdentityProfile.ReadUid("other-owner"), Is.EqualTo("987654"));
            Assert.That(MukJumpIdentityProfile.ReadNickname("other-owner"), Is.EqualTo("보존할이름"));
            MukJumpIdentityProfile.UseStoreForTests(store);
            Assert.That(MukJumpIdentityProfile.LocalUid, Is.EqualTo(newUid));
            Assert.That(MukJumpIdentityProfile.GuestNickname, Is.EqualTo(newNickname));
        }
        [TestCase(" 먹방울_1 ", true)]
        [TestCase("Muk-Jump", true)]
        [TestCase("a", false)]
        [TestCase("<b>먹</b>", false)]
        [TestCase("먹\n점프", false)]
        [TestCase("123456789012345678901", false)]
        [TestCase("1234567890", true)]
        [TestCase("12345678901", false)]
        [TestCase("가나다라마바사아자차", true)]
        [TestCase("가나다라마바사아자차카", false)]
        [TestCase("guest12345", false)]
        public void NamesAreNormalizedAndValidated(string value, bool valid) =>
            Assert.That(MukJumpIdentityProfile.TryNormalizeNickname(value, out _, out _), Is.EqualTo(valid));

        [TestCase("씨발")]
        [TestCase("ㅅㅂ")]
        [TestCase("ㅆㅣㅂㅏㄹ")]
        [TestCase("씨_발")]
        [TestCase("씨1발")]
        [TestCase("개새끼")]
        [TestCase("병-신")]
        [TestCase("야_동")]
        [TestCase("섹1스")]
        [TestCase("니애미")]
        [TestCase("죽어버려")]
        [TestCase("ＦＵＣＫ")]
        [TestCase("f_u_c_k")]
        [TestCase("fuсk")]
        [TestCase("fúck")]
        [TestCase("fuuuck")]
        [TestCase("p0rn")]
        [TestCase("PORN")]
        [TestCase("s3x")]
        [TestCase("ass123")]
        [TestCase("dick01")]
        [TestCase("cock")]
        [TestCase("rape")]
        [TestCase("n1gga")]
        [TestCase("f4ggot")]
        public void NicknamePolicyRejectsAbusiveSexualAndObfuscatedExpressions(string name)
        {
            Assert.That(NicknameContentFilter.ContainsDisallowedContent(name), Is.True);
            Assert.That(MukJumpIdentityProfile.TryNormalizeNickname(name, out _, out string error), Is.False);
            Assert.That(error, Is.EqualTo("사용할 수 없는 표현이 포함되어 있어요"));
        }

        [TestCase("먹방울")]
        [TestCase("시바견")]
        [TestCase("슬픈구름")]
        [TestCase("보석지기")]
        [TestCase("시대발전")]
        [TestCase("GrapeJump")]
        [TestCase("Peacock")]
        [TestCase("Assassin")]
        [TestCase("Classic")]
        [TestCase("Dickens")]
        [TestCase("Scunthorpe")]
        [TestCase("Essex")]
        [TestCase("NightSky10")]
        public void NicknameFilterKeepsOrdinaryNamesAndNonAbusiveNegativeWords(string name)
        {
            Assert.That(NicknameContentFilter.ContainsDisallowedContent(name), Is.False);
            Assert.That(MukJumpIdentityProfile.TryNormalizeNickname(name, out _, out _), Is.True);
        }

        [Test] public void LegacyGuestNamesBecomeStableGeneratedNames()
        {
            MukJumpIdentityProfile.SaveNickname(MukJumpIdentityProfile.LocalScope, "guest(1234567890)");
            string replacement = MukJumpIdentityProfile.GuestNickname;
            Assert.That(replacement.Length, Is.EqualTo(10));
            Assert.That(MukJumpIdentityProfile.IsGeneratedNickname(replacement), Is.True);
            Assert.That(MukJumpIdentityProfile.GuestNickname, Is.EqualTo(replacement));
            MukJumpIdentityProfile.SaveNickname(MukJumpIdentityProfile.LocalScope, "MyOriginalNickname");
            Assert.That(MukJumpIdentityProfile.GuestNickname, Is.EqualTo(replacement));
        }

        [TestCase(MukJumpAccountKind.LocalGuest, "씨_발")]
        [TestCase(MukJumpAccountKind.Apple, "PORN")]
        [TestCase(MukJumpAccountKind.BackendGuest, "병신")]
        [TestCase(MukJumpAccountKind.Apple, "12345678901")]
        public void RejectedNicknameCannotWriteToLocalOrBackendStorage(MukJumpAccountKind kind, string name)
        {
            CreateAccount(kind);
            bool contactedBackend = false;
            bool? accepted = null;
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((_, cb) =>
            { contactedBackend = true; cb(Result(204)); }));
            account.ChangeNickname(name, (ok, _) => accepted = ok);
            Assert.That(accepted, Is.False);
            Assert.That(contactedBackend, Is.False);
            Assert.That(MukJumpIdentityProfile.ReadNickname(MukJumpIdentityProfile.LocalScope), Is.Empty);
            Assert.That(MukJumpIdentityProfile.ReadNickname("apple-a"), Is.Empty);
        }

        [Test] public void UnverifiedSavedAppleIdentityIsNotDisplayedAsCurrentAccount()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Set(account, "backendUidForTests", new Func<string>(() => "1234567"));
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(callback => callback(UserInfo("애플테스트"))));
            Call(account, "RefreshAuthenticatedIdentity");
            Assert.That(account.Nickname, Is.EqualTo("애플테스트"));
            Property(account, "IsOnlineAuthenticated", false);
            Assert.That(account.BackendUid, Is.Empty);
            Assert.That(account.Nickname, Is.EqualTo(MukJumpIdentityProfile.GuestNickname));
            Assert.That(MukJumpIdentityProfile.ReadNickname("apple-a"), Is.EqualTo("애플테스트"),
                "오프라인 표시는 숨기되 재인증용 저장을 삭제하지 않습니다.");
        }

        [Test] public void SettingsUseFullBackendCouponUidAndNeverExposeALocalFallback()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            const string uid = "1234567890123";
            Set(account, "backendUidForTests", new Func<string>(() => uid));
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            Transform page = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage");
            Text label = page.Find("UuidButton/Label").GetComponent<Text>();
            Call(view, "RefreshSettingsUuid");
            Assert.That(account.BackendUid, Is.EqualTo(uid));
            Assert.That(label.text, Is.EqualTo("UID " + uid));
            Assert.That(page.Find("UuidButton").GetComponent<Button>().interactable, Is.True);

            Property(account, "AccountKind", MukJumpAccountKind.LocalGuest);
            Call(view, "RefreshSettingsUuid");
            Assert.That(account.PlayerId, Does.StartWith("local-"), "내부 로컬 계정 식별자는 유지한다.");
            Assert.That(account.BackendUid, Is.Empty);
            Assert.That(label.text, Is.EqualTo("UID —"));
            Assert.That(page.Find("UuidButton").GetComponent<Button>().interactable, Is.False);
        }

        [Test]
        public void SettingsLoadsMissingUidWithoutOpeningAccountAndBothPagesUseIt()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            string liveUid = string.Empty;
            int requests = 0;
            Set(account, "backendUidForTests", new Func<string>(() => liveUid));
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb =>
            {
                requests++;
                liveUid = "1234567890123";
                cb(UserInfo("먹새싹"));
            }));
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            Call(view, "RefreshSettingsUuid");
            Text settingsUid = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage/UuidButton/Label").GetComponent<Text>();
            Assert.That(settingsUid.text, Is.EqualTo("UID " + liveUid));
            Assert.That(requests, Is.EqualTo(1));
            Call(view, "RefreshSettingsUuid");
            Assert.That(requests, Is.EqualTo(1));
            Call(view, "RefreshAccountState");
            var accountUid = (Text)typeof(LobbyOptionsView).GetField("accountPlayerIdText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            Assert.That(accountUid.text, Is.EqualTo(settingsUid.text));
            Assert.That(MukJumpIdentityProfile.ReadUid("apple-a"), Is.EqualTo(liveUid));
        }

        [Test]
        public void MissingUidRefreshIsThrottledWhenServerDoesNotReturnAnId()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Set(account, "backendUidForTests", new Func<string>(() => string.Empty));
            int requests = 0;
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb =>
            { requests++; cb(UserInfo("먹새싹")); }));
            account.RefreshDisplayIdentity();
            account.RefreshDisplayIdentity();
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(account.BackendUid, Is.Empty);
        }

        [Test] public void BackendUidCacheDoesNotFollowAnotherAccountOrAcceptALongUuid()
        {
            CreateAccount(MukJumpAccountKind.BackendGuest);
            Set(account, "backendUidForTests", new Func<string>(() => "1234567890123"));
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb => cb(UserInfo("guest12345"))));
            Call(account, "RefreshAuthenticatedIdentity");
            Assert.That(MukJumpIdentityProfile.ReadUid("apple-a"), Is.EqualTo("1234567890123"));
            Set(account, "backendUidForTests", new Func<string>(() => string.Empty));
            Assert.That(account.BackendUid, Is.EqualTo("1234567890123"));
            Set(account, "currentAccountScopeForTests", new Func<string>(() => "apple-b"));
            Set(account, "backendUidForTests", new Func<string>(() => "01234567-89ab-cdef-0123-456789abcdef"));
            Assert.That(account.BackendUid, Is.Empty);
        }

        [Test]
        public void CachedUidDoesNotSuppressFailedNicknameLookupRetry()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Set(account, "backendUidForTests", new Func<string>(() => "1234567"));
            int calls = 0;
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb =>
            { calls++; cb(calls == 1 ? Result(503) : UserInfo("guest12345")); }));
            Call(account, "RefreshAuthenticatedIdentity");
            Assert.That(account.BackendUid, Is.EqualTo("1234567"));
            Assert.That(account.NeedsNicknameSetup, Is.False);
            Set(account, "displayIdentityRetryAt", -1f);
            account.RefreshDisplayIdentity();
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(account.NeedsNicknameSetup, Is.True);
        }

        [Test]
        public void GuestNicknameWriteFailureCanRetryEvenWithLoadedIdentityAndUid()
        {
            CreateAccount(MukJumpAccountKind.BackendGuest);
            Set(account, "backendUidForTests", new Func<string>(() => "1234567"));
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb => cb(UserInfo(null))));
            int writes = 0;
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((_, cb) =>
            { writes++; cb(Result(writes == 1 ? 503 : 204)); }));
            Call(account, "RefreshAuthenticatedIdentity");
            Set(account, "displayIdentityRetryAt", -1f);
            account.RefreshDisplayIdentity();
            Assert.That(writes, Is.EqualTo(2));
            Assert.That(account.NicknameStatus, Is.EqualTo("닉네임을 변경했어요"));
        }

        [TestCase("guestLoginInFlight")]
        [TestCase("backendProviderVerificationInFlight")]
        public void NicknameCannotBeChangedWhileAccountOrProfileIsUnresolved(string field)
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Set(account, field, true);
            Assert.That(account.CanChangeNickname, Is.False);
        }

        [Test] public void DifferentAccountNamesAndLocalGuestNeverOverwriteEachOther()
        {
            MukJumpIdentityProfile.SaveNickname("apple-a", "먹점프");
            MukJumpIdentityProfile.SaveNickname("apple-b", "구름");
            Assert.That(MukJumpIdentityProfile.ReadNickname("apple-a"), Is.EqualTo("먹점프"));
            Assert.That(MukJumpIdentityProfile.ReadNickname("apple-b"), Is.EqualTo("구름"));
            Assert.That(MukJumpIdentityProfile.IsGeneratedNickname(MukJumpIdentityProfile.GuestNickname), Is.True);
        }
        [TestCase(null, true)]
        [TestCase("guest(1234567890)", true)]
        [TestCase("guest12345", true)]
        [TestCase("먹방울", false)]
        public void ApplePromptsOnlyUntilACustomServerNicknameExists(string name, bool expected)
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb => cb(UserInfo(name))));
            Call(account, "RefreshAuthenticatedIdentity");
            Assert.That(account.NeedsNicknameSetup, Is.EqualTo(expected));
            Assert.That(account.IsNicknameBusy, Is.False);
        }
        [TestCase(null)]
        [TestCase("guest(1234567890)")]
        [TestCase("구름")]
        [TestCase("MyOriginalNickname")]
        public void GuestNicknameIsCreatedOnBackendWithoutTouchingTheCloudSave(string previous)
        {
            CreateAccount(MukJumpAccountKind.BackendGuest);
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb => cb(UserInfo(previous))));
            string written = null;
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((value, cb) => { written = value; cb(Result(204)); }));
            Call(account, "RefreshAuthenticatedIdentity");
            Assert.That(MukJumpIdentityProfile.IsGeneratedNickname(written), Is.True);
            Assert.That(written.Length, Is.EqualTo(10));
            Assert.That(account.Nickname, Is.EqualTo(written));
            Assert.That(account.NeedsNicknameSetup, Is.False);
        }
        [TestCase("guest12345")]
        public void ExistingShortGeneratedServerGuestNamesAreNotRenamed(string name)
        {
            CreateAccount(MukJumpAccountKind.BackendGuest);
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb => cb(UserInfo(name))));
            bool renamed = false;
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((_, cb) =>
            { renamed = true; cb(Result(204)); }));
            Call(account, "RefreshAuthenticatedIdentity");
            Assert.That(renamed, Is.False);
            Assert.That(account.Nickname, Is.EqualTo(name));
        }
        [Test] public void DuplicateNameDoesNotReplaceTheOldNameAndCanBeRetried()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb => cb(UserInfo("원래이름"))));
            Call(account, "RefreshAuthenticatedIdentity");
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((value, cb) => cb(Result(409))));
            bool success = true;
            account.ChangeNickname("중복이름", (ok, _) => success = ok);
            Assert.That(success, Is.False);
            Assert.That(account.Nickname, Is.EqualTo("원래이름"));
            Assert.That(account.NicknameStatus, Is.EqualTo("이미 사용 중인 닉네임이에요"));
            Assert.That(account.CanChangeNickname, Is.True);
        }
        [Test] public void LateNicknameCallbackCannotWriteIntoAnotherAccount()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Action<BackendReturnObject> pending = null;
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((value, cb) => pending = cb));
            bool completed = false;
            account.ChangeNickname("이전이름", (_, _) => completed = true);
            Call(account, "CancelIdentityRequest");
            Set(account, "currentAccountScopeForTests", new Func<string>(() => "apple-b"));
            pending(Result(204));
            Assert.That(completed, Is.False);
            Assert.That(MukJumpIdentityProfile.ReadNickname("apple-b"), Is.Empty);
        }
        [Test] public void TimedOutRequestReleasesInputAndIgnoresLateSuccess()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Action<BackendReturnObject> pending = null;
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((_, cb) => pending = cb));
            bool? result = null;
            account.ChangeNickname("먹방울", (ok, _) => result = ok);
            Set(account, "identityDeadline", -1f);
            Call(account, "PollIdentityRequest");
            Assert.That(result, Is.False);
            Assert.That(account.IsNicknameBusy, Is.False);
            pending(Result(204));
            Assert.That(MukJumpIdentityProfile.ReadNickname("apple-a"), Is.Empty);
        }
        [Test] public void CloudFormatFailureDoesNotBlockSeparateNicknameUpdate()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Set(account, "syncWriteBlocked", true);
            Set(account, "profileResolutionPending", true);
            Property(account, "Phase", MukJumpAccountPhase.Error);
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((_, cb) => cb(Result(204))));
            bool success = false;
            account.ChangeNickname("먹방울", (ok, _) => success = ok);
            Assert.That(success, Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.True, "닉네임 설정으로 기록 보호 잠금이 풀리면 안 됩니다.");
            Assert.That(account.NeedsNicknameSetup, Is.False);
        }
        [Test] public void SettingsShowNicknameBesideUidAndMatchingButtonAboveAccount()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            Transform page = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage");
            var nick = (RectTransform)page.Find("Nickname");
            var uid = (RectTransform)page.Find("UuidButton");
            Assert.That(nick.anchoredPosition.y, Is.EqualTo(uid.anchoredPosition.y));
            Assert.That(nick.anchoredPosition.x, Is.GreaterThan(uid.anchoredPosition.x));
            var button = (RectTransform)page.Find("NicknameButton");
            var link = (RectTransform)page.Find("AccountButton");
            Assert.That(button.sizeDelta, Is.EqualTo(new Vector2(348, 128)));
            Assert.That(button.anchoredPosition, Is.EqualTo(new Vector2(178, -203)));
            Assert.That(page.Find("LeaderboardMenuButton"), Is.Null);
            Assert.That(button.anchoredPosition.y + button.rect.yMin, Is.GreaterThan(link.anchoredPosition.y + link.rect.yMax));
            page.Find("NicknameButton").GetComponent<Button>().onClick.Invoke();
            var input = host.GetComponentInChildren<InputField>(true);
            Assert.That(input, Is.Not.Null);
            Assert.That(input.characterLimit, Is.EqualTo(10));
            Assert.That(input.textComponent.supportRichText, Is.False);
            input.text = "먹방울";
            GameLocalization.SetLanguage(GameLanguage.English);
            Canvas.ForceUpdateCanvases();
            Assert.That(input.text, Is.EqualTo("먹방울"), "언어 변경이 사용자의 입력을 번역하거나 덮지 않아야 합니다.");
        }

        [TestCase(MukJumpAccountKind.LocalGuest)]
        [TestCase(MukJumpAccountKind.BackendGuest)]
        [TestCase(MukJumpAccountKind.Apple)]
        public void EmptyAccountNicknameDisplaysStableGuestNameUntilServerNameArrives(MukJumpAccountKind kind)
        {
            CreateAccount(kind);
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            var label = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage/Nickname").GetComponent<Text>();
            Call(view, "RefreshSettingsUuid");
            string generated = label.text;
            Assert.That(generated, Does.Match(@"^guest\d{5}$"));
            Call(view, "RefreshSettingsUuid");
            Assert.That(label.text, Is.EqualTo(generated));
            if (kind != MukJumpAccountKind.LocalGuest)
            {
                Set(account, "identityLoaded", true);
                Set(account, "identityLoadedScope", "apple-a");
                Set(account, "identityNickname", "   ");
                Call(view, "RefreshSettingsUuid");
                Assert.That(label.text, Is.EqualTo(generated));
                Set(account, "identityNickname", "먹새싹");
                Call(view, "RefreshSettingsUuid");
                Assert.That(label.text, Is.EqualTo(kind == MukJumpAccountKind.Apple ? "먹새싹" : generated));
            }
        }

        [TestCase(MukJumpAccountKind.LocalGuest)]
        [TestCase(MukJumpAccountKind.BackendGuest)]
        public void GuestCannotOpenNicknameAndAppleLinkShowsButton(MukJumpAccountKind kind)
        {
            CreateAccount(kind);
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            var button = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage/NicknameButton").GetComponent<Button>();
            Assert.That(button.gameObject.activeSelf, Is.False);
            button.onClick.Invoke();
            Assert.That(host.transform.Find("NicknameCanvas"), Is.Null);
            bool completed = false;
            view.OpenFirstRunNickname(() => completed = true);
            Assert.That(completed, Is.True);
            Assert.That(host.transform.Find("NicknameCanvas"), Is.Null);
            Property(account, "AccountKind", MukJumpAccountKind.Apple);
            Call(view, "UpdateNicknameUi");
            Assert.That(button.gameObject.activeSelf, Is.True);
            button.onClick.Invoke();
            var popup = host.transform.Find("NicknameCanvas").GetComponent<CanvasGroup>();
            Assert.That(popup.blocksRaycasts, Is.True);
            Property(account, "AccountKind", kind);
            Call(view, "UpdateNicknameUi");
            Assert.That(button.gameObject.activeSelf, Is.False);
            Assert.That(popup.blocksRaycasts, Is.False);
        }

        [TestCase(false, 1179, 2556, 102, 177, 918)]
        [TestCase(true, 1179, 2556, 102, 177, 918)]
        [TestCase(false, 750, 1334, 0, 40, 520)]
        [TestCase(true, 750, 1334, 0, 40, 520)]
        [TestCase(false, 1080, 2400, 72, 96, 900)]
        [TestCase(true, 1080, 2400, 72, 96, 900)]
        public void KeyboardKeepsBothNicknamePopupsVisibleAndEditable(
            bool firstAppleLogin, int width, int height, int bottomInset, int topInset, int keyboardHeight)
        {
            CreateAccount(MukJumpAccountKind.Apple);
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            var safeArea = new Rect(0, bottomInset, width, height - bottomInset - topInset);
            view.SetDisplayMetricsForTests(width, height, safeArea);
            typeof(LobbyOptionsView).GetMethod("OpenNicknamePopup", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(view, new object[] { firstAppleLogin });
            var safe = (RectTransform)host.transform.Find("NicknameCanvas/SafeAreaRoot");
            var paper = (RectTransform)safe.Find("NicknameScroll");
            var input = paper.GetComponentInChildren<InputField>();
            // EditMode에서는 두루마리의 LateUpdate가 진행되지 않으므로 펼침 완료 상태를 재현한다.
            paper.GetComponent<CanvasGroup>().interactable = true;
            float originalScale = paper.localScale.x;
            // 실제 네이티브 키보드처럼 좌상단 기준 y + 높이 = 화면 높이인 사각형을 사용한다.
            ApplyKeyboard(view, new Rect(0, height - keyboardHeight, width, keyboardHeight), true);
            Assert.That(safe.anchorMin.y, Is.EqualTo((float)keyboardHeight / height).Within(.0001f));
            Assert.That(safe.anchorMax.y, Is.EqualTo(safeArea.yMax / height).Within(.0001f));
            Assert.That(paper.localScale.x, Is.GreaterThanOrEqualTo(originalScale * .9f),
                "키보드를 열어도 입력창을 작은 점으로 축소하면 안 됩니다.");
            float halfPaperPixels = paper.sizeDelta.y * paper.localScale.y * height / MobileUiLayout.ReferenceHeight * .5f;
            float centerPixels = (safe.anchorMin.y + safe.anchorMax.y) * height * .5f;
            Assert.That(centerPixels - halfPaperPixels, Is.GreaterThan(keyboardHeight));
            Assert.That(centerPixels + halfPaperPixels, Is.LessThan(safeArea.yMax));

            input.text = string.Empty;
            foreach (char character in "먹방울_Muk")
                input.ProcessEvent(new Event { type = EventType.KeyDown, character = character });
            input.ForceLabelUpdate();
            Assert.That(input.text, Is.EqualTo("먹방울_Muk"));
            Assert.That(input.textComponent.text, Is.EqualTo(input.text));
            Assert.That(input.IsInteractable(), Is.True);

            ApplyKeyboard(view, Rect.zero, false);
            Assert.That(safe.anchorMin.y, Is.EqualTo(safeArea.yMin / height).Within(.0001f));
            Assert.That(paper.localScale.x, Is.EqualTo(originalScale).Within(.0001f));
            Assert.That(input.text, Is.EqualTo("먹방울_Muk"), "키보드를 닫을 때 작성 중인 닉네임을 지우지 않는다.");
        }

        [TestCase(0, 0, 0, false)]
        [TestCase(0, 0, 0, true)]
        [TestCase(1240, 1080, 680, false)]
        [TestCase(1920, 1080, 0, true)]
        [TestCase(0, 1080, 1920, true)]
        [TestCase(2000, 1080, 680, true)]
        public void HiddenOrTransientKeyboardFrameDoesNotCollapseNicknamePopup(
            float top, float width, float height, bool visible)
        {
            CreateAccount(MukJumpAccountKind.Apple);
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            view.SetDisplayMetricsForTests(1080, 1920, new Rect(0, 60, 1080, 1750));
            typeof(LobbyOptionsView).GetMethod("OpenNicknamePopup", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(view, new object[] { false });
            var safe = (RectTransform)host.transform.Find("NicknameCanvas/SafeAreaRoot");
            var paper = (RectTransform)safe.Find("NicknameScroll");
            ApplyKeyboard(view, new Rect(0, top, width, height), visible);
            Assert.That(safe.anchorMin.y, Is.EqualTo(60f / 1920).Within(.0001f));
            Assert.That(paper.localScale, Is.EqualTo(Vector3.one));
        }

        static void ApplyKeyboard(LobbyOptionsView view, Rect area, bool visible) =>
            typeof(LobbyOptionsView).GetMethod("ApplyNicknameKeyboardLayout", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(view, new object[] { area, visible });

        [TestCase(GameLanguage.Korean, false)]
        [TestCase(GameLanguage.English, false)]
        [TestCase(GameLanguage.Korean, true)]
        [TestCase(GameLanguage.English, true)]
        public void RenderNicknamePopupInBothLanguages(GameLanguage language, bool keyboardVisible)
        {
            GameLocalization.SetLanguage(language);
            CreateAccount(MukJumpAccountKind.Apple);
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            view.SetDisplayMetricsForTests(1080, 1920, new Rect(0, 60, 1080, 1750));
            typeof(LobbyOptionsView).GetMethod("OpenNicknamePopup", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(view, new object[] { true });
            var canvas = host.transform.Find("NicknameCanvas").GetComponent<Canvas>();
            var paper = canvas.transform.Find("SafeAreaRoot/NicknameScroll");
            var input = paper.GetComponentInChildren<InputField>();
            input.text = "먹방울_Muk";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
            var cameraHost = new GameObject("NicknamePopupCamera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraHost, scene);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = null;
            Texture2D capture = null;
            try
            {
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)canvas.transform;
                rect.sizeDelta = new Vector2(1080, 1920);
                rect.position = Vector3.zero;
                rect.localScale = Vector3.one;
                var safe = (RectTransform)paper.parent;
                ApplyKeyboard(view, new Rect(0, 1240, 1080, 680), keyboardVisible);
                if (keyboardVisible)
                {
                    var keyboard = new GameObject("KeyboardAreaPreview", typeof(RectTransform), typeof(Image));
                    keyboard.transform.SetParent(canvas.transform, false);
                    var keyboardRect = (RectTransform)keyboard.transform;
                    keyboardRect.anchorMin = Vector2.zero;
                    keyboardRect.anchorMax = new Vector2(1, 680f / 1920);
                    keyboardRect.offsetMin = keyboardRect.offsetMax = Vector2.zero;
                    keyboard.GetComponent<Image>().color = new Color(.15f, .15f, .15f);
                    keyboard.GetComponent<Image>().raycastTarget = false;
                }
                paper.GetComponent<HanjiScrollFrame>().SetPose(1, 0, false);
                paper.GetComponent<CanvasGroup>().alpha = 1;
                paper.GetComponent<CanvasGroup>().interactable = true;
                paper.Find("HanjiScrollArt").GetComponent<CanvasGroup>().alpha = 1;
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                var camera = cameraHost.GetComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 960;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = InkPalette.Paper;
                target = new RenderTexture(540, 960, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                canvas.enabled = false;
                canvas.enabled = true;
                Canvas.ForceUpdateCanvases();
                Assert.That(input.text, Is.EqualTo("먹방울_Muk"));
                Assert.That(paper.Find("SaveButton/Paper/Label").GetComponent<Text>().text,
                    Is.EqualTo(language == GameLanguage.English ? "Save" : "저장"));
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(540, 960, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
                capture.Apply();
                const string folder = "output/quality-polish/nickname-popup";
                System.IO.Directory.CreateDirectory(folder);
                string suffix = keyboardVisible ? "-keyboard" : string.Empty;
                System.IO.File.WriteAllBytes($"{folder}/{language.ToString().ToLowerInvariant()}{suffix}.png", capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                cameraHost.GetComponent<Camera>().targetTexture = null;
                if (capture != null) Object.DestroyImmediate(capture);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(cameraHost);
                Object.DestroyImmediate(host);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void NicknameServerAdapterReservesAndCommitsWithoutPrimaryKeyQuery()
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Set(account, "settings", MukJumpBackendSettings.Load());
            // 정책 계층 전체를 가짜 성공으로 바꾸지 않고 실제 행 해석/쿼리 생성까지 통과한다.
            Set(account, "nicknamePolicyReadForTests", null);
            Set(account, "nicknamePolicyWriteForTests", null);
            Set(account, "getMyDataForTests", new Action<Action<BackendReturnObject>>(cb => cb(Result(200,
                "{\"rows\":[{\"inDate\":{\"S\":\"owned-row\"},\"owner_inDate\":{\"S\":\"apple-a\"}," +
                "\"updatedAt\":{\"S\":\"2026-09-11T00:00:00.000Z\"}}]}"))));
            int writes = 0, updates = 0;
            Set(account, "nicknamePolicyUpdateForTests", new Action<Where, Param, Action<BackendReturnObject>>((where, param, cb) =>
            {
                string query = where.GetJson();
                Assert.That(query, Does.Not.Contain("inDate"));
                Assert.That(query, Does.Contain(writes == 0 ? "updatedAt" : MukJumpNicknameChange.PendingNameColumn));
                writes++; cb(Result(204));
            }));
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((name, cb) =>
            { updates++; cb(Result(204)); }));
            bool? saved = null;
            account.ChangeNickname("먹방울", (ok, _) => saved = ok);
            Assert.That(saved, Is.True);
            Assert.That(writes, Is.EqualTo(2));
            Assert.That(updates, Is.EqualTo(1));
            Assert.That(account.Nickname, Is.EqualTo("먹방울"));
        }

        [TestCase("another-owner", 1)]
        [TestCase("apple-a", 2)]
        public void NicknameServerAdapterRejectsWrongOwnerOrMultipleRows(string owner, int count)
        {
            CreateAccount(MukJumpAccountKind.Apple);
            Set(account, "settings", MukJumpBackendSettings.Load());
            Set(account, "nicknamePolicyReadForTests", null);
            Set(account, "nicknamePolicyWriteForTests", null);
            string row = "{\"inDate\":{\"S\":\"owned-row\"},\"owner_inDate\":{\"S\":\"" + owner +
                "\"},\"updatedAt\":{\"S\":\"2026-09-11T00:00:00.000Z\"}}";
            Set(account, "getMyDataForTests", new Action<Action<BackendReturnObject>>(cb =>
                cb(Result(200, "{\"rows\":[" + row + (count == 2 ? "," + row : "") + "]}"))));
            int writes = 0, updates = 0;
            Set(account, "nicknamePolicyUpdateForTests", new Action<Where, Param, Action<BackendReturnObject>>((_, __, cb) =>
            { writes++; cb(Result(204)); }));
            Set(account, "nicknameUpdateForTests", new Action<string, Action<BackendReturnObject>>((_, cb) =>
            { updates++; cb(Result(204)); }));
            bool? saved = null;
            account.ChangeNickname("먹방울", (ok, _) => saved = ok);
            Assert.That(saved, Is.False); Assert.That(writes, Is.Zero); Assert.That(updates, Is.Zero);
        }

        void CreateAccount(MukJumpAccountKind kind)
        {
            host = new GameObject("NicknameAccountTests");
            account = host.AddComponent<MukJumpAccountRuntime>();
            Call(account, "OnEnable");
            Property(account, "AccountKind", kind);
            Property(account, "IsOnlineAuthenticated", true);
            Property(account, "Phase", MukJumpAccountPhase.OnlineReady);
            Set(account, "currentAccountScopeForTests", new Func<string>(() => "apple-a"));
            Set(account, "identityInfoForTests", new Action<Action<BackendReturnObject>>(cb =>
                cb(UserInfo(kind == MukJumpAccountKind.BackendGuest ? "guest12345" : ""))));
            Set(account, "nicknameTimeForTests", new Action<Action<BackendReturnObject>>(cb =>
                cb(Result(200, "{\"utcTime\":\"2026-09-11T00:00:00Z\"}"))));
            var nicknamePolicy = new MukJumpNicknameChange.State { Row = "nickname-row" };
            Set(account, "nicknamePolicyReadForTests", new Action<Action<MukJumpNicknameChange.State>>(cb => cb(nicknamePolicy)));
            Set(account, "nicknamePolicyWriteForTests", new Action<MukJumpNicknameChange.State, Action<bool>>((state, cb) =>
            { nicknamePolicy = state; cb(true); }));
        }

        [TestCase(GameLanguage.Korean)] [TestCase(GameLanguage.English)] [TestCase(GameLanguage.Japanese)]
        public void NicknameCooldownHintAndRejectionFitWithoutOverlappingInputOrButtons(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            CreateAccount(MukJumpAccountKind.Apple);
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage/NicknameButton")
                .GetComponent<Button>().onClick.Invoke();
            var paper = host.transform.Find("NicknameCanvas/SafeAreaRoot/NicknameScroll");
            var hint = paper.Find("ChangeIntervalHint").GetComponent<Text>();
            var error = paper.Find("Error").GetComponent<Text>();
            InkLocalizedText.SetSource(error, MukJumpNicknameChange.WaitMessage);
            Canvas.ForceUpdateCanvases();
            foreach (var text in new[] { hint, error })
            {
                Assert.That(text.text, Is.EqualTo(GameLocalization.Translate(text == hint ? MukJumpNicknameChange.Hint : MukJumpNicknameChange.WaitMessage)));
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1));
                foreach (char c in text.text) if (!char.IsWhiteSpace(c)) Assert.That(text.font.HasCharacter(c), Is.True, c.ToString());
            }
            var input = (RectTransform)paper.Find("NicknameInput");
            var button = (RectTransform)paper.Find("SaveButton");
            Assert.That(hint.fontSize, Is.LessThan(error.fontSize));
            Assert.That(error.rectTransform.anchoredPosition.y + error.rectTransform.rect.yMax,
                Is.LessThan(input.anchoredPosition.y + input.rect.yMin));
            Assert.That(hint.rectTransform.anchoredPosition.y + hint.rectTransform.rect.yMax,
                Is.LessThan(error.rectTransform.anchoredPosition.y + error.rectTransform.rect.yMin));
            Assert.That(button.anchoredPosition.y + button.rect.yMax,
                Is.LessThan(hint.rectTransform.anchoredPosition.y + hint.rectTransform.rect.yMin));
        }
        static BackendReturnObject UserInfo(string name) => Result(200,
            "{\"row\":{\"nickname\":" + (name == null ? "null" : "\"" + name + "\"") + "}}");
        static BackendReturnObject Result(int status, string json = "{}")
        {
            var result = new BackendReturnObject();
            typeof(BackendReturnObject).GetProperty("StatusCode").SetValue(result, status);
            typeof(BackendReturnObject).GetProperty("ReturnValue").SetValue(result, json);
            return result;
        }
        static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    }
}
