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
            if (Instance == this) Instance = null;
        }

        public void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            EnsureSources();
            if (sources == null || sources.Length == 0) return;

            int selected = FindAvailableSourceIndex();
            AudioSource source = sources[selected];
            nextSource = (selected + 1) % sources.Length;
            if (source.isPlaying)
                source.Stop();
            source.clip = null;
            source.PlayOneShot(clip, Mathf.Clamp01(volume) * masterVolume);
            lastStartedAt[selected] = Time.unscaledTime;
        }

        /// 일시정지 전에 재생 중인 짧은 효과음을 비워, 재개 시 뒤늦게 이어지지 않게 한다.
        public void StopAll()
        {
            EnsureSources();
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
            if (sources != null && sources.Length == count) return;

            var existing = GetComponents<AudioSource>();
            sources = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                AudioSource source = i < existing.Length ? existing[i] : gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                sources[i] = source;
            }
            lastStartedAt = new float[count];
            nextSource = 0;
        }

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
