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
        // 자동 부유·손 놓기 모두 무중력이다. 빠른 투척만 감속하고 저속 부유는 유지한다.
        public const float AirDrag = .30f;
        public const float Restitution = .60f;
        const float CushionImpactSpeed = 120f;
        const float MotionStep = 1f / 120f;
        public const float MaxSpin = 540f;
        const float AngularDrag = .65f;
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
        float elapsed, heldTime, rotation, angularVelocity;
        InkSpriteAnimation heldAnimation;
        public static readonly Vector2 HeldArtOffset = new(-.025f, -.045f);
        bool positioned, holding, trackingThrow;
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
        public float AngularVelocity => angularVelocity;

        bool CanInteract => !InkPremiumShopView.IsOpen && IsVisible && lobby != null && lobby.IsInteractive &&
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
            GoldInkVisual.Apply(body, InkPremiumAppearance.IsEquipped);
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
                    // 입력 여부에 따라 중력이 켜지는 별도 경로를 두지 않는다.
                    int contacts = AdvanceTumble(ref position, ref velocity, ref rotation, ref angularVelocity,
                        area.sizeDelta * .5f - Vector2.one * 4f, drift.sizeDelta, dt);
                    if (trackingThrow)
                    {
                        object completedOwner = cushionAttempt.Record(PermanentGrowthProfile.SkinAccountToken, contacts);
                        if (completedOwner != null && SkinSecrets.CompletedThreeCushion(completedOwner) != 0)
                            GameManager.Instance?.GetComponent<InkAchievementToast>()?.EnqueueLobby((int)InkSkinId.ThreeCushion);
                        // 직접 던진 관성이 잦아들면 업적 집계만 끝내고, 같은 방향으로 계속 떠다닌다.
                        if (velocity.sqrMagnitude <= DriftSpeed * DriftSpeed + .01f)
                        {
                            trackingThrow = false;
                            cushionAttempt.Cancel();
                        }
                    }
                    travel = RotationTravel(area.sizeDelta * .5f - Vector2.one * 4f, drift.sizeDelta, rotation);
                    drift.anchoredPosition = position;
                }
            }
            // 본체와 의상을 한 덩어리로 회전한다. 원화·프레임을 늘이거나 이동 방향으로 정면을 강제하지 않는다.
            drift.localRotation = Quaternion.Euler(0f, 0f, rotation);
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
            travel = RotationTravel(area.sizeDelta * .5f - Vector2.one * 4f, drift.sizeDelta, rotation);
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
            angularVelocity = 0f;
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
            float releasedSpin = ReleaseSpin(-grabOffset, released, drift.sizeDelta);
            CancelDrag();
            velocity = released;
            angularVelocity = releasedSpin;
            trackingThrow = released.sqrMagnitude > DriftSpeed * DriftSpeed;
            // 자동 부유나 정지한 탭은 도전이 아니다. 직접 던진 한 번의 궤적만 이어서 센다.
            if (trackingThrow && !SkinDebugSession.Active && !PermanentGrowthProfile.RequiresRecovery &&
                MukJumpAccountRuntime.Instance?.BlocksGameplayForAccountSync != true)
                cushionAttempt.Begin(PermanentGrowthProfile.SkinAccountToken);
        }
        public void OnEndDrag(PointerEventData e) => OnPointerUp(e);
        void CancelDrag()
        {
            // 화면 전환·백그라운드·의상 변경은 던지기가 아니다. 잡지 않은 자유 이동 속도는 보존한다.
            if (holding) { velocity = Vector2.zero; angularVelocity = 0f; trackingThrow = false; }
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

        /// 자동 부유는 속도를 잃거나 아래로 가속하지 않는다. 경계에서 남은 이동량까지 반사한다.
        public static void AdvanceFloat(ref Vector2 position, ref Vector2 speed, Vector2 limits, float dt)
        {
            if (dt <= 0f) return;
            speed = (speed.sqrMagnitude > .001f ? speed.normalized : new Vector2(3f, 4f).normalized) * DriftSpeed;
            FloatAxis(ref position.x, ref speed.x, limits.x, dt);
            FloatAxis(ref position.y, ref speed.y, limits.y, dt);
        }

        static void FloatAxis(ref float position, ref float speed, float limit, float dt)
        {
            if (limit <= 0f) { position = 0f; speed = 0f; return; }
            float unfolded = Mathf.Clamp(position, -limit, limit) + speed * dt;
            float phase = Mathf.Repeat(unfolded + limit, limit * 4f);
            position = phase <= limit * 2f ? phase - limit : limit * 3f - phase;
            if (phase > limit * 2f) speed = -speed;
            if (position >= limit) speed = -Mathf.Abs(speed);
            else if (position <= -limit) speed = Mathf.Abs(speed);
        }

        // 게임 난수·물리와 분리된 무중력 운동. 고속 투척도 벽 밖으로 빠지지 않는다.
        public static int AdvanceBounce(ref Vector2 position, ref Vector2 speed, Vector2 limits, float dt)
        {
            float angle = 0f, spin = 0f;
            return AdvanceTumble(ref position, ref speed, ref angle, ref spin, limits, Vector2.zero, dt);
        }

        public static float ReleaseSpin(Vector2 gripFromCenter, Vector2 speed, Vector2 size)
        {
            float radius = Mathf.Max(1f, Mathf.Min(size.x, size.y) * .5f);
            // 중심을 잡으면 직선 투척, 가장자리를 잡아 던지면 손의 힘이 회전으로 전달된다.
            return Mathf.Clamp((gripFromCenter.x * speed.y - gripFromCenter.y * speed.x) /
                (radius * radius) * Mathf.Rad2Deg * .45f, -MaxSpin, MaxSpin);
        }

        public static Vector2 RotationTravel(Vector2 arenaHalf, Vector2 size, float angle)
        {
            float cos = Mathf.Abs(Mathf.Cos(angle * Mathf.Deg2Rad));
            float sin = Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad));
            return Vector2.Max(Vector2.zero, arenaHalf - new Vector2(
                size.x * cos + size.y * sin, size.x * sin + size.y * cos) * .5f);
        }

        public static int AdvanceTumble(ref Vector2 position, ref Vector2 speed, ref float angle, ref float spin,
            Vector2 arenaHalf, Vector2 size, float dt)
        {
            Vector2 limits = RotationTravel(arenaHalf, size, angle);
            position = new Vector2(Mathf.Clamp(position.x, -limits.x, limits.x), Mathf.Clamp(position.y, -limits.y, limits.y));
            KeepDrifting(ref speed, limits);
            float radius = Mathf.Min(size.x, size.y) * .5f;
            int contacts = 0;
            float remaining = Mathf.Max(0f, dt);
            while (remaining > 0f)
            {
                float step = Mathf.Min(MotionStep, remaining);
                remaining -= step;
                float decay = Mathf.Exp(-AirDrag * step);
                // X/Y에 같은 감쇠를 적용해 던진 방향을 유지한 채 보통 부유 속도로 돌아간다.
                float magnitude = speed.magnitude;
                float slowingTime = magnitude > DriftSpeed ? Mathf.Min(step, Mathf.Log(magnitude / DriftSpeed) / AirDrag) : 0f;
                float travelTime = (1f - Mathf.Exp(-AirDrag * slowingTime)) / AirDrag;
                position += speed * travelTime + speed.normalized * DriftSpeed * (step - slowingTime);
                speed *= decay;
                if (radius > 0f)
                {
                    float spinDecay = Mathf.Exp(-AngularDrag * step);
                    angle = Mathf.Repeat(angle + spin * (1f - spinDecay) / AngularDrag + 180f, 360f) - 180f;
                    spin *= spinDecay;
                    // 회전한 의상 외곽까지 매 스텝 반영하여 모자·날개가 벽 밖으로 잘리지 않는다.
                    limits = RotationTravel(arenaHalf, size, angle);
                    if ((position.x >= limits.x && speed.x > 0f) || (position.x <= -limits.x && speed.x < 0f))
                        ApplyImpactSpin(ref spin, speed, new Vector2(-Mathf.Sign(speed.x), 0f), radius);
                    if ((position.y >= limits.y && speed.y > 0f) || (position.y <= -limits.y && speed.y < 0f))
                        ApplyImpactSpin(ref spin, speed, new Vector2(0f, -Mathf.Sign(speed.y)), radius);
                }
                bool impact = BounceAxis(ref position.x, ref speed.x, limits.x);
                impact |= BounceAxis(ref position.y, ref speed.y, limits.y);
                KeepDrifting(ref speed, limits);
                // 모서리는 한 번, 느린 자동 부유는 0번. 쓰리쿠션은 직접 던진 경우에만 집계한다.
                if (impact) contacts = Mathf.Min(3, contacts + 1);
            }
            return contacts;
        }

        static void KeepDrifting(ref Vector2 speed, Vector2 limits)
        {
            if (limits.x <= 0f) speed.x = 0f;
            if (limits.y <= 0f) speed.y = 0f;
            if (speed.sqrMagnitude >= DriftSpeed * DriftSpeed) return;
            if (speed.sqrMagnitude < .000001f)
                speed = new Vector2(limits.x > 0f ? 3f : 0f, limits.y > 0f ? 4f : 0f);
            speed = speed.normalized * DriftSpeed;
        }

        static void ApplyImpactSpin(ref float spin, Vector2 speed, Vector2 normal, float radius)
        {
            // 벽 접선 방향의 미끄러짐이 회전을 만든다. 반대쪽 벽·입사 방향에서는 회전 방향도 반대다.
            float rolling = (normal.x * speed.y - normal.y * speed.x) / radius * Mathf.Rad2Deg;
            float impulse = Mathf.Abs(Vector2.Dot(speed, normal)) * (1f + Restitution) / radius * Mathf.Rad2Deg * .5f;
            spin = Mathf.Clamp(Mathf.MoveTowards(spin, rolling, impulse), -MaxSpin, MaxSpin);
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
        static bool BounceAxis(ref float position, ref float speed, float limit)
        {
            if (limit <= 0f) { position = 0f; speed = 0f; return false; }
            bool upper = position >= limit && speed > 0f;
            bool lower = position <= -limit && speed < 0f;
            position = Mathf.Clamp(position, -limit, limit);
            if (!upper && !lower) return false;
            float incoming = Mathf.Abs(speed);
            speed = -speed * Restitution;
            return incoming >= CushionImpactSpeed;
        }
        public static Vector2 RotationEnvelope(Vector2 size)
        {
            // 이전 버전의 미리보기 크기는 보존하고 실제 회전 충돌 범위는 RotationTravel에서 계산한다.
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
