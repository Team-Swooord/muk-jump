using UnityEngine;

namespace MukJump.Core
{
    /// 뒤끝 콘솔에서 먹점프 전용 앱·테이블·랭킹을 만든 뒤 값만 채운다.
    /// 서명키와 토큰은 이 에셋에 넣거나 로그로 출력하지 않는다.
    [CreateAssetMenu(
        fileName = "MukJumpBackendSettings",
        menuName = "MukJump/Backend Settings")]
    public sealed class MukJumpBackendSettings : ScriptableObject
    {
        public const string ResourcePath =
            "MukJump/Settings/MukJumpBackendSettings";

        [SerializeField] bool productionConfigurationVerified;
        [SerializeField] string playerTableName = "MukJumpPlayer";
        [SerializeField] string bestHeightColumn = "bestHeight";
        [SerializeField] string allTimeRankUuid = string.Empty;
        [SerializeField] string androidGoogleWebClientId = string.Empty;
        [SerializeField] string androidAppleServiceId = string.Empty;
        [SerializeField] bool appleRevocationConfigurationVerified;
        [SerializeField] bool appleAccountChangeWebhookVerified;
        [SerializeField] bool androidSigningHashesVerified;

        public bool ProductionConfigurationVerified =>
            productionConfigurationVerified;
        public string PlayerTableName => playerTableName.Trim();
        public string BestHeightColumn => bestHeightColumn.Trim();
        public string AllTimeRankUuid => allTimeRankUuid.Trim();
        public string AndroidGoogleWebClientId =>
            androidGoogleWebClientId.Trim();
        public string AndroidAppleServiceId => androidAppleServiceId.Trim();
        public bool AppleRevocationConfigurationVerified =>
            appleRevocationConfigurationVerified;
        public bool AppleAccountChangeWebhookVerified =>
            appleAccountChangeWebhookVerified;
        public bool AndroidSigningHashesVerified =>
            androidSigningHashesVerified;

        public bool HasRequiredRuntimeValues =>
            productionConfigurationVerified &&
            !string.IsNullOrWhiteSpace(PlayerTableName) &&
            !string.IsNullOrWhiteSpace(BestHeightColumn);

        public static MukJumpBackendSettings Load() =>
            Resources.Load<MukJumpBackendSettings>(ResourcePath);
    }
}
