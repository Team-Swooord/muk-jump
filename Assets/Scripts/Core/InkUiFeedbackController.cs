using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// UI 탭과 영구 성장 성공을 한지 위 먹 번짐으로 알려주는 고정 크기 풀.
    /// 매 클릭마다 오브젝트를 생성하지 않고 transform·opacity만 애니메이션한다.
    [DisallowMultipleComponent]
    public sealed class InkUiFeedbackController : MonoBehaviour
    {
        const int PoolSize = 32;
        const int CanvasSortingOrder = 6200;

        sealed class InkMark
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Start;
            public Vector2 Velocity;
            public Vector2 Size;
            public Color Color;
            public float StartedAt;
            public float Duration;
            public float StartScale;
            public float EndScale;
            public bool Active;
        }

        public static InkUiFeedbackController Instance { get; private set; }

        readonly InkMark[] marks = new InkMark[PoolSize];
        RectTransform canvasRoot;
        GrowthUnlockPresentation unlockPresentation;
        int nextMark;
        int emissionSerial;

        public int ActiveMarkCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < marks.Length; i++)
                    if (marks[i]?.Active == true) count++;
                return count;
            }
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
            EnsureInitialized();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            for (int i = 0; i < marks.Length; i++)
            {
                if (marks[i] == null) continue;
                marks[i].Active = false;
                marks[i].Rect.gameObject.SetActive(false);
            }
            unlockPresentation?.ResetPresentation();
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < marks.Length; i++)
            {
                InkMark mark = marks[i];
                if (mark?.Active != true) continue;
                float t = Mathf.Clamp01((now - mark.StartedAt) /
                                        Mathf.Max(0.01f, mark.Duration));
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                mark.Rect.anchoredPosition =
                    mark.Start + mark.Velocity * (eased * mark.Duration);
                float scale = Mathf.Lerp(mark.StartScale, mark.EndScale, eased);
                mark.Rect.localScale = Vector3.one * scale;
                Color color = mark.Color;
                color.a *= 1f - Mathf.SmoothStep(0.35f, 1f, t);
                mark.Image.color = color;
                if (t < 1f) continue;
                mark.Active = false;
                mark.Rect.gameObject.SetActive(false);
            }
        }

        public static void PlayTap(Vector2 screenPosition)
        {
            Resolve()?.EmitTap(screenPosition);
        }

        public static void PlayGrowthUnlock(
            string growthName,
            Sprite growthIcon)
        {
            Resolve()?.EmitGrowthUnlock(
                growthName,
                growthIcon);
        }

        public static void PlayGrowthUnlock(
            string growthName,
            Sprite growthIcon,
            Vector2 screenPosition,
            Sprite fruitSprite)
        {
            Resolve()?.EmitGrowthUnlock(
                growthName,
                growthIcon,
                screenPosition,
                fruitSprite);
        }

        public static void PlayGrowthUpgrade(
            string growthName,
            Sprite growthIcon,
            int level)
        {
            Resolve()?.EmitGrowthUpgrade(
                growthName,
                growthIcon,
                level);
        }

        /// 화면이 사라질 때 새 컨트롤러를 만들지 않고 재생 중인 성장 연출만 정리한다.
        public static void CancelGrowthPresentation()
        {
            InkUiFeedbackController controller =
                Instance != null
                    ? Instance
                    : FindFirstObjectByType<InkUiFeedbackController>();
            if (controller == null)
                return;
            controller.unlockPresentation?.ResetPresentation();
        }

        static InkUiFeedbackController Resolve()
        {
            if (Instance != null) return Instance;
            var found = FindFirstObjectByType<InkUiFeedbackController>();
            if (found != null)
            {
                // EditMode 테스트·Play 중 재컴파일로 static만 초기화된 경우에도
                // 이미 활성인 컴포넌트를 다시 싱글톤에 연결한다.
                Instance = found;
                found.enabled = true;
                return found;
            }

            if (GameManager.Instance == null) return null;
            return GameManager.Instance.gameObject
                .AddComponent<InkUiFeedbackController>();
        }

        void EnsureInitialized()
        {
            if (canvasRoot != null) return;
            var root = new GameObject(
                "InkUiFeedbackCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            canvas.pixelPerfect = true;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            canvasRoot = root.GetComponent<RectTransform>();
            canvasRoot.anchorMin = Vector2.zero;
            canvasRoot.anchorMax = Vector2.one;
            canvasRoot.offsetMin = Vector2.zero;
            canvasRoot.offsetMax = Vector2.zero;

            unlockPresentation =
                GetComponent<GrowthUnlockPresentation>();
            if (unlockPresentation == null)
                unlockPresentation =
                    gameObject.AddComponent<GrowthUnlockPresentation>();
            unlockPresentation.Initialize(canvasRoot);

            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            for (int i = 0; i < marks.Length; i++)
            {
                var markObject = new GameObject(
                    $"InkTapMark{i + 1:00}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var rect = markObject.GetComponent<RectTransform>();
                rect.SetParent(canvasRoot, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var image = markObject.GetComponent<Image>();
                image.sprite = blob;
                image.raycastTarget = false;
                markObject.SetActive(false);
                marks[i] = new InkMark { Rect = rect, Image = image };
            }
        }

        void EmitTap(Vector2 screenPosition)
        {
            EnsureInitialized();
            Vector2 local = ScreenToLocal(screenPosition);
            emissionSerial++;
            Spawn(local, Vector2.zero, new Vector2(54f, 42f),
                InkPalette.Ink, 0.24f, 0.72f, 1.08f, emissionSerial * 19f);

            for (int i = 0; i < 5; i++)
            {
                float angle = (i * 72f + emissionSerial * 31f) * Mathf.Deg2Rad;
                float speed = 70f + (i % 3) * 24f;
                Vector2 velocity = new(Mathf.Cos(angle) * speed,
                    Mathf.Sin(angle) * speed);
                float size = 13f + (i % 2) * 8f;
                Spawn(local, velocity, new Vector2(size, size),
                    InkPalette.Ink, 0.28f, 0.65f, 1.25f, angle * Mathf.Rad2Deg);
            }
        }

        void EmitGrowthUnlock(
            string growthName,
            Sprite growthIcon)
        {
            EnsureInitialized();
            unlockPresentation?.Play(growthName, growthIcon);
        }

        void EmitGrowthUnlock(
            string growthName,
            Sprite growthIcon,
            Vector2 screenPosition,
            Sprite fruitSprite)
        {
            EnsureInitialized();
            unlockPresentation?.PlayAtNode(
                growthName,
                growthIcon,
                ScreenToLocal(screenPosition),
                fruitSprite);
        }

        void EmitGrowthUpgrade(
            string growthName,
            Sprite growthIcon,
            int level)
        {
            EnsureInitialized();
            unlockPresentation?.PlayUpgrade(
                growthName,
                growthIcon,
                level);
        }

        void Spawn(
            Vector2 start,
            Vector2 velocity,
            Vector2 size,
            Color color,
            float duration,
            float startScale,
            float endScale,
            float rotation)
        {
            InkMark mark = marks[nextMark];
            nextMark = (nextMark + 1) % marks.Length;
            mark.Start = start;
            mark.Velocity = velocity;
            mark.Size = size;
            mark.Color = color;
            mark.StartedAt = Time.unscaledTime;
            mark.Duration = duration;
            mark.StartScale = startScale;
            mark.EndScale = endScale;
            mark.Active = true;
            mark.Rect.gameObject.SetActive(true);
            mark.Rect.anchoredPosition = start;
            mark.Rect.sizeDelta = size;
            mark.Rect.localScale = Vector3.one * startScale;
            mark.Rect.localEulerAngles = new Vector3(0f, 0f, rotation);
            mark.Image.color = color;
            mark.Rect.SetAsLastSibling();
        }

        Vector2 ScreenToLocal(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                screenPosition,
                null,
                out Vector2 local);
            return local;
        }
    }
}
