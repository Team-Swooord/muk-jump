using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 성장 두루마리를 획득했을 때 한 가지 먹결을 고르는 전용 모달.
    /// 씬에 UI 계층을 직렬화하지 않고 최초 사용 시 한 번만 생성해 구형 씬도 지원한다.
    public sealed class GrowthChoiceView : MonoBehaviour
    {
        const string VitalityIconResourcePath = "MukJump/UI/Growth/growth_vitality";
        const string JumpIconResourcePath = "MukJump/UI/Growth/growth_jump";
        const int CanvasSortingOrder = 3000;
        const float RevealDuration = 0.26f;
        const float CloseDuration = 0.16f;
        const float RollOpenDistance = 470f;
        const float ClosedPaperScale = 0.12f;

        [Header("선택 카드 아이콘")]
        [SerializeField] Sprite vitalityIcon;
        [SerializeField] Sprite jumpIcon;

        public static GrowthChoiceView Instance { get; private set; }
        public bool IsOpen { get; private set; }

        CanvasGroup rootGroup;
        RectTransform safeAreaRoot;
        RectTransform panel;
        RectTransform scrollBody;
        RectTransform topRoll;
        RectTransform bottomRoll;
        RectTransform contentRect;
        CanvasGroup contentGroup;
        ChoiceCard vitalityCard;
        ChoiceCard jumpCard;
        RunGrowthController boundController;
        Coroutine visibilityRoutine;
        bool selectionLocked;
        int lastScreenWidth;
        int lastScreenHeight;
        Rect lastSafeArea;

        sealed class ChoiceCard
        {
            public RectTransform Root;
            public Button Button;
            public Image Paper;
            public Image Icon;
            public Text Status;
            public Text Effect;
            public CanvasGroup Group;
            public RectTransform SelectedSeal;
            public Text SelectedSealText;
        }

        void Awake()
        {
            if (Application.isPlaying)
                BuildIfNeeded();
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            Instance = this;
            BuildIfNeeded();
            BindController();
            BindButtons();
            ApplySafeArea();
        }

        void OnDisable()
        {
            bool cancelPendingChoice =
                IsOpen && boundController != null &&
                boundController.HasPendingChoice;
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }

            UnbindButtons();
            IsOpen = false;
            selectionLocked = false;
            if (rootGroup != null)
            {
                rootGroup.alpha = 0f;
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }
            if (cancelPendingChoice)
                boundController?.CancelChoice();
            UnbindController();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            BindController();
            // 씬 전환·게임오버가 성장 선택을 취소한 경우 모달이 다음 화면 위에
            // 남아 입력을 막지 않도록 즉시 정리한다.
            if (IsOpen &&
                (boundController == null || !boundController.HasPendingChoice))
            {
                HideImmediately();
                return;
            }
            if (lastScreenWidth != Screen.width ||
                lastScreenHeight != Screen.height ||
                lastSafeArea != Screen.safeArea)
                ApplySafeArea();
        }

        /// 씬 빌더가 임포트한 스프라이트를 명시적으로 주입할 때 사용한다.
        public void SetSprites(Sprite vitality, Sprite jump)
        {
            vitalityIcon = vitality;
            jumpIcon = jump;
            ResolveSprites();
            ApplyCardSprites();
        }

        public void Show(RunGrowthController controller)
        {
            if (controller == null) return;

            BuildIfNeeded();
            BindController(controller);
            ApplySafeArea();
            selectionLocked = false;
            IsOpen = true;
            SetSelectedCard(null);
            RefreshCards();

            if (visibilityRoutine != null)
                StopCoroutine(visibilityRoutine);
            visibilityRoutine = StartCoroutine(RevealRoutine());
        }

        /// 테스트나 세션 정리에서 사용할 수 있는 닫기 진입점. 플레이 중에는 선택 후에만 호출한다.
        public void Hide()
        {
            if (!IsOpen) return;
            BeginClose();
        }

        void BindController()
        {
            BindController(RunGrowthController.Instance);
        }

        void BindController(RunGrowthController controller)
        {
            if (controller == boundController) return;
            UnbindController();
            boundController = controller;
            if (boundController == null) return;
            boundController.ChoiceRequested += HandleChoiceRequested;
            boundController.ChoiceCancelled += HandleChoiceCancelled;
            boundController.Changed += HandleGrowthChanged;
            // 런타임 폴백으로 이 컴포넌트가 이벤트 뒤에 추가되어도 정지 화면을 복구한다.
            if (boundController.HasPendingChoice && !IsOpen)
                Show(boundController);
        }

        void UnbindController()
        {
            if (boundController != null)
            {
                boundController.ChoiceRequested -= HandleChoiceRequested;
                boundController.ChoiceCancelled -= HandleChoiceCancelled;
                boundController.Changed -= HandleGrowthChanged;
            }
            boundController = null;
        }

        void BindButtons()
        {
            UnbindButtons();
            vitalityCard?.Button.onClick.AddListener(HandleVitalityPressed);
            jumpCard?.Button.onClick.AddListener(HandleJumpPressed);
        }

        void UnbindButtons()
        {
            vitalityCard?.Button.onClick.RemoveListener(HandleVitalityPressed);
            jumpCard?.Button.onClick.RemoveListener(HandleJumpPressed);
        }

        void HandleChoiceRequested()
        {
            if (boundController != null)
                Show(boundController);
        }

        void HandleChoiceCancelled()
        {
            if (IsOpen)
                HideImmediately();
        }

        void HandleGrowthChanged()
        {
            if (IsOpen)
                RefreshCards();
        }

        void HandleVitalityPressed()
        {
            Select(GrowthUpgradeType.Vitality, vitalityCard);
        }

        void HandleJumpPressed()
        {
            Select(GrowthUpgradeType.JumpPower, jumpCard);
        }

        void Select(GrowthUpgradeType type, ChoiceCard selectedCard)
        {
            if (!IsOpen || selectionLocked || boundController == null) return;

            selectionLocked = true;
            SetCardsInteractable(false);
            if (!boundController.TrySelectUpgrade(type))
            {
                selectionLocked = false;
                RefreshCards();
                return;
            }

            SetSelectedCard(selectedCard);
            BeginClose();
        }

        void BeginClose()
        {
            if (visibilityRoutine != null)
                StopCoroutine(visibilityRoutine);
            rootGroup.interactable = false;
            visibilityRoutine = StartCoroutine(CloseRoutine());
        }

        void HideImmediately()
        {
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }

            ApplyRevealPose(0f);
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            IsOpen = false;
            selectionLocked = false;
        }

        IEnumerator RevealRoutine()
        {
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = false;
            ApplyRevealPose(0f);

            float elapsed = 0f;
            while (elapsed < RevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyRevealPose(elapsed / RevealDuration);
                yield return null;
            }

            ApplyRevealPose(1f);
            rootGroup.interactable = true;
            RefreshCards();
            visibilityRoutine = null;
        }

        IEnumerator CloseRoutine()
        {
            float elapsed = 0f;
            while (elapsed < CloseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Smooth01(elapsed / CloseDuration);
                ApplyRevealPose(1f - progress);
                yield return null;
            }

            ApplyRevealPose(0f);
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            IsOpen = false;
            selectionLocked = false;
            visibilityRoutine = null;

            // 닫힘 연출이 끝난 뒤에만 시간을 재개해 모달 뒤 게임이 먼저 움직이지 않게 한다.
            if (boundController != null)
            {
                if (boundController.HasSelectedPendingChoice)
                    boundController.FinishChoice();
                else
                    boundController.CancelChoice();
            }
        }

        void RefreshCards()
        {
            if (boundController == null || vitalityCard == null || jumpCard == null)
                return;

            int vitalityLevel = boundController.VitalityLevel;
            int jumpLevel = boundController.JumpLevel;
            bool vitalityMax = vitalityLevel >= RunGrowthController.MaxVitalityLevel;
            bool jumpMax = jumpLevel >= RunGrowthController.MaxJumpLevel;

            vitalityCard.Status.text = vitalityMax
                ? $"현재 Lv.{vitalityLevel}/{RunGrowthController.MaxVitalityLevel} · 완성"
                : $"현재 Lv.{vitalityLevel}/{RunGrowthController.MaxVitalityLevel} · 완충 {boundController.VitalityCharges}회";
            vitalityCard.Effect.text = vitalityMax
                ? "먹결이 가장 단단해졌습니다"
                : "먹떼 공용 완충 +1\n낙하는 막지 못합니다";

            int totalJumpPercent = jumpLevel * 4;
            jumpCard.Status.text = jumpMax
                ? $"현재 Lv.{jumpLevel}/{RunGrowthController.MaxJumpLevel} · 완성"
                : $"현재 Lv.{jumpLevel}/{RunGrowthController.MaxJumpLevel} · 총 +{totalJumpPercent}%";
            jumpCard.Effect.text = jumpMax
                ? "도약의 기운이 가득 찼습니다"
                : "자동 점프력 +4%\n더 높은 곳까지 솟습니다";

            bool canInteract = IsOpen && !selectionLocked &&
                               rootGroup != null && rootGroup.interactable;
            SetCardState(vitalityCard, canInteract && !vitalityMax, vitalityMax);
            SetCardState(jumpCard, canInteract && !jumpMax, jumpMax);
        }

        static void SetCardState(ChoiceCard card, bool interactable, bool maxed)
        {
            if (card == null) return;
            card.Button.interactable = interactable;
            card.Group.alpha = maxed ? 0.56f : 1f;
            card.Paper.color = maxed
                ? new Color(InkPalette.Paper2.r, InkPalette.Paper2.g, InkPalette.Paper2.b, 0.9f)
                : InkPalette.Paper;
        }

        void SetCardsInteractable(bool interactable)
        {
            if (vitalityCard != null) vitalityCard.Button.interactable = interactable;
            if (jumpCard != null) jumpCard.Button.interactable = interactable;
        }

        void SetSelectedCard(ChoiceCard selected)
        {
            ApplySelectedState(vitalityCard, selected == vitalityCard);
            ApplySelectedState(jumpCard, selected == jumpCard);
        }

        static void ApplySelectedState(ChoiceCard card, bool selected)
        {
            if (card == null) return;
            card.SelectedSeal.gameObject.SetActive(selected);
            card.Root.localScale = selected ? Vector3.one * 1.015f : Vector3.one;
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;

            ResolveSprites();
            var existing = transform.Find("GrowthChoiceCanvas");
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            var root = new GameObject(
                "GrowthChoiceCanvas",
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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            rootGroup = root.GetComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;

            var backdrop = CreateStretchImage(
                "InkWash",
                root.transform,
                new Color(0.035f, 0.032f, 0.028f, 0.6f));
            backdrop.raycastTarget = true;

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", root.transform);
            panel = CreateRect(
                "GrowthScrollPopup",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(840f, 1050f));

            BuildScrollPaper();
            BuildContent();
            ApplyCardSprites();
            ApplyRevealPose(0f);
        }

        void ResolveSprites()
        {
            if (vitalityIcon == null)
                vitalityIcon = Resources.Load<Sprite>(VitalityIconResourcePath);
            if (jumpIcon == null)
                jumpIcon = Resources.Load<Sprite>(JumpIconResourcePath);
        }

        void ApplyCardSprites()
        {
            if (vitalityCard?.Icon != null)
                vitalityCard.Icon.sprite = vitalityIcon;
            if (jumpCard?.Icon != null)
                jumpCard.Icon.sprite = jumpIcon;
        }

        void BuildScrollPaper()
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            scrollBody = CreateRect(
                "ScrollBody",
                panel,
                Vector2.zero,
                new Vector2(780f, 930f));

            var shadow = CreateImage(
                "InkBleedShadow",
                scrollBody,
                brush,
                new Vector2(13f, -15f),
                new Vector2(970f, 790f),
                new Color(0f, 0f, 0f, 0.16f));
            shadow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var outline = CreateImage(
                "ScrollBodyOutline",
                scrollBody,
                brush,
                Vector2.zero,
                new Vector2(952f, 778f),
                InkPalette.Ink);
            outline.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var paper = CreateImage(
                "ScrollPaper",
                scrollBody,
                brush,
                Vector2.zero,
                new Vector2(928f, 752f),
                InkPalette.Paper);
            paper.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            // 붓 마스크 섬유 틈 사이로 뒷배경이 비처럼 비치지 않도록 안쪽을 한지로 채운다.
            CreateImage(
                "PaperCore",
                scrollBody,
                null,
                Vector2.zero,
                new Vector2(686f, 886f),
                InkPalette.Paper);

            topRoll = CreateScrollRoll(panel, RollOpenDistance, true);
            bottomRoll = CreateScrollRoll(panel, -RollOpenDistance, false);
        }

        void BuildContent()
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            contentRect = CreateRect(
                "GrowthContent",
                panel,
                Vector2.zero,
                new Vector2(720f, 900f));
            contentGroup = contentRect.gameObject.AddComponent<CanvasGroup>();

            var title = CreateText(
                "Title",
                contentRect,
                "성장의 두루마리",
                54,
                new Vector2(0f, 382f),
                new Vector2(620f, 76f),
                InkPalette.TextDark,
                FontStyle.Normal);
            AddSoftWeight(title, InkPalette.Ink, 0.2f);

            CreateText(
                "Hint",
                contentRect,
                "하나의 먹결을 고르세요",
                27,
                new Vector2(0f, 322f),
                new Vector2(560f, 44f),
                ReadableMutedColor(),
                FontStyle.Normal);

            CreateImage(
                "TitleDivider",
                contentRect,
                brush,
                new Vector2(0f, 282f),
                new Vector2(340f, 7f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.16f));

            vitalityCard = CreateChoiceCard(
                "VitalityChoice",
                contentRect,
                new Vector2(0f, 113f),
                "먹두께",
                "먹떼 공용 완충 +1",
                vitalityIcon);
            jumpCard = CreateChoiceCard(
                "JumpChoice",
                contentRect,
                new Vector2(0f, -180f),
                "도약",
                "자동 점프력 +4%",
                jumpIcon);

            CreateText(
                "FooterHint",
                contentRect,
                "선택한 먹결은 이번 도전에만 이어집니다",
                22,
                new Vector2(0f, -352f),
                new Vector2(620f, 42f),
                new Color(InkPalette.TextMuted.r, InkPalette.TextMuted.g, InkPalette.TextMuted.b, 0.76f),
                FontStyle.Normal);
        }

        static ChoiceCard CreateChoiceCard(
            string objectName,
            Transform parent,
            Vector2 position,
            string title,
            string defaultEffect,
            Sprite icon)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            var root = CreateRect(objectName, parent, position, new Vector2(680f, 250f));
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var shadow = CreateImage(
                "Shadow",
                root,
                brush,
                new Vector2(7f, -8f),
                new Vector2(674f, 244f),
                new Color(0f, 0f, 0f, 0.15f));
            shadow.raycastTarget = false;

            CreateImage(
                "Outline",
                root,
                brush,
                Vector2.zero,
                new Vector2(674f, 238f),
                InkPalette.Ink);
            var paper = CreateImage(
                "Paper",
                root,
                brush,
                Vector2.zero,
                new Vector2(654f, 216f),
                InkPalette.Paper);
            paper.raycastTarget = true;

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = paper;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.96f, 0.91f, 0.78f, 1f),
                pressedColor = new Color(0.88f, 0.8f, 0.64f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.76f, 0.74f, 0.7f, 0.7f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };

            var iconImage = CreateImage(
                "Icon",
                root,
                icon,
                new Vector2(-225f, 0f),
                new Vector2(172f, 172f),
                Color.white);
            iconImage.preserveAspect = true;

            var nameText = CreateText(
                "Name",
                root,
                title,
                42,
                new Vector2(72f, 60f),
                new Vector2(360f, 58f),
                InkPalette.TextDark,
                FontStyle.Normal);
            nameText.alignment = TextAnchor.MiddleLeft;
            AddSoftWeight(nameText, InkPalette.Ink, 0.18f);

            var statusText = CreateText(
                "Status",
                root,
                "현재 Lv.0",
                24,
                new Vector2(72f, 10f),
                new Vector2(360f, 42f),
                ReadableMutedColor(),
                FontStyle.Normal);
            statusText.alignment = TextAnchor.MiddleLeft;

            var effectText = CreateText(
                "Effect",
                root,
                defaultEffect,
                25,
                new Vector2(72f, -58f),
                new Vector2(370f, 82f),
                InkPalette.TextDark,
                FontStyle.Normal);
            effectText.alignment = TextAnchor.MiddleLeft;

            var seal = CreateRect(
                "SelectedSeal",
                root,
                new Vector2(288f, 83f),
                new Vector2(66f, 66f));
            seal.localEulerAngles = new Vector3(0f, 0f, -7f);
            CreateImage("Seal", seal, blob, Vector2.zero, new Vector2(62f, 62f), InkPalette.Red);
            var sealText = CreateText(
                "Text",
                seal,
                "결",
                25,
                Vector2.zero,
                new Vector2(44f, 42f),
                InkPalette.Paper,
                FontStyle.Normal);
            seal.gameObject.SetActive(false);

            return new ChoiceCard
            {
                Root = root,
                Button = button,
                Paper = paper,
                Icon = iconImage,
                Status = statusText,
                Effect = effectText,
                Group = group,
                SelectedSeal = seal,
                SelectedSealText = sealText,
            };
        }

        void ApplyRevealPose(float progress)
        {
            float t = Mathf.Clamp01(progress);
            float appear = EaseOutCubic(Mathf.InverseLerp(0f, 0.22f, t));
            float unroll = EaseOutCubic(Mathf.InverseLerp(0.02f, 0.82f, t));
            float content = EaseOutCubic(Mathf.InverseLerp(0.25f, 0.86f, t));

            rootGroup.alpha = appear;
            panel.localScale = Vector3.one * Mathf.Lerp(0.965f, 1f, unroll);
            scrollBody.localScale = new Vector3(
                1f,
                Mathf.Lerp(ClosedPaperScale, 1f, unroll),
                1f);
            topRoll.anchoredPosition = Vector2.up * (RollOpenDistance * unroll);
            bottomRoll.anchoredPosition = Vector2.down * (RollOpenDistance * unroll);
            contentGroup.alpha = content;
            contentRect.anchoredPosition = Vector2.down * (14f * (1f - content));
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safe = Screen.safeArea;
            safeAreaRoot.anchorMin = new Vector2(
                Mathf.Clamp01(safe.xMin / Screen.width),
                Mathf.Clamp01(safe.yMin / Screen.height));
            safeAreaRoot.anchorMax = new Vector2(
                Mathf.Clamp01(safe.xMax / Screen.width),
                Mathf.Clamp01(safe.yMax / Screen.height));
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = safe;
        }

        static RectTransform CreateScrollRoll(Transform parent, float y, bool top)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            var root = CreateRect(
                top ? "TopRoll" : "BottomRoll",
                parent,
                new Vector2(0f, y),
                new Vector2(820f, 96f));

            CreateImage(
                "Shadow",
                root,
                brush,
                new Vector2(8f, -7f),
                new Vector2(794f, 78f),
                new Color(0f, 0f, 0f, 0.17f));
            var roll = CreateImage(
                "PaperRoll",
                root,
                brush,
                Vector2.zero,
                new Vector2(804f, 78f),
                InkPalette.Ink);
            CreateImage(
                "Paper",
                roll.transform,
                brush,
                Vector2.zero,
                new Vector2(778f, 58f),
                InkPalette.Paper2);
            CreateImage(
                "FoldShade",
                roll.transform,
                brush,
                new Vector2(0f, top ? -13f : 13f),
                new Vector2(740f, 8f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.12f));

            for (int side = -1; side <= 1; side += 2)
            {
                var cap = CreateImage(
                    side < 0 ? "LeftCap" : "RightCap",
                    root,
                    blob,
                    new Vector2(side * 388f, 0f),
                    new Vector2(82f, 82f),
                    InkPalette.Ink);
                CreateImage(
                    "Paper",
                    cap.transform,
                    blob,
                    Vector2.zero,
                    new Vector2(62f, 62f),
                    InkPalette.Paper2);
                CreateImage(
                    "Axis",
                    cap.transform,
                    blob,
                    Vector2.zero,
                    new Vector2(19f, 19f),
                    InkPalette.Ink);
            }
            return root;
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
            FontStyle style)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            return text;
        }

        static void AddSoftWeight(Text text, Color color, float alpha)
        {
            if (text == null) return;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(color.r, color.g, color.b, alpha);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
        }

        static Color ReadableMutedColor()
        {
            Color color = InkPalette.TextDark;
            color.a = 0.84f;
            return color;
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
