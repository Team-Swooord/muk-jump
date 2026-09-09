using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MukJump.EditorTools
{
    /// 이번 VFX와 연결된 회귀만 실행한다. 열린 프로젝트에 별도 batchmode를 띄우지 않는다.
    public static class VfxRoadmapValidation
    {
        static TestRunnerApi runner;
        static Results callback;

        [MenuItem("MukJump/검증/VFX 계획 관련 회귀 실행")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || runner != null) return;
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            callback = new Results();
            runner.RegisterCallbacks(callback);
            runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                groupNames = new[] {
                    @"^(MukJump\.EditorTests\.)?(InkVfxMotionTests|VfxRoadmapTests|InkContactParticleTests|MobileFeedbackPolishTests|InkDropJumpVfxPoolTests|ItemEffectCloneTests|VfxQualityRuntimeTests|PlayerHealthTests|FeedbackResourceTests|DrawingResourceTests|StrokeCaptureTests|FallingInkRockTests|PauseMenuViewTests)\."
                }
            }));
        }

        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                const string directory = "output/quality-polish/roadmap-validation";
                Directory.CreateDirectory(directory);
                TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(directory + "/final-regression.xml"));
                File.WriteAllText(directory + "/final-regression.txt",
                    $"passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}; inconclusive={result.InconclusiveCount}");
                Debug.Log($"[VfxRoadmap] regression: {result.PassCount} passed, {result.FailCount} failed");
                EditorApplication.delayCall += () =>
                {
                    if (runner != null)
                    {
                        runner.UnregisterCallbacks(callback);
                        Object.DestroyImmediate(runner);
                    }
                    runner = null;
                    callback = null;
                };
            }
        }
    }
}
