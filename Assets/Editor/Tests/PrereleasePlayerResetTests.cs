using System;
using System.Collections.Generic;
using System.IO;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor.Build;

namespace MukJump.EditorTests
{
    public sealed class PrereleasePlayerResetTests
    {
        sealed class Store : PrereleasePlayerReset.IStore
        {
            public string stamp = "v1:23";
            public int clears;
            public bool failClear, failSave, ignoreSave;
            public readonly Dictionary<string, string> data = new();
            public string ReadStamp() => stamp;
            public void ClearLocalData()
            {
                clears++;
                if (failClear) throw new IOException("test-clear");
                data.Clear();
            }
            public void SaveStamp(string value)
            {
                if (failSave) throw new IOException("test-save");
                if (!ignoreSave) stamp = value;
            }
        }

        [TestCase("offline-guest")]
        [TestCase("backend-guest")]
        [TestCase("apple")]
        public void UpgradeClearsEveryAccountSnapshotAndQueuedWrite(string kind)
        {
            var store = new Store();
            foreach (string key in new[] { "kind", "uid", "nickname", "best", "growth", "currency",
                "tutorial", "guestBackup", "appleBackup", "pendingSave", "pendingLeaderboard", "backend.dat" })
                store.data[key] = kind + "-old";
            Assert.That(PrereleasePlayerReset.TryReset("24", store, out var failure), Is.True);
            Assert.That(failure, Is.Null);
            Assert.That(store.data, Is.Empty);
            Assert.That(store.stamp, Is.EqualTo("v1:24"));
            Assert.That(store.clears, Is.EqualTo(1));
        }

        [Test]
        public void SameBuildRestartKeepsFreshGuestAndProgress()
        {
            var store = new Store();
            Assert.That(PrereleasePlayerReset.TryReset("24", store, out _), Is.True);
            store.data["best"] = "237";
            store.data["tutorial"] = "complete";
            store.data["uid"] = "new-user";
            Assert.That(PrereleasePlayerReset.TryReset("24", store, out _), Is.True);
            Assert.That(store.data["best"], Is.EqualTo("237"));
            Assert.That(store.data.Count, Is.EqualTo(3));
            Assert.That(store.clears, Is.EqualTo(1));
        }

        [Test]
        public void NextBuildResetsAgain()
        {
            var store = new Store();
            PrereleasePlayerReset.TryReset("24", store, out _);
            store.data["best"] = "43";
            Assert.That(PrereleasePlayerReset.TryReset("25", store, out _), Is.True);
            Assert.That(store.data, Is.Empty);
            Assert.That(store.clears, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void InterruptedCleanupCannotAuthorizeAndCanRetry(int stage)
        {
            var store = new Store { failClear = stage == 0, failSave = stage == 1, ignoreSave = stage == 2 };
            Assert.That(PrereleasePlayerReset.TryReset("24", store, out var failure), Is.False);
            Assert.That(failure, Is.Not.Null);
            Assert.That(store.stamp, Is.EqualTo("v1:23"));
            store.failClear = store.failSave = store.ignoreSave = false;
            Assert.That(PrereleasePlayerReset.TryReset("24", store, out _), Is.True);
        }

        [TestCase("")]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("invalid")]
        public void MissingNativeQaMarkerCannotDeleteAnything(string build)
        {
            var store = new Store();
            Assert.That(PrereleasePlayerReset.TryReset(build, store, out _), Is.False);
            Assert.That(store.clears, Is.Zero);
        }

        [Test]
        public void FreshInstallRunsOnceWithoutOldStamp()
        {
            var store = new Store { stamp = string.Empty };
            Assert.That(PrereleasePlayerReset.TryReset("24", store, out _), Is.True);
            Assert.That(store.clears, Is.EqualTo(1));
        }

        [TestCase(MukJumpNativeBuildIntent.IosAppStore)]
        [TestCase(MukJumpNativeBuildIntent.AndroidGooglePlay)]
        [TestCase(MukJumpNativeBuildIntent.IosLocalValidation)]
        [TestCase(MukJumpNativeBuildIntent.None)]
        public void NonQaBuildsRejectResetDefine(MukJumpNativeBuildIntent intent)
        {
            Assert.DoesNotThrow(() => MukJumpNativeReleaseBuildGuard.ValidatePrereleaseResetScope(
                intent, Array.Empty<string>(), Array.Empty<string>()));
            Assert.Throws<BuildFailedException>(() => MukJumpNativeReleaseBuildGuard.ValidatePrereleaseResetScope(
                intent, Array.Empty<string>(), new[] { PrereleasePlayerReset.BuildDefine }));
        }

        [Test]
        public void QaRequiresOneShotDefineAndRejectsGlobalDefine()
        {
            var qa = MukJumpNativeBuildIntent.IosTestFlightQa;
            var reset = new[] { PrereleasePlayerReset.BuildDefine };
            Assert.Throws<BuildFailedException>(() => MukJumpNativeReleaseBuildGuard.ValidatePrereleaseResetScope(
                qa, Array.Empty<string>(), Array.Empty<string>()));
            Assert.Throws<BuildFailedException>(() => MukJumpNativeReleaseBuildGuard.ValidatePrereleaseResetScope(
                qa, reset, reset));
            Assert.DoesNotThrow(() => MukJumpNativeReleaseBuildGuard.ValidatePrereleaseResetScope(
                qa, Array.Empty<string>(), reset));
        }

        [Test]
        public void EditorCannotWipeRealPreferences()
        {
            Assert.That(PrereleasePlayerReset.EnsureReady(), Is.True);
            string source = File.ReadAllText("Assets/Scripts/Core/PrereleasePlayerReset.cs");
            Assert.That(source, Does.Contain("#if UNITY_IOS && !UNITY_EDITOR && MUKJUMP_PRERELEASE_RESET"));
            Assert.That(source, Does.Contain("RuntimeInitializeLoadType.SubsystemRegistration"));
            Assert.That(source, Does.Contain("Path.Combine(Application.persistentDataPath, \"backend.dat\")"));
            Assert.That(source, Does.Contain("file.Flush(true)"));
            string splash = File.ReadAllText("Assets/Scripts/Core/StartupBrandSplash.cs");
            string account = File.ReadAllText("Assets/Scripts/Core/MukJumpAccountRuntime.cs");
            Assert.That(splash, Does.Contain("if (!PrereleasePlayerReset.EnsureReady()) return;"));
            Assert.That(account, Does.Contain("if (!PrereleasePlayerReset.EnsureReady()) return;"));
        }
    }
}
