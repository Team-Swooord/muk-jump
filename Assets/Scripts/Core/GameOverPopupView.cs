using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 짧은 반복 플레이 흐름을 방해하지 않는 간결한 게임 종료 두루마리.
    /// MonoBehaviour 파일명과 클래스명을 일치시켜 씬 직렬화 시 Missing Script를 방지한다.
    public sealed partial class GameOverPopupView : MonoBehaviour
    {
        const int CanvasSortingOrder = 5000;
        const float RevealDuration = 0.56f;
        const float RollOpenDistance = 550f;
        const string ReviveRewardLabel = "광고 시청하고 부활하기";
        const float ActionWidth = 640f;
        const float PrimaryActionY = -268f;
        const float SecondaryActionY = -428f;
        const float SingleActionY = -350f;
        const float SingleActionHeight = 132f;
        const float TwoLineActionHeight = 188f;
        const int ResultActionFontSize = 56;
        static readonly Vector2 PanelDesignSize = new(840f, 1200f);
        static readonly Vector2 PanelEdgePadding = new(28f, 32f);

        CanvasGroup rootGroup;
        RectTransform safeAreaRoot;
        RectTransform panel;
        RectTransform scrollBody;
        RectTransform topRoll;
        RectTransform bottomRoll;
        HanjiScrollPaperGraphic paperGraphic;
        HanjiScrollPaperGraphic paperShadow;
        CanvasGroup topRollGroup;
        RectTransform contentRect;
        RectTransform newBestSeal;
        CanvasGroup contentGroup;
        CanvasGroup newBestGroup;
        Text titleText;
        Text heightText;
        Text bestText;
        Text saveNoticeText;
        Text touchHint;
        Button reviveButton;
        Button lobbyButton;
        Text reviveButtonLabel;
        Action reviveRequested;
        Action lobbyRequested;
        Coroutine showRoutine;
        GameOverResult boundResult;
        int lastScreenWidth;
        int lastScreenHeight;
        Rect lastSafeArea;
        float panelLayoutScale = 1f;
        bool closing;
        float currentOpening;
        Action afterClose;
        public bool IsClosing => closing;

        void Update()
        {
            if (!Application.isPlaying || safeAreaRoot == null) return;
            if (lastScreenWidth != Screen.width ||
                lastScreenHeight != Screen.height ||
                lastSafeArea != MobileUiLayout.CurrentSafeArea)
                ApplySafeArea();
            if (!closing && rootGroup.blocksRaycasts && contentGroup.alpha >= .99f &&
                MobileApplicationLifecycle.IsApplicationActive)
                AdvanceGrowthProgress(Time.unscaledDeltaTime);
        }

        public void Show(int height, int best, bool reachedNewBest)
        {
            Show(new GameOverResult(
                height,
                best,
                reachedNewBest,
                0,
                0,
                true));
        }

        public void Show(GameOverResult result)
        {
            Show(result, false, false);
        }

        public void Show(
            GameOverResult result,
            bool canOfferRevive,
            bool settlementPending)
        {
            BuildIfNeeded();
            ApplySafeArea();
            boundResult = result;
            BindResult(result);
            BindGrowthProgress(result, true);
            SetReviveOffer(canOfferRevive, settlementPending);
            closing = false;
            afterClose = null;
            rootGroup.interactable = true;
            if (showRoutine != null)
                StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(ShowRoutine());
        }

        public void ConfigureActions(Action onRevive, Action onLobby)
        {
            reviveRequested = onRevive;
            lobbyRequested = onLobby;
        }

        public void SetReviveOffer(
            bool available,
            bool waitingForAvailability = false)
        {
            BuildIfNeeded();
            if (reviveButton == null || lobbyButton == null)
                return;

            bool visible = available || waitingForAvailability;
            reviveButton.gameObject.SetActive(visible);
            // 광고 요청 중 잠갔던 버튼 상태가 다음 게임오버 팝업까지 남지 않게 한다.
            // 광고 부활 직후 다시 전멸하더라도 메인 버튼은 항상 복구되어야 한다.
            lobbyButton.interactable = true;
            if (visible)
            {
                reviveButton.interactable = available;
                InkLocalizedText.SetSource(reviveButtonLabel, available
                    ? ReviveRewardLabel
                    : "광고 준비 중...");
                ApplyDualActionLayout();
                InkLocalizedText.SetSource(touchHint, "메인으로");
                InkUiStyle.SetActionButtonRole(
                    lobbyButton.GetComponent<Image>(),
                    ActionButtonRole.Secondary);
            }
            else
            {
                bool needsTwoLines = boundResult.PersistenceState !=
                                     GameOverPersistenceState.Complete;
                ApplySingleActionLayout(needsTwoLines);
                if (boundResult.PersistenceState ==
                    GameOverPersistenceState.Complete)
                    InkLocalizedText.SetSource(touchHint, "메인으로");
                InkUiStyle.SetActionButtonRole(
                    lobbyButton.GetComponent<Image>(),
                    ActionButtonRole.Primary);
            }
            InkUiStyle.RefreshActionButtonLayout(
                lobbyButton.GetComponent<Image>());
            ApplyResultActionTypography();
        }

        public void SetReviveRequestInFlight(bool inFlight)
        {
            if (reviveButton == null || lobbyButton == null)
                return;
            reviveButton.interactable = !inFlight;
            lobbyButton.interactable = !inFlight;
            InkLocalizedText.SetSource(reviveButtonLabel, inFlight
                ? "광고 여는 중..."
                : ReviveRewardLabel);
        }

        public void Hide()
        {
            Close(null);
        }

        public void Close(Action completed)
        {
            if (closing) return;
            FinishGrowthProgress();
            if (rootGroup == null || rootGroup.alpha <= 0.001f ||
                !Application.isPlaying || !isActiveAndEnabled ||
                LobbySettingsProfile.ReducedMotionEnabled)
            {
                HideImmediate();
                completed?.Invoke();
                return;
            }
            if (showRoutine != null) StopCoroutine(showRoutine);
            closing = true;
            afterClose = completed;
            rootGroup.interactable = false;
            contentGroup.interactable = false;
            // 월드 입력은 말림이 끝날 때까지 전체 화면 dim이 소유한다.
            rootGroup.blocksRaycasts = true;
            showRoutine = StartCoroutine(CloseRoutine());
        }

        void HideImmediate()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }
            closing = false;
            afterClose = null;
            if (rootGroup == null)
                return;
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        public void RefreshResult(GameOverResult result)
        {
            BuildIfNeeded();
            boundResult = result;
            BindResult(result);
            BindGrowthProgress(result, false);
            if (!result.IsGrowthPreview) SetReviveOffer(false);
            if (showRoutine == null && rootGroup.blocksRaycasts)
            {
                ApplyRevealPose(1f, result.ReachedNewBest);
            }
        }

        public string SaveNoticeLabel =>
            saveNoticeText != null ? saveNoticeText.text : string.Empty;
        public string TouchHintLabel =>
            touchHint != null ? touchHint.text : string.Empty;

        public void ShowPendingAbandonConfirmation()
        {
            BuildIfNeeded();
            if (reviveButton != null)
                reviveButton.gameObject.SetActive(false);
            if (lobbyButton != null)
            {
                lobbyButton.interactable = true;
                ApplySingleActionLayout(true);
            }
            InkLocalizedText.SetSource(touchHint, "기록·먹빛 포기\n한 번 더 눌러 확인");
            InkUiStyle.SetActionButtonRole(
                lobbyButton != null ? lobbyButton.GetComponent<Image>() : null,
                ActionButtonRole.Primary);
            InkUiStyle.RefreshActionButtonLayout(
                lobbyButton != null ? lobbyButton.GetComponent<Image>() : null);
            ApplyResultActionTypography();
        }

        void ApplyDualActionLayout()
        {
            ApplyActionGeometry(
                reviveButton,
                new Vector2(0f, PrimaryActionY),
                new Vector2(ActionWidth, 164f),
                ActionButtonLayout.TwoLine);
            ApplyActionGeometry(
                lobbyButton,
                new Vector2(0f, SecondaryActionY),
                new Vector2(ActionWidth, SingleActionHeight),
                ActionButtonLayout.SingleLine);
        }

        void ApplySingleActionLayout(bool twoLines)
        {
            ApplyActionGeometry(
                lobbyButton,
                new Vector2(0f, SingleActionY),
                new Vector2(
                    ActionWidth,
                    twoLines
                        ? TwoLineActionHeight
                        : SingleActionHeight),
                twoLines
                    ? ActionButtonLayout.TwoLine
                    : ActionButtonLayout.SingleLine);
        }

        static void ApplyActionGeometry(
            Button button,
            Vector2 position,
            Vector2 size,
            ActionButtonLayout layout)
        {
            if (button == null) return;
            RectTransform rect = button.transform as RectTransform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = button.GetComponent<Image>();
            InkUiStyle.SetActionButtonLayout(image, layout);
            InkUiStyle.RefreshActionButtonLayout(image);
        }

        // 결과창의 고정 56px Bold 규격은 공통 버튼의 재배치 후에도 유지한다.
        void ApplyResultActionTypography()
        {
            ApplyResultActionLabel(reviveButtonLabel);
            ApplyResultActionLabel(touchHint);
        }

        static void ApplyResultActionLabel(Text label)
        {
            if (label == null) return;
            label.fontSize = ResultActionFontSize;
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void OnDisable()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }
            HideImmediate();
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;

            var existing = transform.Find("GameOverPopupCanvas");
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            var root = new GameObject(
                "GameOverPopupCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            canvas.pixelPerfect = true;
            var scaler = root.GetComponent<CanvasScaler>();
            MobileUiLayout.ConfigurePortraitScaler(scaler);

            rootGroup = root.GetComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;

            var backdrop = CreateStretchImage(
                "InkWash",
                root.transform,
                InkUiStyle.PopupDimColor);
            InkUiStyle.ConfigurePopupDim(backdrop);

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", root.transform);
            panel = CreateRect(
                "ScrollResultPopup",
                safeAreaRoot,
                Vector2.zero,
                PanelDesignSize);

            BuildScrollPaper();
            BuildContent();
            ApplySafeArea();
            BindResult(new GameOverResult(0, 0, false, 0, 0, true));
            SetReviveOffer(false);
            ApplyRevealPose(0f, false);
        }

        void BuildScrollPaper()
        {
            Texture2D paperTexture = Resources.Load<Texture2D>(
                "MukJump/UI/PermanentGrowth/pg_hanji_background");
            scrollBody = CreateRect(
                "ScrollBody",
                panel,
                Vector2.zero,
                new Vector2(768f, 1100f));

            var shadowRect = CreateRect("InkBleedShadow", scrollBody,
                new Vector2(4f, -6f), new Vector2(748f, 1100f));
            paperShadow = shadowRect.gameObject.AddComponent<HanjiScrollPaperGraphic>();
            paperShadow.Configure(paperTexture, new Color(0.12f, 0.10f, 0.08f, 0.12f));
            var paperRect = CreateRect("ScrollPaper", scrollBody,
                Vector2.zero, new Vector2(748f, 1100f));
            paperGraphic = paperRect.gameObject.AddComponent<HanjiScrollPaperGraphic>();
            paperGraphic.Configure(paperTexture, paperTexture != null ? Color.white : InkPalette.Paper);

            topRoll = CreateScrollRoll(panel, RollOpenDistance, true);
            topRollGroup = topRoll.gameObject.AddComponent<CanvasGroup>();
            topRollGroup.blocksRaycasts = false;
            topRollGroup.interactable = false;
            bottomRoll = CreateScrollRoll(panel, -RollOpenDistance, false);
        }

        void BuildContent()
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();

            contentRect = CreateRect(
                "ResultContent",
                panel,
                Vector2.zero,
                new Vector2(660f, 1020f));
            contentGroup = contentRect.gameObject.AddComponent<CanvasGroup>();

            titleText = CreateText(
                "Title",
                contentRect,
                "도전 끝",
                58,
                new Vector2(0f, 450f),
                new Vector2(600f, 84f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);

            var currentResult = CreateRect(
                "CurrentResult",
                contentRect,
                new Vector2(0f, 260f),
                new Vector2(660f, 280f));
            CreateText(
                "Caption",
                currentResult,
                "이번 고도",
                48,
                new Vector2(0f, 98f),
                new Vector2(600f, 64f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            heightText = CreateText(
                "Value",
                currentResult,
                "0 m",
                156,
                new Vector2(0f, -42f),
                new Vector2(600f, 188f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);

            CreateImage(
                "RecordDivider",
                contentRect,
                brush,
                new Vector2(0f, 104f),
                new Vector2(600f, 6f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.16f));

            var bestResult = CreateRect(
                "BestResult",
                contentRect,
                new Vector2(0f, 20f),
                new Vector2(640f, 156f));
            CreateText(
                "Caption",
                bestResult,
                "최고 고도",
                48,
                new Vector2(0f, 40f),
                new Vector2(600f, 64f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            bestText = CreateText(
                "Value",
                bestResult,
                "0 m",
                64,
                new Vector2(0f, -38f),
                new Vector2(600f, 76f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);

            BuildNewBestSeal(contentRect, blob);
            BuildGrowthProgress(contentRect, brush);

            // 일반 결과에는 기록과 선택만 표시한다. 저장 문제가 있을 때만 안내한다.
            saveNoticeText = CreateText(
                "SaveNotice",
                contentRect,
                string.Empty,
                48,
                new Vector2(0f, -116f),
                new Vector2(600f, 56f),
                InkPalette.TextDark,
                FontStyle.Normal,
                TextAnchor.MiddleCenter);

            var reviveBrush = CreateImage(
                "ReviveBrush",
                contentRect,
                null,
                new Vector2(0f, PrimaryActionY),
                new Vector2(ActionWidth, InkUiStyle.ActionButtonTwoLineHeight),
                InkPalette.Red);
            reviveBrush.raycastTarget = true;
            reviveButton = reviveBrush.gameObject.AddComponent<Button>();
            reviveButton.targetGraphic = reviveBrush;
            reviveButton.onClick.AddListener(HandleRevivePressed);
            reviveButtonLabel = CreateText(
                "Label",
                reviveBrush.transform,
                ReviveRewardLabel,
                InkUiStyle.ActionButtonLabelSize,
                Vector2.zero,
                new Vector2(544f, 120f),
                InkPalette.TextDark,
                FontStyle.Bold);
            InkUiStyle.ConfigureActionButton(
                reviveButton,
                reviveBrush,
                reviveButtonLabel,
                ActionButtonRole.Primary,
                ActionButtonLayout.TwoLine,
                labelStyle: FontStyle.Bold);

            var retryBrush = CreateImage(
                "RetryBrush",
                contentRect,
                null,
                new Vector2(0f, SecondaryActionY),
                new Vector2(ActionWidth, InkUiStyle.MinimumTapHeight),
                InkPalette.Ink);
            retryBrush.raycastTarget = true;
            lobbyButton = retryBrush.gameObject.AddComponent<Button>();
            lobbyButton.targetGraphic = retryBrush;
            lobbyButton.onClick.AddListener(HandleLobbyPressed);
            touchHint = CreateText(
                "TouchHint",
                retryBrush.transform,
                "메인으로",
                InkUiStyle.ActionButtonLabelSize,
                Vector2.zero,
                new Vector2(536f, 80f),
                InkPalette.TextDark,
                FontStyle.Bold);
            InkUiStyle.ConfigureActionButton(
                lobbyButton,
                retryBrush,
                touchHint,
                ActionButtonRole.Secondary,
                ActionButtonLayout.SingleLine,
                labelStyle: FontStyle.Bold);
        }

        void HandleRevivePressed()
        {
            if (closing || reviveButton == null || !reviveButton.interactable)
                return;
            FinishGrowthProgress();
            reviveRequested?.Invoke();
        }

        void HandleLobbyPressed()
        {
            if (closing || lobbyButton == null || !lobbyButton.interactable)
                return;
            FinishGrowthProgress();
            lobbyRequested?.Invoke();
        }

        void BuildNewBestSeal(Transform parent, Sprite blob)
        {
            newBestSeal = CreateRect(
                "NewBestSeal",
                parent,
                new Vector2(254f, 328f),
                new Vector2(120f, 120f));
            newBestSeal.localEulerAngles = new Vector3(0f, 0f, -7f);
            newBestGroup = newBestSeal.gameObject.AddComponent<CanvasGroup>();

            CreateImage(
                "Shadow",
                newBestSeal,
                blob,
                new Vector2(4f, -5f),
                new Vector2(116f, 116f),
                new Color(0f, 0f, 0f, 0.17f));
            CreateImage(
                "Seal",
                newBestSeal,
                blob,
                Vector2.zero,
                new Vector2(112f, 112f),
                InkPalette.Red);
            CreateText(
                "NewBest",
                newBestSeal,
                "신기록",
                40,
                Vector2.zero,
                new Vector2(100f, 64f),
                InkPalette.Paper,
                FontStyle.Bold);
        }

        void BindResult(GameOverResult result)
        {
            InkLocalizedText.SetSource(titleText, ResultTitleForHeight(result.Height));
            titleText.fontSize = 64;
            string height = FormatHeight(result.Height);
            int unitStart = height.LastIndexOf(' ');
            // 숫자가 먼저 읽히도록 단위만 작게 분리한다. 자동 글자 축소는 사용하지 않는다.
            InkLocalizedText.SetSource(heightText, height.Substring(0, unitStart) +
                " <size=72>" + height.Substring(unitStart + 1) + "</size>");
            InkLocalizedText.SetSource(bestText, FormatHeight(result.Best));
            switch (result.PersistenceState)
            {
                case GameOverPersistenceState.ScoreBaselinePending:
                    SetSaveNotice("기록을 확인하지 못했어요");
                    InkLocalizedText.SetSource(touchHint, "이번 판 기록·먹빛 포기");
                    break;
                case GameOverPersistenceState.GrowthRecoveryRequired:
                    SetSaveNotice("성장 저장을 복구해 주세요");
                    InkLocalizedText.SetSource(touchHint, "로비에서 성장 복구");
                    break;
                case GameOverPersistenceState.RecordWritePending:
                    SetSaveNotice("기록 저장을 다시 시도해요");
                    InkLocalizedText.SetSource(touchHint, "재시도 중단하고 로비로");
                    break;
                default:
                    SetSaveNotice(string.Empty);
                    InkLocalizedText.SetSource(touchHint, "메인으로");
                    break;
            }
            newBestSeal.gameObject.SetActive(result.ReachedNewBest);
        }

        public static string ResultTitleForHeight(int meters)
        {
            int height = Mathf.Max(0, meters);
            if (height < 5) return "먹이 아직 덜 말랐어요";
            if (height < 10) return "발판보다 먼저 포기했어요";
            if (height < 20) return "그래도 두 자릿수예요";
            if (height < 50) return "제법 하찮게 올랐어요";
            return "먹방울치고 꽤 높았어요";
        }

        void SetSaveNotice(string message)
        {
            InkLocalizedText.SetSource(saveNoticeText, message);
            saveNoticeText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        // 기존 EditMode 레이아웃 테스트와 구형 호출 경로를 위한 단순 결과 바인딩.
        void BindResults(int height, int best, bool reachedNewBest)
        {
            BindResult(new GameOverResult(
                height,
                best,
                reachedNewBest,
                0,
                0,
                true));
        }

        IEnumerator ShowRoutine()
        {
            rootGroup.blocksRaycasts = true;
            if (LobbySettingsProfile.ReducedMotionEnabled)
            {
                ApplyRevealPose(1f, boundResult.ReachedNewBest);
                showRoutine = null;
                yield break;
            }
            ApplyRevealPose(0f, boundResult.ReachedNewBest);

            float elapsed = 0f;
            while (elapsed < RevealDuration)
            {
                if (!MobileApplicationLifecycle.IsApplicationActive)
                {
                    yield return null;
                    continue;
                }
                elapsed += Time.unscaledDeltaTime;
                ApplyRevealPose(
                    elapsed / RevealDuration,
                    boundResult.ReachedNewBest);
                yield return null;
            }

            ApplyRevealPose(1f, boundResult.ReachedNewBest);
            showRoutine = null;
        }

        IEnumerator CloseRoutine()
        {
            float startOpening = currentOpening;
            float startContent = contentGroup.alpha;
            float startAlpha = rootGroup.alpha;
            float elapsed = 0f;
            while (elapsed < HanjiScrollFrame.CloseDuration)
            {
                if (!MobileApplicationLifecycle.IsApplicationActive)
                {
                    yield return null;
                    continue;
                }
                elapsed = LobbySettingsProfile.ReducedMotionEnabled
                    ? HanjiScrollFrame.CloseDuration : elapsed + Time.unscaledDeltaTime;
                ApplyClosePose(elapsed / HanjiScrollFrame.CloseDuration,
                    startOpening, startContent, startAlpha);
                yield return null;
            }
            Action completed = afterClose;
            showRoutine = null;
            HideImmediate();
            completed?.Invoke();
        }

        void ApplyClosePose(float progress, float startOpening, float startContent, float startAlpha)
        {
            float t = Mathf.Clamp01(progress);
            ApplyPaperPose(Mathf.Lerp(startOpening, 0f, Smooth01(t)), 0f, false);
            contentGroup.alpha = startContent * (1f - Smooth01(Mathf.Clamp01(t / 0.25f)));
            contentGroup.interactable = contentGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
            rootGroup.alpha = startAlpha * (1f - Smooth01(Mathf.InverseLerp(0.78f, 1f, t)));
        }

        /// 시간 대기 없이 팝업 진입 자세를 검증할 수 있도록 정규화된 진행률만 적용한다.
        void ApplyRevealPose(float progress, bool reachedNewBest)
        {
            float t = Mathf.Clamp01(progress);
            float appear = EaseOutCubic(Mathf.InverseLerp(0f, 0.2f, t));
            float unroll = EaseOutCubic(Mathf.InverseLerp(0.02f, 0.92f, t));
            float content = Smooth01(Mathf.InverseLerp(0.58f, 0.95f, t));

            rootGroup.alpha = appear;
            panel.localScale = Vector3.one * panelLayoutScale;
            panel.localEulerAngles = Vector3.zero;
            // 위 축은 고정하고 아래 롤만 내려온다. 한지 UV·내용의 크기는 늘리지 않는다.
            ApplyPaperPose(unroll, 0f, false);
            contentGroup.alpha = content;
            contentGroup.interactable = content >= 0.99f;
            contentGroup.blocksRaycasts = content >= 0.99f;
            contentRect.anchoredPosition = Vector2.zero;

            if (!reachedNewBest)
            {
                newBestGroup.alpha = 0f;
                newBestSeal.localScale = Vector3.one * 0.9f;
                newBestSeal.localEulerAngles = new Vector3(0f, 0f, -12f);
                return;
            }

            float stamp = Mathf.Clamp01(Mathf.InverseLerp(0.62f, 1f, t));
            float stampScale;
            if (stamp < 0.58f)
            {
                float press = EaseOutCubic(stamp / 0.58f);
                stampScale = Mathf.Lerp(0.88f, 1.035f, press);
            }
            else
            {
                float settle = Smooth01(Mathf.InverseLerp(0.58f, 1f, stamp));
                stampScale = Mathf.Lerp(1.035f, 1f, settle);
            }

            newBestGroup.alpha = EaseOutCubic(stamp);
            newBestSeal.localScale = Vector3.one * stampScale;
            newBestSeal.localEulerAngles = new Vector3(
                0f,
                0f,
                Mathf.Lerp(-12f, -7f, EaseOutCubic(stamp)));
        }

        void ApplyPaperPose(float opening, float seconds, bool flutter)
        {
            currentOpening = opening;
            float fraction = Mathf.Lerp(HanjiScrollPaperGraphic.ClosedFraction, 1f, opening);
            scrollBody.localScale = Vector3.one;
            // 결과 화면도 열린 뒤에는 시간과 무관한 정지 자세만 사용한다.
            paperGraphic.SetPose(opening, 0f, 0f);
            paperShadow.SetPose(opening, 0f, 0f);
            topRoll.anchoredPosition = new Vector2(0f, RollOpenDistance);
            // 시작부터 상단 말림이 종이 단면을 덮는다. 전체 등장은 rootGroup이 담당한다.
            topRollGroup.alpha = 1f;
            bottomRoll.anchoredPosition = new Vector2(0f,
                RollOpenDistance - 2f * RollOpenDistance * fraction);
            // 풀리면서 롤이 얇아지는 일회성 동작만 유지한다.
            bottomRoll.localScale = new Vector3(1f, Mathf.Lerp(1.34f, 1f, opening), 1f);
            bottomRoll.localEulerAngles = Vector3.zero;
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(
                safeAreaRoot,
                safe,
                Screen.width,
                Screen.height);

            float previousLayoutScale = Mathf.Max(0.01f, panelLayoutScale);
            float presentationScale = panel != null
                ? panel.localScale.x / previousLayoutScale
                : 1f;
            panelLayoutScale = MobileUiLayout.CalculateFitScale(
                PanelDesignSize,
                safe,
                Screen.width,
                Screen.height,
                PanelEdgePadding);
            if (panel != null)
            {
                panel.anchoredPosition = Vector2.zero;
                panel.localScale = Vector3.one *
                                   (panelLayoutScale * presentationScale);
            }
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = safe;
        }

        static RectTransform CreateScrollRoll(Transform parent, float y, bool top)
        {
            Sprite rollArt = Resources.Load<Sprite>("MukJump/UI/Common/scroll_roll_hanji_v2");
            var root = CreateRect(
                top ? "TopRoll" : "BottomRoll",
                parent,
                new Vector2(0f, y),
                new Vector2(804f, 62f));
            CreateImage(
                "PaperRoll",
                root,
                rollArt,
                Vector2.zero,
                new Vector2(804f, 62f),
                rollArt != null ? Color.white : InkPalette.Paper2);
            return root;
        }

        static string FormatHeight(int meters)
        {
            int nonNegative = Mathf.Max(0, meters);
            return nonNegative >= 10000
                ? $"{nonNegative / 1000f:0.#} km"
                : $"{nonNegative} m";
        }

        static RectTransform CreateRect(
            string objectName, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform CreateStretchRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Image CreateImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image CreateStretchImage(string objectName, Transform parent, Color color)
        {
            var rect = CreateStretchRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size,
            Color color,
            FontStyle style,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            InkLocalizedText.SetSource(text, value);
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignByGeometry = true;
            InkLocalizedText.Bind(text);
            return text;
        }

        static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
