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
        const string PendingBestHeightOwnerKey =
            "MukJump.AppsInToss.PendingBestHeight.Owner";

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
            if (!AppsInTossIdentityPolicy.HasVerifiedIdentity)
                return;
            QueueAndSubmitScore(result.Best);
#endif
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RetryPendingScoreAfterSceneLoad()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            RetryPendingScoreForVerifiedUser();
#endif
        }

        public static bool PendingOwnerMatches(
            string storedOwner,
            string verifiedOwner) =>
            !string.IsNullOrWhiteSpace(storedOwner) &&
            !string.IsNullOrWhiteSpace(verifiedOwner) &&
            string.Equals(
                storedOwner,
                verifiedOwner,
                StringComparison.Ordinal);

        public static bool ShouldClearPendingBestHeightForOwner(
            string storedOwner,
            string submittedOwner,
            string verifiedCurrentOwner,
            int storedBest,
            int submittedBest) =>
            PendingOwnerMatches(storedOwner, submittedOwner) &&
            PendingOwnerMatches(
                submittedOwner,
                verifiedCurrentOwner) &&
            ShouldClearPendingBestHeight(storedBest, submittedBest);

        public static int ResolvePendingBestHeight(
            int storedBest,
            int candidateBest) =>
            Mathf.Max(0, Mathf.Max(storedBest, candidateBest));

        public static int ResolvePendingBestHeightForOwner(
            int storedBest,
            int candidateBest,
            string storedOwner,
            string verifiedOwner) =>
            PendingOwnerMatches(storedOwner, verifiedOwner)
                ? ResolvePendingBestHeight(storedBest, candidateBest)
                : Mathf.Max(0, candidateBest);

        public static bool ShouldClearPendingBestHeight(
            int storedBest,
            int submittedBest) =>
            storedBest <= submittedBest;

        public static bool ShouldRetryCurrentOwnerAfterAttempt(
            string submittedOwner,
            string verifiedCurrentOwner) =>
            !string.IsNullOrWhiteSpace(submittedOwner) &&
            !string.IsNullOrWhiteSpace(verifiedCurrentOwner) &&
            !string.Equals(
                submittedOwner,
                verifiedCurrentOwner,
                StringComparison.Ordinal);

        public static bool IsSuccessfulSubmission(
            SubmitGameCenterLeaderBoardScoreResponse response) =>
            response != null && string.Equals(
                response.StatusCode,
                "SUCCESS",
                StringComparison.OrdinalIgnoreCase);

        public static void OpenLeaderboard()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!AppsInTossIdentityPolicy.HasVerifiedIdentity)
                return;
            OpenLeaderboardAsync();
#else
            Debug.Log("[MukJump] 토스 게임센터는 Apps in Toss에서 열립니다.");
#endif
        }

        public static void RetryPendingScoreForVerifiedUser()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!AppsInTossIdentityPolicy.HasVerifiedIdentity ||
                !PlayerPrefs.HasKey(PendingBestHeightKey))
                return;

            string currentOwner =
                AppsInTossIdentityPolicy.VerifiedOwnerToken;
            string storedOwner = PlayerPrefs.GetString(
                PendingBestHeightOwnerKey,
                string.Empty);
            // 소유자를 증명할 수 없는 구 pending과 다른 사용자의 pending은
            // 현재 토스 사용자에게 제출하지 않는다.
            if (!PendingOwnerMatches(storedOwner, currentOwner))
                return;
            QueueAndSubmitScore(PlayerPrefs.GetInt(
                PendingBestHeightKey,
                0));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static void QueueAndSubmitScore(int bestHeight)
        {
            string owner = AppsInTossIdentityPolicy.VerifiedOwnerToken;
            if (string.IsNullOrEmpty(owner))
                return;
            try
            {
                string storedOwner = PlayerPrefs.GetString(
                    PendingBestHeightOwnerKey,
                    string.Empty);
                int pendingBest = ResolvePendingBestHeightForOwner(
                    PlayerPrefs.GetInt(PendingBestHeightKey, 0),
                    bestHeight,
                    storedOwner,
                    owner);
                PlayerPrefs.SetInt(PendingBestHeightKey, pendingBest);
                PlayerPrefs.SetString(PendingBestHeightOwnerKey, owner);
                PlayerPrefs.Save();
                if (!submitInFlight)
                    SubmitScoreAsync(pendingBest, owner);
            }
            catch (Exception)
            {
                Debug.LogWarning(
                    "[MukJump] 토스 최고 고도 재시도 정보를 저장하지 못했습니다.");
            }
        }

        static async void SubmitScoreAsync(
            int bestHeight,
            string submittedOwner)
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
                if (!IsSuccessfulSubmission(response))
                {
                    submitInFlight = false;
                    Debug.LogWarning(
                        "[MukJump] 토스 순위 제출이 성공으로 확인되지 않아 기록을 보관합니다." +
                        (string.IsNullOrWhiteSpace(response?.StatusCode)
                            ? string.Empty
                            : $" (status: {response.StatusCode})"));
                    if (ShouldRetryCurrentOwnerAfterAttempt(
                            submittedOwner,
                            AppsInTossIdentityPolicy.VerifiedOwnerToken))
                        RetryPendingScoreForVerifiedUser();
                    return;
                }
                int storedBest = PlayerPrefs.GetInt(
                    PendingBestHeightKey,
                    0);
                string storedOwner = PlayerPrefs.GetString(
                    PendingBestHeightOwnerKey,
                    string.Empty);
                if (ShouldClearPendingBestHeightForOwner(
                        storedOwner,
                        submittedOwner,
                        AppsInTossIdentityPolicy.VerifiedOwnerToken,
                        storedBest,
                        bestHeight))
                {
                    PlayerPrefs.DeleteKey(PendingBestHeightKey);
                    PlayerPrefs.DeleteKey(PendingBestHeightOwnerKey);
                }
                PlayerPrefs.Save();
                submitInFlight = false;
                RetryPendingScoreForVerifiedUser();
            }
            catch (Exception exception)
            {
                submitInFlight = false;
                Debug.LogWarning(
                    $"[MukJump] 토스 최고 고도를 저장해 다음 실행 또는 판에 다시 시도합니다: " +
                    exception.Message);
                if (ShouldRetryCurrentOwnerAfterAttempt(
                        submittedOwner,
                        AppsInTossIdentityPolicy.VerifiedOwnerToken))
                    RetryPendingScoreForVerifiedUser();
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
