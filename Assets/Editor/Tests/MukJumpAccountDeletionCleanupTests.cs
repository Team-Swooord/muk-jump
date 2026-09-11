using System;
using System.Collections.Generic;
using BackEnd;
using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class MukJumpAccountDeletionCleanupTests
    {
        const string OwnedRows = "{\"rows\":[{\"inDate\":{\"S\":\"row-1\"}," +
            "\"owner_inDate\":{\"S\":\"owner-a\"},\"bestHeight\":{\"N\":\"156\"}}]}";
        readonly List<string> calls = new();
        readonly Dictionary<string, Action<BackendReturnObject>> replies = new();
        bool current;
        int completed;
        BackendReturnObject outcome;

        [SetUp]
        public void SetUp()
        {
            calls.Clear(); replies.Clear(); current = true; completed = 0; outcome = null;
        }

        void Start(string owner = "owner-a") => MukJumpAccountDeletionCleanup.Run(
            owner, _ => current,
            cb => Capture("read", cb),
            (row, cb) => Capture("unpublish:" + row, cb),
            (row, cb) => Capture("delete:" + row, cb),
            cb => Capture("withdraw", cb),
            result => { completed++; outcome = result; });

        void Capture(string step, Action<BackendReturnObject> callback)
        { calls.Add(step); replies[step] = callback; }

        [Test]
        public void Owned156mIsUnpublishedAndDeletedBeforeWithdrawal()
        {
            Start();
            replies["read"](Result(200, OwnedRows));
            Assert.That(calls, Is.EqualTo(new[] { "read", "unpublish:row-1" }));
            replies["unpublish:row-1"](Result(204));
            Assert.That(calls, Does.Not.Contain("withdraw"));
            replies["delete:row-1"](Result(204));
            replies["withdraw"](Result(204));
            Assert.That(calls, Is.EqualTo(new[] { "read", "unpublish:row-1", "delete:row-1", "withdraw" }));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(outcome.IsSuccess(), Is.True);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void FailureStopsBeforeAnyFollowingMutation(int failAt)
        {
            Start();
            string[] stages = { "read", "unpublish:row-1", "delete:row-1", "withdraw" };
            for (int i = 0; i <= failAt; i++)
                replies[stages[i]](i == failAt ? Result(500) : Result(200, i == 0 ? OwnedRows : "{}"));
            Assert.That(calls.Count, Is.EqualTo(failAt + 1));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(outcome.IsSuccess(), Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void OwnerChangeOrTimeoutIgnoresLateResponses(int staleAt)
        {
            Start();
            string[] stages = { "read", "unpublish:row-1", "delete:row-1", "withdraw" };
            for (int i = 0; i < staleAt; i++)
                replies[stages[i]](Result(200, i == 0 ? OwnedRows : "{}"));
            current = false;
            replies[stages[staleAt]](Result(200, staleAt == 0 ? OwnedRows : "{}"));
            Assert.That(calls.Count, Is.EqualTo(staleAt + 1));
            Assert.That(completed, Is.Zero);
        }

        [Test]
        public void DuplicateRepliesDoNotDeleteOrWithdrawTwice()
        {
            Start();
            foreach (string step in new[] { "read", "unpublish:row-1", "delete:row-1", "withdraw" })
            {
                var reply = Result(200, step == "read" ? OwnedRows : "{}");
                replies[step](reply); replies[step](reply);
            }
            Assert.That(calls.Count, Is.EqualTo(4));
            Assert.That(completed, Is.EqualTo(1));
        }

        [TestCase("{\"rows\":[{\"inDate\":\"row-1\",\"owner_inDate\":\"owner-b\"}]}")]
        [TestCase("{\"rows\":[{\"inDate\":\"row-1\"}]}")]
        [TestCase("{\"rows\":[{\"inDate\":\"\",\"owner_inDate\":\"owner-a\"}]}")]
        [TestCase("{}")]
        public void InvalidOwnershipOrMalformedRowsNeverMutate(string json)
        {
            Start(); replies["read"](Result(200, json));
            Assert.That(calls, Is.EqualTo(new[] { "read" }));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(outcome, Is.Null);
        }

        [TestCase("")] [TestCase(null)]
        public void EmptyOwnerNeverContactsBackend(string owner)
        {
            Start(owner);
            Assert.That(calls, Is.Empty);
            Assert.That(outcome, Is.Null);
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void RetryAfterLostDeleteResponseDoesNotRecreate156mRow()
        {
            Start(); replies["read"](Result(200, OwnedRows));
            replies["unpublish:row-1"](Result(204));
            // 서버 행 삭제는 끝났지만 응답만 유실된 상황.
            replies["delete:row-1"](null);
            Assert.That(calls, Does.Not.Contain("withdraw"));
            calls.Clear(); replies.Clear(); completed = 0;
            Start(); replies["read"](Result(200, "{\"rows\":[]}"));
            replies["withdraw"](Result(204));
            Assert.That(calls, Is.EqualTo(new[] { "read", "withdraw" }));
            Assert.That(outcome.IsSuccess(), Is.True);
        }

        [Test]
        public void RetryAfterUnpublishFailureUnpublishesAgainBeforeDelete()
        {
            Start(); replies["read"](Result(200, OwnedRows));
            replies["unpublish:row-1"](null);
            calls.Clear(); replies.Clear(); completed = 0;
            Start(); replies["read"](Result(200, OwnedRows.Replace("156", "0")));
            Assert.That(calls, Is.EqualTo(new[] { "read", "unpublish:row-1" }));
        }

        [Test]
        public void AllOwnersAreCheckedBeforeDeletingAnyRow()
        {
            string mixed = "{\"rows\":[{\"inDate\":\"one\",\"owner_inDate\":\"owner-a\"}," +
                "{\"inDate\":\"two\",\"owner_inDate\":\"owner-b\"}]}";
            Start(); replies["read"](Result(200, mixed));
            Assert.That(calls, Is.EqualTo(new[] { "read" }));
            Assert.That(outcome, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SdkClearedIdentityCanFinishWithdrawalButAnotherAccountCannot(bool switched)
        {
            string sdkOwner = "owner-a";
            int mutations = 0;
            MukJumpAccountDeletionCleanup.Run("owner-a",
                afterWithdrawal => sdkOwner == "owner-a" || afterWithdrawal && sdkOwner == "",
                cb => cb(Result(200, "{\"rows\":[]}")),
                (_, cb) => { mutations++; cb(Result(204)); },
                (_, cb) => { mutations++; cb(Result(204)); },
                cb => { sdkOwner = switched ? "owner-b" : ""; cb(Result(204)); },
                _ => completed++);
            Assert.That(mutations, Is.Zero);
            Assert.That(completed, Is.EqualTo(switched ? 0 : 1));
        }

        static BackendReturnObject Result(int status, string json = "{}")
        {
            var result = new BackendReturnObject();
            typeof(BackendReturnObject).GetProperty("StatusCode").SetValue(result, status);
            typeof(BackendReturnObject).GetProperty("ReturnValue").SetValue(result, json);
            return result;
        }
    }
}
