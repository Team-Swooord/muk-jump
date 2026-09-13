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
        public const float MaxThrowSpeed = 1800f;
        // idle 원화의 알파 경계 (100, 152)~(924, 866)/1024에 여유를 더한 범위.
        // 투명 프레임 전체를 피팅하면 실제 먹이 너무 작아진다. 원본 그림은 자르지 않는다.
        public static readonly Rect BodyDrawingBounds = new(-.415f, -.36f, .83f, .725f);
        public static readonly Rect HeldDrawingBounds = new(-.415f, -.45f, .83f, .815f);
        [SerializeField] LobbyView lobby;
        [SerializeField] RectTransform area, drift, artwork, logo, start;
        [SerializeField] Image body, hitbox;
        [SerializeField] InkAccessoryImage accessory;
        InkSkinId shown = (InkSkinId)(-1);
        Vector2 fittedSize;
        Vector2 travel;
        Vector2 velocity, grabOffset;
        readonly ThrowTracker throwTracker = new();
        readonly CushionAttempt cushionAttempt = new();
        float elapsed, heldTime;
        InkSpriteAnimation heldAnimation;
        public static readonly Vector2 HeldArtOffset = new(-.025f, -.045f);
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
            view.heldAnimation = InkAccessoryMotion.Held;
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
            if (reduced || SkinDebugSession.Active || PermanentGrowthProfile.RequiresRecovery ||
                MukJumpAccountRuntime.Instance?.BlocksGameplayForAccountSync == true) cushionAttempt.Cancel();
            hitbox.raycastTarget = CanInteract;
            // 손을 멈춘 프레임도 기록해야 잡아 둔 뒤 놓을 때 예전 속도로 튀어나가지 않는다.
            if (holding) throwTracker.Record(drift.anchoredPosition, Time.unscaledTimeAsDouble);
            if (CanInteract && !reduced)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                elapsed += dt;
                if (holding) heldTime += dt;
                if (!holding)
                {
                    Vector2 position = drift.anchoredPosition;
                    int contacts = AdvanceBounce(ref position, ref velocity, travel, dt);
                    drift.anchoredPosition = position;
                    object completedOwner = cushionAttempt.Record(PermanentGrowthProfile.SkinAccountToken, contacts);
                    if (completedOwner != null && SkinSecrets.CompletedThreeCushion(completedOwner) != 0)
                        GameManager.Instance?.GetComponent<InkAchievementToast>()?.EnqueueLobby((int)InkSkinId.ThreeCushion);
                }
            }
            // 기울이기·늘이기·발젓기 없이 착용한 형태 그대로 평행 이동한다.
            drift.localRotation = Quaternion.identity;
            drift.localScale = Vector3.one;
            PresentHeldPose();
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
            velocity = Vector2.zero;
            cushionAttempt.Cancel();
            throwTracker.Reset(drift.anchoredPosition, Time.unscaledTimeAsDouble);
            heldTime = 0f;
            PresentHeldPose();
        }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e)
        {
            if (!holding || pointerId != e.pointerId) return;
            if (!CanInteract || inactiveGeneration != MobileApplicationLifecycle.InactiveGeneration) { CancelDrag(); return; }
            MoveHeldPointer(e);
        }
        void MoveHeldPointer(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var point)) return;
            drift.anchoredPosition = ClampPosition(point + grabOffset);
            throwTracker.Record(drift.anchoredPosition, Time.unscaledTimeAsDouble);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (!holding || pointerId != e.pointerId) return;
            if (!CanInteract || inactiveGeneration != MobileApplicationLifecycle.InactiveGeneration) { CancelDrag(); return; }
            // UI 모듈은 마지막 Drag보다 PointerUp을 먼저 보낼 수 있어 손을 뗀 좌표까지 직접 반영한다.
            MoveHeldPointer(e);
            Vector2 released = LobbySettingsProfile.ReducedMotionEnabled ? Vector2.zero : throwTracker.ReleaseVelocity;
            CancelDrag();
            velocity = released;
            // 자동 부유나 정지한 탭은 도전이 아니다. 직접 던진 한 번의 궤적만 이어서 센다.
            if (released.sqrMagnitude > 1f && !SkinDebugSession.Active && !PermanentGrowthProfile.RequiresRecovery &&
                MukJumpAccountRuntime.Instance?.BlocksGameplayForAccountSync != true)
                cushionAttempt.Begin(PermanentGrowthProfile.SkinAccountToken);
        }
        public void OnEndDrag(PointerEventData e) => OnPointerUp(e);
        void CancelDrag()
        {
            // 화면 전환·백그라운드·의상 변경은 던지기가 아니다. 잡지 않은 자유 이동 속도는 보존한다.
            if (holding) velocity = Vector2.zero;
            cushionAttempt.Cancel();
            holding = false; heldTime = 0f; PresentHeldPose();
        }
        void PresentHeldPose()
        {
            if (body == null) return;
            heldAnimation ??= InkAccessoryMotion.Held;
            Sprite pose = holding ? heldAnimation?.Sample(heldTime, !LobbySettingsProfile.ReducedMotionEnabled) : null;
            body.overrideSprite = pose;
            body.rectTransform.anchoredPosition = pose != null ? HeldArtOffset * body.rectTransform.rect.width : Vector2.zero;
            accessory?.PresentBodyFrame(pose);
        }
        void OnDisable() => CancelDrag();
        void OnApplicationPause(bool paused) { if (paused) CancelDrag(); }
        void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }

        /// 마지막 80ms의 실제 UI 이동량을 사용한다. 화면 배율·프레임률과 게임 시간 배율에 독립적이다.
        public sealed class ThrowTracker
        {
            public const double SampleWindow = .08;
            const int Capacity = 32;
            readonly Vector2[] positions = new Vector2[Capacity];
            readonly double[] times = new double[Capacity];
            int newest, count;

            public void Reset(Vector2 position, double time)
            {
                newest = 0; count = 1; positions[0] = position; times[0] = time;
            }
            public void Record(Vector2 position, double time)
            {
                if (count == 0 || time < times[newest]) { Reset(position, time); return; }
                if (time > times[newest]) { newest = (newest + 1) % Capacity; count = Mathf.Min(count + 1, Capacity); }
                // 같은 프레임의 Drag → Up/EndDrag → LateUpdate는 한 시점으로 취급한다.
                positions[newest] = position; times[newest] = time;
            }
            public Vector2 ReleaseVelocity
            {
                get
                {
                    if (count < 2) return Vector2.zero;
                    double end = times[newest], cutoff = end - SampleWindow;
                    int older = newest, newer = newest;
                    for (int i = 1; i < count; i++)
                    {
                        newer = older; older = (older + Capacity - 1) % Capacity;
                        if (times[older] <= cutoff) break;
                    }
                    double begin = times[older];
                    Vector2 origin = positions[older];
                    if (begin < cutoff)
                    {
                        origin = Vector2.Lerp(origin, positions[newer], (float)((cutoff - begin) / (times[newer] - begin)));
                        begin = cutoff;
                    }
                    double duration = end - begin;
                    if (duration < .001) return Vector2.zero;
                    // 순간 입력 튐만 제한하고 평소 부유 속도로 되돌리거나 임의 방향을 섞지 않는다.
                    return Vector2.ClampMagnitude((positions[newest] - origin) / (float)duration, MaxThrowSpeed);
                }
            }
        }

        // 화면 밖으로 크게 끌거나 긴 프레임이 와도 외곽 안에서 반사한다. 게임 난수/물리는 사용하지 않는다.
        public static int AdvanceBounce(ref Vector2 position, ref Vector2 speed, Vector2 limits, float dt)
        {
            // 프레임 끝의 속도 부호만 보면 두 번 반사된 충돌을 놓친다. 접촉 시각을 먼저 계산한다.
            int contacts = CountCushionContacts(position, speed, limits, dt);
            BounceAxis(ref position.x, ref speed.x, limits.x, dt);
            BounceAxis(ref position.y, ref speed.y, limits.y, dt);
            return contacts;
        }
        static int CountCushionContacts(Vector2 position, Vector2 speed, Vector2 limits, float dt)
        {
            if (dt <= 0) return 0;
            float First(float p, float v, float limit) => limit <= 0 || v == 0 ? float.PositiveInfinity :
                (limit - Mathf.Clamp(p, -limit, limit) * Mathf.Sign(v)) / Mathf.Abs(v);
            float nextX = First(position.x, speed.x, limits.x), nextY = First(position.y, speed.y, limits.y);
            float periodX = speed.x == 0 ? float.PositiveInfinity : 2f * limits.x / Mathf.Abs(speed.x);
            float periodY = speed.y == 0 ? float.PositiveInfinity : 2f * limits.y / Mathf.Abs(speed.y);
            int count = 0;
            // 해금에 필요한 세 번까지만 계산한다. 모서리 동시 접촉은 한 번으로 센다.
            while (count < 3)
            {
                float next = Mathf.Min(nextX, nextY);
                if (next > dt) break;
                bool hitX = Mathf.Abs(nextX - next) < .00001f, hitY = Mathf.Abs(nextY - next) < .00001f;
                if (hitX) nextX += periodX;
                if (hitY) nextY += periodY;
                count++;
            }
            return count;
        }

        /// 진행도는 저장하지 않는다. 재포획·화면 이탈·계정 전환으로 즉시 끊기는 한 번의 던지기다.
        public sealed class CushionAttempt
        {
            object owner;
            int contacts;
            public void Begin(object account) { owner = account; contacts = 0; }
            public void Cancel() { owner = null; contacts = 0; }
            public object Record(object currentAccount, int newContacts)
            {
                if (owner == null || !ReferenceEquals(owner, currentAccount)) { Cancel(); return null; }
                contacts = Mathf.Min(3, contacts + Mathf.Clamp(newContacts, 0, 3));
                if (contacts < 3) return null;
                var completed = owner;
                Cancel();
                return completed;
            }
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
            Vector2 min = Vector2.Min(BodyDrawingBounds.min, HeldDrawingBounds.min);
            Vector2 max = Vector2.Max(BodyDrawingBounds.max, HeldDrawingBounds.max);
            if (sprite != null)
            {
                if (InkSkinCatalog.IsFootwear(id))
                {
                    for (int side = 0; side < 2; side++)
                    {
                        Rect r = InkFootwearPose.DrawingRect(id, sprite, side == 1);
                        min = Vector2.Min(min, r.min); max = Vector2.Max(max, r.max);
                        var held = InkAccessoryMotion.Held;
                        if (held != null) for (int f = 0; f < held.FrameCount; f++)
                        {
                            r = InkFootwearPose.DrawingRect(id, sprite, side == 1, held.Frame(f));
                            min = Vector2.Min(min, r.min); max = Vector2.Max(max, r.max);
                        }
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
