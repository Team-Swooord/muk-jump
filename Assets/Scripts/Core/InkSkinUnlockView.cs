using System;
using UnityEngine;

namespace MukJump.Core
{
    /// 최종 정산 뒤 새 의상만 공개한다. 확인/생략은 연출만 닫고 해금 데이터는 건드리지 않는다.
    [DisallowMultipleComponent]
    public sealed class InkSkinUnlockView : MonoBehaviour
    {
        [SerializeField] Sprite previewBodySprite;
        const float RevealAt = .18f, Duration = 1.23f;
        const float HeroSize = 440f;
        readonly InkSkinId[] rewards = new InkSkinId[InkSkinCatalog.Count - 1];
        CanvasGroup group;
        RectTransform safeRoot, paper, entrance, seal;
        UnityEngine.UI.Image body, accessory;
        UnityEngine.UI.Text title, nameText, requirement, pageText, actionText;
        UnityEngine.UI.Button nextButton, skipButton;
        InkShowroomPresentation presentation;
        object account;
        Action completed, invalidated;
        int count, index;
        float elapsed, nextActionAt;
        float accessoryMotionTime;
        uint inactiveGeneration;
        bool visible, revealed, debugPreview;
        AudioClip revealSound;
        AudioSource revealSource;
        bool soundPlayed;
#if UNITY_EDITOR
        public int SoundPlayCountForTests { get; private set; }
#endif
        public bool IsOpen => visible;
        public int Count => count;
        public InkSkinId Current => count > 0 ? rewards[index] : InkSkinId.Ink;
        public RectTransform Paper => paper;
        public bool IsRevealed => revealed;

