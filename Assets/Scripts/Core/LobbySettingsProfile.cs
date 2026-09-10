using System;
using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Core
{
    public readonly struct LobbyCloudSettingsSnapshot
    {
        public LobbyCloudSettingsSnapshot(
            float bgmVolume,
            float sfxVolume,
            float bgmResumeVolume,
            float sfxResumeVolume,
            int gameplayTutorialVersion,
            bool tutorialSeen)
        {
            BgmVolume = bgmVolume;
            SfxVolume = sfxVolume;
            BgmResumeVolume = bgmResumeVolume;
            SfxResumeVolume = sfxResumeVolume;
            GameplayTutorialVersion = gameplayTutorialVersion;
            TutorialSeen = tutorialSeen;
        }

        public float BgmVolume { get; }
        public float SfxVolume { get; }
        public float BgmResumeVolume { get; }
        public float SfxResumeVolume { get; }
        public int GameplayTutorialVersion { get; }
        public bool TutorialSeen { get; }
    }

    public interface ILobbySettingsStore
    {
        float GetFloat(string key, float fallback);
        void SetFloat(string key, float value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void Save();
    }

    sealed class PlayerPrefsLobbySettingsStore : ILobbySettingsStore
    {
        public float GetFloat(string key, float fallback) =>
            PlayerPrefs.GetFloat(key, fallback);
        public void SetFloat(string key, float value) =>
            PlayerPrefs.SetFloat(key, value);
        public string GetString(string key, string fallback) =>
            PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) =>
            PlayerPrefs.SetString(key, value);
        public int GetInt(string key, int fallback) =>
            PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) =>
            PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    /// 로비 옵션에서 바꾸는 소리·튜토리얼·로컬 플레이어 식별자를 저장한다.
    /// 외부 계정이나 인증 정보는 이 프로필에 저장하지 않는다.
    public static class LobbySettingsProfile
    {
        public const int CurrentGameplayTutorialVersion = 5;

        const string BgmVolumeKey = "MukJump.Settings.BgmVolume";
        const string SfxVolumeKey = "MukJump.Settings.SfxVolume";
        const string BgmResumeVolumeKey = "MukJump.Settings.BgmResumeVolume";
        const string SfxResumeVolumeKey = "MukJump.Settings.SfxResumeVolume";
        const string TutorialSeenKey = "MukJump.Settings.TutorialSeen";
        const string GameplayTutorialVersionKey =
            "MukJump.Settings.GameplayTutorialVersion";
        const string PlayerUidKey = "MukJump.Settings.PlayerUid";
        const string LanguageKey = "MukJump.Settings.Language";
        const string HapticsEnabledKey = "MukJump.Settings.HapticsEnabled";
        const string ReducedMotionEnabledKey =
            "MukJump.Settings.ReducedMotionEnabled";

        static ILobbySettingsStore store = new PlayerPrefsLobbySettingsStore();
        static bool loaded;
        // 완료 저장과 별개인 실행 단위 가드. 씬 재로드·계정 변경으로 초기화하지 않는다.
        static bool gameplayStartedThisSession;
        static float bgmVolume;
        static float sfxVolume;
        static float bgmResumeVolume;
        static float sfxResumeVolume;
        static bool tutorialSeen;
        static int gameplayTutorialVersion;
        static string playerUid;
        static GameLanguage language;

        public static GameLanguage Language { get { EnsureLoaded(); return language; } }

        public static GameLanguage DetectLanguage(SystemLanguage systemLanguage) => systemLanguage switch
        {
            SystemLanguage.Korean => GameLanguage.Korean,
            SystemLanguage.Japanese => GameLanguage.Japanese,
            _ => GameLanguage.English
        };

        static string LanguageCode(GameLanguage value) => value == GameLanguage.Japanese ? "ja"
            : value == GameLanguage.English ? "en" : "ko";

        // 언어는 기기별 선택이다. 클라우드 설정 적용·계정 전환 때 덮어쓰지 않는다.
        public static bool TrySetLanguage(GameLanguage next)
        {
            try { EnsureLoaded(); }
            catch (Exception exception)
            {
                Debug.LogWarning("[MukJump] 언어 설정 읽기 실패: " + exception.Message);
                return false;
            }
            if (next != GameLanguage.Korean && next != GameLanguage.English && next != GameLanguage.Japanese) return false;
            if (language == next)
            {
                GameLocalization.NotifyChanged();
                return true;
            }
            string previous = LanguageCode(language);
            try
            {
                store.SetString(LanguageKey, LanguageCode(next));
                store.Save();
            }
            catch (Exception exception)
            {
                try { store.SetString(LanguageKey, previous); }
                catch (Exception rollbackException)
                {
                    Debug.LogWarning("[MukJump] 언어 선택 복원 실패: " + rollbackException.Message);
                }
                Debug.LogWarning("[MukJump] 언어 선택을 저장하지 못했습니다: " + exception.Message);
                return false;
            }
            language = next;
            GameLocalization.NotifyChanged();
            return true;
        }
        static bool hapticsEnabled;
        static bool reducedMotionEnabled;

        public static event Action Changed;

        public static float BgmVolume
        {
            get
            {
                EnsureLoaded();
                return bgmVolume;
            }
        }

        public static float SfxVolume
        {
            get
            {
                EnsureLoaded();
                return sfxVolume;
            }
        }

        public static float BgmResumeVolume
        {
            get
            {
                EnsureLoaded();
                return bgmResumeVolume;
            }
        }

        public static float SfxResumeVolume
        {
            get
            {
                EnsureLoaded();
                return sfxResumeVolume;
            }
        }

        public static bool TutorialSeen
        {
            get
            {
                EnsureLoaded();
                return tutorialSeen;
            }
        }

        public static int GameplayTutorialVersion
        {
            get
            {
                EnsureLoaded();
                return gameplayTutorialVersion;
            }
        }

        /// 게임플레이 튜토리얼은 설치 프로필 기준으로 최초 한 번만 실행한다.
        /// 안내 문구 버전이 바뀌어도 완료 사용자를 다시 게임으로 강제 진입시키지 않는다.
        public static bool NeedsGameplayTutorial
        {
            get
            {
                try
                {
                    EnsureLoaded();
                    return gameplayTutorialVersion <= 0;
                }
                catch (Exception exception)
                {
                    // 옵션 저장 읽기 실패가 코어 플레이 진입까지 막아서는 안 된다.
                    loaded = false;
                    Debug.LogWarning(
                        $"[MukJump] 튜토리얼 설정을 읽지 못해 이번 세션은 안내 없이 시작합니다: {exception.Message}");
                    return false;
                }
            }
        }

        /// 완료하지 않은 새 설치는 로비를 거치지 않고 첫 게임 안내로 들어간다.
        public static bool ShouldAutoStartGameplayTutorial =>
            !gameplayStartedThisSession && NeedsGameplayTutorial;

        internal static void MarkGameplayStartedThisSession() =>
            gameplayStartedThisSession = true;

        public static string PlayerUid
        {
            get
            {
                EnsureLoaded();
                return playerUid;
            }
        }

        /// 햅틱은 기기별 접근성 선택이므로 계정 클라우드 저장과 분리한다.
        public static bool HapticsEnabled
        {
            get
            {
                EnsureLoaded();
                return hapticsEnabled;
            }
        }

        /// 카메라와 큰 UI 이동 연출을 줄이는 기기별 접근성 선택이다.
        public static bool ReducedMotionEnabled
        {
            get
            {
                EnsureLoaded();
                return reducedMotionEnabled;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            gameplayStartedThisSession = false;
            store = new PlayerPrefsLobbySettingsStore();
            loaded = false;
            Changed = null;
        }

        public static void SetBgmVolume(float value)
        {
            EnsureLoaded();
            float next = NormalizeVolume(value, bgmVolume);
            if (Mathf.Approximately(bgmVolume, next)) return;
            bgmVolume = next;
            store.SetFloat(BgmVolumeKey, bgmVolume);
            if (next > 0.01f)
            {
                bgmResumeVolume = next;
                store.SetFloat(BgmResumeVolumeKey, bgmResumeVolume);
            }
            NotifyChangedSafely();
        }

        public static void SetSfxVolume(float value)
        {
            EnsureLoaded();
            float next = NormalizeVolume(value, sfxVolume);
            if (Mathf.Approximately(sfxVolume, next)) return;
            sfxVolume = next;
            store.SetFloat(SfxVolumeKey, sfxVolume);
            if (next > 0.01f)
            {
                sfxResumeVolume = next;
                store.SetFloat(SfxResumeVolumeKey, sfxResumeVolume);
            }
            NotifyChangedSafely();
        }

        public static void SetHapticsEnabled(bool enabled)
        {
            EnsureLoaded();
            if (hapticsEnabled == enabled) return;
            hapticsEnabled = enabled;
            store.SetInt(HapticsEnabledKey, enabled ? 1 : 0);
            TryFlush();
            NotifyChangedSafely();
        }

        public static void SetReducedMotionEnabled(bool enabled)
        {
            EnsureLoaded();
            if (reducedMotionEnabled == enabled) return;
            reducedMotionEnabled = enabled;
            store.SetInt(ReducedMotionEnabledKey, enabled ? 1 : 0);
            TryFlush();
            NotifyChangedSafely();
        }

        public static void MarkTutorialSeen()
        {
            EnsureLoaded();
            if (tutorialSeen) return;
            tutorialSeen = true;
            store.SetInt(TutorialSeenKey, 1);
            TryFlush();
            NotifyChangedSafely();
        }

        /// 완료 또는 확인된 건너뛰기만 새 안내 버전을 기록한다.
        /// 저장 실패여도 현재 판은 계속하고 다음 실행에서 다시 안내한다.
        public static bool TryMarkGameplayTutorialCompleted()
        {
            try
            {
                EnsureLoaded();
                bool changed = gameplayTutorialVersion <= 0 ||
                               !tutorialSeen;
                gameplayTutorialVersion = CurrentGameplayTutorialVersion;
                tutorialSeen = true;
                store.SetInt(
                    GameplayTutorialVersionKey,
                    gameplayTutorialVersion);
                store.SetInt(TutorialSeenKey, 1);
                store.Save();
                if (changed)
                    NotifyChangedSafely();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MukJump] 튜토리얼 완료 상태를 저장하지 못했습니다: {exception.Message}");
                return false;
            }
        }

        public static void Flush()
        {
            TryFlush();
        }

        /// 옵션 내구 저장 실패가 로비 입력이나 화면 전환을 막지 않게 한다.
        /// 값은 저장소에 이미 기록돼 있으므로 다음 Flush에서 다시 시도한다.
        public static bool TryFlush()
        {
            try
            {
                EnsureLoaded();
                store.Save();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 로비 설정을 저장하지 못해 다음 기회에 " +
                    "다시 시도합니다: " + exception.Message);
                return false;
            }
        }

        /// 같은 계정의 서버 설정을 기기에 적용한다. 알려지지 않은 미래
        /// 튜토리얼 버전은 현재 버전까지만 인정한다.
        public static void ApplyCloudSettings(
            float cloudBgmVolume,
            float cloudSfxVolume,
            int cloudTutorialVersion)
        {
            EnsureLoaded();
            float nextBgm = NormalizeVolume(cloudBgmVolume, bgmVolume);
            float nextSfx = NormalizeVolume(cloudSfxVolume, sfxVolume);

            bgmVolume = nextBgm;
            sfxVolume = nextSfx;
            if (nextBgm > 0.01f)
                bgmResumeVolume = nextBgm;
            if (nextSfx > 0.01f)
                sfxResumeVolume = nextSfx;
            // 첫 안내는 설치 단위다. 계정의 0/완료 값으로 생략하거나 다시 열지 않는다.

            store.SetFloat(BgmVolumeKey, bgmVolume);
            store.SetFloat(SfxVolumeKey, sfxVolume);
            store.SetFloat(BgmResumeVolumeKey, bgmResumeVolume);
            store.SetFloat(SfxResumeVolumeKey, sfxResumeVolume);
            store.SetInt(GameplayTutorialVersionKey, gameplayTutorialVersion);
            store.SetInt(TutorialSeenKey, tutorialSeen ? 1 : 0);
            store.Save();
            NotifyChangedSafely();
        }

        public static LobbyCloudSettingsSnapshot CaptureCloudSettings()
        {
            EnsureLoaded();
            return new LobbyCloudSettingsSnapshot(
                bgmVolume,
                sfxVolume,
                bgmResumeVolume,
                sfxResumeVolume,
                gameplayTutorialVersion,
                tutorialSeen);
        }

        /// 다중 저장소 계정 동기화가 중간 실패했을 때 클라우드가 관리하는
        /// 설정만 정확히 되돌린다. 햅틱·감소 모션·로컬 UID는 건드리지 않는다.
        public static bool TryRestoreCloudSettings(
            LobbyCloudSettingsSnapshot snapshot)
        {
            EnsureLoaded();
            bgmVolume = NormalizeVolume(snapshot.BgmVolume, bgmVolume);
            sfxVolume = NormalizeVolume(snapshot.SfxVolume, sfxVolume);
            bgmResumeVolume = Mathf.Clamp(
                NormalizeVolume(snapshot.BgmResumeVolume, bgmResumeVolume),
                0.1f,
                1f);
            sfxResumeVolume = Mathf.Clamp(
                NormalizeVolume(snapshot.SfxResumeVolume, sfxResumeVolume),
                0.1f,
                1f);
            // 계정 rollback 중 완료한 기기 튜토리얼도 되감지 않는다.

            try
            {
                store.SetFloat(BgmVolumeKey, bgmVolume);
                store.SetFloat(SfxVolumeKey, sfxVolume);
                store.SetFloat(BgmResumeVolumeKey, bgmResumeVolume);
                store.SetFloat(SfxResumeVolumeKey, sfxResumeVolume);
                store.SetInt(
                    GameplayTutorialVersionKey,
                    gameplayTutorialVersion);
                store.SetInt(TutorialSeenKey, tutorialSeen ? 1 : 0);
                store.Save();
                NotifyChangedSafely();
                return true;
            }
            catch (Exception exception)
            {
                // 메모리 값은 rollback 대상 그대로 유지해 현재 화면에 서버의
                // 부분 적용값이 노출되지 않게 하고, 상위 계정 흐름은 계속 차단한다.
                NotifyChangedSafely();
                Debug.LogWarning(
                    "[MukJump] 동기화 실패 뒤 로컬 설정 복원을 내구 저장하지 " +
                    "못했습니다: " + exception.Message);
                return false;
            }
        }

        /// 회원 탈퇴 뒤 계정에 연결됐던 옵션·튜토리얼 식별값을 기본값으로
        /// 덮어쓰고 새 로컬 게스트 식별자를 만든다.
        public static bool TryResetForAccountDeletion()
        {
            try
            {
                EnsureLoaded();
                bgmVolume = 1f;
                sfxVolume = 1f;
                bgmResumeVolume = 1f;
                sfxResumeVolume = 1f;
                hapticsEnabled = true;
                reducedMotionEnabled = false;
                tutorialSeen = false;
                gameplayTutorialVersion = 0;
                playerUid = "MUK-" +
                            Guid.NewGuid().ToString("N")
                                .Substring(0, 8)
                                .ToUpperInvariant();

                store.SetFloat(BgmVolumeKey, bgmVolume);
                store.SetFloat(SfxVolumeKey, sfxVolume);
                store.SetFloat(BgmResumeVolumeKey, bgmResumeVolume);
                store.SetFloat(SfxResumeVolumeKey, sfxResumeVolume);
                store.SetInt(TutorialSeenKey, tutorialSeen ? 1 : 0);
                store.SetInt(GameplayTutorialVersionKey, gameplayTutorialVersion);
                store.SetInt(HapticsEnabledKey, 1);
                store.SetInt(ReducedMotionEnabledKey, 0);
                store.SetString(PlayerUidKey, playerUid);
                store.Save();
                // 명시적인 탈퇴로 만든 새 게스트에만 첫 안내를 다시 허용한다.
                gameplayStartedThisSession = false;
                NotifyChangedSafely();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"회원 탈퇴 후 로컬 옵션 삭제에 실패했습니다: {exception.Message}");
                return false;
            }
        }

        static void NotifyChangedSafely()
        {
            Action listeners = Changed;
            if (listeners == null)
                return;

            foreach (Action listener in listeners.GetInvocationList())
            {
                try
                {
                    listener();
                }
                catch (Exception exception)
                {
                    // 설정 저장은 이미 끝난 상태다. 깨진 UI 관찰자 하나가
                    // 저장 성공을 실패로 바꾸거나 다른 화면 갱신을 막지 않는다.
                    Debug.LogWarning(
                        "[MukJump] 로비 설정 변경 알림 구독자 예외를 격리했습니다: " +
                        exception.Message);
                }
            }
        }

        /// 손상된 로컬/서버 값이 오디오와 다음 저장에 NaN을 전파하지 않는다.
        public static float NormalizeVolume(float value, float fallback = 1f)
        {
            if (float.IsNaN(fallback) || float.IsInfinity(fallback)) fallback = 1f;
            return Mathf.Clamp01(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value);
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            bgmVolume = NormalizeVolume(store.GetFloat(BgmVolumeKey, 1f));
            sfxVolume = NormalizeVolume(store.GetFloat(SfxVolumeKey, 1f));
            bgmResumeVolume = Mathf.Clamp(
                NormalizeVolume(store.GetFloat(
                    BgmResumeVolumeKey,
                    bgmVolume > 0.01f ? bgmVolume : 0.8f), 0.8f),
                0.1f,
                1f);
            sfxResumeVolume = Mathf.Clamp(
                NormalizeVolume(store.GetFloat(
                    SfxResumeVolumeKey,
                    sfxVolume > 0.01f ? sfxVolume : 0.8f), 0.8f),
                0.1f,
                1f);
            tutorialSeen = store.GetInt(TutorialSeenKey, 0) != 0;
            gameplayTutorialVersion = Mathf.Max(
                0,
                store.GetInt(GameplayTutorialVersionKey, 0));
            hapticsEnabled = store.GetInt(HapticsEnabledKey, 1) != 0;
            reducedMotionEnabled =
                store.GetInt(ReducedMotionEnabledKey, 0) != 0;
            string savedLanguage = store.GetString(LanguageKey, string.Empty);
            language = savedLanguage == "ja" ? GameLanguage.Japanese : savedLanguage == "en"
                ? GameLanguage.English : savedLanguage == "ko" ? GameLanguage.Korean
                : DetectLanguage(Application.systemLanguage);
            playerUid = store.GetString(PlayerUidKey, string.Empty);
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                playerUid = "MUK-" +
                            Guid.NewGuid().ToString("N")
                                .Substring(0, 8)
                                .ToUpperInvariant();
                store.SetString(PlayerUidKey, playerUid);
                store.Save();
            }
            // 일시적인 읽기 실패가 기본값을 영구 캐시하지 않도록 성공한 뒤 확정한다.
            loaded = true;
        }

