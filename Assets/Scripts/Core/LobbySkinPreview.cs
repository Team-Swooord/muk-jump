using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 게임 개체와 분리된 로비 장식. 드래그는 UI 안에서만 처리하고 게임 물리·기록은 건드리지 않는다.
    [DisallowMultipleComponent]
    public sealed class LobbySkinPreview : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float PreviewScale = .85f;
        public const float DriftSpeed = 32f;
        // idle 원화의 알파 경계 (100, 152)~(924, 866)/1024에 여유를 더한 범위.
        // 투명 프레임 전체를 피팅하면 실제 먹이 너무 작아진다. 원본 그림은 자르지 않는다.
        public static readonly Rect BodyDrawingBounds = new(-.415f, -.36f, .83f, .725f);
        [SerializeField] LobbyView lobby;
        [SerializeField] RectTransform area, drift, artwork, logo, start;
        [SerializeField] Image body, hitbox;
        [SerializeField] InkAccessoryImage accessory;
        InkSkinId shown = (InkSkinId)(-1);
        Vector2 fittedSize;
        Vector2 travel;
        Vector2 velocity, grabOffset;
        float elapsed;
        bool positioned, holding;
        int pointerId;
        uint inactiveGeneration;
        // 게임 난수·저장값과 분리된 시드. 로비를 새로 만들 때마다 다른 경로를 탄다.
        readonly int motionSeed = System.Guid.NewGuid().GetHashCode();
        public InkSkinId DisplayedSkin => shown;
        public RectTransform Drift => drift;
        public RectTransform Area => area;
        public bool IsVisible => drift != null && drift.gameObject.activeSelf;
        public bool IsDragging => holding;
        public Vector2 Velocity => velocity;
        public Vector2 Travel => travel;

        bool CanInteract => IsVisible && lobby != null && lobby.IsInteractive &&
            lobby.ActiveMenu != LobbyMenuSelection.Options && !StartupBrandSplash.IsBlockingInput &&
            MobileApplicationLifecycle.IsApplicationActive &&
            (GameManager.Instance == null || (GameManager.Instance.State == GameState.Lobby && !GameManager.Instance.IsPaused));

        public static LobbySkinPreview Build(RectTransform parent, LobbyView owner, Sprite idle)
        {
            var root = CreateRect("LobbySkinPreview", parent);
            // 메뉴·로고는 앞에서 그려지고 입력도 우선한다. 화면 전체를 덮는 입력판은 만들지 않는다.
            root.SetAsFirstSibling();
            var view = root.gameObject.AddComponent<LobbySkinPreview>();
            view.area = root; view.lobby = owner;
            view.logo = owner.LogoRect;
            view.start = owner.StartButton.transform as RectTransform;
            view.drift = CreateRect("Drift", root);
            view.hitbox = view.drift.gameObject.AddComponent<Image>();
            view.hitbox.color = Color.clear;
            view.hitbox.canvasRenderer.cullTransparentMesh = false;
            view.hitbox.raycastTarget = true;
            view.artwork = CreateRect("Artwork", view.drift);
            view.body = CreateRect("OriginalInkBody", view.artwork).gameObject.AddComponent<UnityEngine.UI.Image>();
            view.body.sprite = idle; view.body.preserveAspect = true; view.body.raycastTarget = false;
            view.accessory = CreateRect("Accessory", view.artwork).gameObject.AddComponent<InkAccessoryImage>();
            view.accessory.preserveAspect = true; view.accessory.raycastTarget = false;
            view.RefreshSkin(InkSkinId.Ink);
            view.RefreshLayout();
            return view;
        }

        void LateUpdate()
        {
            if (lobby == null || body == null || drift == null) return;
            bool show = lobby.IsVisible && !StartupBrandSplash.IsBlockingInput &&
                (GameManager.Instance == null || GameManager.Instance.State == GameState.Lobby);
            if (drift.gameObject.activeSelf != show) drift.gameObject.SetActive(show);
            if (!show) { CancelDrag(); return; }
            var id = InkSkinPreferences.Equipped;
            if (shown != id) RefreshSkin(id);
            RefreshLayout();
            bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
            if (!CanInteract || inactiveGeneration != MobileApplicationLifecycle.InactiveGeneration) CancelDrag();
            hitbox.raycastTarget = CanInteract;
            if (CanInteract && !reduced)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                elapsed += dt;
                if (!holding)
                {
                    Vector2 position = drift.anchoredPosition;
                    AdvanceBounce(ref position, ref velocity, travel, dt);
                    drift.anchoredPosition = position;
                }
            }
            // 기울이기·늘이기·발젓기 없이 착용한 형태 그대로 평행 이동한다.
            drift.localRotation = Quaternion.identity;
            drift.localScale = Vector3.one;
            accessory.PresentMotion(shown, elapsed, InkAccessoryMotion.Strength);
        }

        void RefreshSkin(InkSkinId id)
        {
            shown = id;
            accessory.sprite = InkSkinCatalog.Sprite(id);
            accessory.enabled = accessory.sprite != null;
            if (InkSkinCatalog.BehindBody(id)) accessory.transform.SetAsFirstSibling();
            else accessory.transform.SetAsLastSibling();
            fittedSize = new Vector2(-1, -1);
            CancelDrag();
        }

        void RefreshLayout()
        {
            if (logo == null || start == null || area.parent is not RectTransform parent) return;
            // 크기는 이전 로고–시작 버튼 사이의 크기를 유지하고 이동 범위만 Safe Area 전체로 넓힌다.
            float contentScale = logo.lossyScale.x / parent.lossyScale.x;
            float top = parent.InverseTransformPoint(logo.TransformPoint(new Vector3(0,
                Mathf.Lerp(logo.rect.yMin, logo.rect.yMax, .38f), 0))).y - 16f * contentScale;
            float bottom = parent.InverseTransformPoint(start.TransformPoint(new Vector3(0, start.rect.yMax, 0))).y + 20f * contentScale;
            float adInset = LobbyAdLayout.CalculateCanvasInset(MobileUiLayout.ReferenceHeight,
                Mathf.Max(LobbyAdLayout.TopInsetFraction, LobbyAdLayout.MeasuredTopInsetFraction));
            area.anchoredPosition = new Vector2(0, -adInset * .5f);
            area.sizeDelta = new Vector2(Mathf.Max(1f, parent.rect.width), Mathf.Max(1f, parent.rect.height - adInset));
            if (fittedSize == area.sizeDelta) return;
            fittedSize = area.sizeDelta;
            CancelDrag();
            Rect bounds = CostumeBounds(shown, accessory.sprite);
            Vector2 envelope = RotationEnvelope(bounds.size);
            float size = PreviewScale * Mathf.Max(.01f, Mathf.Min(360f * contentScale,
                Mathf.Min((area.rect.width - 64f * contentScale) * .48f / envelope.x,
                    Mathf.Max(1f, top - bottom) * .70f / envelope.y)));
            body.rectTransform.sizeDelta = Vector2.one * size;
            artwork.anchoredPosition = -bounds.center * size;
            if (accessory.enabled)
            {
                float width = size * InkSkinCatalog.Width(shown);
                accessory.rectTransform.sizeDelta = new Vector2(width, width * accessory.sprite.rect.height / accessory.sprite.rect.width);
                accessory.rectTransform.anchoredPosition = InkSkinCatalog.Offset(shown) * size;
            }
            // 액세서리를 포함한 외곽으로 벽을 판정한다. 투명한 터치 영역도 이 범위를 넘지 않는다.
            drift.sizeDelta = bounds.size * size;
            travel = Vector2.Max(Vector2.zero, (area.sizeDelta - drift.sizeDelta) * .5f - Vector2.one * 4f);
            if (!positioned)
            {
                drift.anchoredPosition = new Vector2(0, (top + bottom) * .5f - area.anchoredPosition.y);
                float angle = (float)new System.Random(motionSeed).NextDouble() * Mathf.PI * 2f;
                velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * DriftSpeed;
                positioned = true;
            }
            drift.anchoredPosition = ClampPosition(drift.anchoredPosition);
        }

        Vector2 ClampPosition(Vector2 position) => new(
            Mathf.Clamp(position.x, -travel.x, travel.x), Mathf.Clamp(position.y, -travel.y, travel.y));

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnPointerDown(PointerEventData e)
        {
            if (holding || !CanInteract || e.button != PointerEventData.InputButton.Left ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var point)) return;
            holding = true; pointerId = e.pointerId;
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            grabOffset = drift.anchoredPosition - point;
        }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e)
        {
            if (!holding || pointerId != e.pointerId) return;
            if (!CanInteract || inactiveGeneration != MobileApplicationLifecycle.InactiveGeneration) { CancelDrag(); return; }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var point)) return;
            Vector2 position = ClampPosition(point + grabOffset), delta = position - drift.anchoredPosition;
            if (delta.sqrMagnitude > .01f) velocity = delta.normalized * DriftSpeed;
            drift.anchoredPosition = position;
        }
        public void OnPointerUp(PointerEventData e) { if (holding && pointerId == e.pointerId) CancelDrag(); }
        public void OnEndDrag(PointerEventData e) => OnPointerUp(e);
        void CancelDrag() => holding = false;
        void OnDisable() => CancelDrag();
        void OnApplicationPause(bool paused) { if (paused) CancelDrag(); }
        void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }

        // 화면 밖으로 크게 끌거나 긴 프레임이 와도 외곽 안에서 반사한다. 게임 난수/물리는 사용하지 않는다.
        public static void AdvanceBounce(ref Vector2 position, ref Vector2 speed, Vector2 limits, float dt)
        {
            BounceAxis(ref position.x, ref speed.x, limits.x, dt);
            BounceAxis(ref position.y, ref speed.y, limits.y, dt);
        }
        static void BounceAxis(ref float position, ref float speed, float limit, float dt)
        {
            if (limit <= 0f) { position = 0f; return; }
            float offset = Mathf.Clamp(position, -limit, limit) + speed * Mathf.Max(0f, dt) + limit;
            float folded = Mathf.Repeat(offset, 4f * limit);
            position = folded <= 2f * limit ? folded - limit : 3f * limit - folded;
            if (folded > 2f * limit) speed = -speed;
            if (position >= limit) speed = -Mathf.Abs(speed);
            else if (position <= -limit) speed = Mathf.Abs(speed);
        }
        public static Vector2 RotationEnvelope(Vector2 size)
        {
            // 이전 버전의 미리보기 크기만 보존한다. 실제 먹은 더 이상 기울이지 않는다.
            float s = Mathf.Sin(24f * Mathf.Deg2Rad);
            return new Vector2(size.x + size.y * s, size.y + size.x * s);
        }
        public static Rect CostumeBounds(InkSkinId id, Sprite sprite)
        {
            Vector2 min = BodyDrawingBounds.min, max = BodyDrawingBounds.max;
            if (sprite != null)
            {
                if (InkSkinCatalog.IsFootwear(id))
                {
                    for (int side = 0; side < 2; side++)
                    {
                        Rect r = InkFootwearPose.DrawingRect(id, sprite, side == 1);
                        min = Vector2.Min(min, r.min); max = Vector2.Max(max, r.max);
                    }
                }
                else
                {
                    float width = InkSkinCatalog.Width(id);
                    Vector2 half = new Vector2(width, width * sprite.rect.height / sprite.rect.width) * .5f;
                    if (InkAccessoryMotion.Supports(id)) half *= 1f + InkAccessoryMotion.Padding * 2f;
                    min = Vector2.Min(min, InkSkinCatalog.Offset(id) - half);
                    max = Vector2.Max(max, InkSkinCatalog.Offset(id) + half);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            return rect;
        }
    }
}
