using UnityEngine;

namespace MukJump.Core
{
    /// 게임 개체와 분리된 로비 장식. 착용 정보만 읽으며 입력·물리·기록을 만들지 않는다.
    [DisallowMultipleComponent]
    public sealed class LobbySkinPreview : MonoBehaviour
    {
        const float MaximumTilt = 24f;
        public const float PreviewScale = .85f;
        const float MaximumBodyStretch = 1.025f;
        // idle 원화의 알파 경계 (100, 152)~(924, 866)/1024에 여유를 더한 범위.
        // 투명 프레임 전체를 피팅하면 실제 먹이 너무 작아진다. 원본 그림은 자르지 않는다.
        public static readonly Rect BodyDrawingBounds = new(-.415f, -.36f, .83f, .725f);
        [SerializeField] LobbyView lobby;
        [SerializeField] RectTransform area, drift, artwork, logo, start;
        [SerializeField] UnityEngine.UI.Image body;
        [SerializeField] LobbyInkSwimMotion swim;
        [SerializeField] InkAccessoryImage accessory;
        InkSkinId shown = (InkSkinId)(-1);
        Vector2 fittedSize;
        Vector2 travel;
        float elapsed;
        // 게임 난수·저장값과 분리된 시드. 로비를 새로 만들 때마다 다른 경로를 탄다.
        readonly int motionSeed = System.Guid.NewGuid().GetHashCode();
        public InkSkinId DisplayedSkin => shown;
        public RectTransform Drift => drift;
        public RectTransform Area => area;
        public bool IsVisible => drift != null && drift.gameObject.activeSelf;

        public static LobbySkinPreview Build(RectTransform parent, LobbyView owner, Sprite idle)
        {
            var root = CreateRect("LobbySkinPreview", parent);
            var view = root.gameObject.AddComponent<LobbySkinPreview>();
            view.area = root; view.lobby = owner;
            view.logo = owner.LogoRect;
            view.start = owner.StartButton.transform as RectTransform;
            view.drift = CreateRect("Drift", root);
            view.artwork = CreateRect("Artwork", view.drift);
            view.body = CreateRect("OriginalInkBody", view.artwork).gameObject.AddComponent<UnityEngine.UI.Image>();
            view.body.sprite = idle; view.body.preserveAspect = true; view.body.raycastTarget = false;
            view.swim = view.body.gameObject.AddComponent<LobbyInkSwimMotion>();
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
            if (!show) return;
            var id = InkSkinPreferences.Equipped;
            if (shown != id) RefreshSkin(id);
            RefreshLayout();
            bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
            if (MobileApplicationLifecycle.IsApplicationActive && lobby.IsInteractive &&
                lobby.ActiveMenu != LobbyMenuSelection.Options && !reduced &&
                (GameManager.Instance == null || !GameManager.Instance.IsPaused))
                elapsed += Mathf.Min(Time.unscaledDeltaTime, .05f);
            ApplyPose(reduced ? 0f : elapsed, reduced);
            accessory.PresentMotion(shown, elapsed, InkAccessoryMotion.Strength);
            swim?.Present(elapsed, InkAccessoryMotion.Strength);
            accessory.PresentSwim(elapsed, InkAccessoryMotion.Strength);
        }

        void RefreshSkin(InkSkinId id)
        {
            shown = id;
            accessory.sprite = InkSkinCatalog.Sprite(id);
            accessory.enabled = accessory.sprite != null;
            if (InkSkinCatalog.BehindBody(id)) accessory.transform.SetAsFirstSibling();
            else accessory.transform.SetAsLastSibling();
            fittedSize = new Vector2(-1, -1);
        }

