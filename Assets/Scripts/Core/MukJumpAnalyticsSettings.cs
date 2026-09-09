using System;
using UnityEngine;

namespace MukJump.Core
{
    /// Firebase 설정 자체는 원본 파일에 둔다. 런타임에는 유효성 결과만 포함한다.
    public sealed class MukJumpAnalyticsSettings : ScriptableObject
    {
        public bool iosConfigured;
        public bool androidConfigured;
        public bool allowDevelopmentCollection;
    }

    public static class MukJumpAnalyticsPrivacy
    {
        public const string ConsentKey = "MukJump.Analytics.Consent.v1";
        public static event Action Changed;
        public static bool HasConsent => PlayerPrefs.GetInt(ConsentKey, 0) == 1;

        public static bool TrySetConsent(bool granted)
        {
            // 거부는 저장 실패 때도 현재 세션에서 즉시 적용한다.
            if (!granted) MukJumpAnalytics.SetCollectionEnabled(false);
            int previous = PlayerPrefs.GetInt(ConsentKey, 0);
            try
            {
                PlayerPrefs.SetInt(ConsentKey, granted ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch
            {
                PlayerPrefs.SetInt(ConsentKey, granted ? previous : 0);
                return false;
            }
            if (Changed != null)
                foreach (Action callback in Changed.GetInvocationList())
                    try { callback(); } catch { }
            return true;
        }
    }
}
