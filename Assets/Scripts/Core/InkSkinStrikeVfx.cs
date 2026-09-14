using MukJump.Player;
using UnityEngine;

namespace MukJump.Core
{
    /// 30초 쿨다운의 타격 한 개만 담당하는 고정 재사용 슬롯. 그림은 실제 4프레임 원화다.
    [DisallowMultipleComponent]
    public sealed class InkSkinStrikeVfx : MonoBehaviour
    {
        public const float Duration = .36f;
        [SerializeField, HideInInspector] SpriteRenderer strike;
        InkSpriteAnimation animation;
        AudioClip sound;
        float elapsed;
        bool playing;
        public bool IsPlaying => playing;
        public float Elapsed => elapsed;
        public SpriteRenderer Renderer => strike;

        void OnEnable()
        {
            // 재컴파일 후에도 기존 슬롯을 찾아 재사용한다. 전투 시점에는 로드/생성하지 않는다.
            animation = Resources.Load<InkSpriteAnimation>("MukJump/SpriteAnimations/mask_strike");
            if (strike == null) strike = transform.Find("SkinStrikePooled")?.GetComponent<SpriteRenderer>();
            if (strike == null)
            {
                var root = new GameObject("SkinStrikePooled", typeof(SpriteRenderer));
                root.transform.SetParent(transform, false);
                strike = root.GetComponent<SpriteRenderer>();
            }
            if (sound == null)
            {
                var samples = InkSkinStrikeSound.BuildSamples();
                sound = AudioClip.Create("InkBanishingStrike", samples.Length, 1, InkSkinStrikeSound.SampleRate, false);
                sound.SetData(samples, 0);
            }
            Stop();
        }

        public bool Play(PlayerController player, Vector3 enemyPosition)
        {
            if (!isActiveAndEnabled || strike == null || animation == null || animation.FrameCount != 4 ||
                player == null || GameManager.Instance == null || !GameManager.Instance.IsGameplayTicking ||
                !MobileApplicationLifecycle.IsApplicationActive) return false;
            var body = player.GetComponent<SpriteRenderer>();
            strike.sharedMaterial = body.sharedMaterial;
            strike.sortingLayerID = body.sortingLayerID;
            strike.sortingOrder = body.sortingOrder + 5;
            strike.transform.position = Vector3.Lerp(player.transform.position, enemyPosition, .35f);
            strike.transform.rotation = Quaternion.identity;
            float width = LobbySettingsProfile.ReducedMotionEnabled ? 1.35f : 1.8f;
            strike.transform.localScale = Vector3.one * (width / Mathf.Max(.01f, animation.Frame(0).bounds.size.x));
            strike.flipX = enemyPosition.x < player.transform.position.x;
            strike.color = Color.white;
            elapsed = 0; playing = true; PresentFrame();
            VfxAudioManager.Instance?.PlayOneShot(sound, .58f);
            return true;
        }

        void Update() => Advance(Time.deltaTime);
        void Advance(float seconds)
        {
            if (!playing) return;
            var game = GameManager.Instance;
            if (game == null || game.State != GameState.Playing) { Stop(); return; }
            if (!game.IsGameplayTicking || !MobileApplicationLifecycle.IsApplicationActive)
            { strike.enabled = false; return; }
            if (float.IsFinite(seconds) && seconds > 0) elapsed += seconds;
            float duration = LobbySettingsProfile.ReducedMotionEnabled ? .20f : Duration;
            if (elapsed >= duration) { Stop(); return; }
            PresentFrame();
        }
        void PresentFrame()
        {
            bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
            strike.sprite = animation.Frame(reduced ? 1 : Mathf.Min(3, Mathf.FloorToInt(elapsed / .09f)));
            strike.color = new Color(1, 1, 1, reduced ? Mathf.Clamp01(1 - elapsed / .20f) : 1);
            strike.enabled = true;
        }
        void Stop()
        {
            playing = false; elapsed = 0;
            if (strike == null) return;
            strike.enabled = false; strike.sprite = null; strike.color = Color.white; strike.flipX = false;
            strike.transform.localPosition = Vector3.zero;
            strike.transform.localRotation = Quaternion.identity;
            strike.transform.localScale = Vector3.one;
        }
        void OnDisable()
        {
            Stop();
            if (sound == null) return;
            var owned = sound; sound = null;
            if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned);
        }
    }

    /// 원본 효과음: 짧은 바람 베기와 목탁 같은 마른 타격. 게임 난수 스트림을 사용하지 않는다.
    public static class InkSkinStrikeSound
    {
        public const int SampleRate = 22050;
        public const float Duration = .30f;
        public static float[] BuildSamples()
        {
            var samples = new float[(int)(SampleRate * Duration)];
            uint noise = 0x71C9A31u; double previousNoise = 0, peak = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)SampleRate;
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                double white = (noise & 65535) / 32767.5 - 1;
                double sweep = (white - previousNoise) * .14 * System.Math.Exp(-20 * t);
                previousNoise = white;
                double wood = .65 * System.Math.Sin(2 * System.Math.PI * 680 * t) * System.Math.Exp(-48 * t) +
                    .20 * System.Math.Sin(2 * System.Math.PI * 1030 * t) * System.Math.Exp(-62 * t);
                double body = .18 * System.Math.Sin(2 * System.Math.PI * 150 * t) * System.Math.Exp(-28 * t);
                double attack = System.Math.Min(1, t / .003);
                double tail = System.Math.Clamp((Duration - t) / .04, 0, 1);
                samples[i] = (float)((sweep + wood + body) * attack * tail * tail);
                peak = System.Math.Max(peak, System.Math.Abs(samples[i]));
            }
            float gain = peak > 0 ? (float)(.72 / peak) : 0;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            return samples;
        }
    }
}
