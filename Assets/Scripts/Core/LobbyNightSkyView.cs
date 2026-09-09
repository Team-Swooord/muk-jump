using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MukJump.Core
{
    /// 로고를 열 번 탭하면 해·달이 조금씩 지며 낮과 밤을 바꾼다. 원화·맵 순서는 유지한다.
    [DisallowMultipleComponent]
    public sealed class LobbyNightSkyView : MonoBehaviour
    {
        public const string CleanSkyResourcePath = "MukJump/Background/Lobby/lobby_day_sky_v1";
        public const float TransitionSeconds = 5.8f;
        public const int RequiredTaps = 10;
        public const float DepartureHoldSeconds = 0.12f;
        public const float ChargeTravelFraction = 0.72f;
        public const float DepartureEndPhase = 0.34f;
        public const float ArrivalStartPhase = 0.38f;
        const float TapMotionSmoothTime = 0.24f;
        // 두 언어의 실제 글씨 띠만 포함한다. 큰 PNG의 위아래 투명 여백은 탭 대상이 아니다.
        public static readonly Rect LogoTapUv = new(0.10f, 0.32f, 0.82f, 0.42f);
        public static readonly Color CelestialRed = new(0.78f, 0.16f, 0.16f, 1f);
        public static readonly Color DayDiscColor = new(0.98f, 0.94f, 0.80f, 1f);
        public static readonly Vector2 SunHomeUv = new(0.68333f, 0.90156f);
        public static readonly Vector2 MoonHomeUv = new(1f - 0.68333f, 0.90156f);
        const float DiscWidthFraction = 94f / 1080f;
        static readonly int NightId = Shader.PropertyToID("_Night");
        static readonly int EraseSunId = Shader.PropertyToID("_EraseSun");
        static readonly int CleanSkyId = Shader.PropertyToID("_CleanSkyTex");
        static readonly int SplitForegroundId = Shader.PropertyToID("_SplitForeground");
        static readonly int RidgeSamplesId = Shader.PropertyToID("_RidgeSamples");
        static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        // 0번 원화의 가까운 산 능선. 먼 산은 달 뒤에 남기고 이 능선 아래만 가린다.
        // 원화 UV 기준 1/32 간격. 이 윤곽으로 원화를 실제 앞산·뒷배경으로 분리한다.
        static readonly float[] RidgeHeights = {
            .727f, .735f, .751f, .761f, .765f, .763f, .769f, .785f,
            .799f, .806f, .805f, .797f, .783f, .768f, .760f, .757f,
            .747f, .736f, .739f, .737f, .732f, .720f, .709f, .704f,
            .715f, .710f, .720f, .732f, .739f, .738f, .730f, .719f, .709f
        };

        [SerializeField] Camera worldCamera;
        [SerializeField] SpriteRenderer[] backgrounds;
        [SerializeField] Sprite stageZero;
        [SerializeField] Texture2D cleanSky;
        [SerializeField] SpriteRenderer sun;
        [SerializeField] SpriteRenderer moon;
        // Play 중 재컴파일 때 천체의 현재 자세도 복구한다.
        [SerializeField] float progress;
        [SerializeField] bool targetNight;
        [SerializeField] float departureHold;
        [SerializeField] float celestialTravel;
        [SerializeField] float celestialVelocity;
        Material material;
        Material discMaterial;
        Mesh mountainMesh;
        MeshRenderer foregroundMountains;
        Sprite mountainSource;
        MaterialPropertyBlock mountainProperties;
        const int MountainSegments = 128;
        readonly Vector4[] ridgeSamples = new Vector4[33];
        Material[] previousMaterials;
        MaterialPropertyBlock properties;
        LobbyView lobby;
        LobbyOptionsView options;
        RectTransform logoTarget;
        LobbyLogoTapTarget logoInput;
        RectTransform recordTarget;
        readonly Vector3[] recordCorners = new Vector3[4];
        bool layoutInMainMenu = true;
        readonly List<RaycastResult> hits = new(8);
        PointerEventData pointerData;
        EventSystem pointerSystem;
        int tapCount;
        bool pressedHere;
        Vector2 pressPosition;
        float pressDuration;
        float pressTravel;

        public int TapCount => tapCount;
        public float Progress => progress;
        public bool IsTransitioning => Mathf.Abs(progress - (targetNight ? 1f : 0f)) > 0.0001f;

        public void Configure(Camera camera, SpriteRenderer[] layers, Sprite firstStage, Texture2D sky)
        {
            worldCamera = camera;
            backgrounds = layers;
            stageZero = firstStage;
            cleanSky = sky;
            sun = EnsureDisc("LobbySun", sun);
            moon = EnsureDisc("LobbyMoon", moon);
            sun.enabled = moon.enabled = false;
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            Initialize();
        }

        void Initialize()
        {
            if (worldCamera == null) worldCamera = GetComponentInParent<Camera>();
            if (backgrounds == null || backgrounds.Length != 2)
                backgrounds = new[] { transform.Find("BackgroundCurrent")?.GetComponent<SpriteRenderer>(),
                    transform.Find("BackgroundNext")?.GetComponent<SpriteRenderer>() };
            if (stageZero == null && backgrounds[0] != null) stageZero = backgrounds[0].sprite;
            if (cleanSky == null) cleanSky = Resources.Load<Texture2D>(CleanSkyResourcePath);
            if (LobbyNightState.Initialized)
            {
                progress = LobbyNightState.Progress;
                targetNight = LobbyNightState.TargetNight;
                departureHold = LobbyNightState.DepartureHold;
                celestialTravel = LobbyNightState.CelestialTravel;
                celestialVelocity = LobbyNightState.CelestialVelocity;
            }
            else StoreMotion();
            sun = EnsureDisc("LobbySun", sun);
            moon = EnsureDisc("LobbyMoon", moon);
            sun.sprite = InkUiTextureFactory.CreateCelestialDiscSprite();
            moon.sprite = InkUiTextureFactory.CreateHanjiMoonSprite();
            if (discMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    discMaterial = new Material(shader)
                        { name = "MukJump_Celestial", hideFlags = HideFlags.HideAndDontSave };
                }
            }
            if (discMaterial != null) sun.sharedMaterial = moon.sharedMaterial = discMaterial;
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>("MukJump/Shaders/BackgroundNight");
                if (shader != null) material = new Material(shader)
                    { name = "MukJump_BackgroundNight", hideFlags = HideFlags.HideAndDontSave };
                properties = new MaterialPropertyBlock();
                previousMaterials = new Material[backgrounds.Length];
                for (int i = 0; i < backgrounds.Length; i++)
                    if (backgrounds[i] != null)
                    {
                        previousMaterials[i] = backgrounds[i].sharedMaterial;
                        if (material != null) backgrounds[i].sharedMaterial = material;
                    }
            }
            ApplyVisuals();
        }

        void EnsureMountainLayers()
        {
            if (foregroundMountains == null)
            {
                var existing = transform.Find("LobbyForegroundMountains");
                var go = existing != null ? existing.gameObject : new GameObject("LobbyForegroundMountains");
                go.transform.SetParent(transform, false);
                go.layer = gameObject.layer;
                foregroundMountains = go.GetComponent<MeshRenderer>();
                if (foregroundMountains == null) foregroundMountains = go.AddComponent<MeshRenderer>();
                foregroundMountains.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                foregroundMountains.receiveShadows = false;
            }
            // Unity의 누락/파괴된 컴포넌트는 ??가 아니라 Unity null 비교로 검사한다.
            // Renderer만 남은 부분 복구에서도 Filter를 재생성하고 기존 Mesh를 다시 연결한다.
            var filter = foregroundMountains.GetComponent<MeshFilter>();
            if (filter == null) filter = foregroundMountains.gameObject.AddComponent<MeshFilter>();
            if (mountainMesh == null && filter.sharedMesh != null &&
                filter.sharedMesh.name == "LobbyForegroundMountainCut") mountainMesh = filter.sharedMesh;
            foregroundMountains.sharedMaterial = material;
            if (mountainMesh != null && mountainSource == stageZero)
            {
                if (filter.sharedMesh != mountainMesh) filter.sharedMesh = mountainMesh;
                return;
            }
            ReleaseMountainMesh();
            mountainSource = stageZero;
            Bounds bounds = stageZero.bounds;
            Rect textureRect = stageZero.rect;
            Vector2 textureSize = new(stageZero.texture.width, stageZero.texture.height);
            var vertices = new Vector3[(MountainSegments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[MountainSegments * 6];
            for (int i = 0; i <= MountainSegments; i++)
            {
                float x = i / (float)MountainSegments;
                float height = MountainRidgeHeight(x);
                Vector4 packed = ridgeSamples[i / 4];
                packed[i % 4] = height;
                ridgeSamples[i / 4] = packed;
                for (int row = 0; row < 2; row++)
                {
                    int vertex = i * 2 + row;
                    float y = row == 0 ? 0f : height;
                    vertices[vertex] = new Vector3(bounds.min.x + bounds.size.x * x,
                        bounds.min.y + bounds.size.y * y, 0f);
                    uv[vertex] = new Vector2((textureRect.x + textureRect.width * x) / textureSize.x,
                        (textureRect.y + textureRect.height * y) / textureSize.y);
                    colors[vertex] = Color.white;
                }
                if (i == MountainSegments) continue;
                int index = i * 6, v = i * 2;
                triangles[index] = v; triangles[index + 1] = v + 1; triangles[index + 2] = v + 2;
                triangles[index + 3] = v + 1; triangles[index + 4] = v + 3; triangles[index + 5] = v + 2;
            }
            mountainMesh = new Mesh { name = "LobbyForegroundMountainCut", hideFlags = HideFlags.HideAndDontSave };
            mountainMesh.vertices = vertices;
            mountainMesh.uv = uv;
            mountainMesh.colors = colors;
            mountainMesh.triangles = triangles;
            mountainMesh.RecalculateBounds();
            filter.sharedMesh = mountainMesh;
            material.SetVectorArray(RidgeSamplesId, ridgeSamples);
        }

        public static float MountainRidgeHeight(float x)
        {
            float sample = Mathf.Clamp01(x) * 32f;
            int i = Mathf.Min(Mathf.FloorToInt(sample), 31);
            float t = sample - i;
            float a = RidgeHeights[Mathf.Max(0, i - 1)], b = RidgeHeights[i];
            float c = RidgeHeights[i + 1], d = RidgeHeights[Mathf.Min(32, i + 2)];
            return .5f * (2f * b + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t * t +
                (-a + 3f * b - 3f * c + d) * t * t * t);
        }

        void PlaceMountainLayer(SpriteRenderer painting, float blend)
        {
            Transform layer = foregroundMountains.transform;
            layer.SetPositionAndRotation(painting.transform.position, painting.transform.rotation);
            layer.localScale = painting.transform.localScale;
            foregroundMountains.sortingLayerID = painting.sortingLayerID;
            foregroundMountains.sortingOrder = -6;
            mountainProperties ??= new MaterialPropertyBlock();
            mountainProperties.SetTexture(MainTextureId, painting.sprite.texture);
            mountainProperties.SetColor("_Color", painting.color);
            mountainProperties.SetFloat(NightId, blend);
            mountainProperties.SetFloat(EraseSunId, 0f);
            mountainProperties.SetFloat(SplitForegroundId, 0f);
            foregroundMountains.SetPropertyBlock(mountainProperties);
            foregroundMountains.enabled = true;
        }

        void ReleaseMountainMesh()
        {
            if (mountainMesh == null) return;
            if (Application.isPlaying) Destroy(mountainMesh);
            else DestroyImmediate(mountainMesh);
            mountainMesh = null;
        }

        SpriteRenderer EnsureDisc(string name, SpriteRenderer existing)
        {
            if (existing != null) return existing;
            var child = transform.Find(name);
            if (child != null) return child.GetComponent<SpriteRenderer>();
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -7;
            return renderer;
        }

        void LateUpdate()
        {
            if (material == null) return;
            ResolveLobbyTargets();
            GameManager manager = GameManager.Instance;
            LobbyScreenNavigator navigator = LobbyScreenNavigator.Instance;
            bool mainMenu = IsMainMenu(manager, navigator);
            bool active = MobileApplicationLifecycle.IsApplicationActive;
            Tick(Time.unscaledDeltaTime, active && (manager == null || !manager.IsPaused),
                mainMenu, CanTapLogo(), LobbySettingsProfile.ReducedMotionEnabled);
        }

        void ResolveLobbyTargets()
        {
            if (lobby == null) lobby = FindAnyObjectByType<LobbyView>();
            if (lobby != null)
            {
                logoTarget = lobby.LogoRect;
                recordTarget = lobby.BestRecordRect;
            }
            if (logoTarget != null && (logoInput == null || logoInput.transform != logoTarget))
            {
                if (logoInput != null) logoInput.Bind(null);
                logoInput = logoTarget.GetComponent<LobbyLogoTapTarget>() ??
                    logoTarget.gameObject.AddComponent<LobbyLogoTapTarget>();
                logoInput.Bind(HandleLogoTap);
            }
            if (options == null) options = FindAnyObjectByType<LobbyOptionsView>();
        }

        static bool IsMainMenu(GameManager manager, LobbyScreenNavigator navigator) =>
            manager != null && manager.State == GameState.Lobby &&
            (navigator == null || navigator.CurrentSection == LobbyScreenNavigator.LobbySection.Lobby);

        bool CanTapLogo()
        {
            GameManager manager = GameManager.Instance;
            LobbyScreenNavigator navigator = LobbyScreenNavigator.Instance;
            return IsMainMenu(manager, navigator) && lobby != null && lobby.IsVisible && lobby.IsInteractive &&
                !manager.IsTransitioning && (navigator == null || !navigator.IsTransitioning) &&
                (options == null || !options.IsOpen) && !StartupBrandSplash.IsBlockingInput &&
                MobileApplicationLifecycle.IsApplicationActive;
        }

        void HandleLogoTap()
        {
            // UI가 전달한 탭을 다시 포인터 장치의 현재 상태로 검사하면 짧은 모바일 탭을 놓친다.
            if (CanTapLogo() && !IsTransitioning && material != null && cleanSky != null)
                RegisterTap();
        }

        void Tick(float deltaTime, bool animate, bool mainMenu, bool inputAllowed, bool reducedMotion)
        {
            float dt = float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0f : Mathf.Clamp(deltaTime, 0f, 0.05f);
            layoutInMainMenu = mainMenu;
            if (!inputAllowed || !mainMenu)
            {
                pressedHere = false;
                tapCount = 0;
                logoInput?.CancelPress();
            }
            if (animate)
            {
                bool wasTransitioning = IsTransitioning;
                float heldTime = Mathf.Min(departureHold, dt);
                departureHold -= heldTime;
                float duration = (reducedMotion ? 0.35f : TransitionSeconds) - DepartureHoldSeconds;
                progress = Mathf.MoveTowards(progress, targetNight ? 1f : 0f, (dt - heldTime) / duration);
                if (wasTransitioning && !IsTransitioning)
                {
                    // 들어오는 천체는 정확히 홈에서 감속을 마친다. 다음 탭은 새 천체에서 시작한다.
                    celestialTravel = celestialVelocity = 0f;
                }
                else if (dt > 0f)
                {
                    float desired = IsTransitioning
                        ? Mathf.Lerp(ChargeTravelFraction, 1f, SmoothRange(0f, DepartureEndPhase, TransitionPhase))
                        : ChargeTravelFraction * tapCount / RequiredTaps;
                    // 연속 탭과 열 번째 탭 모두 현재 위치·속도에서 이어 가며 위로 되튀지 않는다.
                    celestialTravel = Mathf.SmoothDamp(celestialTravel, desired, ref celestialVelocity,
                        TapMotionSmoothTime, Mathf.Infinity, dt);
                }
                if (pressedHere) pressDuration += dt;
            }
            StoreMotion();
            ApplyVisuals(reducedMotion);
        }

        float TransitionPhase => targetNight ? progress : 1f - progress;

        void StoreMotion() => LobbyNightState.Set(progress, targetNight, departureHold,
            celestialTravel, celestialVelocity);

        bool IsOverUi(Vector2 position)
        {
            var system = EventSystem.current;
            if (system == null) return false;
            if (pointerData == null || pointerSystem != system)
            {
                pointerData = new PointerEventData(system);
                pointerSystem = system;
            }
            pointerData.position = position;
            hits.Clear();
            system.RaycastAll(pointerData, hits);
            foreach (var hit in hits)
            {
                if (hit.gameObject == null) continue;
                // UI는 맨 앞의 대상이 입력을 받는다. 로고 뒤쪽 그래픽은 로고를 차단하지 않는다.
                return logoTarget == null ||
                    (hit.gameObject.transform != logoTarget && !hit.gameObject.transform.IsChildOf(logoTarget));
            }
            return false;
        }

        void ProcessPointer(Vector2 position, bool pressed, bool held, bool released, bool allowed, bool overUi)
        {
            if (!allowed || IsTransitioning || material == null || cleanSky == null)
            { pressedHere = false; return; }
            if (pressed)
            {
                pressedHere = !overUi && HitLogo(position);
                pressPosition = position;
                pressDuration = pressTravel = 0f;
            }
            if (pressedHere && (held || released))
                pressTravel = Mathf.Max(pressTravel, Vector2.Distance(pressPosition, position));
            if (released)
            {
                bool tap = pressedHere && !overUi && HitLogo(position) && pressDuration <= 0.65f &&
                    pressTravel <= Mathf.Max(10f, worldCamera.pixelWidth * 0.022f);
                pressedHere = false;
                if (tap) RegisterTap();
            }
            else if (!held) pressedHere = false;
        }

        void RegisterTap()
        {
            if (++tapCount != RequiredTaps) return;
            tapCount = 0;
            targetNight = !targetNight;
            MukJumpAnalytics.Night(targetNight);
            departureHold = DepartureHoldSeconds;
            StoreMotion();
        }

        bool HitLogo(Vector2 position)
        {
            if (logoTarget == null || !logoTarget.gameObject.activeInHierarchy) return false;
            var canvas = logoTarget.GetComponentInParent<UnityEngine.Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return IsInsideLogoBand(logoTarget, position, uiCamera);
        }

        public static bool IsInsideLogoBand(RectTransform target, Vector2 position, Camera uiCamera)
        {
            if (target == null || !target.gameObject.activeInHierarchy ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(target, position, uiCamera, out Vector2 local))
                return false;
            Rect rect = target.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            return LogoTapUv.Contains(new Vector2((local.x - rect.xMin) / rect.width,
                (local.y - rect.yMin) / rect.height));
        }

        void ApplyVisuals(bool reducedMotion = false)
        {
            if (material == null || backgrounds == null || worldCamera == null) return;
            // 머티리얼이 남은 부분 리로드에서도 관리 캐시는 다시 연결한다.
            properties ??= new MaterialPropertyBlock();
            if (stageZero != null && material != null) EnsureMountainLayers();
            if (foregroundMountains != null) foregroundMountains.enabled = false;
            float blend = LobbyNightState.Blend;
            SpriteRenderer first = null;
            float firstAlpha = 0f;
            foreach (var background in backgrounds)
            {
                if (background == null) continue;
                bool isFirst = background.sprite != null && background.sprite == stageZero;
                background.GetPropertyBlock(properties);
                properties.SetFloat(NightId, blend);
                // 시작·성장 진입은 탭만 막는다. 진행 중인 해가 원화 위치로 되돌아가면 안 된다.
                properties.SetFloat(EraseSunId, isFirst && cleanSky != null ? 1f : 0f);
                properties.SetFloat(SplitForegroundId, isFirst && cleanSky != null && mountainMesh != null ? 1f : 0f);
                if (cleanSky != null) properties.SetTexture(CleanSkyId, cleanSky);
                background.SetPropertyBlock(properties);
                if (isFirst && background.enabled && background.color.a > firstAlpha)
                { first = background; firstAlpha = background.color.a; }
            }
            if (first == null || cleanSky == null)
            { sun.enabled = moon.enabled = false; return; }
            PlaceMountainLayer(first, blend);

            Rect skyLane = ResolveLobbySkyLane();
            Vector2 sunHome = HomeUvInView(first, SunHomeUv, skyLane);
            Vector2 moonHome = HomeUvInView(first, MoonHomeUv, skyLane);
            float sunTravel = targetNight ? 1f : celestialTravel;
            float moonTravel = targetNight ? celestialTravel : 1f;
            Color sunColor = DayDiscColor;
            Color moonColor = CelestialRed;
            sunColor.a = targetNight ? 0f : 1f;
            moonColor.a = targetNight ? 1f : 0f;
            if (IsTransitioning)
            {
                float phase = TransitionPhase;
                float arrivalTravel = 1f - ArrivalEase(Mathf.InverseLerp(ArrivalStartPhase, 1f, phase));
                // 한쪽이 완전히 사라진 뒤 잠깐 비우고 반대편의 상승을 시작한다.
                // 이동 중에는 원색·불투명도를 유지한다. 사라짐은 실제 앞산 레이어가 담당한다.
                float leaveAlpha = reducedMotion ? 1f - SmoothRange(0.14f, DepartureEndPhase, phase)
                    : phase < DepartureEndPhase ? 1f : 0f;
                float enterAlpha = reducedMotion ? SmoothRange(ArrivalStartPhase, 0.64f, phase)
                    : phase > ArrivalStartPhase ? 1f : 0f;
                sunTravel = targetNight ? celestialTravel : arrivalTravel;
                moonTravel = targetNight ? arrivalTravel : celestialTravel;
                sunColor.a = targetNight ? leaveAlpha : enterAlpha;
                moonColor.a = targetNight ? enterAlpha : leaveAlpha;
            }
            Vector2 sunUv = reducedMotion ? sunHome : SymmetricDiscArc(sunHome, moonHome, sunTravel);
            Vector2 moonUv = reducedMotion ? moonHome : SymmetricDiscArc(moonHome, sunHome, moonTravel);
            float diameter = first.bounds.size.x * DiscWidthFraction;
            if (skyLane.height > 0f)
                diameter = Mathf.Min(diameter, Mathf.Max(1f, skyLane.height - 20f) *
                    worldCamera.orthographicSize * 2f / worldCamera.pixelHeight);
            PlaceDisc(sun, first, sunUv, sunColor, firstAlpha, diameter);
            PlaceDisc(moon, first, moonUv, moonColor, firstAlpha, diameter);
        }

        static float SmoothRange(float start, float end, float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));

        // 양 끝의 속도와 가속도를 0으로 맞춰 마지막 프레임에서 '딱' 멈추지 않는다.
        public static float ArrivalEase(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        public static Vector2 SymmetricDiscArc(Vector2 home, Vector2 oppositeHome, float travel)
        {
            float t = Mathf.Clamp01(travel);
            // 두 홈의 중점을 축으로 같은 곡선을 반사한다. 오른쪽 하강 ↔ 왼쪽 우상향 상승.
            Vector2 low = new(home.x + (home.x - oppositeHome.x) * .42f,
                Mathf.Min(.65f, home.y - .18f));
            Vector2 bend = new(Mathf.Lerp(home.x, low.x, .7f), home.y);
            return Vector2.Lerp(Vector2.Lerp(home, bend, t), Vector2.Lerp(bend, low, t), t);
        }

        Rect ResolveLobbySkyLane()
        {
            if (!layoutInMainMenu || recordTarget == null) return Rect.zero;
            var canvas = recordTarget.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            recordTarget.GetWorldCorners(recordCorners);
            float recordTop = Mathf.Max(RectTransformUtility.WorldToScreenPoint(uiCamera, recordCorners[1]).y,
                RectTransformUtility.WorldToScreenPoint(uiCamera, recordCorners[2]).y);
            return CalculateSkyLane(MobileUiLayout.CurrentSafeArea,
                Screen.height * LobbyAdLayout.GameplayTopInsetFraction, recordTop);
        }

        public static Rect CalculateSkyLane(Rect safeArea, float bannerHeight, float recordTop)
        {
            float ceiling = safeArea.yMax - Mathf.Max(0f, bannerHeight);
            return Rect.MinMaxRect(safeArea.xMin, Mathf.Min(recordTop, ceiling), safeArea.xMax, ceiling);
        }

        Vector2 HomeUvInView(SpriteRenderer painting, Vector2 uv, Rect skyLane)
        {
            // iPad의 cover crop에서도 해·달의 도착점은 화면 안에 둔다. 원화 지우기 UV는 바꾸지 않는다.
            Bounds bounds = painting.sprite.bounds;
            Vector3 world = painting.transform.TransformPoint(new Vector3(
                bounds.min.x + bounds.size.x * uv.x, bounds.min.y + bounds.size.y * uv.y, 0f));
            Vector3 viewport = worldCamera.WorldToViewportPoint(world);
            viewport.x = Mathf.Clamp(viewport.x, 0.09f, 0.91f);
            viewport.y = Mathf.Clamp(viewport.y, 0.09f, 0.91f);
            if (skyLane.height > 0f)
            {
                // 원화 UV가 아니라 실제 배너 하단과 최고 기록 패널 상단 사이에 양쪽을 정렬한다.
                Rect pixels = worldCamera.pixelRect;
                viewport.x = (Mathf.Lerp(skyLane.xMin, skyLane.xMax, uv.x) - pixels.xMin) / pixels.width;
                viewport.y = (skyLane.center.y - pixels.yMin) / pixels.height;
            }
            Vector3 local = painting.transform.InverseTransformPoint(worldCamera.ViewportToWorldPoint(viewport));
            return new Vector2((local.x - bounds.min.x) / bounds.size.x, (local.y - bounds.min.y) / bounds.size.y);
        }

        void PlaceDisc(SpriteRenderer disc, SpriteRenderer painting, Vector2 uv, Color color, float alpha, float diameter)
        {
            Bounds bounds = painting.sprite.bounds;
            Vector3 local = new(bounds.min.x + bounds.size.x * uv.x, bounds.min.y + bounds.size.y * uv.y, 0f);
            disc.transform.position = painting.transform.TransformPoint(local);
            // 로고 탭은 곡선 이동으로만 피드백한다. 천체의 색·크기·펄스는 바꾸지 않는다.
            float scale = diameter / disc.sprite.bounds.size.x;
            disc.transform.localScale = Vector3.one * scale;
            color.a *= alpha;
            disc.color = color;
            disc.enabled = color.a > 0.001f;
        }

        void OnDisable()
        {
            if (logoInput != null) logoInput.Bind(null);
            logoInput = null;
            tapCount = 0;
            pressedHere = false;
            if (sun != null) sun.enabled = false;
            if (moon != null) moon.enabled = false;
            if (foregroundMountains != null) foregroundMountains.enabled = false;
            ReleaseMountainMesh();
            if (backgrounds != null && previousMaterials != null)
                for (int i = 0; i < backgrounds.Length; i++)
                    if (backgrounds[i] != null && i < previousMaterials.Length)
                    {
                        backgrounds[i].SetPropertyBlock(null);
                        backgrounds[i].sharedMaterial = previousMaterials[i];
                    }
            if (material != null)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            material = null;
            if (discMaterial != null)
            {
                if (sun != null) sun.sharedMaterial = null;
                if (moon != null) moon.sharedMaterial = null;
                if (Application.isPlaying) Destroy(discMaterial);
                else DestroyImmediate(discMaterial);
            }
            discMaterial = null;
        }

#if UNITY_EDITOR
        public void InitializeForTests() => Initialize();
        public void DisableForTests() => OnDisable();
        public void TickForTests(float dt, bool animate = true, bool mainMenu = true, bool inputAllowed = true, bool reduced = false) =>
            Tick(dt, animate, mainMenu, inputAllowed, reduced);
        public void PointerForTests(Vector2 position, bool pressed, bool held, bool released, bool allowed = true, bool overUi = false) =>
            ProcessPointer(position, pressed, held, released, allowed, overUi);
        public void SetLogoForTests(RectTransform target) => logoTarget = target;
        public void SetRecordForTests(RectTransform target) => recordTarget = target;
        public bool IsOverUiForTests(Vector2 position) => IsOverUi(position);
#endif
    }
}
