using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로비에서 먹빛을 사용해 먹방울이를 영구 강화하는 전용 화면.
    /// 플레이 중 3지선다 두루마리 UI와 데이터 소유권을 공유하지 않는다.
    [DisallowMultipleComponent]
    public sealed class PermanentGrowthView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4050;

        sealed class GrowthRow
        {
            public RectTransform Root;
            public Image Paper;
            public Button Button;
            public Text Name;
            public Text Description;
            public Text Level;
            public Text Effect;
            public Text Cost;
            public PermanentGrowthType Type;
        }

        readonly List<GrowthRow> rows =
            new(PermanentGrowthCatalog.All.Count);

        CanvasGroup rootGroup;
        RectTransform safeAreaRoot;
        Text balanceText;
        GameManager manager;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;

        public bool IsOpen =>
            rootGroup != null && rootGroup.blocksRaycasts;
        public int CreatedRowCount => rows.Count;
        public string BalanceLabel => balanceText != null
            ? balanceText.text
            : string.Empty;

        void Awake()
        {
            BuildIfNeeded();
            CloseImmediate();
        }

        void OnEnable()
        {
            BuildIfNeeded();
            BindManager();
            PermanentGrowthProfile.Changed += HandleProfileChanged;
        }

        void OnDisable()
        {
            PermanentGrowthProfile.Changed -= HandleProfileChanged;
            UnbindManager();
            CloseImmediate();
        }

        void Update()
        {
            if (manager == null)
                BindManager();
            if (manager != null && manager.State != GameState.Lobby && IsOpen)
                Close();
            if (Screen.width != lastScreenWidth ||
                Screen.height != lastScreenHeight ||
                Screen.safeArea != lastSafeArea)
                ApplySafeArea();
        }

        public void Open()
        {
            BuildIfNeeded();
            BindManager();
            if (manager == null || manager.State != GameState.Lobby)
            {
                CloseImmediate();
                return;
            }
            Refresh();
            SetVisible(true);
        }

        public void Close()
        {
            SetVisible(false);
        }

        public void BuildForTests()
        {
            BuildIfNeeded();
            Refresh();
            CloseImmediate();
        }

        void BindManager()
        {
            GameManager next = GameManager.Instance;
            if (ReferenceEquals(manager, next)) return;
            UnbindManager();
            manager = next;
            if (manager != null)
                manager.StateChanged += HandleStateChanged;
        }

        void UnbindManager()
        {
            if (manager != null)
                manager.StateChanged -= HandleStateChanged;
            manager = null;
        }

        void HandleStateChanged(GameState previous, GameState current)
        {
            if (current != GameState.Lobby)
                Close();
        }

        void HandleProfileChanged()
        {
            Refresh();
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;

            var stale = transform.Find("PermanentGrowthCanvas");
            if (stale != null)
            {
                if (Application.isPlaying)
                    Destroy(stale.gameObject);
                else
                    DestroyImmediate(stale.gameObject);
            }

            var root = new GameObject(
                "PermanentGrowthCanvas",
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
            var dim = CreateStretchImage(
                "InkDim",
                root.transform,
                new Color(0.025f, 0.023f, 0.02f, 0.62f));
            dim.raycastTarget = true;

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", root.transform);
            var panel = CreateRect(
                "PermanentGrowthScroll",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(900f, 1510f));
            BuildScrollFrame(panel);

            var title = CreateText(
                "Title",
                panel,
                "먹방울 성장",
                62,
                new Vector2(0f, 620f),
                new Vector2(700f, 86f),
                InkPalette.TextDark,
                FontStyle.Bold);
            AddReadableWeight(title, 0.22f);

            var subtitle = CreateText(
                "Subtitle",
                panel,
                "모든 도전에 계속 적용되는 힘",
                31,
                new Vector2(0f, 548f),
                new Vector2(720f, 62f),
                ReadableMutedColor(),
                FontStyle.Bold);
            AddReadableWeight(subtitle, 0.12f);

            var balanceBrush = CreateImage(
                "CurrencyBrush",
                panel,
                InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, 465f),
                new Vector2(500f, 88f),
                InkPalette.Ink);
            balanceText = CreateText(
                "Balance",
                balanceBrush.transform,
                "보유 먹빛 0",
                35,
                Vector2.zero,
                new Vector2(430f, 62f),
                InkPalette.Paper,
                FontStyle.Bold);

            for (int i = 0; i < PermanentGrowthCatalog.All.Count; i++)
            {
                int slot = i;
                GrowthRow row = CreateGrowthRow(panel, i);
                row.Type = PermanentGrowthCatalog.All[i].Type;
                row.Button.onClick.AddListener(() => HandlePurchase(slot));
                rows.Add(row);
            }

            var footer = CreateText(
                "PermanentHint",
                panel,
                "두루마리 성장과 별개 · 게임을 꺼도 저장됩니다",
                28,
                new Vector2(0f, -500f),
                new Vector2(730f, 70f),
                ReadableMutedColor(),
                FontStyle.Bold);
            AddReadableWeight(footer, 0.12f);

            var close = CreateBrushButton(
                "CloseButton",
                panel,
                "닫기",
                new Vector2(0f, -625f),
                new Vector2(420f, 90f),
                36);
            close.onClick.AddListener(Close);

            ApplySafeArea();
            Refresh();
        }

        void BuildScrollFrame(Transform panel)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            var shadow = CreateImage(
                "InkShadow",
                panel,
                brush,
                new Vector2(12f, -15f),
                new Vector2(1510f, 890f),
                new Color(0f, 0f, 0f, 0.16f));
            shadow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var outline = CreateImage(
                "ScrollOutline",
                panel,
                brush,
                Vector2.zero,
                new Vector2(1490f, 870f),
                InkPalette.Ink);
            outline.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var paper = CreateImage(
                "HanjiPaper",
                panel,
                brush,
                Vector2.zero,
                new Vector2(1462f, 842f),
                InkPalette.Paper);
            paper.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            CreateImage(
                "PaperCore",
                panel,
                null,
                Vector2.zero,
                new Vector2(790f, 1390f),
                InkPalette.Paper);
        }

        GrowthRow CreateGrowthRow(Transform parent, int index)
        {
            float y = 330f - index * 190f;
            var root = CreateRect(
                $"PermanentGrowth{index + 1}",
                parent,
                new Vector2(0f, y),
                new Vector2(790f, 166f));
            var outline = CreateImage(
                "Outline",
                root,
                null,
                Vector2.zero,
                new Vector2(790f, 166f),
                InkPalette.Ink);
            var paper = CreateImage(
                "Paper",
                outline.transform,
                null,
                Vector2.zero,
                new Vector2(778f, 154f),
                InkPalette.Paper2);
            paper.raycastTarget = true;
            var button = outline.gameObject.AddComponent<Button>();
            button.targetGraphic = paper;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = ReadableButtonColors();

            var name = CreateText(
                "Name",
                paper.transform,
                "영구 성장",
                37,
                new Vector2(-246f, 45f),
                new Vector2(280f, 50f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            var level = CreateText(
                "Level",
                paper.transform,
                "Lv. 0 / 6",
                26,
                new Vector2(265f, 47f),
                new Vector2(180f, 42f),
                ReadableMutedColor(),
                FontStyle.Bold,
                TextAnchor.MiddleRight);
            var description = CreateText(
                "Description",
                paper.transform,
                "효과 설명",
                25,
                new Vector2(-95f, -3f),
                new Vector2(590f, 42f),
                InkPalette.TextMuted,
                FontStyle.Normal,
                TextAnchor.MiddleLeft);
            var effect = CreateText(
                "Effect",
                paper.transform,
                "현재 +0% → 다음 +0%",
                25,
                new Vector2(-165f, -50f),
                new Vector2(450f, 42f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            var cost = CreateText(
                "Cost",
                paper.transform,
                "먹빛 0",
                27,
                new Vector2(255f, -48f),
                new Vector2(200f, 48f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleRight);

            return new GrowthRow
            {
                Root = root,
                Paper = paper,
                Button = button,
                Name = name,
                Description = description,
                Level = level,
                Effect = effect,
                Cost = cost,
            };
        }

        void HandlePurchase(int slot)
        {
            if (slot < 0 || slot >= rows.Count) return;
            PermanentGrowthProfile.TryPurchase(rows[slot].Type);
        }

        void Refresh()
        {
            if (balanceText == null) return;
            balanceText.text = $"보유 먹빛 {PermanentGrowthProfile.Currency}";

            for (int i = 0; i < rows.Count; i++)
            {
                GrowthRow row = rows[i];
                PermanentGrowthDefinition definition =
                    PermanentGrowthCatalog.Get(row.Type);
                if (definition == null) continue;

                int level = PermanentGrowthProfile.GetLevel(row.Type);
                bool maxed = level >= definition.MaxLevel;
                int cost = definition.GetCost(level);
                float currentPercent = definition.GetPercentAtLevel(level);
                float nextPercent = definition.GetPercentAtLevel(level + 1);
                string sign = definition.ReducesValue ? "-" : "+";

                row.Name.text = definition.Name;
                row.Description.text = definition.Description;
                row.Level.text = $"Lv. {level} / {definition.MaxLevel}";
                row.Effect.text = maxed
                    ? $"{definition.EffectUnit} {sign}{FormatPercent(currentPercent)}% · 완성"
                    : $"현재 {sign}{FormatPercent(currentPercent)}%  →  " +
                      $"{sign}{FormatPercent(nextPercent)}%";
                row.Cost.text = maxed ? "완성" : $"먹빛 {cost}";
                row.Button.interactable = !maxed &&
                                          PermanentGrowthProfile.Currency >= cost;
                row.Paper.color = maxed
                    ? new Color(
                        InkPalette.Paper.r,
                        InkPalette.Paper.g,
                        InkPalette.Paper.b,
                        1f)
                    : InkPalette.Paper2;
            }
        }

        void SetVisible(bool visible)
        {
            if (rootGroup == null) return;
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = visible;
            rootGroup.blocksRaycasts = visible;
            ApplySafeArea();
        }

        void CloseImmediate()
        {
            SetVisible(false);
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

        static string FormatPercent(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.#");
        }

        static RectTransform CreateRect(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size)
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

        static Image CreateStretchImage(
            string objectName,
            Transform parent,
            Color color)
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
            text.text = value;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            return text;
        }

        static Button CreateBrushButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            int fontSize)
        {
            var brush = CreateImage(
                objectName,
                parent,
                InkUiTextureFactory.CreateBrushSprite(),
                position,
                size,
                InkPalette.Ink);
            brush.raycastTarget = true;
            var button = brush.gameObject.AddComponent<Button>();
            button.targetGraphic = brush;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = ReadableButtonColors();
            CreateText(
                "Label",
                brush.transform,
                label,
                fontSize,
                Vector2.zero,
                size - new Vector2(36f, 14f),
                InkPalette.TextLight,
                FontStyle.Bold);
            return button;
        }

        static ColorBlock ReadableButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.94f, 0.88f, 1f);
            colors.pressedColor = new Color(0.76f, 0.72f, 0.64f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.52f, 0.5f, 0.46f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        static Color ReadableMutedColor()
        {
            Color muted = InkPalette.TextMuted;
            muted.a = 0.9f;
            return muted;
        }

        static void AddReadableWeight(Text text, float alpha)
        {
            if (text == null) return;
            var shadow = text.gameObject.AddComponent<Shadow>();
            Color ink = InkPalette.Ink;
            shadow.effectColor = new Color(ink.r, ink.g, ink.b, alpha);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
        }
    }
}
