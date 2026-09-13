using UnityEngine;

namespace MukJump.Core
{
    /// 손으로 그린 원화 프레임과 재생 속도. 런타임에는 공유 Sprite 참조만 교체한다.
    public sealed class InkSpriteAnimation : ScriptableObject
    {
        [SerializeField] Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField] float framesPerSecond = 6f;
        public int FrameCount => frames.Length;
        public float FramesPerSecond => framesPerSecond;
        public float Duration => frames.Length / framesPerSecond;
        public Sprite Frame(int index) => frames.Length == 0 ? null : frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        public Sprite Sample(float seconds, bool animate = true)
        {
            if (frames.Length == 0) return null;
            if (!animate || !float.IsFinite(seconds)) return frames[0];
            return frames[Mathf.FloorToInt(Mathf.Repeat(seconds, Duration) * framesPerSecond) % frames.Length];
        }
    }
}