        public bool Show(ulong mask, object owner, Action onCompleted, Action onInvalidated = null)
            => ShowCore(mask, owner, onCompleted, onInvalidated, false);
        public bool ShowPreview(ulong mask, object owner, Action onCompleted, Action onInvalidated = null)
            => SkinDebugSession.Available && ShowCore(mask, owner, onCompleted, onInvalidated, true);
        bool ShowCore(ulong mask, object owner, Action onCompleted, Action onInvalidated, bool preview)
        {
            if (visible || !ReferenceEquals(owner, PermanentGrowthProfile.SkinAccountToken)) return false;
            count = 0;
            for (int i = 1; i < InkSkinCatalog.Count; i++)
                if (InkSkinCatalog.IsAvailable((InkSkinId)i) && (mask & (1UL << i)) != 0 &&
                    (preview || InkSkinAchievements.IsUnlocked((InkSkinId)i)))
                    rewards[count++] = (InkSkinId)i;
            if (count == 0) return false;
            Build();
            account = owner; completed = onCompleted; invalidated = onInvalidated; index = 0; visible = true; debugPreview = preview;
            group.alpha = 1f; group.interactable = group.blocksRaycasts = true;
            BindCurrent(); ApplyLayout(); PointerInput.SuppressUntilRelease();
            return true;
        }
        void BindCurrent()
        {
            StopSound(); soundPlayed = false;
            presentation.Cancel(); elapsed = 0f; revealed = false;
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            nextActionAt = Time.unscaledTime + .16f;
            var id = Current;
            bool secret = InkSkinCatalog.Get(id).Tier is SkinTier.Hidden or SkinTier.UltraHidden;
            InkLocalizedText.SetSource(title, secret ? "비밀 의상 발견" : "새 의상 해금");
            InkLocalizedText.SetSource(nameText, InkSkinCatalog.Name(id));
            InkLocalizedText.SetSource(requirement, InkSkinAchievements.Requirement(id));
            InkLocalizedText.SetSource(actionText, index + 1 < count ? "다음 의상" : "확인");
            pageText.text = count > 1 ? $"{index + 1} / {count}" : "";
            skipButton.gameObject.SetActive(count > 1);
            body.sprite = previewBodySprite; body.enabled = previewBodySprite != null;
            accessory.sprite = InkSkinCatalog.Sprite(id); accessory.enabled = accessory.sprite != null;
            if (InkSkinCatalog.BehindBody(id)) accessory.transform.SetAsFirstSibling();
            else accessory.transform.SetAsLastSibling();
            if (accessory.sprite != null)
            {
                float width = HeroSize * InkSkinCatalog.Width(id);
                accessory.rectTransform.sizeDelta = new Vector2(width, width * accessory.sprite.rect.height / accessory.sprite.rect.width);
                accessory.rectTransform.anchoredPosition = InkSkinCatalog.Offset(id) * HeroSize;
            }
            body.color = accessory.color = Color.white;
            presentation.RememberCostumePose();
            if (LobbySettingsProfile.ReducedMotionEnabled || !Application.isPlaying)
            { FinishReveal(); PlayRevealSound(); }
            else ApplyPose();
        }
        void Update()
        {
            if (!visible) return;
            if (!IsCurrentContext()) { Invalidate(); return; }
            ApplyLayout();
            if (!MobileApplicationLifecycle.IsApplicationActive || inactiveGeneration != MobileApplicationLifecycle.InactiveGeneration)
            { StopSound(); soundPlayed = true; FinishReveal(); return; }
            if (revealSource != null) revealSource.volume = InkSkinUnlockSound.Volume * LobbySettingsProfile.SfxVolume;
            if (GameManager.Instance == null || !GameManager.Instance.IsPaused)
                accessoryMotionTime += Mathf.Min(.05f, Time.unscaledDeltaTime);
            (accessory as InkAccessoryImage)?.PresentMotion(Current, accessoryMotionTime,
                revealed ? InkAccessoryMotion.Strength : 0f);
            if (LobbySettingsProfile.ReducedMotionEnabled)
            { FinishReveal(); PlayRevealSound(); return; }
            elapsed = Mathf.Min(Duration, elapsed + Mathf.Min(.05f, Time.unscaledDeltaTime));
            if (!revealed && elapsed >= RevealAt)
            {
                revealed = true;
                presentation.Wear();
                PlayRevealSound();
            }
            ApplyPose();
        }
        void ApplyPose()
        {
            float t = Mathf.Clamp01((elapsed - RevealAt) / .4f);
            float u = t - 1f;
            float arrive = 1f + 2.2f * u * u * u + 1.2f * u * u;
            entrance.localScale = Vector3.one * Mathf.LerpUnclamped(.83f, 1f, arrive);
            entrance.anchoredPosition = new Vector2(0f, (1f - t) * -22f);
            body.color = accessory.color = revealed ? Color.white : Color.black;
            float stamp = Mathf.Clamp01((elapsed - .42f) / .2f);
            seal.gameObject.SetActive(stamp > 0f);
            seal.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, stamp);
        }
        void FinishReveal()
        {
            if (group == null) return;
            elapsed = Duration; revealed = true; presentation.Cancel();
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            entrance.localScale = Vector3.one; entrance.anchoredPosition = Vector2.zero;
            body.color = accessory.color = Color.white;
            seal.gameObject.SetActive(true); seal.localScale = Vector3.one;
        }
        bool IsCurrentContext() => ReferenceEquals(account, PermanentGrowthProfile.SkinAccountToken) &&
            (debugPreview && SkinDebugSession.Available || InkSkinAchievements.IsUnlocked(Current)) &&
            (MukJumpAccountRuntime.Instance == null || !MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync) &&
            (GameManager.Instance == null || GameManager.Instance.State == GameState.GameOver);
        bool CanAdvance() => visible && IsCurrentContext() && MobileApplicationLifecycle.IsApplicationActive &&
            (!Application.isPlaying || Time.unscaledTime >= nextActionAt);
        public void Advance()
        {
            if (!CanAdvance()) return;
            PointerInput.SuppressUntilRelease();
            nextActionAt = Time.unscaledTime + .16f;
            if (elapsed < Duration) { FinishReveal(); PlayRevealSound(); return; }
            if (++index >= count) { index = count - 1; Complete(); return; }
            BindCurrent();
        }
        public void AcknowledgeAll()
        {
            if (!CanAdvance()) return;
            PointerInput.SuppressUntilRelease(); Complete();
        }
        void Complete()
        {
            var callback = completed;
            Cancel(); callback?.Invoke();
        }
        void Invalidate()
        {
            var callback = invalidated;
            Cancel(); callback?.Invoke();
        }
        public void Cancel()
        {
            StopSound();
            visible = false; completed = invalidated = null; account = null;
            presentation?.Cancel();
            if (group == null) return;
            group.alpha = 0f; group.interactable = group.blocksRaycasts = false;
        }
        void OnDisable() => Cancel();
        void OnApplicationPause(bool paused) { if (paused) SuspendReveal(); }
        void OnApplicationFocus(bool focused) { if (!focused) SuspendReveal(); }
        void SuspendReveal()
        {
            StopSound();
            if (!visible) return;
            soundPlayed = true;
            FinishReveal();
        }
        void OnDestroy()
        {
            StopSound();
            if (revealSource != null) revealSource.clip = null;
            if (revealSound == null) return;
            if (Application.isPlaying) Destroy(revealSound);
            else DestroyImmediate(revealSound);
            revealSound = null;
        }
        void StopSound() { if (revealSource != null) revealSource.Stop(); }
        void PlayRevealSound()
        {
            if (soundPlayed || !Application.isPlaying || !visible ||
                !MobileApplicationLifecycle.IsApplicationActive || !IsCurrentContext()) return;
            // 무음으로 지나간 해금도 소진해 설정 변경·앱 복귀에 뒤늦게 울리지 않는다.
            soundPlayed = true;
            if (revealSource == null || revealSound == null || LobbySettingsProfile.SfxVolume <= 0f) return;
            revealSource.volume = InkSkinUnlockSound.Volume * LobbySettingsProfile.SfxVolume;
            revealSource.Play();
#if UNITY_EDITOR
            SoundPlayCountForTests++;
#endif
        }
        void Build()
        {
            if (group != null) return;
            var root = new GameObject("SkinUnlockCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5100;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<UnityEngine.UI.CanvasScaler>());
            group = root.GetComponent<CanvasGroup>();
            var dim = Stretch("InputBlocker", root.transform).gameObject.AddComponent<UnityEngine.UI.Image>();
            dim.color = Color.clear; dim.raycastTarget = true;
            safeRoot = Stretch("SafeAreaRoot", root.transform);
            paper = Rect("UnlockStagePage", safeRoot, Vector2.zero, new Vector2(880, 1460));
            title = Label("Title", paper, "새 의상 해금", 62, new Vector2(0, 584), new Vector2(720, 100), true);
            var stage = Rect("UnlockStage", paper, new Vector2(0, 120), new Vector2(760, 790));
            stage.gameObject.AddComponent<UnityEngine.UI.RectMask2D>().softness = new Vector2Int(12, 16);
            entrance = Rect("Entrance", stage, Vector2.zero, Vector2.one * HeroSize);
            var focus = Rect("Preview", entrance, Vector2.zero, Vector2.one * HeroSize);
            body = Picture("OriginalInkBody", focus, previewBodySprite, Vector2.zero, Vector2.one * HeroSize);
            accessory = Picture("Accessory", focus, null, Vector2.zero, Vector2.one);
            presentation = stage.gameObject.AddComponent<InkShowroomPresentation>(); presentation.Initialize(stage, focus);
            nameText = Label("Name", paper, "", 58, new Vector2(0, -255), new Vector2(720, 92), true);
            requirement = Label("Requirement", paper, "", 32, new Vector2(0, -347), new Vector2(700, 76), false);
            requirement.color = InkPalette.TextMuted;
            seal = Rect("UnlockedSeal", paper, new Vector2(0, 462), new Vector2(94, 76));
            seal.localRotation = Quaternion.Euler(0, 0, -6f); InkHudSurface.Ensure(seal, true);
            var sealText = Label("Label", seal, "해금", 38, Vector2.zero, new Vector2(84, 62), true);
            sealText.color = InkPalette.Paper;
            pageText = Label("Page", paper, "", 30, new Vector2(0, -414), new Vector2(300, 42), false);
            InkLocalizedText.Exclude(pageText);
            nextButton = Button("Next", paper, "확인", new Vector2(0, -502), new Vector2(620, 108), out actionText);
            nextButton.onClick.AddListener(Advance);
            skipButton = Button("AcknowledgeAll", paper, "모두 확인", new Vector2(0, -619), new Vector2(420, 92), out _);
            skipButton.onClick.AddListener(AcknowledgeAll);
            var audioRoot = new GameObject("UnlockRevealAudio", typeof(AudioSource));
            audioRoot.transform.SetParent(root.transform, false);
            revealSource = audioRoot.GetComponent<AudioSource>();
            revealSource.playOnAwake = revealSource.loop = false;
            revealSource.spatialBlend = 0f;
            revealSource.ignoreListenerPause = false;
            float[] samples = InkSkinUnlockSound.BuildSamples();
            revealSound = AudioClip.Create("Ink_Unlock_BrightPlucks", samples.Length, 1, InkSkinUnlockSound.SampleRate, false);
            revealSound.SetData(samples, 0); revealSource.clip = revealSound;
            group.alpha = 0f; group.interactable = group.blocksRaycasts = false;
        }
        void ApplyLayout()
        {
            if (safeRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(safeRoot, safe, Screen.width, Screen.height);
            paper.localScale = Vector3.one * MobileUiLayout.CalculateFitScale(new Vector2(940, 1560), safe,
                Screen.width, Screen.height, Vector2.one * 28f);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static RectTransform Stretch(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        static UnityEngine.UI.Image Picture(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent, position, size);
            UnityEngine.UI.Image image = name=="Accessory" ? rect.gameObject.AddComponent<InkAccessoryImage>() : rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; return image;
        }
        static UnityEngine.UI.Text Label(string name, Transform parent, string source, int size, Vector2 position, Vector2 bounds, bool strong)
        {
            var text = Rect(name, parent, position, bounds).gameObject.AddComponent<UnityEngine.UI.Text>();
            InkLocalizedText.SetSource(text, source);
            InkUiStyle.ApplyReadableText(text, size, TextAnchor.MiddleCenter, strong, true);
            text.color = InkPalette.TextDark; text.raycastTarget = false; return text;
        }
        static UnityEngine.UI.Button Button(string name, Transform parent, string source, Vector2 position, Vector2 size, out UnityEngine.UI.Text label)
        {
            var rect = Rect(name, parent, position, size);
            var hit = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = Color.clear;
            var surface = Picture("Paper", rect, InkUiStyle.ActionButtonSprite, Vector2.zero, size);
            InkUiStyle.ConfigureHanjiSurface(surface);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            InkUiStyle.ConfigureButton(button, surface); button.colors = InkUiStyle.ActionButtonColors();
            label = Label("Label", rect, source, 44, Vector2.zero, size - new Vector2(32, 16), true); return button;
        }
#if UNITY_EDITOR
        public void BuildForTests(Sprite sprite) { previewBodySprite = sprite; Build(); }
#endif
    }
}
