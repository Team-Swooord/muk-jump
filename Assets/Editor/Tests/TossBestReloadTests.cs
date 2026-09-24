using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class TossBestReloadTests
    {
        GameObject host;
        MemoryScoreStore store;
        ScoreManager score;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryScoreStore { Best = 137, Seconds = -1 };
            ScoreManager.UseStoreForTests(store);
            host = new GameObject("TossBestReload");
            score = host.AddComponent<ScoreManager>();
            typeof(ScoreManager).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(score, null);
            Assert.That(ScoreManager.TryReloadAfterAppsInTossIdentity(), Is.True);
            Assert.That(score.Best, Is.EqualTo(137));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            ScoreManager.RestoreDefaultStoreForTests();
        }

        [TestCase(0)]
        [TestCase(58)]
        public void SameOwnerLateLowerReadCannotReplaceConfirmedBest(int lower)
        {
            store.Best = lower;
            store.Seconds = 12;
            Assert.That(ScoreManager.TryReloadAfterAppsInTossIdentity(), Is.True);
            Assert.That(score.Best, Is.EqualTo(137));
            Assert.That(store.Best, Is.EqualTo(137));
            Assert.That(score.BestTimeSeconds, Is.EqualTo(-1), "다른 고도의 시간을 가져오지 않는다.");
            Assert.That(score.Height, Is.Zero, "현재 판·성장 보상을 만들어내지 않는다.");
        }

        [Test]
        public void FailedReadKeepsConfirmedBestWithoutWritingEmptyValue()
        {
            store.ThrowOnLoad = true;
            LogAssert.Expect(LogType.Warning, "토스 사용자 확인 뒤 최고 기록을 다시 읽지 못했습니다: Injected score read failure");
            Assert.That(ScoreManager.TryReloadAfterAppsInTossIdentity(), Is.False);
            Assert.That(score.Best, Is.EqualTo(137));
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(score.HasConfirmedBest, Is.False);
        }

        [Test]
        public void IdentityReloadPreservesUnconfirmedCandidateForSaveRetry()
        {
            store.ThrowOnSave = true;
            LogAssert.Expect(LogType.Warning, "최고 기록 저장에 실패했지만 결과 화면은 계속 표시합니다: Injected score write failure");
            Assert.That(score.TryCommitBestCandidate(180), Is.False);
            store.ThrowOnSave = false;
            Assert.That(ScoreManager.TryReloadAfterAppsInTossIdentity(), Is.True);
            Assert.That(score.HasPendingBestSaveRetry, Is.True);
            Assert.That(score.TryCommitBestCandidate(0), Is.True);
            Assert.That(score.Best, Is.EqualTo(180));
            Assert.That(score.HasPendingBestSaveRetry, Is.False);
        }

        [Test]
        public void ExplicitAccountSwitchStillReplacesInsteadOfMergingAnotherUsersBest()
        {
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(22), Is.True);
            Assert.That(ScoreManager.TryReloadAfterAppsInTossIdentity(), Is.True);
            Assert.That(score.Best, Is.EqualTo(22));
            Assert.That(store.Best, Is.EqualTo(22));
        }
    }
}
