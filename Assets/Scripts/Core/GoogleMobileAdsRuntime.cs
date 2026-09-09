#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
using System;
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
        const float RequestTimeoutSeconds = 30f;
#if MUKJUMP_TEST_ADS
        const bool ForceTestAdsForBuild = true;
#else
        const bool ForceTestAdsForBuild = false;
#endif

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
        bool configurationReady;
        bool sdkConfigured;
        bool initializationInFlight;
        double nextBannerLoadTime;
        double nextConsentRetryTime;
        double consentDeadline;
        double initializationDeadline;
        double bannerLoadDeadline;
        double nextSdkSetupRetryTime;
        float bannerHeightPixels;
        long consentGeneration;
        long initializationGeneration;
        long bannerLoadGeneration;
#if UNITY_IOS
        bool trackingAuthorizationFlowStarted;
        double trackingAuthorizationDeadline;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            LobbyAdLayout.ReserveDefaultTopInset();
            if (FindAnyObjectByType<GoogleMobileAdsRuntime>() == null)
                new GameObject(nameof(GoogleMobileAdsRuntime))
                    .AddComponent<GoogleMobileAdsRuntime>();
        }

        void OnEnable()
        {
            LobbyAdLayout.ReserveDefaultTopInset();
            DontDestroyOnLoad(gameObject);
            configurationReady = TryResolveConfiguration();
            if (!configurationReady)
            {
                LobbyAdLayout.ClearTopInset();
                return;
            }
            TryConfigureSdkAndBeginFlow();
        }

        void Update()
        {
            ProcessRequestWatchdogs();
            if (!configurationReady)
                return;
            if (!sdkConfigured)
            {
                if (Time.realtimeSinceStartupAsDouble >=
                    nextSdkSetupRetryTime)
                    TryConfigureSdkAndBeginFlow();
                return;
            }
#if UNITY_IOS
            if (!trackingAuthorizationResolved &&
                !trackingAuthorizationFlowStarted &&
                Time.realtimeSinceStartupAsDouble >=
                nextSdkSetupRetryTime)
                TryBeginTrackingAuthorizationFlow();
#endif
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
            // 공급자를 폐기한 뒤 initialized가 남으면 재활성화해도 부활
            // 광고를 다시 등록하지 못한다. 새 공급자는 초기화 경로로 만든다.
            initialized = false;
            nextConsentRetryTime = 0d;
            nextSdkSetupRetryTime = 0d;
            consentDeadline = 0d;
            initializationDeadline = 0d;
            consentGeneration++;
            initializationGeneration++;
            bannerLoadGeneration++;
            consentGathering = false;
            initializationInFlight = false;
#if UNITY_IOS
            trackingAuthorizationFlowStarted = false;
            trackingAuthorizationDeadline = 0d;
#endif
            HideAndDestroyBanner();
            try
            {
                if (ReferenceEquals(MonetizationAds.Provider, provider))
                    MonetizationAds.ResetProvider();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 광고 공급자 해제 실패: " + exception.Message);
            }
            try
            {
                provider?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 광고 객체 정리 실패: " + exception.Message);
            }
            provider = null;
            LobbyAdLayout.ClearTopInset();
            try
            {
                GoogleMobileAdsPrivacy.Reset();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 광고 개인정보 경계 해제 실패: " + exception.Message);
            }
            trackingAuthorizationResolved = false;
            sdkConfigured = false;
            configurationReady = false;
        }

        void ProcessRequestWatchdogs()
        {
            double now = Time.realtimeSinceStartupAsDouble;
#if UNITY_IOS
            if (GoogleMobileAdsRuntimePolicy
                .HasTrackingAuthorizationTimedOut(
                    trackingAuthorizationFlowStarted,
                    now,
                    trackingAuthorizationDeadline))
            {
                ResolveTrackingAuthorizationAfterFailure(
                    "먹점프 ATT 응답 시간이 초과되어 비맞춤형 광고 흐름을 계속합니다.");
            }
#endif
            if (consentGathering && now >= consentDeadline)
            {
                consentGathering = false;
                consentGeneration++;
                nextConsentRetryTime = now + ConsentRetryDelaySeconds;
                Debug.LogWarning(
                    "먹점프 광고 동의 응답 시간이 초과되었습니다.");
            }
            if (initializationInFlight && now >= initializationDeadline)
            {
                initializationInFlight = false;
                initializationGeneration++;
                nextConsentRetryTime = now + ConsentRetryDelaySeconds;
                Debug.LogWarning(
                    "먹점프 광고 SDK 초기화 응답 시간이 초과되었습니다.");
            }
            if (bannerLoading && now >= bannerLoadDeadline)
            {
                Debug.LogWarning(
                    "먹점프 로비 배너 로드 시간이 초과되었습니다.");
                ScheduleBannerRetry();
            }
        }

        static GoogleAdsPlatform CurrentPlatform()
        {
#if UNITY_ANDROID
            return GoogleAdsPlatform.Android;
#else
            return GoogleAdsPlatform.IOS;
#endif
        }

        bool TryResolveConfiguration()
        {
            try
            {
                settings = Resources.Load<MukJumpGoogleAdsSettings>(
                    MukJumpGoogleAdsSettings.ResourcePath);
                GoogleAdsPlatform platform = CurrentPlatform();
                bool useTestAds = settings != null
                    ? settings.ShouldUseTestAds(
                        isEditor: false,
                        isDevelopmentBuild: Debug.isDebugBuild,
                        forceTestAds: ForceTestAdsForBuild)
                    : Debug.isDebugBuild || ForceTestAdsForBuild;

                if (useTestAds)
                {
                    units = GoogleMobileAdsTestIds.For(platform);
                    Debug.Log(
                        ForceTestAdsForBuild
                            ? "먹점프 Google 광고: TestFlight QA 공식 테스트 ID 강제 사용"
                            : "먹점프 Google 광고: Development 테스트 ID 사용");
                    return true;
                }
                if (settings == null)
                {
                    Debug.LogError(
                        "먹점프 Google 운영 광고를 시작하지 않습니다. " +
                        "광고 설정 에셋을 찾을 수 없습니다.");
                    return false;
                }
                if (!settings.TryValidateProduction(
                    platform,
                    out string error))
                {
                    Debug.LogError(
                        $"먹점프 Google 운영 광고를 시작하지 않습니다. {error}");
                    return false;
                }

                units = settings.UnitsFor(platform);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "먹점프 Google 광고 설정 확인 실패: " +
                    exception.Message);
                return false;
            }
        }

        void TryConfigureSdkAndBeginFlow()
        {
            if (!configurationReady || !isActiveAndEnabled)
                return;
            try
            {
                // 11.4.0에서 속성은 obsolete이지만 하위 광고 객체의
                // 모든 콜백을 Unity 메인 스레드로 보내는 호환 경로다.
#pragma warning disable CS0618
                MobileAds.RaiseAdEventsOnUnityMainThread = true;
#pragma warning restore CS0618
                MobileAds.SetiOSAppPauseOnBackground(true);
                MobileAds.SetRequestConfiguration(new RequestConfiguration
                {
                    MaxAdContentRating = MaxAdContentRating.G,
                    AgeRestrictedTreatment =
                        AgeRestrictedTreatment.Unspecified,
                    PublisherFirstPartyIdEnabled = false,
                    PublisherPrivacyPersonalizationState =
                        PublisherPrivacyPersonalizationState.Disabled,
                });
                GoogleMobileAdsPrivacy.Register(
                    ShowPrivacyOptions,
                    isRequired: false);
                sdkConfigured = true;
                nextSdkSetupRetryTime = 0d;
            }
            catch (Exception exception)
            {
                sdkConfigured = false;
                nextSdkSetupRetryTime =
                    Time.realtimeSinceStartupAsDouble +
                    ConsentRetryDelaySeconds;
                try
                {
                    GoogleMobileAdsPrivacy.Reset();
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning(
                        "먹점프 광고 SDK 설정 실패 정리 오류: " +
                        cleanupException.Message);
                }
                Debug.LogWarning(
                    "먹점프 광고 SDK 설정 실패, 재시도합니다: " +
                    exception.Message);
                return;
            }

#if UNITY_IOS
            TryBeginTrackingAuthorizationFlow();
#else
            trackingAuthorizationResolved = true;
            GatherConsent();
#endif
        }

