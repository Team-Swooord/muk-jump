using MukJump.EditorTools;
using NUnit.Framework;
using Unity.Pipeline;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class AgentToolingTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("{")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("{\"enableInBuilds\":false}")]
        [TestCase("{\"enableInBuilds\":true,\"autoStart\":false}")]
        [TestCase("{\"enableInBuilds\":false,\"autoStart\":true}")]
        [TestCase("{\"enableInBuilds\":\"false\",\"autoStart\":false}")]
        [TestCase("{\"enableInBuilds\":false,\"autoStart\":0}")]
        [TestCase("{\"enableInBuilds\":true,\"enableInBuilds\":false,\"autoStart\":false}")]
        public void UnsafeOrAmbiguousSettingsFailClosed(string json) =>
            Assert.That(MukJumpAgentSafety.InspectSettings(json), Is.Not.Empty);

        [Test]
        public void ExplicitDisabledSettingsPass() =>
            Assert.That(MukJumpAgentSafety.InspectSettings(
                "{\"enableInBuilds\":false,\"autoStart\":false}"), Is.Empty);

        [TestCase("Assets/Settings/Pipeline/Resources/RuntimePipelineConfig.asset", true)]
        [TestCase("Assets/Other/resources/runtimepipelinebuildinfo.asset", true)]
        [TestCase("Assets\\Other\\Resources\\RuntimePipelineConfig.asset", true)]
        [TestCase("Assets/Settings/RuntimePipelineConfig.asset", false)]
        [TestCase("Assets/Resources/PipelineNotices.asset", false)]
        public void OnlyPlayerResourcesAreFlagged(string path, bool expected) =>
            Assert.That(MukJumpAgentSafety.IsRuntimePipelineResource(path), Is.EqualTo(expected));

        [Test]
        public void InstalledToolingPassesBuildGate() => MukJumpAgentSafety.Validate();

        [Test]
        public void DisabledSettingsNeverCreatePlayerDriver()
        {
            Assert.That(RuntimePipelineBootstrap.Instance, Is.Null);
            Assert.That(RuntimePipelineBootstrap.Bootstrap(), Is.Null);
            Assert.That(Object.FindObjectsByType<RuntimePipelineDriver>(FindObjectsInactive.Include), Is.Empty);
        }

        [Test]
        public void AuditDoesNotDirtyOrChangeLoadedScene()
        {
            var before = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool wasDirty = scene.isDirty;
            Assert.That(MukJumpAgentAudit.Audit(), Is.Not.Null);
            CollectionAssert.AreEqual(before,
                UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup());
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }

        [Test]
        public void CapturePathIsUniqueAndOutsideImportedAssets()
        {
            string first = MukJumpAgentAudit.CreateCapturePath();
            Assert.That(first, Does.StartWith(System.IO.Path.GetFullPath("output/qa/pipeline-captures") +
                                             System.IO.Path.DirectorySeparatorChar));
            Assert.That(first, Does.EndWith(".png"));
            Assert.That(first, Is.Not.EqualTo(MukJumpAgentAudit.CreateCapturePath()));
        }

        [Test]
        public void CaptureRefusesEditModeInsteadOfTakingCameraOnlyPicture()
        {
            Assert.That(UnityEditor.EditorApplication.isPlaying, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => MukJumpAgentAudit.CaptureUi());
        }
    }
}
