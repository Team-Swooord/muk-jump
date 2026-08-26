using System;

namespace MukJump.Core
{
    /// 옵션 UI가 Google UMP 형식에 직접 의존하지 않도록 개인정보 선택 화면을 중계한다.
    public static class GoogleMobileAdsPrivacy
    {
        static Action<Action<string>> showOptions;

        public static bool IsAvailable => showOptions != null;
        public static bool IsRequired { get; private set; }

        public static void ShowOptions(Action<string> onCompleted)
        {
            if (showOptions == null)
            {
                onCompleted?.Invoke(
                    "현재 플랫폼에서는 광고 개인정보 선택이 필요하지 않습니다");
                return;
            }
            showOptions(onCompleted);
        }

        internal static void Register(
            Action<Action<string>> handler,
            bool isRequired)
        {
            showOptions = handler;
            IsRequired = isRequired;
        }

        internal static void UpdateRequired(bool isRequired)
        {
            IsRequired = isRequired;
        }

        internal static void Reset()
        {
            showOptions = null;
            IsRequired = false;
        }
    }
}
