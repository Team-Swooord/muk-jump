using System;
using AppsInToss;
using UnityEngine;

namespace MukJump.Core
{
    /// Apps in Toss 공식 게임센터에 검증 완료된 최고 고도만 전달한다.
    /// 네이티브 스토어와 에디터에서는 아무 외부 호출도 하지 않는다.
    public static class AppsInTossGameCenterRuntime
    {
        const int ApiTimeoutMilliseconds = 10000;
        const string PendingBestHeightKey =
            "MukJump.AppsInToss.PendingBestHeight";

#if UNITY_WEBGL && !UNITY_EDITOR
        static bool submitInFlight;
#endif

        public static bool IsEligible(GameOverResult result) =>
            result.PersistenceState == GameOverPersistenceState.Complete &&
            result.RewardsAllowed &&
            result.RecordSaved &&
            result.Best >= 0;

        public static void SubmitCompletedRun(GameOverResult result)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!IsEligible(result))
                return;
            QueueAndSubmitScore(result.Best);
#endif
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RetryPendingScoreAfterSceneLoad()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (PlayerPrefs.HasKey(PendingBestHeightKey))
                QueueAndSubmitScore(PlayerPrefs.GetInt(
                    PendingBestHeightKey,
                    0));
#endif
        }

        public static int ResolvePendingBestHeight(
            int storedBest,
            int candidateBest) =>
            Mathf.Max(0, Mathf.Max(storedBest, candidateBest));

        public static bool ShouldClearPendingBestHeight(
            int storedBest,
            int submittedBest) =>
            storedBest <= submittedBest;

        public static bool HasSubmissionResponse(
            SubmitGameCenterLeaderBoardScoreResponse response) =>
            response != null;

        public static void OpenLeaderboard()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OpenLeaderboardAsync();
#else
            Debug.Log("[MukJump] 토스 게임센터는 Apps in Toss에서 열립니다.");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static void QueueAndSubmitScore(int bestHeight)
        {
            int pendingBest = ResolvePendingBestHeight(
                PlayerPrefs.GetInt(PendingBestHeightKey, 0),
                bestHeight);
            PlayerPrefs.SetInt(PendingBestHeightKey, pendingBest);
            PlayerPrefs.Save();
            if (!submitInFlight)
                SubmitScoreAsync(pendingBest);
        }

        static async void SubmitScoreAsync(int bestHeight)
        {
            submitInFlight = true;
            try
            {
                var parameters = new SubmitGameCenterLeaderBoardScoreParams
                {
                    Score = Mathf.Max(0, bestHeight).ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                };
                SubmitGameCenterLeaderBoardScoreResponse response =
                    await AIT.SubmitGameCenterLeaderBoardScore(
                    parameters,
                    ApiTimeoutMilliseconds);
                if (!HasSubmissionResponse(response))
                {
                    submitInFlight = false;
                    Debug.LogWarning(
                        "[MukJump] 현재 토스 앱 버전에서 순위 제출 결과를 확인하지 못해 기록을 보관합니다.");
                    return;
                }
                int storedBest = PlayerPrefs.GetInt(
                    PendingBestHeightKey,
                    0);
                if (ShouldClearPendingBestHeight(
                        storedBest,
                        bestHeight))
                    PlayerPrefs.DeleteKey(PendingBestHeightKey);
                PlayerPrefs.Save();
                submitInFlight = false;
                if (PlayerPrefs.HasKey(PendingBestHeightKey))
                    SubmitScoreAsync(PlayerPrefs.GetInt(
                        PendingBestHeightKey,
                        0));
            }
            catch (Exception exception)
            {
                submitInFlight = false;
                Debug.LogWarning(
                    $"[MukJump] 토스 최고 고도를 저장해 다음 실행 또는 판에 다시 시도합니다: " +
                    exception.Message);
            }
        }

        static async void OpenLeaderboardAsync()
        {
            try
            {
                await AIT.OpenGameCenterLeaderboard(ApiTimeoutMilliseconds);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MukJump] 토스 게임센터를 열지 못했습니다: " +
                    exception.Message);
            }
        }
#endif
    }
}
