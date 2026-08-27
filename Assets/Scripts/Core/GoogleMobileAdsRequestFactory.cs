#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
using GoogleMobileAds.Api;

namespace MukJump.Core
{
    /// ATT 선택과 무관하게 모든 Google 광고를 비맞춤형으로 요청한다.
    /// 배너·보상형·전면형이 같은 정책을 공유하도록 한 곳에서 만든다.
    public static class GoogleMobileAdsRequestFactory
    {
        public static AdRequest CreateNonPersonalized()
        {
            var request = new AdRequest();
            request.Extras["npa"] = "1";
            return request;
        }
    }
}
#endif
