#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using MukJump.EditorTools;

namespace MukJump.EditorTools
{
    /// Unity Recorder를 시작하기 전에 다음 Play를 제출 영상 시나리오로 예약한다.
    public sealed class MukJumpRecordingScenarioWindow : EditorWindow
    {
        Vector2 scroll;

        [MenuItem("MukJump/Recording/NAN 2026 촬영 시나리오")]
        static void Open()
        {
            var window = GetWindow<MukJumpRecordingScenarioWindow>();
            window.titleContent = new GUIContent("먹점프 촬영");
            window.minSize = new Vector2(440f, 610f);
            window.Show();
        }

        void OnEnable()
        {
            EditorApplication.update -= Repaint;
            EditorApplication.update += Repaint;
        }

        void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(
                "먹점프 · NAN 2026 제출 영상",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "실제 저장을 건드리지 않는 에디터 전용 약 50초 자동 시나리오입니다.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8f);

            DrawRecorderSetup();
            EditorGUILayout.Space(8f);
            DrawScenarioControls();
            EditorGUILayout.Space(10f);
            DrawTimeline();
        }

        void DrawRecorderSetup()
        {
            EditorGUILayout.LabelField("1. Unity Recorder 설정", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Movie Clip · Game View · 1080×1920 · MP4(H.264) · High · " +
                "30fps · Audio On · 0~50초로 설정하세요. 녹화 직전 Game View 탭을 " +
                "마지막으로 한 번 선택해야 합니다.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Package Manager 열기", GUILayout.Height(30f)))
                    EditorApplication.ExecuteMenuItem(
                        "Window/Package Management/Package Manager");
                if (GUILayout.Button("Recorder 패키지명 복사", GUILayout.Height(30f)))
                {
                    EditorGUIUtility.systemCopyBuffer =
                        "com.unity.recorder@5.1.7";
                    ShowNotification(new GUIContent("패키지명을 복사했습니다"));
                }
            }
        }

        void DrawScenarioControls()
        {
            EditorGUILayout.LabelField("2. 촬영 시나리오", EditorStyles.boldLabel);
            bool armed = SessionState.GetBool(
                RecordingScenarioDirector.ArmSessionKey,
                false);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    armed
                        ? "예약 완료. Recorder Window의 START RECORDING을 누르면 " +
                          "Play와 함께 자동 시나리오가 시작됩니다."
                        : "먼저 촬영을 예약한 뒤 Recorder Window에서 START RECORDING을 누르세요.",
                    armed ? MessageType.Info : MessageType.None);

                GUI.backgroundColor = armed
                    ? new Color(0.72f, 0.86f, 0.72f)
                    : Color.white;
                if (GUILayout.Button(
                        armed ? "촬영 시나리오 예약됨" : "다음 Play 촬영 예약",
                        GUILayout.Height(44f)))
                {
                    SessionState.SetBool(
                        RecordingScenarioDirector.ArmSessionKey,
                        true);
                }
                GUI.backgroundColor = Color.white;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("예약 취소", GUILayout.Height(30f)))
                        SessionState.SetBool(
                            RecordingScenarioDirector.ArmSessionKey,
                            false);
                    if (GUILayout.Button("예약하고 바로 미리보기", GUILayout.Height(30f)))
                    {
                        SessionState.SetBool(
                            RecordingScenarioDirector.ArmSessionKey,
                            true);
                        EditorApplication.isPlaying = true;
                    }
                }
                return;
            }

            RecordingScenarioDirector director =
                RecordingScenarioDirector.Instance;
            if (director == null)
            {
                EditorGUILayout.HelpBox(
                    "현재 Play는 촬영 모드가 아닙니다. Play를 종료하고 '다음 Play 촬영 예약'을 눌러 주세요.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(
                $"현재: {director.CurrentStageIndex + 1}/{RecordingScenarioDirector.StageCount} " +
                $"{director.CurrentStageLabel} · {director.ElapsedSeconds:0.0}초");
            Rect progressRect = GUILayoutUtility.GetRect(1f, 18f);
            EditorGUI.ProgressBar(
                progressRect,
                director.Progress01,
                $"{director.Progress01 * 100f:0}%");
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("다음 장면", GUILayout.Height(34f)))
                    director.AdvanceToNextStage();
                if (GUILayout.Button("자동 진행 정지", GUILayout.Height(34f)))
                    director.StopScenario();
            }
        }

        void DrawTimeline()
        {
            EditorGUILayout.LabelField("촬영 순서", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(250f));
            int current = RecordingScenarioDirector.Instance != null
                ? RecordingScenarioDirector.Instance.CurrentStageIndex
                : -1;
            for (int i = 0; i < RecordingScenarioDirector.StageCount; i++)
            {
                bool active = i == current;
                Color previous = GUI.color;
                if (active)
                    GUI.color = new Color(1f, 0.72f, 0.76f);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label($"{i + 1:00}", GUILayout.Width(28f));
                    GUILayout.Label(
                        RecordingScenarioDirector.GetStageLabel(i),
                        GUILayout.ExpandWidth(true));
                    GUILayout.Label(
                        $"{RecordingScenarioDirector.GetStageDuration(i):0.0}s",
                        GUILayout.Width(48f));
                }
                GUI.color = previous;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField(
                $"연출 합계 {RecordingScenarioDirector.ScheduledDuration:0.0}초 + " +
                "화면 전환 대기 ≈ 48~50초",
                EditorStyles.miniLabel);
        }
    }
}
#endif
