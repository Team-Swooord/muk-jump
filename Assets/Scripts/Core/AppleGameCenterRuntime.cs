using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MukJump.Core
{
    /// Game Center의 별도 계정/순위를 보존한다. 뒤끝 계정을 같은 사람으로 추측하지 않는다.
    public sealed class AppleGameCenterRuntime : MonoBehaviour
    {
        static AppleGameCenterRuntime instance;
        static readonly List<MukJumpLeaderboardEntry> entries = new();
        static int request;
        float deadline;
        public static event Action Changed;
        public static IReadOnlyList<MukJumpLeaderboardEntry> Entries => entries;
        public static string Status { get; private set; } = string.Empty;
        public static bool Loading { get; private set; }
        public static string LeaderboardId => MukJumpBackendSettings.Load()?.AppleGameCenterLeaderboardId ?? string.Empty;
        public static bool Configured => !string.IsNullOrWhiteSpace(LeaderboardId);

        [Serializable] class Response
        {
            public int request;
            public string error;
            public Row[] rows;
        }
        [Serializable] class Row { public int rank; public int height; public string name; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            entries.Clear();
            Loading = false;
            Status = string.Empty;
            Changed = null;
            request = 0;
        }

        void OnEnable() { instance = this; }
        void OnDestroy() { if (instance == this) instance = null; }

        public static void LoadLeaderboard()
        {
            if (Loading) return;
            entries.Clear();
            if (!Configured)
            {
                Status = "Game Center 순위를 준비 중이에요";
                Changed?.Invoke();
                return;
            }
#if UNITY_IOS && !UNITY_EDITOR
            if (instance == null)
            {
                var root = new GameObject("MukJumpAppleGameCenterRuntime");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<AppleGameCenterRuntime>();
            }
            Loading = true;
            Status = "Game Center에 연결하는 중";
            instance.deadline = Time.realtimeSinceStartup + 60f;
            MukJumpGameCenterLoad(LeaderboardId, ++request);
#else
            Status = "Game Center는 iPhone에서 이용할 수 있어요";
#endif
            Changed?.Invoke();
        }

        void Update()
        {
            if (!Loading || Time.realtimeSinceStartup < deadline) return;
            Loading = false;
            request++;
            Status = "연결이 지연되고 있어요. 다시 시도해 주세요";
            Changed?.Invoke();
        }

        // UnitySendMessage에서 호출한다. 외부 닉네임은 UI에서 rich text를 사용하지 않는다.
        [UnityEngine.Scripting.Preserve]
        public void ReceiveLeaderboard(string json)
        {
            Response response;
            try { response = JsonUtility.FromJson<Response>(json); }
            catch { return; }
            if (response == null || !Loading || response.request != request) return;
            Loading = false;
            entries.Clear();
            if (!string.IsNullOrEmpty(response.error))
                Status = response.error == "auth" ? "Game Center 로그인이 필요해요" :
                    "순위를 불러오지 못했어요. 다시 시도해 주세요";
            else
            {
                if (response.rows != null)
                    for (int i = 0; i < Math.Min(10, response.rows.Length); i++)
                    {
                        Row row = response.rows[i];
                        if (row != null) entries.Add(new MukJumpLeaderboardEntry(row.rank, row.height, row.name, "APPLE"));
                    }
                Status = entries.Count == 0 ? "아직 등록된 기록이 없어요" : string.Empty;
            }
            Changed?.Invoke();
        }

        [UnityEngine.Scripting.Preserve]
        public void AccountChanged(string unused)
        {
            request++;
            Loading = false;
            entries.Clear();
            Status = "Game Center 계정이 바뀌었어요. 새로고침해 주세요";
            Changed?.Invoke();
        }

        public static void BeginRun()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Configured) MukJumpGameCenterBeginRun();
#endif
        }

        public static void SubmitCompletedRun(GameOverResult result)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Configured && AppsInTossGameCenterRuntime.IsEligible(result) && result.Height >= 0)
                // 가져온 전체 최고 기록이 아닌 이 Game Center 사용자가 실제 플레이한 높이만 제출한다.
                MukJumpGameCenterSubmit(LeaderboardId, result.Height);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void MukJumpGameCenterLoad(string id, int request);
        [DllImport("__Internal")] static extern void MukJumpGameCenterBeginRun();
        [DllImport("__Internal")] static extern void MukJumpGameCenterSubmit(string id, int height);
#endif
    }
}
