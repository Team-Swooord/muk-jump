using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MukJump.Core;

namespace MukJump.EditorTests
{
    public sealed class SubmissionSafetyTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void DebugHeightCannotOverwriteSavedBest()
        {
            root = new GameObject("SubmissionSafetyTests");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            int savedBest = score.Best;

            score.ResetOrigin(0f);
            score.DebugSetHeight(savedBest + 1000, null);
            score.SaveBest();

            Assert.That(score.RecordsAllowed, Is.False);
            Assert.That(score.Best, Is.EqualTo(savedBest));
            Assert.That(score.IsNewBestThisRun, Is.False);
        }

        [Test]
        public void EnablingDebugInvincibilityTaintsCurrentRun()
        {
            root = new GameObject("SubmissionSafetyTests");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            score.ResetOrigin(0f);
            var manager = root.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");

            manager.ToggleDebugInvincible();

            Assert.That(GameManager.DebugToolsAvailable, Is.True,
                "EditMode 검증에서는 개발 도구가 활성화되어야 합니다.");
            Assert.That(manager.DebugInvincible, Is.True);
            Assert.That(score.RecordsAllowed, Is.False);
        }

        [Test]
        public void MainSceneContainsNoMissingScriptReferences()
        {
            const string scenePath = "Assets/Scenes/Main.unity";
            string source = File.ReadAllText(scenePath);

            Assert.That(Regex.IsMatch(
                    source, @"m_Script:\s*\{\s*fileID:\s*0(?:\s*,|\s*\})"),
                Is.False,
                "Main 씬에 Missing Script(fileID 0)가 남아 있습니다.");

            MatchCollection references = Regex.Matches(
                source,
                @"m_Script:\s*\{\s*fileID:\s*\d+\s*,\s*guid:\s*([0-9a-f]{32})");
            Assert.That(references.Count, Is.GreaterThan(0));
            foreach (Match reference in references)
            {
                string guid = reference.Groups[1].Value;
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Not.Empty,
                    $"Main 씬의 스크립트 GUID를 찾을 수 없습니다: {guid}");
            }
        }

        static object Invoke(object target, string methodName)
        {
            return target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }
    }
}
