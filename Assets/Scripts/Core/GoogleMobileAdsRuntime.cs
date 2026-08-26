#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace MukJump.Core
{
    /// 네이티브 빌드에서 동의 수집 → SDK 초기화 → 로비 배너와 부활 광고를 연결한다.
    public sealed class GoogleMobileAdsRuntime : MonoBehaviour
    {
        const float BannerRetryDelaySeconds = 20f;

        MukJumpGoogleAdsSettings settings;
        GoogleAdUnitSet units;
        GoogleMobileAdsProvider provider;
        BannerView banner;
        LobbyScreenNavigator lobbyNavigator;
        LobbyOptionsView lobbyOptions;
        bool initialized;
        bool bannerLoaded;
        bool bannerVisible;
        bool bannerLoading;
        double nextBannerLoadTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GoogleMobileAdsRuntime>() == null)
                new GameObject(nameof(GoogleMobileAdsRuntime))
                    .AddComponent<GoogleMobileAdsRuntime>();
        }

        void OnEnable()
        {
            DontDestroyOnLoad(gameObject);
            settings = Resources.Load<MukJumpGoogleAdsSettings>(
                MukJumpGoogleAdsSettings.ResourcePath);
            GoogleAdsPlatform platform = CurrentPlatform();
            bool useTestAds = Debug.isDebugBuild;

            if (useTestAds)
            {
                units = GoogleMobileAdsTestIds.For(platform);
                Debug.Log("먹점프 Google 광고: Development 테스트 ID 사용");
            }
            else if (settings == null)
            {
                Debug.LogError(
                    "먹점프 Google 운영 광고를 시작하지 않습니다. " +
                    "광고 설정 에셋을 찾을 수 없습니다.");
                return;
            }
            else if (!settings.TryValidateProduction(platform, out string error))
            {
                Debug.LogError(
                    $"먹점프 Google 운영 광고를 시작하지 않습니다. {error}");
                return;
            }
            else
            {
                units = settings.UnitsFor(platform);
            }

            // 11.4.0에서 속성은 obsolete이지만 하위 광고 객체의
            // 모든 콜백을 한 번에 Unity 메인 스레드로 보내는 호환 경로다.
#pragma warning disable CS0618
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
#pragma warning restore CS0618
            MobileAds.SetiOSAppPauseOnBackground(true);
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                MaxAdContentRating = MaxAdContentRating.G,
            });
            GoogleMobileAdsPrivacy.Register(
                ShowPrivacyOptions,
                isRequired: false);
            GatherConsent();
        }

        void Update()
        {
            if (!initialized) return;
            provider?.Tick();
            UpdateLobbyBanner();
        }

        void OnDisable()
        {
            HideAndDestroyBanner();
            if (ReferenceEquals(MonetizationAds.Provider, provider))
                MonetizationAds.ResetProvider();
            provider?.Dispose();
            provider = null;
            GoogleMobileAdsPrivacy.Reset();
        }

        static GoogleAdsPlatform CurrentPlatform()
        {
#if UNITY_ANDROID
            return GoogleAdsPlatform.Android;
#else
            return GoogleAdsPlatform.IOS;
#endif
        }

        void GatherConsent()
        {
            ConsentInformation.Update(
                new ConsentRequestParameters
                {
                    TagForUnderAgeOfConsent = false,
                },
                updateError =>
                {
                    UpdatePrivacyRequirement();
                    if (updateError != null)
                    {
                        Debug.LogWarning(
                            $"광고 개인정보 상태 갱신 실패: {updateError.Message}");
                        if (ConsentInformation.CanRequestAds())
                            InitializeAds();
                        return;
                    }

                    ConsentForm.LoadAndShowConsentFormIfRequired(
                        formError =>
                        {
                            UpdatePrivacyRequirement();
                            if (formError != null)
                                Debug.LogWarning(
                                    $"광고 동의 화면 표시 실패: {formError.Message}");
                            if (ConsentInformation.CanRequestAds())
                                InitializeAds();
                            else
                                Debug.Log(
                                    "광고 동의가 없어 이번 실행에서는 광고를 요청하지 않습니다.");
                        });
                });
        }

        void InitializeAds()
        {
            if (initialized) return;
            MobileAds.Initialize(_ =>
            {
                if (!isActiveAndEnabled) return;
                initialized = true;
                provider = new GoogleMobileAdsProvider(
                    units.Rewarded,
                    settings != null && settings.EnablePostRunInterstitial
                        ? units.Interstitial
                        : string.Empty);
                MonetizationAds.RegisterProvider(provider);
                provider.Preload(
                    FullScreenAdPlacement.GameOverReviveReward);
                if (settings != null && settings.EnablePostRunInterstitial)
                    provider.Preload(
                        FullScreenAdPlacement.PostRunInterstitial);
                LoadBannerIfNeeded();
            });
        }

        void UpdateLobbyBanner()
        {
            bool bannerEnabled = settings == null ||
                                 settings.EnableLobbyBanner;
            bool shouldShow = bannerEnabled &&
                              GoogleMobileAdsPresentation
                                  .ShouldShowLobbyBanner(
                                      GameManager.Instance,
                                      ResolveNavigator(),
                                      ResolveOptions());
            if (!shouldShow)
            {
                if (bannerVisible)
                {
                    banner?.Hide();
                    bannerVisible = false;
                }
                return;
            }

            LoadBannerIfNeeded();
            if (bannerLoaded && !bannerVisible)
            {
                banner.Show();
                bannerVisible = true;
            }
        }

        void LoadBannerIfNeeded()
        {
            if (banner != null ||
                bannerLoading ||
                string.IsNullOrWhiteSpace(units?.Banner) ||
                Time.realtimeSinceStartupAsDouble < nextBannerLoadTime)
                return;

            int safeWidth = MobileAds.Utils.GetDeviceSafeWidth();
            if (safeWidth <= 0)
            {
                nextBannerLoadTime =
                    Time.realtimeSinceStartupAsDouble +
                    BannerRetryDelaySeconds;
                return;
            }

            bannerLoading = true;
            AdSize size = AdSize
                .GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(
                    safeWidth);
            banner = new BannerView(units.Banner, size, AdPosition.Top);
            banner.Hide();
            banner.OnBannerAdLoaded += () =>
            {
                bannerLoading = false;
                bannerLoaded = true;
                UpdateLobbyBanner();
            };
            banner.OnBannerAdLoadFailed += error =>
            {
                Debug.LogWarning($"먹점프 로비 배너 로드 실패: {error}");
                HideAndDestroyBanner();
                nextBannerLoadTime =
                    Time.realtimeSinceStartupAsDouble +
                    BannerRetryDelaySeconds;
            };
            banner.LoadAd(new AdRequest());
        }

        LobbyScreenNavigator ResolveNavigator()
        {
            if (lobbyNavigator == null)
                lobbyNavigator = LobbyScreenNavigator.Instance;
            return lobbyNavigator;
        }

        LobbyOptionsView ResolveOptions()
        {
            if (lobbyOptions == null)
                lobbyOptions = FindAnyObjectByType<LobbyOptionsView>();
            return lobbyOptions;
        }

        void HideAndDestroyBanner()
        {
            banner?.Hide();
            banner?.Destroy();
            banner = null;
            bannerLoaded = false;
            bannerLoading = false;
            bannerVisible = false;
        }

        void UpdatePrivacyRequirement()
        {
            GoogleMobileAdsPrivacy.UpdateRequired(
                ConsentInformation.PrivacyOptionsRequirementStatus ==
                PrivacyOptionsRequirementStatus.Required);
        }

        void ShowPrivacyOptions(System.Action<string> onCompleted)
        {
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                UpdatePrivacyRequirement();
                onCompleted?.Invoke(error == null
                    ? "광고 개인정보 선택을 저장했습니다"
                    : $"광고 개인정보 화면 오류 · {error.Message}");
            });
        }
    }
}
#endif
