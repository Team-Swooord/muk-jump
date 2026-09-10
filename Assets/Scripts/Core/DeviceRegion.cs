using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace MukJump.Core
{
    /// 실제 국적이나 위치가 아닌 기기의 국가·지역. 언어 선택과 독립적이다.
    public static class DeviceRegion
    {
        public const string Column = "deviceRegion";
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void MukJumpCopyDeviceRegion(StringBuilder buffer, int capacity);
#endif
        public static string Current
        {
            get
            {
                try
                {
#if UNITY_IOS && !UNITY_EDITOR
                    var buffer = new StringBuilder(8);
                    MukJumpCopyDeviceRegion(buffer, buffer.Capacity);
                    return Normalize(buffer.ToString());
#elif UNITY_ANDROID && !UNITY_EDITOR
                    using var localeClass = new AndroidJavaClass("java.util.Locale");
                    using var locale = localeClass.CallStatic<AndroidJavaObject>("getDefault");
                    return Normalize(locale.Call<string>("getCountry"));
#elif UNITY_WEBGL && !UNITY_EDITOR
                    return string.Empty;
#else
                    return Normalize(RegionInfo.CurrentRegion.TwoLetterISORegionName);
#endif
                }
                catch (Exception) { return string.Empty; }
            }
        }

        public static string Normalize(string value)
        {
            string code = value?.Trim().ToUpperInvariant() ?? string.Empty;
            if (code.Length != 2 || code[0] < 'A' || code[0] > 'Z' ||
                code[1] < 'A' || code[1] > 'Z') return string.Empty;
            return code;
        }

        public static string IconResource(string region)
        {
            string code = Normalize(region);
            if (code.Length != 2) return "MukJump/RegionFlags/1f310";
            return "MukJump/RegionFlags/" + (0x1f1e6 + code[0] - 'A').ToString("x") +
                "-" + (0x1f1e6 + code[1] - 'A').ToString("x");
        }
    }
}
