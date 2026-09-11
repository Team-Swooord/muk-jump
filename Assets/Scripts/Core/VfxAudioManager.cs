using UnityEngine;

namespace MukJump.Core
{
    /// 짧은 VFX 효과음이 서로 끊기지 않도록 순환 AudioSource 풀로 재생한다.
    [DisallowMultipleComponent]
    public class VfxAudioManager : MonoBehaviour
    {
        [SerializeField, Range(2, 12)] int sourceCount = 6;
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;

        public static VfxAudioManager Instance { get; private set; }

        AudioSource[] sources;
        float[] lastStartedAt;
        int nextSource;

        void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MukJump] 중복 VfxAudioManager를 비활성화합니다.", this);
                enabled = false;
                return;
            }
            Instance = this;
            EnsureSources();
        }

        void OnDisable()
        {
            StopAll();
            if (Instance == this) Instance = null;
        }

        public void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip == null || !isActiveAndEnabled) return;
            float gain = FiniteVolume(volume) * FiniteVolume(masterVolume) *
                         FiniteVolume(LobbySettingsProfile.SfxVolume);
            // 들리지 않는 요청이 이미 재생 중인 피격음을 밀어내지 않는다.
            if (gain <= 0f) return;
            EnsureSources();
            if (sources == null || sources.Length == 0) return;

            int selected = FindAvailableSourceIndex();
            AudioSource source = sources[selected];
            nextSource = (selected + 1) % sources.Length;
            if (source.isPlaying)
                source.Stop();
            source.clip = null;
            source.PlayOneShot(clip, gain);
            lastStartedAt[selected] = Time.unscaledTime;
        }

        /// 일시정지 전에 재생 중인 짧은 효과음을 비워, 재개 시 뒤늦게 이어지지 않게 한다.
        public void StopAll()
        {
            // 종료/비활성화 경로에서 없어진 재생기를 새로 만들지 않는다.
            if (sources == null) return;
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null) continue;
                sources[i].Stop();
                sources[i].clip = null;
            }
            nextSource = 0;
        }

        void EnsureSources()
        {
            int count = Mathf.Clamp(sourceCount, 2, 12);
            bool complete = sources != null && sources.Length == count &&
                            lastStartedAt != null && lastStartedAt.Length == count;
            if (complete)
                for (int i = 0; i < count; i++)
                    if (sources[i] == null || !sources[i].enabled) { complete = false; break; }
            if (complete) return;

            var existing = GetComponents<AudioSource>();
            var previousSources = sources;
            var previousStartedAt = lastStartedAt;
            sources = new AudioSource[count];
            lastStartedAt = new float[count];
            for (int i = 0; i < count; i++)
            {
                AudioSource source = i < existing.Length ? existing[i] : gameObject.AddComponent<AudioSource>();
                source.enabled = true;
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                sources[i] = source;
                // 일부 컴포넌트만 복구해도 나머지 소리와 오래된 재생 순서는 보존한다.
                int previousIndex = previousSources == null ? -1 : System.Array.IndexOf(previousSources, source);
                if (previousStartedAt != null && previousIndex >= 0 && previousIndex < previousStartedAt.Length)
                    lastStartedAt[i] = previousStartedAt[previousIndex];
            }
            for (int i = count; i < existing.Length; i++) existing[i].Stop();
            nextSource %= count;
        }

        static float FiniteVolume(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);

        int FindAvailableSourceIndex()
        {
            int oldestIndex = nextSource;
            float oldestStart = float.PositiveInfinity;
            for (int offset = 0; offset < sources.Length; offset++)
            {
                int index = (nextSource + offset) % sources.Length;
                if (sources[index] == null || !sources[index].isPlaying)
                    return index;
                if (lastStartedAt[index] >= oldestStart) continue;
                oldestStart = lastStartedAt[index];
                oldestIndex = index;
            }
            return oldestIndex;
        }

        void OnValidate()
        {
            sourceCount = Mathf.Clamp(sourceCount, 2, 12);
            masterVolume = Mathf.Clamp01(masterVolume);
        }
    }
}