#if UNITY_IOS
        void TryBeginTrackingAuthorizationFlow()
        {
            if (!sdkConfigured || trackingAuthorizationResolved ||
                trackingAuthorizationFlowStarted || !isActiveAndEnabled)
                return;
            trackingAuthorizationFlowStarted = true;
            trackingAuthorizationDeadline = 0d;
            try
            {
                StartCoroutine(
                    RequestTrackingAuthorizationThenGatherConsent());
            }
            catch (Exception exception)
            {
                ResolveTrackingAuthorizationAfterFailure(
                    "먹점프 ATT 흐름 시작 실패, 비맞춤형 광고를 계속합니다: " +
                    exception.Message);
            }
        }

        IEnumerator RequestTrackingAuthorizationThenGatherConsent()
        {
            // 초기 focus=true만으로 요청하면 iOS 활성화·브랜드 전환과 경합할 수 있다.
            // 첫 게임 화면이 준비되고 활성 상태가 안정된 뒤 Apple 기본 창만 요청한다.
            // timeScale=0인 첫 안내 중에도 이 대기는 진행한다.
            float readySeconds = 0f;
            while (trackingAuthorizationFlowStarted && isActiveAndEnabled)
            {
                yield return null;
                bool ready = GoogleMobileAdsRuntimePolicy.CanRequestTrackingAuthorization(
                    Application.isFocused,
                    MobileApplicationLifecycle.IsApplicationActive,
                    StartupBrandSplash.IsBlockingInput,
                    GameManager.Instance != null);
                readySeconds = ready ? readySeconds + Time.unscaledDeltaTime : 0f;
                if (readySeconds >= 0.5f) break;
            }
            if (!trackingAuthorizationFlowStarted || !isActiveAndEnabled)
                yield break;

            trackingAuthorizationDeadline =
                Time.realtimeSinceStartupAsDouble + RequestTimeoutSeconds;

            if (!TryGetTrackingAuthorizationStatus(out var status))
                yield break;
            if (status == ATTrackingStatusBinding
                    .AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                try
                {
                    ATTrackingStatusBinding.RequestAuthorizationTracking();
                }
                catch (Exception exception)
                {
                    ResolveTrackingAuthorizationAfterFailure(
                        "먹점프 ATT 요청 실패, 비맞춤형 광고를 계속합니다: " +
                        exception.Message);
                    yield break;
                }

                while (trackingAuthorizationFlowStarted &&
                       isActiveAndEnabled)
                {
                    yield return null;
                    if (!trackingAuthorizationFlowStarted ||
                        !isActiveAndEnabled)
                        yield break;
                    if (!TryGetTrackingAuthorizationStatus(out status))
                        yield break;
                    if (status != ATTrackingStatusBinding
                            .AuthorizationTrackingStatus.NOT_DETERMINED)
                        break;
                }
            }

            if (trackingAuthorizationFlowStarted && isActiveAndEnabled)
                ResolveTrackingAuthorizationAndGatherConsent();
        }

        bool TryGetTrackingAuthorizationStatus(
            out ATTrackingStatusBinding.AuthorizationTrackingStatus status)
        {
            try
            {
                status = ATTrackingStatusBinding
                    .GetAuthorizationTrackingStatus();
                return true;
            }
            catch (Exception exception)
            {
                status = ATTrackingStatusBinding
                    .AuthorizationTrackingStatus.NOT_DETERMINED;
                ResolveTrackingAuthorizationAfterFailure(
                    "먹점프 ATT 상태 확인 실패, 비맞춤형 광고를 계속합니다: " +
                    exception.Message);
                return false;
            }
        }

        void ResolveTrackingAuthorizationAfterFailure(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
                Debug.LogWarning(warning);
            ResolveTrackingAuthorizationAndGatherConsent();
        }

        void ResolveTrackingAuthorizationAndGatherConsent()
        {
            trackingAuthorizationFlowStarted = false;
            trackingAuthorizationDeadline = 0d;
            if (!isActiveAndEnabled || trackingAuthorizationResolved)
                return;
            trackingAuthorizationResolved = true;
            GatherConsent();
        }
