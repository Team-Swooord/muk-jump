using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MukJump.EditorTools
{
    public static class MukJumpAgentAudit
    {
        // 설치본의 즉시 backbuffer 캡처는 배치 에디터에서 빈 프레임을 반환할 수 있다.
        // 기존 게임플레이 검사와 같은 프레임 말미 캡처를 사용하고 Assets에는 쓰지 않는다.
        [CliCommand("mukjump_capture_ui", "Queue composited Game view PNG under output/qa; inspect file after next frame",
            Tags = new[] { "mukjump", "capture" })]
        public static object CaptureUi()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                throw new System.InvalidOperationException("실행 중이며 에디터 일시정지가 해제된 Game 뷰가 필요합니다.");
            string path = CreateCapturePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);
            EditorApplication.QueuePlayerLoopUpdate();
            return new { status = "queued", path, source = "composited-game-view",
                width = Screen.width, height = Screen.height };
        }

        internal static string CreateCapturePath() => Path.GetFullPath(Path.Combine("output", "qa",
            "pipeline-captures", "game-ui-" + System.Guid.NewGuid().ToString("N") + ".png"));

        // 읽기 전용: 씬을 열거나 저장하거나 계정/PlayerPrefs에 접근하지 않는다.
        [CliCommand("mukjump_audit", "Read-only MukJump identity, scene freshness and Player safety checks",
            Tags = new[] { "mukjump", "diagnostics" })]
        public static object Audit()
        {
            var scenes = new List<object>();
            int missingScripts = 0;
            bool dirty = false;
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded) continue;
                int missing = CountMissingScripts(scene.GetRootGameObjects());
                missingScripts += missing;
                dirty |= scene.isDirty;
                scenes.Add(new { scene.path, scene.isDirty, scene.rootCount, missingScripts = missing });
            }

            string[] safetyIssues = MukJumpAgentSafety.CollectIssues();
            bool sourceCurrent = MukJumpSceneBuilder.SavedMainSceneMatchesCurrentSource();
            return new
            {
                schemaVersion = 1,
                projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                unityVersion = Application.unityVersion,
                pipelineVersion = MukJumpAgentSafety.InstalledPackageVersion(),
                target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                playing = EditorApplication.isPlayingOrWillChangePlaymode,
                compiling = EditorApplication.isCompiling,
                updating = EditorApplication.isUpdating,
                sceneSourceCurrent = sourceCurrent,
                // 현재 로드된 씬 검사다. 모든 프리팹/미개방 씬을 검증했다고 표시하지 않는다.
                loadedScenes = scenes,
                missingScriptsInLoadedScenes = missingScripts,
                enabledBuildScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                    .Select(scene => scene.path).ToArray(),
                playerSafetyIssues = safetyIssues,
                readyForValidation = !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                                     !EditorApplication.isPlayingOrWillChangePlaymode &&
                                     !dirty && sourceCurrent && missingScripts == 0 && safetyIssues.Length == 0
            };
        }

        internal static int CountMissingScripts(IEnumerable<GameObject> roots)
        {
            int missing = 0;
            foreach (GameObject root in roots)
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            return missing;
        }
    }
}
