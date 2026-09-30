using System;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 실제 게임 플레이를 촬영한 프레임을 재생한다. 권한·장착·게임 진행은 변경하지 않는다.
    /// 동영상 디코더·네트워크 대신 작은 단일 텍스처를 양 플랫폼에서 동일하게 사용한다.
    [DisallowMultipleComponent]
    public sealed class GoldInkRecordedPreview : MonoBehaviour
    {
        public const string ResourcePath = "MukJump/UI/GoldPreview/gold-jump";
        public const int Columns = 6, Rows = 8, FrameCount = 48, FramesPerSecond = 16;
        public const int StillFrame = 43;
        CanvasGroup group;
        RectTransform safeRoot, paper;
        HanjiScrollFrame scroll;
        RawImage movie;
        Text unavailable;
        Texture2D recording;
        Action returned;
        float elapsed;
        int lastFrame = -1;
        public bool IsOpen { get; private set; }
        public int DisplayedFrame => lastFrame;
        public bool HasRecording => recording != null;

        public void Open(Action onReturn)
        {
            Build();
            returned = onReturn;
            scroll.CancelClose();
            recording = Resources.Load<Texture2D>(ResourcePath);
            movie.texture = recording; movie.enabled = recording != null;
            unavailable.gameObject.SetActive(recording == null);
            IsOpen = true; elapsed = 0; lastFrame = -1;
            group.alpha = 1; group.interactable = group.blocksRaycasts = true;
            PointerInput.SuppressUntilRelease();
            Layout(); ShowFrame(LobbySettingsProfile.ReducedMotionEnabled ? StillFrame : 0);
        }
        public void Close()
        {
            if (!IsOpen || !group.interactable) return;
            group.interactable = false;
            PointerInput.SuppressUntilRelease();
            scroll.Close(() =>
            {
                var callback = returned; Hide(); callback?.Invoke();
            }, group);
        }
        public void Hide()
        {
            IsOpen = false; returned = null;
            if (group != null) { group.alpha = 0; group.interactable = group.blocksRaycasts = false; }
            if (movie != null) movie.texture = null;
            if (recording != null) Resources.UnloadAsset(recording);
            recording = null; elapsed = 0; lastFrame = -1;
        }
        void OnDisable() => Hide();
        void Update()
        {
            if (!IsOpen) return;
            Layout();
            if (!MobileApplicationLifecycle.IsApplicationActive) return;
            if (LobbySettingsProfile.ReducedMotionEnabled) { ShowFrame(StillFrame); return; }
            elapsed = (elapsed + Time.unscaledDeltaTime) % (FrameCount / (float)FramesPerSecond);
            ShowFrame(Mathf.FloorToInt(elapsed * FramesPerSecond));
        }
        public static Rect FrameUv(int index)
        {
            index = Mathf.Clamp(index, 0, FrameCount - 1);
            // 촬영 아틀라스는 좌상단부터 행 우선으로 보관한다.
            const float insetX = .5f / (192 * Columns), insetY = .5f / (256 * Rows);
            return new Rect(index % Columns / (float)Columns + insetX,
                1f - (index / Columns + 1f) / Rows + insetY,
                1f / Columns - 2 * insetX, 1f / Rows - 2 * insetY);
        }
        void ShowFrame(int index)
        {
            if (lastFrame == index) return;
            movie.uvRect = FrameUv(index); lastFrame = index;
        }
        void Layout()
        {
            MobileUiLayout.ApplyCurrentSafeArea(safeRoot);
            paper.localScale = Vector3.one * MobileUiLayout.CalculateFitScale(new Vector2(920, 1640),
                MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height, Vector2.zero);
        }
        void Build()
        {
            if (group != null) return;
            var root = new GameObject("GoldPreviewCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4400;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<CanvasScaler>());
            group = root.GetComponent<CanvasGroup>();
            var dim = Rect("Blocker", root.transform, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            Stretch(dim.rectTransform); InkUiStyle.ConfigurePopupDim(dim);
            safeRoot = Rect("SafeArea", root.transform, Vector2.zero, Vector2.zero); Stretch(safeRoot);
            paper = Rect("PreviewPaper", safeRoot, Vector2.zero, new Vector2(880, 1380));
            scroll = HanjiScrollFrame.Attach(paper, new Vector2(880, 1360));
            InkUiStyle.ConfigurePopupDismiss(dim, paper, Close,
                () => IsOpen && group.interactable && (!Application.isPlaying || scroll.IsReady));
            Label("Title", "황금 먹의 특별한 착지효과", 64, 535, 170, true);
            movie = Rect("RecordedJump", paper, new Vector2(0, 45), new Vector2(576, 768)).gameObject.AddComponent<RawImage>();
            movie.raycastTarget = false;
            unavailable = Label("Unavailable", "미리보기를 불러오지 못했어요", 36, 45, 140, false);
            Hide();
        }
        Text Label(string name, string source, int font, float y, float height, bool strong)
        {
            var text = Rect(name, paper, new Vector2(0, y), new Vector2(740, height)).gameObject.AddComponent<Text>();
            InkLocalizedText.SetSource(text, source);
            InkUiStyle.ApplyReadableText(text, font, TextAnchor.MiddleCenter, strong, true);
            text.color = InkPalette.TextDark; text.raycastTarget = false; return text;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
