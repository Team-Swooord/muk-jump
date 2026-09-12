using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class InkSkinUnlockSoundTests
    {
        [Test] public void BrightPlucksAreShortDeterministicAndHaveClickFreeHeadroom()
        {
            float[] samples = InkSkinUnlockSound.BuildSamples();
            Assert.That(samples.Length, Is.EqualTo((int)(44100 * .72f)));
            Assert.That(samples.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), Is.True);
            Assert.That(samples.Max(x => Math.Abs(x)), Is.EqualTo(.76f).Within(.00001f));
            Assert.That(Math.Abs(samples[0]), Is.LessThan(.00001f));
            Assert.That(Math.Abs(samples[samples.Length - 1]), Is.LessThan(.00001f));
            Assert.That(Math.Sqrt(samples.Average(x => (double)x * x)), Is.InRange(.1, .35));
            CollectionAssert.AreEqual(samples, InkSkinUnlockSound.BuildSamples());
            // 같은 합성 결과를 기획자가 바로 들을 수 있는 미리듣기로 남긴다.
            string path = Path.GetFullPath("output/qa/ink-showroom/unlock-bright-preview.wav");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var file = new BinaryWriter(File.Create(path));
            file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); file.Write(36 + samples.Length * 2);
            file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); file.Write(16);
            file.Write((short)1); file.Write((short)1); file.Write(InkSkinUnlockSound.SampleRate);
            file.Write(InkSkinUnlockSound.SampleRate * 2); file.Write((short)2); file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes("data")); file.Write(samples.Length * 2);
            foreach (float sample in samples) file.Write((short)Math.Round(sample * InkSkinUnlockSound.Volume * short.MaxValue));
        }

        [Test] public void ViewOwnsOneReusableNonStreamingClipAndReleasesIt()
        {
            var host = new GameObject("UnlockSoundLifetimeTest");
            AudioClip clip = null;
            try
            {
                var view = host.AddComponent<InkSkinUnlockView>(); view.BuildForTests(null);
                var source = host.GetComponentInChildren<AudioSource>(); clip = source.clip;
                Assert.That(clip.name, Is.EqualTo("Ink_Unlock_BrightPlucks"));
                Assert.That(clip.frequency, Is.EqualTo(44100)); Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(clip.length, Is.EqualTo(.72f).Within(.0001f));
                var actual = new float[clip.samples]; Assert.That(clip.GetData(actual, 0), Is.True);
                CollectionAssert.AreEqual(InkSkinUnlockSound.BuildSamples(), actual);
                Assert.That(source.playOnAwake || source.loop, Is.False);
                Assert.That(source.spatialBlend, Is.Zero);
                view.BuildForTests(null);
                Assert.That(host.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(1));
                Assert.That(source.clip, Is.SameAs(clip));
                // EditMode에서는 일반 MonoBehaviour 파괴 콜백을 실행하지 않으므로 직접 검사한다.
                // 실제 PlayMode의 Destroy 정리는 아래 테스트에서 프레임을 넘겨 검증한다.
                Call(view, "OnDestroy");
            }
            finally { Object.DestroyImmediate(host); }
            Assert.That(clip == null, Is.True, "생성한 AudioClip을 화면 수명과 함께 해제한다.");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator RewardSoundPlaysOnceAndRespectsMuteReducedMotionCancellationAndBackground()
        {
            // 실제 계정·로비·서버에 접속하지 않고 해금 화면의 재생 경계만 검사한다.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore { Best = 2000 });
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var host = new GameObject("UnlockAudioPlayTest", typeof(AudioListener));
            host.AddComponent<ScoreManager>();
            var view = host.AddComponent<InkSkinUnlockView>();
            view.BuildForTests(null);
            var source = host.GetComponentInChildren<AudioSource>(); var clip = source.clip;
            try
            {
                PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat);
                Assert.That(view.Show(Bit(InkSkinId.Gat) | Bit(InkSkinId.NightBat), PermanentGrowthProfile.SkinAccountToken, null), Is.True);
                Assert.That(view.SoundPlayCountForTests, Is.Zero);
                yield return WaitForSound(view, 1);
                Assert.That(source.isPlaying, Is.True, "Play 호출뿐 아니라 실제 AudioSource 재생 상태 확인");
                Call(view, "Update"); Call(view, "Update");
                Assert.That(view.SoundPlayCountForTests, Is.EqualTo(1), "일반 의상 공개 1회");
                Set(view, "nextActionAt", 0f); view.Advance();
                Assert.That(view.SoundPlayCountForTests, Is.EqualTo(1), "연출 마무리는 소리를 반복하지 않는다.");
                Set(view, "nextActionAt", 0f); view.Advance();
                Assert.That(view.Current, Is.EqualTo(InkSkinId.NightBat));
                Assert.That(source.isPlaying, Is.False, "다음 항목 시작 때 이전 잔향 중단");
                // 공개 직전 빠르게 확인하더라도 공개와 소리는 같이 한 번만 발생한다.
                Set(view, "elapsed", .165f); Set(view, "nextActionAt", 0f); view.Advance();
                Assert.That(view.SoundPlayCountForTests, Is.EqualTo(2), "히든 의상 공개 1회");
                view.Cancel(); Assert.That(source.isPlaying, Is.False);

                LobbySettingsProfile.SetReducedMotionEnabled(true);
                Show(view); Assert.That(view.SoundPlayCountForTests, Is.EqualTo(3), "동작 줄이기는 소리를 없애지 않는다.");
                Call(view, "Update"); Assert.That(view.SoundPlayCountForTests, Is.EqualTo(3));
                LobbySettingsProfile.SetSfxVolume(.25f); Call(view, "Update");
                Assert.That(source.volume, Is.EqualTo(.25f * InkSkinUnlockSound.Volume).Within(.0001f));
                LobbySettingsProfile.SetSfxVolume(0f); Call(view, "Update"); Assert.That(source.volume, Is.Zero);
                view.Cancel(); Show(view); Assert.That(view.SoundPlayCountForTests, Is.EqualTo(3));
                LobbySettingsProfile.SetSfxVolume(1f); Call(view, "Update");
                Assert.That(view.SoundPlayCountForTests, Is.EqualTo(3), "무음으로 지난 항목을 뒤늦게 재생하지 않는다.");
                view.Cancel();

                Show(view); Assert.That(view.SoundPlayCountForTests, Is.EqualTo(4));
                Call(view, "OnApplicationPause", true);
                Assert.That(source.isPlaying, Is.False, "프레임 중단 전 네이티브 일시정지에서 즉시 중단");
                Call(view, "OnApplicationPause", false); Call(view, "Update");
                Assert.That(view.SoundPlayCountForTests, Is.EqualTo(4)); view.Cancel();
                Show(view); Call(view, "OnApplicationFocus", false); Assert.That(source.isPlaying, Is.False);
                Call(view, "OnApplicationFocus", true); Call(view, "Update");
                Assert.That(view.SoundPlayCountForTests, Is.EqualTo(5)); view.Cancel();

                LobbySettingsProfile.SetReducedMotionEnabled(false);
                Show(view); MobileApplicationLifecycle.SetPlatformVisibility(false); Call(view, "Update");
                MobileApplicationLifecycle.SetPlatformVisibility(true); yield return null;
                Assert.That(view.SoundPlayCountForTests, Is.EqualTo(5), "공개 전 백그라운드 이동 후에도 늦게 재생하지 않는다.");
                view.Cancel();
                Show(view); yield return WaitForSound(view, 6);
                Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(), Is.True);
                Call(view, "Update"); Assert.That(view.IsOpen || source.isPlaying, Is.False);
                Assert.That(source.clip, Is.SameAs(clip), "항목마다 음원을 재생성하지 않는다.");
                Object.Destroy(host); yield return null; yield return null;
                Assert.That(clip == null, Is.True);
            }
            finally
            {
                if (host != null) Object.Destroy(host);
                MobileApplicationLifecycle.SetPlatformVisibility(true); Application.runInBackground = oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
            }
            yield return new ExitPlayMode();
        }

        static ulong Bit(InkSkinId id) => 1UL << (int)id;
        static void Show(InkSkinUnlockView view) => Assert.That(view.Show(Bit(InkSkinId.Gat), PermanentGrowthProfile.SkinAccountToken, null), Is.True);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static IEnumerator WaitForSound(InkSkinUnlockView view, int count)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (view.SoundPlayCountForTests < count && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.SoundPlayCountForTests, Is.EqualTo(count));
        }
    }
}
