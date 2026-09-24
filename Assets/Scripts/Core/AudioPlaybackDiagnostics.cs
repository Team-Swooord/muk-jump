using UnityEngine;

namespace MukJump.Core
{
    /// 개발판 또는 명시한 로컬 진단 빌드에서만 소리 요청·차단 상태를 기록한다.
    public static class AudioPlaybackDiagnostics
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || MUKJUMP_AUDIO_DIAGNOSTICS
        [System.Serializable]
        public sealed class Snapshot
        {
            public string reason, platform, state, pauseReason, sourceName, clipName, loadState;
            public bool listenerPaused, applicationActive, sourceEnabled, sourceMuted, sourcePlaying;
            public float realtime, timeScale, bgmSetting, sfxSetting, requestedGain, sourceVolume;
            public int clipSamples, clipChannels, activePoolSources;
        }

        public static event System.Action<Snapshot> Captured;
        static float nextLogAt;

        public static Snapshot Capture(string reason, AudioSource source = null,
            AudioClip clip = null, float gain = 0f)
        {
            var manager = GameManager.Instance;
            var pool = VfxAudioManager.Instance;
            int active = 0;
            if (pool != null)
                foreach (var voice in pool.GetComponents<AudioSource>())
                    if (voice.isPlaying) active++;
            return new Snapshot
            {
                reason = reason, platform = Application.platform.ToString(),
                realtime = Time.realtimeSinceStartup, timeScale = Time.timeScale,
                state = manager != null ? manager.State.ToString() : "none",
                pauseReason = manager != null ? manager.PauseReason.ToString() : "none",
                listenerPaused = AudioListener.pause,
                applicationActive = MobileApplicationLifecycle.IsApplicationActive,
                bgmSetting = LobbySettingsProfile.BgmVolume, sfxSetting = LobbySettingsProfile.SfxVolume,
                requestedGain = gain, activePoolSources = active,
                sourceName = source != null ? source.gameObject.name : "none",
                sourceEnabled = source != null && source.isActiveAndEnabled,
                sourceMuted = source != null && source.mute,
                sourcePlaying = source != null && source.isPlaying,
                sourceVolume = source != null ? source.volume : 0f,
                clipName = clip != null ? clip.name : "none",
                loadState = clip != null ? clip.loadState.ToString() : "none",
                clipSamples = clip != null ? clip.samples : 0,
                clipChannels = clip != null ? clip.channels : 0,
            };
        }
#endif

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("MUKJUMP_AUDIO_DIAGNOSTICS")]
        public static void Record(string reason, AudioSource source = null,
            AudioClip clip = null, float gain = 0f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || MUKJUMP_AUDIO_DIAGNOSTICS
            // 구독자가 없는 에디터와 일반 출시 Player에는 매 효과음의 진단 할당이 없다.
            if (Captured == null && Application.isEditor) return;
            bool stateChange = reason == "music-state" || reason == "game-pause" || reason == "game-resume";
            if (Captured == null && !stateChange && Time.realtimeSinceStartup < nextLogAt) return;
            Snapshot snapshot = Capture(reason, source, clip, gain);
            Captured?.Invoke(snapshot);
#if (DEVELOPMENT_BUILD || MUKJUMP_AUDIO_DIAGNOSTICS) && !UNITY_EDITOR
            if (stateChange || Time.realtimeSinceStartup >= nextLogAt)
            {
                nextLogAt = Time.realtimeSinceStartup + .5f;
                Debug.Log("[MukJumpAudio] " + JsonUtility.ToJson(snapshot));
            }
#endif
#endif
        }
    }
}