#if UNITY_EDITOR
        public static void UseStoreForTests(ILobbySettingsStore testStore)
        {
            gameplayStartedThisSession = false;
            store = testStore ??
                    throw new ArgumentNullException(nameof(testStore));
            loaded = false;
            Changed = null;
        }

        public static void RestoreDefaultStoreForTests()
        {
            gameplayStartedThisSession = false;
            store = new PlayerPrefsLobbySettingsStore();
            loaded = false;
            Changed = null;
        }
#endif
    }

#if UNITY_EDITOR
    public sealed class MemoryLobbySettingsStore : ILobbySettingsStore
    {
        readonly Dictionary<string, float> floats = new();
        readonly Dictionary<string, string> strings = new();
        readonly Dictionary<string, int> ints = new();

        public int SaveCount { get; private set; }
        public bool ThrowOnSave { get; set; }

        public float GetFloat(string key, float fallback) =>
            floats.TryGetValue(key, out float value) ? value : fallback;
        public void SetFloat(string key, float value) => floats[key] = value;
        public string GetString(string key, string fallback) =>
            strings.TryGetValue(key, out string value) ? value : fallback;
        public void SetString(string key, string value) =>
            strings[key] = value ?? string.Empty;
        public int GetInt(string key, int fallback) =>
            ints.TryGetValue(key, out int value) ? value : fallback;
        public void SetInt(string key, int value) => ints[key] = value;
        public void Save()
        {
            if (ThrowOnSave)
                throw new InvalidOperationException(
                    "Injected lobby settings save failure");
            SaveCount++;
        }
    }
#endif
}
