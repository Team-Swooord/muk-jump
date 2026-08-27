using System;
using UnityEngine;

namespace MukJump.Core
{
    public enum GoogleAdsPlatform
    {
        Android,
        IOS,
    }

    [Serializable]
    public sealed class GoogleAdUnitSet
    {
        [SerializeField] string banner;
        [SerializeField] string rewarded;
        [SerializeField] string interstitial;

        public string Banner => banner?.Trim() ?? string.Empty;
        public string Rewarded => rewarded?.Trim() ?? string.Empty;
        public string Interstitial => interstitial?.Trim() ?? string.Empty;

        public GoogleAdUnitSet() { }

        public GoogleAdUnitSet(
            string bannerId,
            string rewardedId,
            string interstitialId)
        {
            banner = bannerId?.Trim() ?? string.Empty;
            rewarded = rewardedId?.Trim() ?? string.Empty;
            interstitial = interstitialId?.Trim() ?? string.Empty;
        }

#if UNITY_EDITOR
        public void Configure(
            string bannerId,
            string rewardedId,
            string interstitialId)
        {
            banner = bannerId?.Trim() ?? string.Empty;
            rewarded = rewardedId?.Trim() ?? string.Empty;
            interstitial = interstitialId?.Trim() ?? string.Empty;
        }
#endif
    }

    /// Google이 공개한 전용 테스트 광고 단위다. 에디터와 Development Build는
    /// 운영 광고 단위를 절대 요청하지 않고 이 값만 사용한다.
    public static class GoogleMobileAdsTestIds
    {
        public const string AndroidApp =
            "ca-app-pub-3940256099942544~3347511713";
        public const string IOSApp =
            "ca-app-pub-3940256099942544~1458002511";

        public static GoogleAdUnitSet For(GoogleAdsPlatform platform)
        {
            return platform == GoogleAdsPlatform.Android
                ? new GoogleAdUnitSet(
                    "ca-app-pub-3940256099942544/9214589741",
                    "ca-app-pub-3940256099942544/5224354917",
                    "ca-app-pub-3940256099942544/1033173712")
                : new GoogleAdUnitSet(
                    "ca-app-pub-3940256099942544/2435281174",
                    "ca-app-pub-3940256099942544/1712485313",
                    "ca-app-pub-3940256099942544/4411468910");
        }

        public static string AppIdFor(GoogleAdsPlatform platform)
        {
            return platform == GoogleAdsPlatform.Android
                ? AndroidApp
                : IOSApp;
        }
    }

    [CreateAssetMenu(
        fileName = "MukJumpGoogleAdsSettings",
        menuName = "MukJump/Google Mobile Ads Settings")]
    public sealed class MukJumpGoogleAdsSettings : ScriptableObject
    {
        public const string ResourcePath =
            "MukJump/Settings/MukJumpGoogleAdsSettings";

        [Header("노출 정책")]
        [SerializeField] bool enableLobbyBanner = true;
        [SerializeField] bool enablePostRunInterstitial;

        [Header("운영 전 최종 확인")]
        [SerializeField] bool productionConfigurationVerified;
        [SerializeField, TextArea(3, 6)] string verificationNote =
            "cysbandcs@gmail.com AdMob 계정에서 먹점프 Android·iOS 앱과 " +
            "로비 배너·게임오버 부활 보상형 광고 단위를 생성했습니다.";

        [Header("Android 운영값")]
        [SerializeField] string androidAppId;
        [SerializeField] GoogleAdUnitSet android = new();

        [Header("iOS 운영값")]
        [SerializeField] string iosAppId;
        [SerializeField] GoogleAdUnitSet ios = new();

        public bool EnableLobbyBanner => enableLobbyBanner;
        public bool EnablePostRunInterstitial => enablePostRunInterstitial;
        public bool ProductionConfigurationVerified =>
            productionConfigurationVerified;
        public string VerificationNote => verificationNote;

        public string AppIdFor(GoogleAdsPlatform platform)
        {
            return (platform == GoogleAdsPlatform.Android
                    ? androidAppId
                    : iosAppId)?.Trim() ?? string.Empty;
        }

        public GoogleAdUnitSet UnitsFor(GoogleAdsPlatform platform)
        {
            return platform == GoogleAdsPlatform.Android ? android : ios;
        }

        public bool ShouldUseTestAds(bool isEditor, bool isDevelopmentBuild)
        {
            // 실수로 운영 광고를 누르는 무효 트래픽을 막기 위해
            // 에디터와 Development Build는 항상 Google 테스트 ID만 쓴다.
            return isEditor || isDevelopmentBuild;
        }

