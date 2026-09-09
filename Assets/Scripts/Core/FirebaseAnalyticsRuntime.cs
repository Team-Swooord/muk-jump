using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR
using Firebase;
using Firebase.Analytics;
#endif

namespace MukJump.Core
{
    /// 네이티브 스토어 전용. 토스·에디터는 Firebase 네이티브 SDK를 실행하지 않는다.
    public sealed class FirebaseAnalyticsRuntime : MonoBehaviour
    {
        bool initializing;
        bool ready;
        bool configurationReady;
        MukJumpAnalyticsSettings settings;
#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR
        FirebaseApp app;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            var root = new GameObject("FirebaseAnalyticsRuntime");
            DontDestroyOnLoad(root);
            root.AddComponent<FirebaseAnalyticsRuntime>();
#endif
        }

        void LoadConfiguration()
        {
            settings = Resources.Load<MukJumpAnalyticsSettings>("MukJump/Settings/MukJumpAnalyticsSettings");
#if UNITY_IOS && !UNITY_EDITOR
            configurationReady = settings != null && settings.iosConfigured;
#elif UNITY_ANDROID && !UNITY_EDITOR
            configurationReady = settings != null && settings.androidConfigured;
#endif
        }

        void OnEnable()
        {
            LoadConfiguration();
            MukJumpAnalyticsPrivacy.Changed += ApplyPrivacy;
            ApplyPrivacy();
        }
        void OnDisable()
        {
            MukJumpAnalyticsPrivacy.Changed -= ApplyPrivacy;
            MukJumpAnalytics.AttachSink(null);
            MukJumpAnalytics.SetCollectionEnabled(false);
#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR
            if (ready)
                try { FirebaseAnalytics.SetAnalyticsCollectionEnabled(false); } catch { }
#endif
        }

        void ApplyPrivacy()
        {
            bool collect = configurationReady &&
                (!Debug.isDebugBuild || settings.allowDevelopmentCollection) &&
                MukJumpAnalyticsPrivacy.HasConsent;
            MukJumpAnalytics.SetCollectionEnabled(collect);
#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR
            if (ready)
            {
                try
                {
                    ApplySdkConsent(collect);
                    if (collect) MukJumpAnalytics.AttachSink(Send);
                    else MukJumpAnalytics.AttachSink(null);
                }
                catch
                {
                    MukJumpAnalytics.SetCollectionEnabled(false);
                    MukJumpAnalytics.AttachSink(null);
                }
            }
            else if (collect && !initializing) Initialize();
#endif
        }

#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR
        async void Initialize()
        {
            initializing = true;
            try
            {
                var status = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (this == null || !isActiveAndEnabled) return;
                if (status != DependencyStatus.Available)
                {
                    MukJumpAnalytics.SetCollectionEnabled(false);
                    return;
                }
                app = FirebaseApp.DefaultInstance;
                ready = true;
                // SDK 초기화 도중 철회됐을 수도 있으므로 최신 선택을 다시 읽는다.
                ApplyPrivacy();
            }
            catch
            {
                MukJumpAnalytics.SetCollectionEnabled(false);
                Debug.LogWarning("먹점프: 분석 초기화를 건너뜁니다. 게임은 계속 사용할 수 있습니다.");
            }
            finally { initializing = false; }
        }

        static void ApplySdkConsent(bool collect)
        {
            FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
            {
                [ConsentType.AnalyticsStorage] = collect ? ConsentStatus.Granted : ConsentStatus.Denied,
                [ConsentType.AdStorage] = ConsentStatus.Denied,
                [ConsentType.AdUserData] = ConsentStatus.Denied,
                [ConsentType.AdPersonalization] = ConsentStatus.Denied,
            });
            FirebaseAnalytics.SetUserId(null);
            FirebaseAnalytics.SetUserProperty("allow_ad_personalization_signals", "false");
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(collect);
            if (!collect) FirebaseAnalytics.ResetAnalyticsData();
        }

        static void Send(MukJumpAnalyticsEvent value)
        {
            var parameters = new List<Parameter>(value.Parameters.Count);
            foreach (var pair in value.Parameters)
            {
                if (pair.Value is string text) parameters.Add(new Parameter(pair.Key, text));
                else if (pair.Value is long integer) parameters.Add(new Parameter(pair.Key, integer));
                else if (pair.Value is double number) parameters.Add(new Parameter(pair.Key, number));
            }
            FirebaseAnalytics.LogEvent(value.Name, parameters.ToArray());
        }
#endif
    }
}