        void RefreshLayout()
        {
            if (logo == null || start == null || area.parent is not RectTransform parent) return;
            // 한글·영문 로고의 실제 먹획 하단(정규화 y >= .38)부터 시작 버튼 위까지 쓴다.
            float top = parent.InverseTransformPoint(logo.TransformPoint(new Vector3(0,
                Mathf.Lerp(logo.rect.yMin, logo.rect.yMax, .38f), 0))).y - 16f;
            float bottom = parent.InverseTransformPoint(start.TransformPoint(new Vector3(0, start.rect.yMax, 0))).y + 20f;
            area.anchoredPosition = new Vector2(0, (top + bottom) * .5f);
            area.sizeDelta = new Vector2(Mathf.Max(1f, parent.rect.width - 64f), Mathf.Max(1f, top - bottom));
            if (fittedSize == area.sizeDelta) return;
            fittedSize = area.sizeDelta;
            Rect bounds = CostumeBounds(shown, accessory.sprite);
            Vector2 envelope = RotationEnvelope(bounds.size);
            float size = PreviewScale * Mathf.Max(.01f, Mathf.Min(360f,
                Mathf.Min(area.rect.width * .48f / envelope.x, area.rect.height * .70f / envelope.y)));
            body.rectTransform.sizeDelta = Vector2.one * size;
            artwork.anchoredPosition = -bounds.center * size;
            if (accessory.enabled)
            {
                float width = size * InkSkinCatalog.Width(shown);
                accessory.rectTransform.sizeDelta = new Vector2(width, width * accessory.sprite.rect.height / accessory.sprite.rect.width);
                accessory.rectTransform.anchoredPosition = InkSkinCatalog.Offset(shown) * size;
            }
            // 팔다리 젓기·신발 변위·미세한 몸통 신축까지 포함한 이동 여백이다.
            envelope = RotationEnvelope(bounds.size + LobbyInkSwimMotion.Padding * 2f) * MaximumBodyStretch;
            travel = Vector2.Max(Vector2.zero, (area.sizeDelta - envelope * size) * .5f - Vector2.one * 4f);
            ApplyPose(elapsed, false);
        }

        void ApplyPose(float time, bool reduced)
        {
            drift.anchoredPosition = reduced ? Vector2.zero : Vector2.Scale(DriftAt(time, motionSeed), travel);
            drift.localRotation = Quaternion.Euler(0, 0, reduced ? 0 : TiltAt(time, motionSeed));
            drift.localScale = reduced ? Vector3.one : SwimScaleAt(time);
        }

        public static Vector2 DriftAt(float time, int seed = 0) => new(
            Wander(time / 1.65f, seed), Wander(time / 1.35f, seed ^ 0x51F2));
        public static float TiltAt(float time, int seed = 0)
        {
            // 이동하는 쪽으로 머리를 내밀며 전진한다. 방향 전환에도 반전·각도 점프가 없다.
            float vx = (DriftAt(time + .02f, seed).x - DriftAt(time - .02f, seed).x) / .04f;
            return -19f * vx / Mathf.Sqrt(.16f + vx * vx) +
                2.5f * Mathf.Sin(time * (Mathf.PI * 2f / LobbyInkSwimMotion.CycleSeconds));
        }
        public static Vector3 SwimScaleAt(float time)
        {
            float stroke = Mathf.Sin(time * (Mathf.PI * 2f / LobbyInkSwimMotion.CycleSeconds));
            return new Vector3(1f + .018f * stroke, 1f - .025f * stroke, 1f);
        }
        static float Wander(float time, int seed)
        {
            int segment = Mathf.FloorToInt(time);
            float t = time - segment;
            float a = Knot(segment - 1, seed), b = Knot(segment, seed);
            float c = Knot(segment + 1, seed), d = Knot(segment + 2, seed);
            // 무작위 제어점을 부드럽게 이어 경계에서 급정지·순간이동하지 않는다.
            // 제어점 ±.78, Catmull-Rom 최대 절댓값 가중치 1.25 → 항상 ±.975 안쪽.
            return .5f * (2*b + (-a+c)*t + (2*a-5*b+4*c-d)*t*t + (-a+3*b-3*c+d)*t*t*t);
        }
        static float Knot(int index, int seed)
        {
            unchecked
            {
                uint h = (uint)index * 0x9E3779B9u ^ (uint)seed;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return ((h & 0xFFFFu) / 65535f * 2f - 1f) * .78f;
            }
        }
        public static Vector2 RotationEnvelope(Vector2 size)
        {
            float s = Mathf.Sin(MaximumTilt * Mathf.Deg2Rad);
            // cos를 1로 잡아 모든 중간 각도의 외곽까지 보수적으로 포함한다.
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
