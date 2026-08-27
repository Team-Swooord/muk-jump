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
            SubmitScoreAsync(result.Best);
#endif
        }

        public static void OpenLeaderboard()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OpenLeaderboardAsync();
#else
            Debug.Log("[MukJump] 토스 게임센터는 Apps in Toss에서 열립니다.");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static async void SubmitScoreAsync(int bestHeight)
        {
            try
            {
                var parameters = new SubmitGameCenterLeaderBoardScoreParams
                {
                    Score = Mathf.Max(0, bestHeight).ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                };
                await AIT.SubmitGameCenterLeaderBoardScore(
                    parameters,
                    ApiTimeoutMilliseconds);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MukJump] 토스 최고 고도 제출을 다음 판에 다시 시도합니다: " +
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
