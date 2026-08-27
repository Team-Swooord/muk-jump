#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
using System.Collections;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

namespace MukJump.Core
{
    /// 네이티브 빌드에서 동의 수집 → SDK 초기화 → 로비 배너와 부활 광고를 연결한다.
    public sealed class GoogleMobileAdsRuntime : MonoBehaviour
    {
        const float BannerRetryDelaySeconds = 20f;
        const float ConsentRetryDelaySeconds = 20f;

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
        bool consentGathering;
        bool trackingAuthorizationResolved;
        double nextBannerLoadTime;
        double nextConsentRetryTime;
        float bannerHeightPixels;

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
                // 로그인만으로 사용자의 연령을 단정하지 않는다. 광고 SDK의
                // 최신 통합 연령 설정을 미지정으로 두고 UMP/스토어 설정에서
                // 실제 대상 연령 정책을 일관되게 적용한다.
                AgeRestrictedTreatment =
                    AgeRestrictedTreatment.Unspecified,
                // ATT 선택과 별개로 모든 요청은 비맞춤형이며, SDK 8.7.0+
                // 기본값인 퍼블리셔 1차 식별자도 사용하지 않는다.
                PublisherFirstPartyIdEnabled = false,
                PublisherPrivacyPersonalizationState =
                    PublisherPrivacyPersonalizationState.Disabled,
            });
            GoogleMobileAdsPrivacy.Register(
                ShowPrivacyOptions,
                isRequired: false);
#if UNITY_IOS
            StartCoroutine(RequestTrackingAuthorizationThenGatherConsent());
#else
            trackingAuthorizationResolved = true;
            GatherConsent();
#endif
        }

        void Update()
        {
            if (!initialized)
            {
                if (trackingAuthorizationResolved &&
                    !consentGathering &&
                    Time.realtimeSinceStartupAsDouble >= nextConsentRetryTime)
                    GatherConsent();
                return;
            }
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
            LobbyAdLayout.ClearTopInset();
            GoogleMobileAdsPrivacy.Reset();
            trackingAuthorizationResolved = false;
        }

        static GoogleAdsPlatform CurrentPlatform()
        {
#if UNITY_ANDROID
            return GoogleAdsPlatform.Android;
#else
            return GoogleAdsPlatform.IOS;
#endif
        }

#if UNITY_IOS
        IEnumerator RequestTrackingAuthorizationThenGatherConsent()
        {
            // Apple은 앱이 활성 상태일 때만 ATT 알림을 표시한다.
            yield return null;
            while (!Application.isFocused)
                yield return null;

            var status = ATTrackingStatusBinding
                .GetAuthorizationTrackingStatus();
            if (status == ATTrackingStatusBinding
                    .AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                ATTrackingStatusBinding.RequestAuthorizationTracking();
                while (ATTrackingStatusBinding
                           .GetAuthorizationTrackingStatus() ==
                       ATTrackingStatusBinding
                           .AuthorizationTrackingStatus.NOT_DETERMINED)
                    yield return null;
            }

            trackingAuthorizationResolved = true;
            GatherConsent();
        }
#endif

        void GatherConsent()
        {
            if (consentGathering || initialized)
                return;
            consentGathering = true;
            ConsentInformation.Update(
                new ConsentRequestParameters(),
                updateError =>
                {
                    UpdatePrivacyRequirement();
                    if (updateError != null)
                    {
                        consentGathering = false;
                        Debug.LogWarning(
                            $"광고 개인정보 상태 갱신 실패: {updateError.Message}");
                        if (ConsentInformation.CanRequestAds())
                            InitializeAds();
                        else
                            nextConsentRetryTime =
                                Time.realtimeSinceStartupAsDouble +
                                ConsentRetryDelaySeconds;
                        return;
                    }

                    ConsentForm.LoadAndShowConsentFormIfRequired(
                        formError =>
                        {
                            consentGathering = false;
                            UpdatePrivacyRequirement();
                            if (formError != null)
                            {
                                Debug.LogWarning(
                                    $"광고 동의 화면 표시 실패: {formError.Message}");
                                nextConsentRetryTime =
                                    Time.realtimeSinceStartupAsDouble +
                                    ConsentRetryDelaySeconds;
                            }
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
                    LobbyAdLayout.ClearTopInset();
                }
                return;
            }

            LoadBannerIfNeeded();
            if (bannerLoaded && !bannerVisible)
            {
                banner.Show();
                bannerVisible = true;
                LobbyAdLayout.SetTopInsetPixels(bannerHeightPixels);
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
                bannerHeightPixels = banner.GetHeightInPixels();
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
            banner.LoadAd(
                GoogleMobileAdsRequestFactory.CreateNonPersonalized());
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
            bannerHeightPixels = 0f;
            LobbyAdLayout.ClearTopInset();
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
