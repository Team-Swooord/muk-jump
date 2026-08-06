using System;
using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Core
{
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
    /// 플랫폼 로그인 버튼은 표시만 하며 이 프로필에 인증 정보는 저장하지 않는다.
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

        static ILobbySettingsStore store = new PlayerPrefsLobbySettingsStore();
        static bool loaded;
        static float bgmVolume;
        static float sfxVolume;
        static float bgmResumeVolume;
        static float sfxResumeVolume;
        static bool tutorialSeen;
        static int gameplayTutorialVersion;
        static string playerUid;

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

        /// 최초 접속에서만 로비를 건너뛰고 게임 화면의 정지형 안내로 바로 진입한다.
        public static bool ShouldAutoStartGameplayTutorial =>
            NeedsGameplayTutorial;

        public static string PlayerUid
        {
            get
            {
                EnsureLoaded();
                return playerUid;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            store = new PlayerPrefsLobbySettingsStore();
            loaded = false;
            Changed = null;
        }

        public static void SetBgmVolume(float value)
        {
            EnsureLoaded();
            float next = Mathf.Clamp01(value);
            if (Mathf.Approximately(bgmVolume, next)) return;
            bgmVolume = next;
            store.SetFloat(BgmVolumeKey, bgmVolume);
            if (next > 0.01f)
            {
                bgmResumeVolume = next;
                store.SetFloat(BgmResumeVolumeKey, bgmResumeVolume);
            }
            Changed?.Invoke();
        }

        public static void SetSfxVolume(float value)
        {
            EnsureLoaded();
            float next = Mathf.Clamp01(value);
            if (Mathf.Approximately(sfxVolume, next)) return;
            sfxVolume = next;
            store.SetFloat(SfxVolumeKey, sfxVolume);
            if (next > 0.01f)
            {
                sfxResumeVolume = next;
                store.SetFloat(SfxResumeVolumeKey, sfxResumeVolume);
            }
            Changed?.Invoke();
        }

        public static void MarkTutorialSeen()
        {
            EnsureLoaded();
            if (tutorialSeen) return;
            tutorialSeen = true;
            store.SetInt(TutorialSeenKey, 1);
            store.Save();
            Changed?.Invoke();
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
                    Changed?.Invoke();
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
            EnsureLoaded();
            store.Save();
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            bgmVolume = Mathf.Clamp01(store.GetFloat(BgmVolumeKey, 1f));
            sfxVolume = Mathf.Clamp01(store.GetFloat(SfxVolumeKey, 1f));
            bgmResumeVolume = Mathf.Clamp(
                store.GetFloat(
                    BgmResumeVolumeKey,
                    bgmVolume > 0.01f ? bgmVolume : 0.8f),
                0.1f,
                1f);
            sfxResumeVolume = Mathf.Clamp(
                store.GetFloat(
                    SfxResumeVolumeKey,
                    sfxVolume > 0.01f ? sfxVolume : 0.8f),
                0.1f,
                1f);
            tutorialSeen = store.GetInt(TutorialSeenKey, 0) != 0;
            gameplayTutorialVersion = Mathf.Max(
                0,
                store.GetInt(GameplayTutorialVersionKey, 0));
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
        }

#if UNITY_EDITOR
        public static void UseStoreForTests(ILobbySettingsStore testStore)
        {
            store = testStore ??
                    throw new ArgumentNullException(nameof(testStore));
            loaded = false;
            Changed = null;
        }

        public static void RestoreDefaultStoreForTests()
        {
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
        public void Save() => SaveCount++;
    }
#endif
}