        public bool TryValidateProduction(
            GoogleAdsPlatform platform,
            out string error)
        {
            if (enablePostRunInterstitial)
            {
                error =
                    "먹점프 1.0은 강제 전면 광고를 사용하지 않습니다. " +
                    "로비 배너와 선택형 부활 보상 광고만 허용합니다.";
                return false;
            }
            if (!productionConfigurationVerified)
            {
                error = "먹점프 AdMob 운영 설정의 '검증 완료'가 꺼져 있습니다.";
                return false;
            }

            string appId = AppIdFor(platform);
            GoogleAdUnitSet units = UnitsFor(platform);
            if (!IsAppId(appId))
            {
                error = $"{platform} AdMob 앱 ID가 비었거나 형식이 올바르지 않습니다.";
                return false;
            }
            if (enableLobbyBanner && !IsAdUnitId(units.Banner))
            {
                error = $"{platform} 로비 배너 광고 단위 ID가 필요합니다.";
                return false;
            }
            if (!IsAdUnitId(units.Rewarded))
            {
                error = $"{platform} 부활 보상형 광고 단위 ID가 필요합니다.";
                return false;
            }
            if (enablePostRunInterstitial &&
                !IsAdUnitId(units.Interstitial))
            {
                error = $"{platform} 전면 광고 단위 ID가 필요합니다.";
                return false;
            }

            string publisher = PublisherPrefix(appId);
            foreach (string adUnitId in RequiredUnitIds(units))
            {
                if (!string.Equals(
                        publisher,
                        PublisherPrefix(adUnitId),
                        StringComparison.Ordinal))
                {
                    error = $"{platform} 앱 ID와 광고 단위 ID의 게시자 번호가 다릅니다.";
                    return false;
                }
                if (adUnitId.StartsWith(
                        "ca-app-pub-3940256099942544/",
                        StringComparison.Ordinal))
                {
                    error = $"{platform} 운영 설정에 Google 테스트 광고 ID가 들어 있습니다.";
                    return false;
                }
            }

            error = string.Empty;
            return true;

            string[] RequiredUnitIds(GoogleAdUnitSet value)
            {
                if (enableLobbyBanner && enablePostRunInterstitial)
                    return new[]
                    {
                        value.Banner, value.Rewarded, value.Interstitial,
                    };
                if (enableLobbyBanner)
                    return new[] { value.Banner, value.Rewarded };
                if (enablePostRunInterstitial)
                    return new[] { value.Rewarded, value.Interstitial };
                return new[] { value.Rewarded };
            }
        }

        public static bool IsAppId(string value)
        {
            return IsNumericIdentifier(value, '~');
        }

        public static bool IsAdUnitId(string value)
        {
            return IsNumericIdentifier(value, '/');
        }

        public static string PublisherPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string trimmed = value.Trim();
            int separator = trimmed.IndexOfAny(new[] { '~', '/' });
            return separator > 0 ? trimmed[..separator] : string.Empty;
        }

        static bool IsNumericIdentifier(string value, char separator)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string trimmed = value.Trim();
            const string prefix = "ca-app-pub-";
            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            int split = trimmed.IndexOf(separator, prefix.Length);
            if (split <= prefix.Length || split >= trimmed.Length - 1)
                return false;
            for (int i = prefix.Length; i < trimmed.Length; i++)
            {
                if (i == split) continue;
                if (!char.IsDigit(trimmed[i])) return false;
            }
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForTests(
            bool verified,
            bool lobbyBanner,
            bool postRunInterstitial,
            string androidApplicationId,
            GoogleAdUnitSet androidUnits,
            string iosApplicationId,
            GoogleAdUnitSet iosUnits)
        {
            productionConfigurationVerified = verified;
            enableLobbyBanner = lobbyBanner;
            enablePostRunInterstitial = postRunInterstitial;
            androidAppId = androidApplicationId?.Trim() ?? string.Empty;
            iosAppId = iosApplicationId?.Trim() ?? string.Empty;
            android = androidUnits ?? new GoogleAdUnitSet();
            ios = iosUnits ?? new GoogleAdUnitSet();
        }

        public void ConfigureProductionAdMobIds()
        {
            enableLobbyBanner = true;
            enablePostRunInterstitial = false;
            productionConfigurationVerified = true;
            verificationNote =
                "2026-08-26 cysbandcs@gmail.com AdMob 계정에서 먹점프 전용 " +
                "Android·iOS 앱과 배너·보상형 광고 단위를 생성해 확인했습니다. " +
                "스토어 출시 후 각 AdMob 앱을 실제 스토어 목록과 연결해야 합니다.";
            androidAppId = "ca-app-pub-2944517353618559~3718407207";
            iosAppId = "ca-app-pub-2944517353618559~8630103905";
            android ??= new GoogleAdUnitSet();
            ios ??= new GoogleAdUnitSet();
            android.Configure(
                "ca-app-pub-2944517353618559/6947205773",
                "ca-app-pub-2944517353618559/4202000231",
                string.Empty);
            ios.Configure(
                "ca-app-pub-2944517353618559/3377777224",
                "ca-app-pub-2944517353618559/5343691518",
                string.Empty);
        }
#endif
    }
}
