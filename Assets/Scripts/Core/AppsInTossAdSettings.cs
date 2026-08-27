using UnityEngine;

namespace MukJump.Core
{
    [CreateAssetMenu(
        fileName = "AppsInTossAdSettings",
        menuName = "MukJump/Apps in Toss Ad Settings")]
    public sealed class AppsInTossAdSettings : ScriptableObject
    {
        [SerializeField] string rewardedAdGroupId;
        [SerializeField] string interstitialAdGroupId;
        [SerializeField] string bannerAdGroupId;

        public string RewardedAdGroupId => rewardedAdGroupId;
        public string InterstitialAdGroupId => interstitialAdGroupId;
        public string BannerAdGroupId => bannerAdGroupId;

#if UNITY_EDITOR
        public void Configure(
            string rewardedId,
            string interstitialId,
            string bannerId = null)
        {
            rewardedAdGroupId = rewardedId?.Trim() ?? string.Empty;
            interstitialAdGroupId = interstitialId?.Trim() ?? string.Empty;
            bannerAdGroupId = bannerId?.Trim() ?? string.Empty;
        }
#endif
    }
}
