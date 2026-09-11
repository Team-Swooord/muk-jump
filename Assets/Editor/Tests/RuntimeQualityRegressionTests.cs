using System;
using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class RuntimeQualityRegressionTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> cleanup = new();

        [SetUp]
        public void SetUp() => LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [TestCase(PointerEventData.InputButton.Right)]
        [TestCase(PointerEventData.InputButton.Middle)]
        public void NonPrimaryClickDoesNotAnimate(PointerEventData.InputButton input)
        {
            var feedback = MakeButton();
            var data = Pointer(1, input);
            feedback.OnPointerDown(data);
            feedback.OnPointerUp(data);
            feedback.OnPointerClick(data);
            Assert.That(Get<float>(feedback, "targetScale"), Is.EqualTo(1f));
            Assert.That(Get<bool>(feedback, "isAnimating"), Is.False);
        }

        [Test]
        public void SecondFingerCannotReleaseTheOriginalPress()
        {
            var feedback = MakeButton();
            feedback.OnPointerDown(Pointer(12));
            feedback.OnPointerDown(Pointer(13));
            feedback.OnPointerExit(Pointer(13));
            feedback.OnPointerUp(Pointer(13));
            feedback.OnPointerClick(Pointer(13));
            Assert.That(Get<float>(feedback, "targetScale"), Is.EqualTo(.97f));
            feedback.OnPointerUp(Pointer(12));
            Assert.That(Get<float>(feedback, "targetScale"), Is.EqualTo(1f));
        }

        [Test]
        public void LockedParentPreventsPressFeedback()
        {
            var feedback = MakeButton();
            var group = feedback.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            Call(feedback.GetComponent<UnityEngine.UI.Button>(), "OnCanvasGroupChanged");
            feedback.OnPointerDown(null);
            Assert.That(Get<bool>(feedback, "isAnimating"), Is.False);
        }

        [Test]
        public void DisabledButtonRestoresEvenAnAlreadySettledPress()
        {
            var feedback = MakeButton();
            feedback.OnPointerDown(null);
            Step(feedback, 1f);
            Assert.That(Get<bool>(feedback, "isAnimating"), Is.False);
            Assert.That(feedback.transform.localScale.x, Is.EqualTo(.97f));
            feedback.GetComponent<UnityEngine.UI.Button>().interactable = false;
            Step(feedback, .01f);
            Assert.That(feedback.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Get<bool>(feedback, "pointerHeld"), Is.False);
        }

        [TestCase("OnApplicationPause", true)]
        [TestCase("OnApplicationFocus", false)]
        public void ApplicationInterruptionCancelsHeldPress(string method, bool value)
        {
            var feedback = MakeButton();
            feedback.OnPointerDown(null);
            Step(feedback, .1f);
            Call(feedback, method, value);
            Assert.That(feedback.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Get<bool>(feedback, "isAnimating"), Is.False);
            Assert.That(Get<bool>(feedback, "pointerHeld"), Is.False);
        }

        [Test]
        public void PressUsesSizeAppliedByLayoutAfterAwake()
        {
            var feedback = MakeButton();
            var size = new Vector3(.6f, .7f, 1f);
            feedback.transform.localScale = size;
            feedback.OnPointerDown(null);
            Step(feedback, 1f);
            Assert.That(feedback.transform.localScale, Is.EqualTo(size * .97f));
            feedback.OnPointerUp(null);
            Step(feedback, 1f);
            Assert.That(feedback.transform.localScale, Is.EqualTo(size));
        }

        [Test]
        public void IdleDisableDoesNotUndoNewLayoutScale()
        {
            var feedback = MakeButton();
            var size = Vector3.one * .55f;
            feedback.transform.localScale = size;
            Call(feedback, "OnDisable");
            Assert.That(feedback.transform.localScale, Is.EqualTo(size));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void PressAndReleaseRemainConsistentAtDifferentFrameRates(int fps)
        {
            var feedback = MakeButton();
            feedback.OnPointerDown(null);
            for (int i = 0; i < fps / 5; i++) Step(feedback, 1f / fps);
            Assert.That(feedback.transform.localScale.x, Is.EqualTo(.97f).Within(.0004f));
            feedback.OnPointerUp(null);
            for (int i = 0; i < fps / 5; i++) Step(feedback, 1f / fps);
            Assert.That(feedback.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void ReducedMotionKeepsItsOriginalSmallPress()
        {
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            var feedback = MakeButton();
            feedback.OnPointerDown(null);
            Step(feedback, 1f);
            Assert.That(feedback.transform.localScale.x, Is.EqualTo(.995f));
        }

        [Test]
        public void AudioPoolRepairsMissingSourcesWithoutReplacingSurvivors()
        {
            var pool = MakePool();
            var original = Get<AudioSource[]>(pool, "sources");
            var survivor = original[1];
            Get<float[]>(pool, "lastStartedAt")[1] = 42f;
            Object.DestroyImmediate(original[0]);
            Call(pool, "EnsureSources");
            var repaired = Get<AudioSource[]>(pool, "sources");
            Assert.That(repaired.Length, Is.EqualTo(6));
            foreach (var source in repaired) Assert.That(source != null, Is.True);
            Assert.That(pool.GetComponents<AudioSource>().Length, Is.EqualTo(6));
            int index = Array.IndexOf(repaired, survivor);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(Get<float[]>(pool, "lastStartedAt")[index], Is.EqualTo(42f));
            for (int i = 0; i < 100; i++) Call(pool, "EnsureSources");
            Assert.That(Get<AudioSource[]>(pool, "sources"), Is.SameAs(repaired), "정상 프레임에는 배열을 다시 할당하지 않는다.");
        }

        [Test]
        public void AudioPoolRepairsDisabledSlotAndMissingTimingCache()
        {
            var pool = MakePool();
            Get<AudioSource[]>(pool, "sources")[2].enabled = false;
            Set(pool, "lastStartedAt", null);
            Call(pool, "EnsureSources");
            Assert.That(Get<float[]>(pool, "lastStartedAt").Length, Is.EqualTo(6));
            foreach (var source in Get<AudioSource[]>(pool, "sources")) Assert.That(source.enabled, Is.True);
        }

        [Test]
        public void StoppingOrDisablingAudioDoesNotCreateMissingSources()
        {
            var pool = MakePool();
            Object.DestroyImmediate(Get<AudioSource[]>(pool, "sources")[0]);
            pool.StopAll();
            Call(pool, "OnDisable");
            Assert.That(pool.GetComponents<AudioSource>().Length, Is.EqualTo(5));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InaudibleRequestsDoNotConsumeTheNextVoice(float volume)
        {
            var pool = MakePool();
            var clip = Track(AudioClip.Create("QualitySilentProbe", 100, 1, 22050, false));
            pool.PlayOneShot(clip, volume);
            Assert.That(Get<int>(pool, "nextSource"), Is.Zero);
        }

        [Test]
        public void GlobalMuteDoesNotConsumeTheNextVoice()
        {
            var pool = MakePool();
            LobbySettingsProfile.SetSfxVolume(0f);
            pool.PlayOneShot(Track(AudioClip.Create("QualityMutedProbe", 100, 1, 22050, false)));
            Assert.That(Get<int>(pool, "nextSource"), Is.Zero);
        }

        [Test]
        public void UnchangedSettingsUidDoesNotAllocateEveryFrame()
        {
            var apply = MakeUidView(out _, out _);
            for (int i = 0; i < 50; i++) apply("1234567890123");
            using var recorder = Unity.Profiling.ProfilerRecorder.StartNew(
                Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc", 4096,
                Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            for (int i = 0; i < 1000; i++) apply("1234567890123");
            recorder.Stop();
            Assert.That(recorder.Valid, Is.True);
            Assert.That(recorder.WrappedAround, Is.False);
            Assert.That(recorder.Count, Is.Zero, "같은 UID 1,000회 갱신에 새 할당이 없어야 합니다.");
        }

        [Test]
        public void UidCacheImmediatelyFollowsAccountChangeAndSignOut()
        {
            var apply = MakeUidView(out var view, out var label);
            apply("111111");
            Assert.That(label.text, Is.EqualTo("UID 111111"));
            Set(view, "uuidCopyStatus", "복사 완료");
            Set(view, "uuidCopyStatusUntil", float.PositiveInfinity);
            apply("222222");
            Assert.That(label.text, Is.EqualTo("UID 222222"));
            apply(string.Empty);
            Assert.That(label.text, Is.EqualTo("UID —"));
            Assert.That(Get<UnityEngine.UI.Button>(view, "settingsUuidButton").interactable, Is.False);
            apply("333333");
            Assert.That(label.text, Is.EqualTo("UID 333333"));
            Assert.That(Get<UnityEngine.UI.Button>(view, "settingsUuidButton").interactable, Is.True);
        }

        Action<string> MakeUidView(out LobbyOptionsView view, out UnityEngine.UI.Text label)
        {
            var root = Track(new GameObject("QualityUid", typeof(RectTransform)));
            root.SetActive(false);
            view = root.AddComponent<LobbyOptionsView>();
            var button = root.AddComponent<UnityEngine.UI.Button>();
            var labelRoot = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            labelRoot.transform.SetParent(root.transform, false);
            label = labelRoot.GetComponent<UnityEngine.UI.Text>();
            Set(view, "settingsUuidText", label);
            Set(view, "settingsUuidButton", button);
            return (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), view,
                typeof(LobbyOptionsView).GetMethod("ApplySettingsUuid", Private));
        }

        InkUiPressFeedback MakeButton()
        {
            var root = Track(new GameObject("QualityButton", typeof(RectTransform), typeof(UnityEngine.UI.Button)));
            var feedback = root.AddComponent<InkUiPressFeedback>();
            Call(feedback, "Awake");
            Call(feedback, "OnEnable");
            return feedback;
        }

        VfxAudioManager MakePool()
        {
            var pool = Track(new GameObject("QualityAudioPool")).AddComponent<VfxAudioManager>();
            Call(pool, "EnsureSources");
            return pool;
        }

        T Track<T>(T value) where T : Object { cleanup.Add(value); return value; }
        static PointerEventData Pointer(int id, PointerEventData.InputButton button = PointerEventData.InputButton.Left) =>
            new(null) { pointerId = id, button = button };
        static void Step(InkUiPressFeedback feedback, float delta) => Call(feedback, "AdvancePress", delta);
        static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    }
}
