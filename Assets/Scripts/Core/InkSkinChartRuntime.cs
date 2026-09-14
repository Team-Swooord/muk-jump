using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using BackEnd;
using BackEnd.Content;
#endif

namespace MukJump.Core
{
    /// 로그인 성공 후 공통 차트만 읽는다. 실패해도 로그인·튜토리얼·플레이를 막지 않는다.
    public sealed class InkSkinChartRuntime : MonoBehaviour
    {
        bool startedRun;
        int requestGeneration;
        string pendingJson;
        static InkSkinChartRuntime instance;
        public static string LastStatus { get; private set; } = "bundled";
        static string CachePath => Path.Combine(Application.persistentDataPath, "skin-chart-v1.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; LastStatus = "bundled"; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
#if !UNITY_EDITOR && !UNITY_WEBGL
            if (instance != null) return;
            instance = new GameObject("SkinChartRuntime").AddComponent<InkSkinChartRuntime>();
            DontDestroyOnLoad(instance.gameObject);
            try
            {
                if (File.Exists(CachePath) && new FileInfo(CachePath).Length <= InkSkinChart.MaxJsonLength * 4)
                {
                    string json = File.ReadAllText(CachePath);
                    if (InkSkinChart.TryParse(json, out var cached, out _))
                    { InkSkinChart.Apply(cached); LastStatus = "cached"; }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { LastStatus = "cache-unavailable"; }
#endif
        }
        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby) startedRun = true;
            if (pendingJson == null) return;
            string json = pendingJson;
            pendingJson = null;
            if (!InkSkinChart.TryParse(json, out var snapshot, out _)) { LastStatus = "rejected"; return; }
            // 한 번이라도 시작한 세션은 판 복구·결과 정산까지 기존 규칙을 고정한다.
            if (CanApplyDownloaded(startedRun, GameManager.Instance?.State ?? GameState.Lobby))
            { InkSkinChart.Apply(snapshot); LastStatus = "remote"; }
            else LastStatus = "next-launch";
            try
            {
                string temporary = CachePath + ".pending";
                File.WriteAllText(temporary, json);
                if (File.Exists(CachePath)) File.Replace(temporary, CachePath, null);
                else File.Move(temporary, CachePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            { Debug.LogWarning("[MukJump] 스킨 차트 캐시 저장 실패. 다음 접속에서 다시 조회합니다."); }
        }
        public static bool CanApplyDownloaded(bool hasStartedRun, GameState state) => !hasStartedRun && state == GameState.Lobby;
        IEnumerator Start()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // 온라인이 될 때까지 대기만 한다. 인증 생성·복구는 기존 계정 시스템만 담당한다.
            while (MukJumpAccountRuntime.Instance?.IsOnlineAuthenticated != true || !Backend.IsInitialized)
                yield return new WaitForSecondsRealtime(2);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int generation = ++requestGeneration;
                bool done = false;
                LastStatus = "fetching";
                try
                {
                    Backend.CDN.Content.Table.Get(table => {
                        if (this == null || generation != requestGeneration) return;
                        try
                        {
                            if (!table.IsSuccess()) { done = true; return; }
                            var selected = table.GetContentTableItemList().FindAll(x => x.chartName == InkSkinChart.ChartName);
                            if (selected.Count != 1 || string.IsNullOrEmpty(selected[0].selectedChartFileId)) { done = true; return; }
                            // CDN API는 SendQueue에 넣지 않는다. 동일 파일 ID의 콘솔 수정도 새로 읽는다.
                            Backend.CDN.Content.Get(selected, null, content => {
                                if (this == null || generation != requestGeneration) return;
                                try
                                {
                                    if (content.IsSuccess())
                                    {
                                        var items = content.GetContentList();
                                        if (items.Count == 1 && items[0].chartId == selected[0].chartId)
                                            pendingJson = items[0].contentString;
                                    }
                                }
                                catch (Exception) { LastStatus = "download-failed"; }
                                done = true;
                            });
                        }
                        catch (Exception) { done = true; LastStatus = "download-failed"; }
                    });
                }
                catch (Exception) { done = true; }
                float deadline = Time.realtimeSinceStartup + 20;
                while (!done && Time.realtimeSinceStartup < deadline) yield return null;
                if (pendingJson != null || LastStatus is "remote" or "next-launch") yield break;
                ++requestGeneration;
                LastStatus = InkSkinChart.HasRemoteData ? "cached" : "bundled";
                yield return new WaitForSecondsRealtime(10 * (attempt + 1));
            }
#else
            yield break;
#endif
        }
    }
}
