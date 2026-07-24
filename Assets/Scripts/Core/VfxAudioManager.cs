using UnityEngine;

namespace MukJump.Core
{
    /// 짧은 VFX 효과음이 서로 끊기지 않도록 순환 AudioSource 풀로 재생한다.
    public class VfxAudioManager : MonoBehaviour
    {
        [SerializeField, Range(2, 12)] int sourceCount = 6;
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;

        public static VfxAudioManager Instance { get; private set; }

        AudioSource[] sources;
        int nextSource;

        void OnEnable()
        {
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

            AudioSource source = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;
            source.Stop();
            source.clip = null;
            source.PlayOneShot(clip, Mathf.Clamp01(volume) * masterVolume);
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
            nextSource = 0;
        }

        void OnValidate()
        {
            sourceCount = Mathf.Clamp(sourceCount, 2, 12);
            masterVolume = Mathf.Clamp01(masterVolume);
        }
    }
}
