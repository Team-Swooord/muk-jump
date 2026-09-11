using System;
using BackEnd;
using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class NicknameCooldownTests
    {
        MukJumpNicknameChange.State state;
        string name, message;
        DateTimeOffset now;
        bool live, success, writeFails;
        int updates;
        int response;
        Action<BackendReturnObject> delayed;

        [SetUp] public void SetUp()
        {
            state = new MukJumpNicknameChange.State { Row = "owned-row" };
            name = "원래이름"; message = "";
            now = DateTimeOffset.Parse("2026-09-11T00:00:00Z");
            live = true; success = writeFails = false; updates = 0; response = 204; delayed = null;
        }
        void Run(string requested = "새이름", bool delay = false)
        {
            MukJumpNicknameChange.Run(requested, () => live,
                cb => cb(Result(200, "{\"utcTime\":\"" + now.ToString("O") + "\"}")),
                cb => cb(Result(200, "{\"row\":{\"nickname\":\"" + name + "\"}}")),
                cb => cb(state),
                (value, cb) => { if (!writeFails) state = value; cb(!writeFails); },
                (value, cb) => { updates++; if (delay) delayed = cb;
                    else { if (response == 204) name = value; cb(Result(response)); } },
                (ok, text) => { success = ok; message = text; });
        }
        [TestCase(0, false)] [TestCase(13, false)] [TestCase(14, true)] [TestCase(15, true)]
        public void FourteenDayServerBoundaryIsEnforced(int days, bool allowed)
        {
            state.ChangedAt = now.AddDays(-days).ToString("O"); Run();
            Assert.That(success, Is.EqualTo(allowed)); Assert.That(updates, Is.EqualTo(allowed ? 1 : 0));
        }
        [Test] public void OneSecondBeforeBoundaryIsBlocked()
        { state.ChangedAt = now.AddDays(-14).AddSeconds(1).ToString("O"); Run(); Assert.That(updates, Is.Zero); }
        [TestCase("broken")] [TestCase("2030-01-01T00:00:00Z")]
        public void InvalidOrFutureHistoryFailsClosed(string value)
        { state.ChangedAt = value; Run(); Assert.That(updates, Is.Zero); }
        [Test] public void FirstCustomNameStartsCooldownAndRepeatedSameNameDoesNotExtendIt()
        {
            name = "guest12345"; Run(); var changed = state.ChangedAt;
            Assert.That(success, Is.True); Assert.That(state.PendingName, Is.Empty);
            now = now.AddDays(1); Run(); Assert.That(state.ChangedAt, Is.EqualTo(changed));
            Run("다른이름"); Assert.That(success, Is.False); Assert.That(updates, Is.EqualTo(1));
        }
        [Test] public void AnotherSessionUsesServerHistoryNotEmptyLocalCache()
        { Run(); name = "새이름"; success = false; Run("재로그인"); Assert.That(success, Is.False); }
        [Test] public void DuplicateNameRollsBackReservationWithoutUsingCooldown()
        { response = 409; Run(); Assert.That(success, Is.False); Assert.That(state.ChangedAt, Is.Empty);
          response = 204; Run("다른이름"); Assert.That(success, Is.True); }
        [Test] public void ReservationFailureNeverChangesNickname()
        { writeFails = true; Run(); Assert.That(updates, Is.Zero); }
        [Test] public void LostResponseKeepsReservationAndCanRetryOnlySamePendingName()
        {
            response = 500; Run(); Assert.That(state.PendingName, Is.EqualTo("새이름"));
            Run("다른이름"); Assert.That(updates, Is.EqualTo(1));
            response = 204; Run(); Assert.That(success, Is.True); Assert.That(updates, Is.EqualTo(2));
        }
        [Test] public void AccountChangeIgnoresLateNicknameResponse()
        { Run(delay: true); live = false; delayed(Result(204)); Assert.That(success, Is.False); Assert.That(message, Is.Empty); }
        [Test] public void ReservationQueryExcludesPrimaryKeyButKeepsServerVersionGuard()
        {
            state.ExpectedUpdatedAt = "2026-09-11T00:00:00.000Z";
            string query = MukJumpAccountRuntime.BuildNicknameUpdateCondition(state).GetJson();
            Assert.That(query, Does.Not.Contain("inDate"));
            Assert.That(query, Does.Contain("updatedAt"));
            Assert.That(query, Does.Contain(state.ExpectedUpdatedAt));
        }
        [Test] public void CompletionAndRollbackQueriesKeepReservationOwnershipGuard()
        {
            state.ExpectedChangedAt = now.ToString("O"); state.ExpectedPendingName = "새이름";
            string query = MukJumpAccountRuntime.BuildNicknameUpdateCondition(state).GetJson();
            Assert.That(query, Does.Not.Contain("inDate"));
            Assert.That(query, Does.Contain(MukJumpNicknameChange.ChangedAtColumn));
            Assert.That(query, Does.Contain(MukJumpNicknameChange.PendingNameColumn));
            Assert.That(query, Does.Contain(state.ExpectedChangedAt));
        }
        [TestCase(1)] [TestCase(13)] [TestCase(20)]
        public void FailedReservationStartsFreshCooldownWhenActuallyChangedOnRetry(int days)
        {
            response = 500; Run();
            now = now.AddDays(days); response = 204; Run();
            Assert.That(success, Is.True);
            Assert.That(DateTimeOffset.Parse(state.ChangedAt), Is.EqualTo(now));
            Run("다른이름");
            Assert.That(success, Is.False);
            Assert.That(updates, Is.EqualTo(2));
        }
        [TestCase(GameLanguage.Korean)] [TestCase(GameLanguage.English)] [TestCase(GameLanguage.Japanese)]
        public void CooldownCopyHasLocalizedText(GameLanguage language)
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            GameLocalization.SetLanguage(language);
            try
            {
                foreach (string text in new[] { MukJumpNicknameChange.Hint, MukJumpNicknameChange.WaitMessage })
                {
                    string translated = GameLocalization.Translate(text);
                    Assert.That(translated, Is.Not.Empty);
                    if (language != GameLanguage.Korean) Assert.That(translated, Is.Not.EqualTo(text));
                }
            }
            finally { LobbySettingsProfile.RestoreDefaultStoreForTests(); }
        }
        static BackendReturnObject Result(int status, string json = "{}")
        {
            var result = new BackendReturnObject();
            typeof(BackendReturnObject).GetProperty("StatusCode").SetValue(result, status);
            typeof(BackendReturnObject).GetProperty("ReturnValue").SetValue(result, json); return result;
        }
    }
}
