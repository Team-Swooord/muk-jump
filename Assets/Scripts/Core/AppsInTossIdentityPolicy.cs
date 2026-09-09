using System;
using UnityEngine;

namespace MukJump.Core
{
    public enum AppsInTossOwnerBindingDecision
    {
        RejectInvalid,
        BindNew,
        AcceptExisting,
        RejectOwnerMismatch,
    }

    /// Apps in Toss의 익명 게임 식별자를 먼저 검증하고, 이 브라우저에 남은
    /// 로컬 기록의 소유자와 일치할 때만 게임 시작과 게임센터 제출을 허용한다.
    /// 원문 hash는 로그나 PlayerPrefs key에 쓰지 않고 전용 값으로만 보관한다.
    public static class AppsInTossIdentityPolicy
    {
        public const string UserHashKey =
            "MukJump.AppsInToss.UserHash.v1";

        [Serializable]
        sealed class UserKeyEnvelope
        {
            public string type;
            public string hash;
        }

        static bool hasVerifiedIdentity;
        static string verifiedOwnerToken = string.Empty;
        static string verifiedUserHash = string.Empty;

        public static bool HasVerifiedIdentity => hasVerifiedIdentity;
        public static string VerifiedOwnerToken => hasVerifiedIdentity
            ? verifiedOwnerToken
            : string.Empty;
        // 설정에서 본인 ID를 표시·복사할 때만 사용한다. 미검증 저장값은 노출하지 않는다.
        public static string VerifiedUserHash => hasVerifiedIdentity ? verifiedUserHash : string.Empty;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            hasVerifiedIdentity = false;
            verifiedOwnerToken = string.Empty;
            verifiedUserHash = string.Empty;
        }

        public static bool TryExtractUserHash(
            string rawResult,
            out string userHash)
        {
            userHash = string.Empty;
            string normalized = rawResult?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
                return false;

            if (normalized.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    UserKeyEnvelope envelope =
                        JsonUtility.FromJson<UserKeyEnvelope>(normalized);
                    if (envelope == null ||
                        !string.Equals(
                            envelope.type?.Trim(),
                            "HASH",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(envelope.hash))
                        return false;
                    userHash = envelope.hash.Trim();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            // 현재 공식 계약의 성공값은 HASH object뿐이다. 임의 primitive를
            // 미래 hash로 추측하면 새 오류값을 영구 소유자로 묶을 수 있다.
            return false;
        }

        public static bool IsGameCenterProfileReady(string statusCode) =>
            string.Equals(
                statusCode?.Trim(),
                "SUCCESS",
                StringComparison.OrdinalIgnoreCase);

        public static AppsInTossOwnerBindingDecision ResolveOwnerBinding(
            string storedHash,
            string incomingHash)
        {
            string incoming = incomingHash?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(incoming) || IsFailureSentinel(incoming))
                return AppsInTossOwnerBindingDecision.RejectInvalid;

            string stored = storedHash?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(stored))
                return AppsInTossOwnerBindingDecision.BindNew;
            return string.Equals(stored, incoming, StringComparison.Ordinal)
                ? AppsInTossOwnerBindingDecision.AcceptExisting
                : AppsInTossOwnerBindingDecision.RejectOwnerMismatch;
        }

        public static string CreateOwnerToken(string userHash)
        {
            string normalized = userHash?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(normalized) ||
                   IsFailureSentinel(normalized)
                ? string.Empty
                : Hash128.Compute(normalized).ToString();
        }

        /// 저장 성공뿐 아니라 같은 값의 readback까지 확인한 뒤에만 런타임
        /// 식별을 연다. 다른 토스 사용자로 바뀌면 기존 기록을 섞지 않고 차단한다.
        public static bool TryBindVerifiedIdentity(
            string userHash,
            out AppsInTossOwnerBindingDecision decision)
        {
            hasVerifiedIdentity = false;
            verifiedOwnerToken = string.Empty;
            verifiedUserHash = string.Empty;
            string incoming = userHash?.Trim() ?? string.Empty;
            try
            {
                string stored = PlayerPrefs.GetString(
                    UserHashKey,
                    string.Empty);
                decision = ResolveOwnerBinding(stored, incoming);
                if (decision ==
                        AppsInTossOwnerBindingDecision.RejectInvalid ||
                    decision ==
                        AppsInTossOwnerBindingDecision.RejectOwnerMismatch)
                    return false;

                if (decision == AppsInTossOwnerBindingDecision.BindNew)
                {
                    PlayerPrefs.SetString(UserHashKey, incoming);
                    PlayerPrefs.Save();
                }

                if (!string.Equals(
                        PlayerPrefs.GetString(UserHashKey, string.Empty),
                        incoming,
                        StringComparison.Ordinal))
                    return false;

                string ownerToken = CreateOwnerToken(incoming);
                if (string.IsNullOrEmpty(ownerToken))
                    return false;
                verifiedOwnerToken = ownerToken;
                verifiedUserHash = incoming;
                hasVerifiedIdentity = true;
                return true;
            }
            catch (Exception)
            {
                decision = AppsInTossOwnerBindingDecision.RejectInvalid;
                return false;
            }
        }

        public static void MarkIdentityUnverified()
        {
            hasVerifiedIdentity = false;
            verifiedOwnerToken = string.Empty;
            verifiedUserHash = string.Empty;
        }

        static bool IsFailureSentinel(string value) =>
            string.Equals(value, "ERROR", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                value,
                "INVALID_CATEGORY",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                value,
                "NOT_AVAILABLE",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                value,
                "UNDEFINED",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase);
    }
}