#endif

        void GatherConsent()
        {
            if (consentGathering || initialized)
                return;
            consentGathering = true;
            long generation = ++consentGeneration;
            consentDeadline = Time.realtimeSinceStartupAsDouble +
                              RequestTimeoutSeconds;
            try
            {
                ConsentInformation.Update(
                    new ConsentRequestParameters(),
                    updateError =>
                    {
                        if (!IsCurrentConsentRequest(generation))
                            return;
                        try
                        {
                            UpdatePrivacyRequirement();
                            if (updateError != null)
                            {
                                FinishConsentRequest(generation);
                                Debug.LogWarning(
                                    $"광고 개인정보 상태 갱신 실패: {updateError.Message}");
                                if (CanRequestAdsSafely())
                                    InitializeAds();
                                else
                                    ScheduleConsentRetry();
                                return;
                            }

                            consentDeadline =
                                Time.realtimeSinceStartupAsDouble +
                                RequestTimeoutSeconds;
                            try
                            {
                                ConsentForm.LoadAndShowConsentFormIfRequired(
                                    formError => HandleConsentFormResult(
                                        generation,
                                        formError));
                            }
                            catch (Exception exception)
                            {
                                FailConsentRequest(
                                    generation,
                                    "광고 동의 화면 요청 실패",
                                    exception);
                            }
                        }
                        catch (Exception exception)
                        {
                            FailConsentRequest(
                                generation,
                                "광고 개인정보 결과 처리 실패",
                                exception);
                        }
                    });
            }
            catch (Exception exception)
            {
                FailConsentRequest(
                    generation,
                    "광고 개인정보 상태 요청 실패",
                    exception);
            }
        }

        bool IsCurrentConsentRequest(long generation) =>
            isActiveAndEnabled && consentGathering &&
            generation == consentGeneration;

        void HandleConsentFormResult(long generation, FormError formError)
        {
            if (!IsCurrentConsentRequest(generation))
                return;
            try
            {
                UpdatePrivacyRequirement();
                FinishConsentRequest(generation);
                if (formError != null)
                {
                    Debug.LogWarning(
                        $"광고 동의 화면 표시 실패: {formError.Message}");
                    ScheduleConsentRetry();
                }
                if (CanRequestAdsSafely())
                    InitializeAds();
                else
                    Debug.Log(
                        "광고 동의가 없어 이번 실행에서는 광고를 요청하지 않습니다.");
            }
            catch (Exception exception)
            {
                FailConsentRequest(
                    generation,
                    "광고 동의 화면 결과 처리 실패",
                    exception);
            }
        }

        void FinishConsentRequest(long generation)
        {
            if (generation != consentGeneration)
                return;
            consentGathering = false;
            consentGeneration++;
            consentDeadline = 0d;
        }

        void FailConsentRequest(
            long generation,
            string context,
            Exception exception)
        {
            if (generation != consentGeneration || !consentGathering)
                return;
            consentGathering = false;
            consentGeneration++;
            consentDeadline = 0d;
            ScheduleConsentRetry();
            Debug.LogWarning($"{context}: {exception.Message}");
        }

        void ScheduleConsentRetry()
        {
            nextConsentRetryTime = Time.realtimeSinceStartupAsDouble +
                                   ConsentRetryDelaySeconds;
        }

        bool CanRequestAdsSafely()
        {
            try
            {
                return ConsentInformation.CanRequestAds();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "광고 요청 가능 상태 확인 실패: " + exception.Message);
                return false;
            }
        }

        void InitializeAds()
        {
            if (initialized || initializationInFlight)
                return;
            initializationInFlight = true;
            long generation = ++initializationGeneration;
            initializationDeadline = Time.realtimeSinceStartupAsDouble +
                                     RequestTimeoutSeconds;
            try
            {
                MobileAds.Initialize(_ =>
                    HandleAdsInitialized(generation));
            }
            catch (Exception exception)
            {
                FailAdsInitialization(
                    generation,
                    "광고 SDK 초기화 요청 실패",
                    exception);
            }
        }

        void HandleAdsInitialized(long generation)
        {
            if (!isActiveAndEnabled || !initializationInFlight ||
                generation != initializationGeneration)
                return;

            initializationInFlight = false;
            initializationGeneration++;
            initializationDeadline = 0d;
            try
            {
                var newProvider = new GoogleMobileAdsProvider(
                    units.Rewarded,
                    settings != null && settings.EnablePostRunInterstitial
                        ? units.Interstitial
                        : string.Empty);
                provider = newProvider;
                MonetizationAds.RegisterProvider(newProvider);
                initialized = true;
                newProvider.Preload(
                    FullScreenAdPlacement.GameOverReviveReward);
                if (settings != null && settings.EnablePostRunInterstitial)
                    newProvider.Preload(
                        FullScreenAdPlacement.PostRunInterstitial);
                LoadBannerIfNeeded();
            }
            catch (Exception exception)
            {
                initialized = false;
                try
                {
                    if (ReferenceEquals(MonetizationAds.Provider, provider))
                        MonetizationAds.ResetProvider();
                    provider?.Dispose();
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning(
                        "광고 SDK 초기화 실패 정리 오류: " +
                        cleanupException.Message);
                }
                provider = null;
                ScheduleConsentRetry();
                Debug.LogWarning(
                    "광고 SDK 초기화 결과 처리 실패: " + exception.Message);
            }
        }

        void FailAdsInitialization(
            long generation,
            string context,
            Exception exception)
        {
            if (generation != initializationGeneration ||
                !initializationInFlight)
                return;
            initializationInFlight = false;
            initializationGeneration++;
            initializationDeadline = 0d;
            ScheduleConsentRetry();
            Debug.LogWarning($"{context}: {exception.Message}");
        }

        void UpdateLobbyBanner()
        {
            bool bannerEnabled = settings == null ||
                                 settings.EnableLobbyBanner;
            bool shouldShow = bannerEnabled &&
                              GoogleMobileAdsPresentation
                                  .ShouldShowTopBanner(
                                      GameManager.Instance,
                                      ResolveNavigator(),
                                      ResolveOptions());
            if (!shouldShow)
            {
                if (bannerVisible)
                {
                    bannerVisible = false;
                    LobbyAdLayout.MarkBannerHidden();
                    try
                    {
                        banner?.Hide();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "먹점프 로비 배너 숨김 실패: " + exception.Message);
                        ScheduleBannerRetry();
                    }
                }
                return;
            }

            LoadBannerIfNeeded();
            if (bannerLoaded && !bannerVisible)
            {
                try
                {
                    banner.Show();
                    bannerVisible = true;
                    LobbyAdLayout.SetTopInsetPixels(bannerHeightPixels);
                    LobbyAdLayout.MarkBannerVisible();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "먹점프 로비 배너 표시 실패: " + exception.Message);
                    ScheduleBannerRetry();
                }
            }
        }

        void LoadBannerIfNeeded()
        {
            if (banner != null ||
                bannerLoading ||
                string.IsNullOrWhiteSpace(units?.Banner) ||
                Time.realtimeSinceStartupAsDouble < nextBannerLoadTime)
                return;

            int safeWidth;
            try
            {
                safeWidth = MobileAds.Utils.GetDeviceSafeWidth();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 로비 배너 안전 폭 확인 실패: " + exception.Message);
                nextBannerLoadTime =
                    Time.realtimeSinceStartupAsDouble +
                    BannerRetryDelaySeconds;
                return;
            }
            if (safeWidth <= 0)
            {
                nextBannerLoadTime =
                    Time.realtimeSinceStartupAsDouble +
                    BannerRetryDelaySeconds;
                return;
            }

            bannerLoading = true;
            bannerLoadDeadline = Time.realtimeSinceStartupAsDouble +
                                 RequestTimeoutSeconds;
            long generation = ++bannerLoadGeneration;
            try
            {
                AdSize size = AdSize
                    .GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(
                        safeWidth);
                var created = new BannerView(
                    units.Banner,
                    size,
                    AdPosition.Top);
                banner = created;
                try
                {
                    created.Hide();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "먹점프 로비 배너 초기 숨김 실패: " +
                        exception.Message);
                }
                created.OnBannerAdLoaded += () =>
                    HandleBannerLoaded(generation, created);
                created.OnBannerAdLoadFailed += error =>
                    HandleBannerLoadFailed(generation, created, error);
                MukJumpAnalytics.Ad(AnalyticsAdStage.LoadRequested, banner: true);
                created.LoadAd(
                    GoogleMobileAdsRequestFactory.CreateNonPersonalized());
            }
            catch (Exception exception)
            {
                if (generation != bannerLoadGeneration)
                    return;
                Debug.LogWarning(
                    "먹점프 로비 배너 로드 요청 실패: " + exception.Message);
                ScheduleBannerRetry();
            }
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
            bannerLoadGeneration++;
            BannerView captured = banner;
            banner = null;
            bannerLoaded = false;
            bannerLoading = false;
            bannerVisible = false;
            bannerLoadDeadline = 0d;
            bannerHeightPixels = 0f;
            LobbyAdLayout.MarkBannerHidden();
            SafeHideAndDestroyBanner(captured);
        }

        void HandleBannerLoaded(long generation, BannerView expectedBanner)
        {
            if (!isActiveAndEnabled || generation != bannerLoadGeneration ||
                !bannerLoading || !ReferenceEquals(banner, expectedBanner))
                return;
            try
            {
                float height = expectedBanner.GetHeightInPixels();
                if (float.IsNaN(height) || float.IsInfinity(height) ||
                    height <= 0f)
                    throw new InvalidOperationException(
                        "배너 높이가 올바르지 않습니다");
                bannerLoading = false;
                bannerLoadGeneration++;
                bannerLoadDeadline = 0d;
                bannerLoaded = true;
                MukJumpAnalytics.Ad(AnalyticsAdStage.Loaded, banner: true);
                bannerHeightPixels = height;
                nextBannerLoadTime = 0d;
                UpdateLobbyBanner();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 로비 배너 로드 결과 처리 실패: " +
                    exception.Message);
                ScheduleBannerRetry();
            }
        }

        void HandleBannerLoadFailed(
            long generation,
            BannerView expectedBanner,
            LoadAdError error)
        {
            if (generation != bannerLoadGeneration || !bannerLoading ||
                !ReferenceEquals(banner, expectedBanner))
                return;
            Debug.LogWarning($"먹점프 로비 배너 로드 실패: {error}");
            MukJumpAnalytics.Ad(AnalyticsAdStage.LoadFailed, banner: true);
            ScheduleBannerRetry();
        }

        void ScheduleBannerRetry()
        {
            bannerLoadGeneration++;
            BannerView captured = banner;
            banner = null;
            bannerLoaded = false;
            bannerLoading = false;
            bannerVisible = false;
            bannerLoadDeadline = 0d;
            bannerHeightPixels = 0f;
            nextBannerLoadTime = Time.realtimeSinceStartupAsDouble +
                                 BannerRetryDelaySeconds;
            LobbyAdLayout.MarkBannerHidden();
            SafeHideAndDestroyBanner(captured);
        }

        static void SafeHideAndDestroyBanner(BannerView target)
        {
            if (target == null)
                return;
            try
            {
                target.Hide();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 로비 배너 정리 중 숨김 실패: " +
                    exception.Message);
            }
            try
            {
                target.Destroy();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 로비 배너 폐기 실패: " + exception.Message);
            }
        }

        void UpdatePrivacyRequirement()
        {
            GoogleMobileAdsPrivacy.UpdateRequired(
                ConsentInformation.PrivacyOptionsRequirementStatus ==
                PrivacyOptionsRequirementStatus.Required);
        }

        void ShowPrivacyOptions(Action<string> onCompleted)
        {
            bool completionSent = false;
            void Complete(string message)
            {
                if (completionSent)
                    return;
                completionSent = true;
                try
                {
                    onCompleted?.Invoke(message);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "광고 개인정보 완료 처리 실패: " +
                        exception.Message);
                }
            }

            try
            {
                ConsentForm.ShowPrivacyOptionsForm(error =>
                {
                    try
                    {
                        UpdatePrivacyRequirement();
                        Complete(error == null
                            ? "광고 개인정보 선택을 저장했습니다"
                            : $"광고 개인정보 화면 오류 · {error.Message}");
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "광고 개인정보 결과 처리 실패: " +
                            exception.Message);
                        Complete("광고 개인정보 화면을 완료하지 못했습니다");
                    }
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "광고 개인정보 화면 요청 실패: " + exception.Message);
                Complete("광고 개인정보 화면을 열지 못했습니다");
            }
        }
    }
}
#endif

namespace MukJump.Core
{
    /// 네이티브 SDK가 없는 에디터에서도 ATT timeout 계약을 검증하기 위한 순수 정책이다.
    public static class GoogleMobileAdsRuntimePolicy
    {
        public static bool CanRequestTrackingAuthorization(
            bool focused, bool applicationActive, bool startupBlocking, bool mainReady) =>
            focused && applicationActive && !startupBlocking && mainReady;

        public static bool HasTrackingAuthorizationTimedOut(
            bool requestInFlight,
            double now,
            double deadline) =>
            requestInFlight && deadline > 0d && now >= deadline;
    }
}
