using System;
using System.IO;
using UnityEngine;

namespace MukJump.Core
{
    // 출시 전 TestFlight에만 포함되는 빌드별 초기화. 서버 삭제는 콘솔에서 별도로 한다.
    public static class PrereleasePlayerReset
    {
        public const string BuildDefine = "MUKJUMP_PRERELEASE_RESET";
        public const string StampFileName = "MukJumpPrereleaseReset.build";
        public const string PlistKey = "MukJumpPrereleaseReset";

        public interface IStore
        {
            string ReadStamp();
            void ClearLocalData();
            void SaveStamp(string stamp);
        }

        public static bool TryReset(string build, IStore store, out Exception failure)
        {
            failure = null;
            try
            {
                if (!int.TryParse(build, out int number) || number <= 0)
                    throw new InvalidOperationException("초기화 빌드 번호가 유효하지 않습니다.");
                string stamp = "v1:" + number;
                if (store.ReadStamp() == stamp) return true;
                // 삭제가 중간에 실패하면 완료 표식을 남기지 않는다. 다음 실행에서 재시도한다.
                store.ClearLocalData();
                store.SaveStamp(stamp);
                if (store.ReadStamp() != stamp)
                    throw new IOException("초기화 완료 표식을 저장하지 못했습니다.");
                return true;
            }
            catch (Exception exception)
            {
                failure = exception;
                return false;
            }
        }

        public static bool EnsureReady()
        {
#if UNITY_IOS && !UNITY_EDITOR && MUKJUMP_PRERELEASE_RESET
            if (ready) return true;
            try
            {
                string build = MJPrereleaseResetBuildNumber().ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                ready = TryReset(build, new DeviceStore(), out Exception failure);
                if (!ready) Debug.LogError("[MukJump] 테스트 데이터 초기화 실패: " + failure.GetType().Name);
                return ready;
            }
            catch (Exception exception)
            {
                Debug.LogError("[MukJump] 테스트 초기화 시작 실패: " + exception.GetType().Name);
                return false;
            }
#else
            return true;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR && MUKJUMP_PRERELEASE_RESET
        static bool ready;

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int MJPrereleaseResetBuildNumber();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.I1)]
        static extern bool MJPrereleaseResetNativePreferences();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void BeforeAnyAccountLoad()
        {
            ready = false;
            EnsureReady();
        }

        sealed class DeviceStore : IStore
        {
            string StampPath => Path.Combine(Application.persistentDataPath, StampFileName);
            public string ReadStamp() => File.Exists(StampPath) ? File.ReadAllText(StampPath) : string.Empty;
            public void ClearLocalData()
            {
                // PlayerPrefs에는 모든 계정의 프로필·백업·미전송 기록이 들어 있다.
                // 뒤끝 인증/게스트 자격은 별도 backend.dat이므로 SDK 초기화 전에 함께 지운다.
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                string backendFile = Path.Combine(Application.persistentDataPath, "backend.dat");
                File.Delete(backendFile);
                if (File.Exists(backendFile)) throw new IOException("뒤끝 기기 저장 삭제 실패");
                if (!MJPrereleaseResetNativePreferences())
                    throw new IOException("네이티브 게임 대기 기록 삭제 실패");
            }
            public void SaveStamp(string stamp)
            {
                // PlayerPrefs 캐시의 읽기 성공을 디스크 저장 성공으로 오인하지 않는다.
                using var file = new FileStream(StampPath, FileMode.Create, FileAccess.Write);
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(stamp);
                file.Write(bytes, 0, bytes.Length);
                file.Flush(true);
            }
        }
#endif
    }
}
