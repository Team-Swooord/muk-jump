using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MukJump.EditorTools
{
    public static class MukJumpEditModeTestRunner
    {
        static TestRunnerApi runner;
        static ResultLogger resultLogger;
        static double nextRequestPollTime;
        static bool runScheduled;
        // 외부 검증은 이 요청 파일을 만든 뒤 에셋 새로고침으로 실행한다.
        const string RequestPath = "Temp/MukJumpRunAllTests.request";
        const string CancelRequestPath =
            "Temp/MukJumpCancelEditModeTests.request";
        const string CurrentTestPath =
            "Temp/MukJumpCurrentEditModeTest.txt";
        const string ActiveRunPath =
            "Temp/MukJumpRunAllTests.active";
        const string ResultPath = "Temp/MukJumpRunAllTests.result";
        const string FailurePath = "Temp/MukJumpRunAllTests.failures";

        [InitializeOnLoadMethod]
        static void InstallRequestWatcher()
        {
            InstallResultLoggerIfActive();
            EditorApplication.update -= PollRequestedTests;
            EditorApplication.update += PollRequestedTests;
            PollRequestedTests();
        }

        static void PollRequestedTests()
        {
            if (EditorApplication.timeSinceStartup < nextRequestPollTime)
                return;
            nextRequestPollTime = EditorApplication.timeSinceStartup + 1d;
            if (File.Exists(CancelRequestPath))
            {
                File.Delete(CancelRequestPath);
                DeleteIfExists(RequestPath);
                EditorApplication.delayCall += CancelAllRunningTests;
                return;
            }
            if (!File.Exists(RequestPath))
                return;
            if (runScheduled)
                return;
            runScheduled = true;
            EditorApplication.delayCall += RunRequestedTests;
        }

        static void RunRequestedTests()
        {
            runScheduled = false;
            if (!File.Exists(RequestPath))
                return;

            // 실제 실행 직전에만 요청 파일을 지운다. 패키지 초기화나 도메인
            // 리로드가 delayCall보다 먼저 발생하면 요청 파일이 남아 다음 로드에서
            // 다시 예약되므로 테스트 요청이 사라지지 않는다.
            File.Delete(RequestPath);
            RunAll();
        }

        static void CancelAllRunningTests()
        {
            // Unity Test Framework의 공개 API는 실행 GUID가 있어야 중지할 수 있다.
            // 도메인 리로드 뒤 복원된 작업의 GUID는 공개되지 않으므로, 복원된 러너만
            // 리플렉션으로 찾아 각 러너의 공개 CancelRun 메서드를 호출한다.
            Assembly assembly = typeof(TestRunnerApi).Assembly;
            Type holderType = assembly.GetType(
                "UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder");
            PropertyInfo instanceProperty = holderType?.GetProperty(
                "instance",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            object holder = instanceProperty?.GetValue(null);
            MethodInfo getAllRunners = holderType?.GetMethod(
                "GetAllRunners",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (getAllRunners?.Invoke(holder, null) is not System.Array runners)
            {
                Debug.LogWarning("[MukJump] 중지할 Unity 테스트 실행을 찾지 못했습니다.");
                DeleteIfExists(ActiveRunPath);
                DeleteIfExists(CurrentTestPath);
                return;
            }

            int canceled = 0;
            foreach (object activeRunner in runners)
            {
                MethodInfo cancel = activeRunner?.GetType().GetMethod(
                    "CancelRun",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (cancel?.Invoke(activeRunner, null) is true)
                    canceled++;
            }
            Debug.Log($"[MukJump] Unity 테스트 실행 {canceled}개 중지 요청 완료");
            DeleteIfExists(ActiveRunPath);
            DeleteIfExists(CurrentTestPath);
        }

        [MenuItem("MukJump/Diagnostics/Run All EditMode Tests %#t")]
        public static void RunAll()
        {
            if (runner != null || File.Exists(ActiveRunPath))
            {
                Debug.LogWarning("[MukJump] EditMode 테스트가 이미 실행 중입니다.");
                return;
            }

            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            DeleteIfExists(CurrentTestPath);
            DeleteIfExists(ResultPath);
            DeleteIfExists(FailurePath);
            File.WriteAllText(ActiveRunPath, DateTime.UtcNow.ToString("O"));
            InstallResultLoggerIfActive();
            runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode
            }));
            Debug.Log("[MukJump] 전체 EditMode 테스트를 시작합니다.");
        }

        static void InstallResultLoggerIfActive()
        {
            if (!File.Exists(ActiveRunPath) || resultLogger != null)
                return;

            // EnterPlayMode 기반 EditMode 테스트는 도메인을 다시 로드한다.
            // plain managed callback은 이때 사라지므로 실행 표식을 기준으로
            // 매 도메인에서 새 ScriptableObject callback을 다시 등록한다.
            resultLogger = ScriptableObject.CreateInstance<ResultLogger>();
            resultLogger.hideFlags = HideFlags.HideAndDontSave;
            TestRunnerApi.RegisterTestCallback(resultLogger);
        }

        sealed class ResultLogger : ScriptableObject, ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                int total = result.PassCount + result.FailCount +
                            result.SkipCount + result.InconclusiveCount;
                Debug.Log(
                    $"[MukJump] EditMode 테스트 완료: " +
                    $"{result.PassCount}/{total} 통과, " +
                    $"실패 {result.FailCount}, 건너뜀 {result.SkipCount}");
                File.WriteAllText(
                    ResultPath,
                    $"pass={result.PassCount}\n" +
                    $"fail={result.FailCount}\n" +
                    $"skip={result.SkipCount}\n" +
                    $"inconclusive={result.InconclusiveCount}\n" +
                    $"total={total}\n");
                DeleteIfExists(CurrentTestPath);
                DeleteIfExists(ActiveRunPath);
                TestRunnerApi.UnregisterTestCallback(this);
                runner = null;
                resultLogger = null;
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                        DestroyImmediate(this);
                };
            }

            public void TestStarted(ITestAdaptor test)
            {
                if (test != null)
                    File.WriteAllText(CurrentTestPath, test.FullName ?? test.Name);
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result == null || result.FailCount <= 0)
                    return;
                File.AppendAllText(
                    FailurePath,
                    $"{result.FullName}\n{result.Message}\n{result.StackTrace}\n\n");
            }
        }

        static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
