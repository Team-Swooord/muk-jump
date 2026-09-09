using System;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class LobbyNightSkyTests
    {
        static readonly string[] MapPaths = {
            "Assets/Art/Background/Maps/map_00_quiet_mountain.png",
            "Assets/Art/Background/Maps/map_01_wind_ridge.png",
            "Assets/Art/Background/Maps/map_02_ink_rain_valley.png",
            "Assets/Art/Background/Maps/map_03_black_cliff.png",
            "Assets/Resources/MukJump/Background/Endless/map_04_ink_galaxy_gate.png",
            "Assets/Resources/MukJump/Background/Endless/map_05_celestial_lotus.png",
            "Assets/Resources/MukJump/Background/Endless/map_06_heavenly_ink_river.png",
        };
        GameObject root;
        Camera camera;
        LobbyNightSkyView view;
        SpriteRenderer current, next, sun, moon;
        Sprite[] maps;
        Material original;
        RectTransform logo;

        [SetUp]
        public void Setup()
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            LobbyNightState.Reset();
            LobbyAdLayout.ClearTopInset();
            root = new GameObject("NightSkyFixture");
            camera = root.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.aspect = 9f / 16f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = InkPalette.Paper;
            camera.cullingMask = 1 << 31;
            var background = new GameObject("Background");
            background.transform.SetParent(root.transform, false);
            background.transform.localPosition = new Vector3(0, 0, 10);
            background.layer = 31;
            maps = MapPaths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToArray();
            Assert.That(maps.All(sprite => sprite != null), Is.True);
            current = Layer(background.transform, "BackgroundCurrent", -10);
            next = Layer(background.transform, "BackgroundNext", -9);
            current.sprite = maps[0];
            original = current.sharedMaterial;
            next.color = Color.clear;
            view = background.AddComponent<LobbyNightSkyView>();
            view.Configure(camera, new[] { current, next }, maps[0],
                Resources.Load<Texture2D>(LobbyNightSkyView.CleanSkyResourcePath));
            view.InitializeForTests();
            logo = new GameObject("Logo", typeof(RectTransform)).GetComponent<RectTransform>();
            logo.SetParent(root.transform, false);
            PlaceLogo(camera.pixelWidth, camera.pixelHeight);
            view.SetLogoForTests(logo);
            sun = background.transform.Find("LobbySun").GetComponent<SpriteRenderer>();
            moon = background.transform.Find("LobbyMoon").GetComponent<SpriteRenderer>();
            Fit(current);
            view.TickForTests(0);
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null) view.DisableForTests();
            if (root != null) Object.DestroyImmediate(root);
            LobbyNightState.Reset();
            LobbyAdLayout.ClearTopInset();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        static SpriteRenderer Layer(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = 31;
            var layer = go.AddComponent<SpriteRenderer>();
            layer.sortingOrder = order;
            return layer;
        }

        void Fit(SpriteRenderer renderer)
        {
            Vector2 size = renderer.sprite.bounds.size;
            float height = camera.orthographicSize * 2;
            renderer.transform.localScale = Vector3.one * Mathf.Max(height / size.y, height * camera.aspect / size.x);
        }

        Vector2 Center(SpriteRenderer disc) => camera.WorldToScreenPoint(disc.transform.position);
        void PlaceLogo(int width, int height)
        {
            logo.position = new Vector3(width * .5f, height * .55f, 0);
            logo.sizeDelta = new Vector2(width * .7f, height * .2f);
        }
        Vector2 LogoPoint => RectTransformUtility.WorldToScreenPoint(null, logo.position);
        void TapAt(Vector2 point)
        {
            view.PointerForTests(point, true, true, false);
            view.PointerForTests(point, false, false, true);
        }
        void TapLogo() => TapAt(LogoPoint);
        void Advance(float seconds, bool menu = true)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds * 60); i++) view.TickForTests(1f / 60, mainMenu: menu);
        }
        void Night()
        {
            for (int i = 0; i < 10; i++) TapLogo();
            Advance(LobbyNightSkyView.TransitionSeconds + .1f);
        }

        [Test]
        public void EveryLogoTapLowersSunSmoothlyAndTenthContinuesWithoutJumping()
        {
            Vector3 position = sun.transform.position, scale = sun.transform.localScale;
            Color startColor = sun.color;
            for (int count = 1; count <= 9; count++)
            {
                TapLogo(); view.TickForTests(0);
                Assert.That(sun.transform.position, Is.EqualTo(position), "탭 직후 좌표가 튀지 않습니다.");
                Advance(0.25f);
                Assert.That(sun.transform.position.y, Is.LessThan(position.y));
                position = sun.transform.position;
                Assert.That(sun.transform.localScale, Is.EqualTo(scale));
                Assert.That(view.Progress, Is.Zero);
                Assert.That(sun.color, Is.EqualTo(startColor), "탭해도 원래 달 색을 유지합니다.");
            }
            TapLogo();
            view.TickForTests(0);
            Assert.That(sun.transform.position, Is.EqualTo(position));
            Assert.That(sun.color, Is.EqualTo(startColor));
            Advance(0.15f);
            Assert.That(sun.transform.position.y, Is.LessThan(position.y), "열 번째 탭도 현재 위치에서 계속 내려갑니다.");
            Assert.That(sun.transform.localScale, Is.EqualTo(scale));
            Advance(0.1f);
            Assert.That(sun.transform.position.y, Is.LessThan(position.y));
            Advance(LobbyNightSkyView.TransitionSeconds);
            Assert.That(moon.color, Is.EqualTo(LobbyNightSkyView.CelestialRed));
            Assert.That(moon.transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void RedMoonKeepsItsColorAcrossAllTenTaps()
        {
            Night();
            Vector3 position = moon.transform.position, scale = moon.transform.localScale;
            for (int tap = 1; tap <= 9; tap++)
            {
                TapLogo(); view.TickForTests(0);
                Assert.That(moon.transform.position, Is.EqualTo(position));
                Assert.That(moon.transform.localScale, Is.EqualTo(scale));
                Assert.That(moon.color, Is.EqualTo(LobbyNightSkyView.CelestialRed));
                Advance(.25f);
                Assert.That(moon.transform.position.y, Is.LessThan(position.y));
                position = moon.transform.position;
            }
            TapLogo(); view.TickForTests(0);
            Assert.That(moon.color, Is.EqualTo(LobbyNightSkyView.CelestialRed));
            Assert.That(moon.transform.position, Is.EqualTo(position));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingMountainFilterRecoversWithOrWithoutRenderer(bool removeRenderer)
        {
            Transform mountain = view.transform.Find("LobbyForegroundMountains");
            Mesh mesh = mountain.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(mountain.GetComponent<MeshFilter>());
            if (removeRenderer) Object.DestroyImmediate(mountain.GetComponent<MeshRenderer>());
            Assert.DoesNotThrow(() => view.TickForTests(0));
            Assert.That(mountain.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(mountain.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(mountain.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(view.transform.Cast<Transform>().Count(child => child.name == "LobbyForegroundMountains"),
                Is.EqualTo(1), "복구할 때 앞산 오브젝트를 중복 생성하지 않습니다.");
        }

        [Test]
        public void ReinitializationRepairsRendererOnlyMountainObject()
        {
            view.DisableForTests();
            var mountain = view.transform.Find("LobbyForegroundMountains");
            Object.DestroyImmediate(mountain.GetComponent<MeshFilter>());
            Assert.DoesNotThrow(() => view.InitializeForTests());
            Assert.That(mountain.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Assert.That(mountain.GetComponent<MeshRenderer>().enabled, Is.True);
        }

        [TestCase(.31667f, .68333f, .90156f)]
        [TestCase(.23f, .79f, .86f)]
        public void WholeArcAndHomesAreMirroredIncludingOffCenterSafeArea(float left, float right, float height)
        {
            Vector2 rightHome = new(right, height), leftHome = new(left, height);
            Vector2 previous = rightHome;
            for (int step = 0; step <= 100; step++)
            {
                float travel = step / 100f;
                Vector2 setting = LobbyNightSkyView.SymmetricDiscArc(rightHome, leftHome, travel);
                Vector2 risingReversed = LobbyNightSkyView.SymmetricDiscArc(leftHome, rightHome, travel);
                Assert.That(setting.x + risingReversed.x, Is.EqualTo(left + right).Within(.000001f));
                Assert.That(setting.y, Is.EqualTo(risingReversed.y).Within(.000001f));
                Assert.That(setting.x, Is.GreaterThanOrEqualTo(previous.x));
                Assert.That(setting.y, Is.LessThanOrEqualTo(previous.y));
                previous = setting;
            }
            Assert.That(previous.y, Is.LessThanOrEqualTo(.65f), "완전히 산 뒤로 내려가야 합니다.");
        }

        [Test]
        public void ForegroundIsSeparateGeometryAboveDiscsAndColorsStayOpaqueWhileMoving()
        {
            Assert.That(sun.sharedMaterial, Is.SameAs(moon.sharedMaterial));
            var foreground = view.transform.Find("LobbyForegroundMountains").GetComponent<MeshRenderer>();
            Assert.That(foreground.sortingOrder, Is.GreaterThan(sun.sortingOrder));
            Assert.That(current.sortingOrder, Is.LessThan(sun.sortingOrder));
            Mesh mesh = foreground.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.EqualTo(258));
            Assert.That(mesh.bounds.max.y, Is.LessThan(current.sprite.bounds.max.y));
            Assert.That(ShaderUtil.ShaderHasError(current.sharedMaterial.shader), Is.False);
            var block = new MaterialPropertyBlock();
            current.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_SplitForeground"), Is.EqualTo(1f));
            foreground.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_SplitForeground"), Is.Zero);
            Assert.That(block.GetTexture("_MainTex"), Is.SameAs(current.sprite.texture));
            for (int i = 0; i < 10; i++) TapLogo();
            Advance(.6f);
            Assert.That(sun.color, Is.EqualTo(LobbyNightSkyView.DayDiscColor));
            Assert.That(moon.enabled, Is.False);
            Advance(2.5f);
            Assert.That(moon.color, Is.EqualTo(LobbyNightSkyView.CelestialRed));
            Assert.That(sun.enabled, Is.False);
        }

        [Test]
        public void TenCompletedTapsAndOnlyTenStartSunset()
        {
            for (int i = 0; i < 9; i++) TapLogo();
            Assert.That(view.TapCount, Is.EqualTo(9));
            Assert.That(view.IsTransitioning, Is.False);
            view.PointerForTests(LogoPoint, true, true, false);
            Assert.That(view.IsTransitioning, Is.False, "눌림 유지가 아니라 실제 탭 해제로 세어야 합니다.");
            view.PointerForTests(LogoPoint, false, false, true);
            Assert.That(view.IsTransitioning, Is.True);
            Assert.That(view.TapCount, Is.Zero);
            Advance(LobbyNightSkyView.TransitionSeconds + .1f);
            Assert.That(view.Progress, Is.EqualTo(1));
            Assert.That(sun.enabled, Is.False);
            Assert.That(moon.enabled, Is.True);
        }

        [Test]
        public void DragLongPressCancellationAndUiDoNotCount()
        {
            Vector2 center = LogoPoint;
            view.PointerForTests(center, true, true, false);
            view.PointerForTests(center + Vector2.right * camera.pixelWidth * 0.2f, false, true, false);
            view.PointerForTests(center, false, false, true);
            Assert.That(view.TapCount, Is.Zero);
            view.PointerForTests(center, true, true, false);
            Advance(1);
            view.PointerForTests(center, false, false, true);
            Assert.That(view.TapCount, Is.Zero);
            view.PointerForTests(center, true, true, false, overUi: true);
            view.PointerForTests(center, false, false, true, overUi: true);
            view.PointerForTests(center, true, true, false);
            view.PointerForTests(center, false, false, false);
            view.PointerForTests(center, false, false, true);
            Assert.That(view.TapCount, Is.Zero);
            for (int i = 0; i < 9; i++) TapLogo();
            view.TickForTests(0, inputAllowed: false);
            Assert.That(view.TapCount, Is.Zero, "옵션·성장·게임으로 나가면 부분 탭은 취소합니다.");
        }

        [Test]
        public void OnlyVisibleLogoBandCountsNotTransparentMarginsOrHiddenLogo()
        {
            foreach (Vector2 uv in new[] { new Vector2(.5f, .9f), new Vector2(.5f, .1f), new Vector2(.02f, .5f) })
            {
                Vector2 local = logo.rect.min + Vector2.Scale(logo.rect.size, uv);
                TapAt(RectTransformUtility.WorldToScreenPoint(null, logo.TransformPoint(local)));
            }
            TapAt(Center(sun));
            Assert.That(view.TapCount, Is.Zero);
            TapLogo();
            Assert.That(view.TapCount, Is.EqualTo(1));
            logo.gameObject.SetActive(false);
            TapLogo();
            Assert.That(view.TapCount, Is.EqualTo(1));
        }

        [Test]
        public void CancelledPartialChargeReturnsHomeWithoutSnapping()
        {
            Vector3 home = sun.transform.position;
            for (int i = 0; i < 4; i++) TapLogo();
            Advance(.5f);
            Vector3 lowered = sun.transform.position;
            Assert.That(lowered.y, Is.LessThan(home.y));
            view.TickForTests(0, inputAllowed: false);
            Assert.That(sun.transform.position, Is.EqualTo(lowered));
            Advance(2.5f);
            Assert.That(Vector3.Distance(sun.transform.position, home), Is.LessThan(.0005f));
            Assert.That(view.IsTransitioning, Is.False);
        }

        [Test]
        public void ReinitializationKeepsDescentPositionAndVelocity()
        {
            for (int i = 0; i < 10; i++) TapLogo();
            Advance(1.2f);
            Vector3 before = sun.transform.position;
            float velocity = LobbyNightState.CelestialVelocity;
            Assert.That(velocity, Is.GreaterThan(0));
            view.DisableForTests();
            view.InitializeForTests();
            Assert.That(sun.transform.position, Is.EqualTo(before));
            Assert.That(LobbyNightState.CelestialVelocity, Is.EqualTo(velocity));
            view.TickForTests(1f / 60);
            Assert.That(sun.transform.position.y, Is.LessThan(before.y));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RapidTapsGiveContinuousArrivalWithGentleStop(bool startAtNight)
        {
            if (startAtNight) Night();
            SpriteRenderer leaving = startAtNight ? moon : sun;
            SpriteRenderer arriving = startAtNight ? sun : moon;
            Vector3 before = leaving.transform.position;
            for (int i = 0; i < 10; i++) TapLogo();
            view.TickForTests(0);
            Assert.That(leaving.transform.position, Is.EqualTo(before));
            float previousY = arriving.transform.position.y, largestStep = 0, finalStep = 0;
            for (int frame = 0; frame < 130 && view.IsTransitioning; frame++)
            {
                view.TickForTests(.05f);
                float step = arriving.transform.position.y - previousY;
                Assert.That(step, Is.GreaterThanOrEqualTo(-.00003f), "상승 중 뒤로 튀지 않습니다.");
                largestStep = Mathf.Max(largestStep, step);
                finalStep = step;
                previousY = arriving.transform.position.y;
            }
            Assert.That(view.IsTransitioning, Is.False);
            Assert.That(largestStep, Is.GreaterThan(.01f));
            Assert.That(finalStep, Is.LessThan(largestStep * .03f));
            Vector3 home = arriving.transform.position;
            Advance(.5f);
            Assert.That(arriving.transform.position, Is.EqualTo(home));
            Assert.That(LobbyNightSkyView.ArrivalEase(.01f), Is.LessThan(.00002f));
            Assert.That(LobbyNightSkyView.ArrivalEase(.99f), Is.GreaterThan(.99998f));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void OutgoingDiscIsCompletelyGoneBeforeOtherDiscStartsRising(bool startAtNight, bool reduced)
        {
            if (startAtNight) Night();
            SpriteRenderer leaving = startAtNight ? moon : sun;
            SpriteRenderer arriving = startAtNight ? sun : moon;
            for (int i = 0; i < 10; i++) TapLogo();
            view.TickForTests(0f, reduced: reduced);
            Vector3 arrivalStart = arriving.transform.position;
            bool sawDeparture = false, sawArrival = false, sawEmptySky = false;
            for (int frame = 0; frame < 400 && view.IsTransitioning; frame++)
            {
                view.TickForTests(1f / 60f, reduced: reduced);
                float phase = startAtNight ? 1f - view.Progress : view.Progress;
                Assert.That(leaving.color.a * arriving.color.a, Is.Zero, "천체 두 개가 동시에 보여서는 안 됩니다.");
                if (phase <= LobbyNightSkyView.ArrivalStartPhase)
                {
                    Assert.That(arriving.enabled, Is.False);
                    Assert.That(arriving.transform.position, Is.EqualTo(arrivalStart));
                }
                sawDeparture |= leaving.enabled;
                sawArrival |= arriving.enabled;
                sawEmptySky |= leaving.color.a == 0f && arriving.color.a == 0f;
            }
            Assert.That(sawDeparture && sawArrival, Is.True);
            if (!reduced) Assert.That(sawEmptySky, Is.True, "사라짐과 등장 사이에도 분명한 간격을 둡니다.");
            Assert.That(leaving.enabled, Is.False);
            Assert.That(arriving.color.a, Is.EqualTo(1f));
        }

        [Test]
        public void LogoUiEventsCountFastTapsOnceAndRejectDragMultitouchAndCancelledPress()
        {
            var events = new GameObject("LogoEventSystem", typeof(EventSystem));
            events.transform.SetParent(root.transform, false);
            var image = logo.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var target = logo.gameObject.AddComponent<LobbyLogoTapTarget>();
            target.Bind(TapLogo);
            Assert.That(image.raycastTarget, Is.True, "로고도 실제 UI 입력 대상이어야 합니다.");
            var data = new PointerEventData(events.GetComponent<EventSystem>())
                { position = LogoPoint, pointerId = 3, button = PointerEventData.InputButton.Left };
            // 프레임 사이에 눌렀다 뗀 터치도 버튼과 같은 이벤트 경로에서 놓치지 않는다.
            for (int i = 1; i <= 10; i++)
            {
                ExecuteEvents.Execute(logo.gameObject, data, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(logo.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(logo.gameObject, data, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(logo.gameObject, data, ExecuteEvents.pointerClickHandler);
                Assert.That(view.TapCount, Is.EqualTo(i % 10));
            }
            Assert.That(view.IsTransitioning, Is.True);
            Advance(LobbyNightSkyView.TransitionSeconds + .1f);

            target.OnPointerDown(data);
            data.position += Vector2.right * 100f;
            target.OnDrag(data);
            data.position = LogoPoint;
            target.OnPointerUp(data);
            target.OnPointerClick(data);
            Assert.That(view.TapCount, Is.Zero, "드래그 후 로고로 돌아와도 탭이 아닙니다.");

            target.OnPointerDown(data);
            var second = new PointerEventData(events.GetComponent<EventSystem>())
                { position = LogoPoint, pointerId = 4, button = PointerEventData.InputButton.Left };
            target.OnPointerDown(second);
            target.OnPointerUp(second);
            target.OnPointerClick(second);
            Assert.That(view.TapCount, Is.Zero);
            target.CancelPress();
            target.OnPointerUp(data);
            target.OnPointerClick(data);
            Assert.That(view.TapCount, Is.Zero, "취소되거나 팝업으로 차단된 터치는 세지 않습니다.");

            target.OnPointerDown(data);
            typeof(LobbyLogoTapTarget).GetField("pressedAt", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(target, Time.unscaledTime - 1f);
            target.OnPointerUp(data);
            target.OnPointerClick(data);
            Assert.That(view.TapCount, Is.Zero, "긴 누름은 탭으로 세지 않습니다.");
        }

        [Test]
        public void LogoRaycastIncludesLettersButNotTransparentMarginsAndRespectsFrontmostUi()
        {
            // EditMode의 미렌더 Overlay는 Graphic.depth=-1이라 raycast 대상이 아니다.
            // 격리된 카메라로 실제 UI mesh를 렌더한 뒤 앞뒤 입력 순서를 검사한다.
            var canvasHost = new GameObject("LogoRaycastCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasHost.transform.SetParent(root.transform, false);
            canvasHost.layer = 31;
            canvasHost.transform.position = Vector3.zero;
            canvasHost.transform.localScale = Vector3.one * .01f;
            var canvas = canvasHost.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 20000;
            var events = new GameObject("LogoEventSystem", typeof(EventSystem));
            events.transform.SetParent(root.transform, false);
            var eventSystem = events.GetComponent<EventSystem>();
            var raycaster = canvasHost.GetComponent<GraphicRaycaster>();
            const System.Reflection.BindingFlags lifecycleFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            // 두 컴포넌트는 ExecuteAlways가 아니므로 EditMode에서 생명주기를 직접 연결한다.
            typeof(EventSystem).GetMethod("OnEnable", lifecycleFlags).Invoke(eventSystem, null);
            typeof(BaseRaycaster).GetMethod("OnEnable", lifecycleFlags).Invoke(raycaster, null);
            EventSystem.current = eventSystem;
            logo.SetParent(canvasHost.transform, false);
            logo.gameObject.layer = 31;
            logo.anchoredPosition = Vector2.zero;
            logo.sizeDelta = new Vector2(600, 200);
            logo.gameObject.AddComponent<RawImage>();
            var target = logo.gameObject.AddComponent<LobbyLogoTapTarget>();
            target.Bind(TapLogo);
            var behind = new GameObject("BehindLogo", typeof(RectTransform), typeof(Image));
            behind.transform.SetParent(canvasHost.transform, false);
            behind.layer = 31;
            behind.transform.SetAsFirstSibling();
            var rect = behind.GetComponent<RectTransform>();
            rect.position = logo.position;
            rect.sizeDelta = logo.sizeDelta;
            var renderTarget = new RenderTexture(540, 960, 24);
            try
            {
                renderTarget.Create();
                camera.targetTexture = renderTarget;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                Vector2 point = camera.WorldToScreenPoint(logo.position);
                Assert.That(target.IsRaycastLocationValid(point, camera), Is.True);
                Vector2 margin = camera.WorldToScreenPoint(logo.TransformPoint(new Vector2(0f, logo.rect.height * .4f)));
                Assert.That(target.IsRaycastLocationValid(margin, camera), Is.False);
                Assert.That(view.IsOverUiForTests(point), Is.False, "로고 뒤의 이미지가 입력을 막지 않습니다.");
                behind.transform.SetAsLastSibling();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                Assert.That(behind.GetComponent<Image>().depth, Is.GreaterThanOrEqualTo(0));
                Assert.That(view.IsOverUiForTests(point), Is.True, "로고 위의 팝업은 입력을 막아야 합니다.");
            }
            finally
            {
                typeof(BaseRaycaster).GetMethod("OnDisable", lifecycleFlags).Invoke(raycaster, null);
                typeof(EventSystem).GetMethod("OnDisable", lifecycleFlags).Invoke(eventSystem, null);
                camera.targetTexture = null;
                Object.DestroyImmediate(renderTarget);
            }
        }

        [TestCase(1080, 1920, 0, 96)]
        [TestCase(1179, 2556, 177, 150)]
        [TestCase(1440, 3200, 180, 250)]
        [TestCase(1536, 2048, 48, 180)]
        public void CelestialLaneStaysExactlyBetweenNotchBannerAndBestRecord(int width, int height, int notch, int banner)
        {
            Rect safe = Rect.MinMaxRect(0, 34, width, height - notch);
            float recordTop = height * .72f;
            Rect lane = LobbyNightSkyView.CalculateSkyLane(safe, banner, recordTop);
            Assert.That(lane.yMin, Is.EqualTo(recordTop));
            Assert.That(lane.yMax, Is.EqualTo(height - notch - banner));
            Assert.That(lane.center.y - recordTop, Is.EqualTo(lane.yMax - lane.center.y).Within(.001f));
            float right = Mathf.Lerp(lane.xMin, lane.xMax, LobbyNightSkyView.SunHomeUv.x);
            float left = Mathf.Lerp(lane.xMin, lane.xMax, LobbyNightSkyView.MoonHomeUv.x);
            Assert.That(right + left, Is.EqualTo(width).Within(.001f));
        }

        [Test]
        public void CelestialLanePlacesBothDiscsInsideActualUiGapAndFollowsBannerResize()
        {
            var record = new GameObject("BestRecord", typeof(RectTransform)).GetComponent<RectTransform>();
            record.SetParent(root.transform, false);
            record.sizeDelta = new Vector2(200f, 50f);
            Rect safe = MobileUiLayout.CurrentSafeArea;
            record.position = new Vector3(Screen.width * .5f, safe.yMax - Screen.height * .12f - 140f, 0f);
            view.SetRecordForTests(record);
            foreach (float banner in new[] { .08f, .12f })
            {
                LobbyAdLayout.SetTopInsetFraction(banner);
                view.TickForTests(0f);
                float top = safe.yMax - Screen.height * banner;
                float bottom = record.position.y + 25f;
                Vector2 sunPosition = Center(sun);
                Assert.That(sunPosition.y, Is.EqualTo((top + bottom) * .5f).Within(.01f));
                float radiusPixels = sun.bounds.size.y * camera.pixelHeight / (camera.orthographicSize * 4f);
                Assert.That(sunPosition.y + radiusPixels, Is.LessThan(top));
                Assert.That(sunPosition.y - radiusPixels, Is.GreaterThan(bottom));
                Night();
                Vector2 moonPosition = Center(moon);
                Assert.That(moonPosition.y, Is.EqualTo(sunPosition.y).Within(.01f));
                Assert.That(moonPosition.x + sunPosition.x, Is.EqualTo(safe.xMin + safe.xMax).Within(.01f));
                for (int i = 0; i < 10; i++) TapLogo();
                Advance(LobbyNightSkyView.TransitionSeconds + .1f);
            }
        }

        [Test]
        public void SameSizeDiscsSetOnRightAndRiseOnLeftWithoutLoop()
        {
            Vector3 sunStart = sun.transform.position;
            Assert.That(sun.sprite, Is.Not.SameAs(moon.sprite));
            Assert.That(sun.sprite.bounds.size, Is.EqualTo(moon.sprite.bounds.size));
            for (int i = 0; i < 10; i++) TapLogo();
            Advance(1.7f);
            Assert.That(sun.transform.position.y, Is.LessThan(sunStart.y));
            Assert.That(sun.transform.position.x, Is.GreaterThan(sunStart.x));
            Advance(LobbyNightSkyView.TransitionSeconds - 1.6f);
            Assert.That(moon.transform.position.x, Is.LessThan(current.transform.position.x));
            Assert.That(moon.transform.position.y, Is.EqualTo(sunStart.y).Within(0.01f));
            Assert.That(moon.bounds.size, Is.EqualTo(sun.bounds.size));
            Vector3 settled = moon.transform.position;
            Advance(30);
            Assert.That(moon.transform.position, Is.EqualTo(settled));
            for (int i = 0; i < 10; i++) TapLogo();
            Advance(LobbyNightSkyView.TransitionSeconds + .1f);
            Assert.That(view.Progress, Is.Zero);
            Assert.That(sun.enabled, Is.True);
            Assert.That(moon.enabled, Is.False);
            Assert.That(sun.transform.position, Is.EqualTo(sunStart));
        }

        [Test]
        public void HanjiMoonHasQuietPaperWashAndSoftEdgesWithoutChangingTheSun()
        {
            Assert.That(sun.sprite, Is.SameAs(InkUiTextureFactory.CreateCelestialDiscSprite()));
            Assert.That(moon.sprite, Is.SameAs(InkUiTextureFactory.CreateHanjiMoonSprite()));
            Assert.That(moon.sprite.rect, Is.EqualTo(sun.sprite.rect));
            Assert.That(moon.sprite.pixelsPerUnit, Is.EqualTo(sun.sprite.pixelsPerUnit));
            Assert.That(moon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(moon.sprite.texture.isReadable, Is.False, "CPU 원화는 생성 후 해제합니다.");
            Sprite cached = moon.sprite;
            Night();
            Advance(10f);
            Assert.That(moon.sprite, Is.SameAs(cached));

            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            try
            {
                target.Create();
                Graphics.Blit(moon.sprite.texture, target);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                pixels.Apply();
                float minimum = 1f, maximum = 0f;
                for (int y = 28; y < 100; y++)
                for (int x = 28; x < 100; x++)
                {
                    Color pixel = pixels.GetPixel(x, y);
                    minimum = Mathf.Min(minimum, pixel.r);
                    maximum = Mathf.Max(maximum, pixel.r);
                    Assert.That(pixel.a, Is.GreaterThan(.99f));
                    Assert.That(pixel.r, Is.EqualTo(pixel.g).Within(.005f));
                    Assert.That(pixel.r, Is.EqualTo(pixel.b).Within(.005f));
                }
                Assert.That(maximum - minimum, Is.InRange(.02f, .14f),
                    "단색은 피하되 밝고 어두운 점이 빽빽한 질감으로 돌아가면 안 됩니다.");
                float smallestEdge = 64f, largestEdge = 0f;
                for (int angle = 0; angle < 360; angle += 5)
                {
                    float radians = angle * Mathf.Deg2Rad;
                    float lastOpaque = 0f;
                    for (float radius = 52f; radius < 63f; radius += .25f)
                    {
                        int x = Mathf.RoundToInt(63.5f + Mathf.Cos(radians) * radius);
                        int y = Mathf.RoundToInt(63.5f + Mathf.Sin(radians) * radius);
                        if (pixels.GetPixel(x, y).a >= .5f) lastOpaque = radius;
                    }
                    smallestEdge = Mathf.Min(smallestEdge, lastOpaque);
                    largestEdge = Mathf.Max(largestEdge, lastOpaque);
                }
                Assert.That(largestEdge - smallestEdge, Is.InRange(1f, 4.5f),
                    "완벽한 원도 큰 가시도 아닌 짧은 한지 가장자리를 유지해야 합니다.");
                Assert.That(pixels.GetPixel(0, 0).a, Is.Zero);
                Assert.That(pixels.GetPixel(127, 127).a, Is.Zero);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                target.Release();
                Object.DestroyImmediate(target);
            }

            camera.aspect = 1f;
            camera.transform.position = new Vector3(moon.transform.position.x, moon.transform.position.y, -10f);
            camera.orthographicSize = moon.bounds.size.y * .64f;
            const string folder = "output/quality-polish/lobby-night";
            Directory.CreateDirectory(folder);
            Capture(folder + "/hanji-moon-closeup.png", 192, 192);
        }

        [Test]
        public void MoonPixelModelHasSubtleWashWithoutHighFrequencySpeckle()
        {
            int minimum = 255, maximum = 0, largestAdjacentChange = 0;
            for (int y = 24; y < 104; y++)
            for (int x = 24; x < 104; x++)
            {
                Color32 pixel = InkUiTextureFactory.SampleHanjiMoonPixel(x, y);
                Assert.That(pixel.a, Is.EqualTo(255), "달 내부에 밝은 구멍을 만들지 않습니다.");
                Assert.That(pixel.g, Is.EqualTo(pixel.r));
                Assert.That(pixel.b, Is.EqualTo(pixel.r));
                Assert.That(pixel, Is.EqualTo(InkUiTextureFactory.SampleHanjiMoonPixel(x, y)));
                minimum = Math.Min(minimum, pixel.r);
                maximum = Math.Max(maximum, pixel.r);
                largestAdjacentChange = Math.Max(largestAdjacentChange,
                    Math.Abs(pixel.r - InkUiTextureFactory.SampleHanjiMoonPixel(x + 1, y).r));
                largestAdjacentChange = Math.Max(largestAdjacentChange,
                    Math.Abs(pixel.r - InkUiTextureFactory.SampleHanjiMoonPixel(x, y + 1).r));
            }
            Assert.That(minimum, Is.GreaterThanOrEqualTo(235));
            Assert.That(maximum - minimum, Is.InRange(6, 20), "넓고 은은한 종이 얼룩만 남깁니다.");
            Assert.That(largestAdjacentChange, Is.LessThanOrEqualTo(3), "1px 명암 대비가 점무늬로 보이면 안 됩니다.");
        }

        [Test]
        public void MoonPixelModelKeepsGentlyUnevenOutlineWithoutSpikes()
        {
            float smallest = 64f, largest = 0f;
            for (int angle = 0; angle < 360; angle += 5)
            {
                float radians = angle * Mathf.Deg2Rad;
                float lastOpaque = 0f;
                for (float radius = 52f; radius < 64f; radius += .25f)
                {
                    int x = Mathf.RoundToInt(63.5f + Mathf.Cos(radians) * radius);
                    int y = Mathf.RoundToInt(63.5f + Mathf.Sin(radians) * radius);
                    if (InkUiTextureFactory.SampleHanjiMoonPixel(x, y).a >= 128) lastOpaque = radius;
                }
                smallest = Mathf.Min(smallest, lastOpaque);
                largest = Mathf.Max(largest, lastOpaque);
            }
            Assert.That(smallest, Is.GreaterThanOrEqualTo(58f));
            Assert.That(largest, Is.LessThanOrEqualTo(63f));
            Assert.That(largest - smallest, Is.InRange(1f, 4.5f));
            foreach (var corner in new[] { new Vector2Int(0, 0), new Vector2Int(127, 0),
                new Vector2Int(0, 127), new Vector2Int(127, 127) })
                Assert.That(InkUiTextureFactory.SampleHanjiMoonPixel(corner.x, corner.y).a, Is.Zero);
        }

        [Test]
        public void LostPropertyBlockRecoversWithoutResettingNightOrMaterial()
        {
            Night();
            var material = current.sharedMaterial;
            var field = typeof(LobbyNightSkyView).GetField("properties",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(view, null);
            Assert.DoesNotThrow(() => view.TickForTests(0f));
            Assert.That(field.GetValue(view), Is.Not.Null);
            Assert.That(current.sharedMaterial, Is.SameAs(material));
            Assert.That(view.Progress, Is.EqualTo(1f));
            Assert.That(LobbyNightState.TargetNight, Is.True);
        }

        [Test]
        public void PauseAndAppSuspensionFreezeButGameEntryDoesNotResetNight()
        {
            for (int i = 0; i < 10; i++) TapLogo();
            Advance(1);
            float before = view.Progress;
            Vector3 sunBefore = sun.transform.position;
            float alphaBefore = sun.color.a;
            view.TickForTests(0, mainMenu: false, inputAllowed: false);
            Assert.That(sun.transform.position, Is.EqualTo(sunBefore));
            Assert.That(sun.color.a, Is.EqualTo(alphaBefore));
            var block = new MaterialPropertyBlock();
            current.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_EraseSun"), Is.EqualTo(1));
            view.TickForTests(600, animate: false, inputAllowed: false);
            Assert.That(view.Progress, Is.EqualTo(before));
            view.TickForTests(600);
            Assert.That(view.Progress - before, Is.LessThanOrEqualTo(0.0105f));
            Advance(LobbyNightSkyView.TransitionSeconds, menu: false);
            Assert.That(LobbyNightState.Blend, Is.EqualTo(1));
            Assert.That(moon.enabled, Is.True);
            view.DisableForTests();
            Assert.That(current.sharedMaterial, Is.SameAs(original));
            view.InitializeForTests();
            view.TickForTests(0);
            Assert.That(view.Progress, Is.EqualTo(1), "로비 왕복과 재활성화는 밤 선택을 유지합니다.");
        }

        [Test]
        public void ReducedMotionUsesShortColorFadeAndNoTravel()
        {
            Vector3 start = sun.transform.position;
            for (int i = 0; i < 10; i++)
            {
                TapLogo(); view.TickForTests(.05f, reduced: true);
                Assert.That(sun.transform.position, Is.EqualTo(start));
            }
            view.TickForTests(0.05f, reduced: true);
            Assert.That(sun.transform.position, Is.EqualTo(start));
            for (int i = 0; i < 10; i++) view.TickForTests(0.05f, reduced: true);
            Assert.That(view.Progress, Is.EqualTo(1));
        }

        [TestCase(1080,1920)]
        [TestCase(1179,2556)]
        [TestCase(1440,3200)]
        [TestCase(1536,2048)]
        public void LogoTapRemainsAvailableBelowBannerWithCoverCroppedPainting(int width, int height)
        {
            var target = new RenderTexture(width,height,0);
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                PlaceLogo(width, height);
                Fit(current);
                view.TickForTests(0);
                Vector3 point = camera.WorldToViewportPoint(sun.transform.position);
                Assert.That(point.x, Is.InRange(0.08f,0.92f));
                Assert.That(point.y, Is.InRange(0.08f,0.92f));
                TapAt(Center(sun));
                Assert.That(view.TapCount, Is.Zero, "광고와 겹치는 해는 더 이상 탭 대상이 아닙니다.");
                TapLogo();
                Assert.That(view.TapCount, Is.EqualTo(1));
                Vector3 round = sun.bounds.size;
                Assert.That(round.x, Is.EqualTo(round.y).Within(0.0001f));
            }
            finally { camera.targetTexture = null; Object.DestroyImmediate(target); }
        }

        [Test]
        public void AllSevenMapsAndBothPhysicalRenderersKeepNightAndCrossfadeAlpha()
        {
            Night();
            Material shared = current.sharedMaterial;
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < maps.Length; i++)
            {
                current.sprite = maps[i];
                next.sprite = maps[(i + 1) % maps.Length];
                current.color = new Color(1,1,1,0.7f);
                next.color = new Color(1,1,1,0.3f);
                current.flipX = i >= 4;
                view.TickForTests(0, mainMenu: false);
                foreach (var layer in new[] { current, next })
                {
                    layer.GetPropertyBlock(block);
                    Assert.That(block.GetFloat("_Night"), Is.EqualTo(1));
                    Assert.That(block.GetFloat("_EraseSun"), Is.EqualTo(layer.sprite == maps[0] ? 1f : 0f));
                    Assert.That(layer.sharedMaterial, Is.SameAs(shared));
                }
                Assert.That(current.color.a, Is.EqualTo(0.7f));
                Assert.That(next.color.a, Is.EqualTo(0.3f));
                Assert.That(current.sprite, Is.SameAs(maps[i]));
                Assert.That(current.flipX, Is.EqualTo(i >= 4));
            }
            Assert.That(view.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
            Assert.That(LobbyNightState.CloudTint.b, Is.GreaterThan(LobbyNightState.CloudTint.r));
        }

        [Test]
        public void ShaderCompilesAndCleanSkyIsNotAnExtraNightPainting()
        {
            Shader shader = Resources.Load<Shader>("MukJump/Shaders/BackgroundNight");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.That(Resources.Load<Texture2D>(LobbyNightSkyView.CleanSkyResourcePath), Is.Not.Null);
            Assert.That(Directory.GetFiles("Assets/Resources/MukJump/Background/Lobby", "*.png").Length, Is.EqualTo(1));
        }

        [Test]
        public void RenderAllMapsInDayAndNightAndTransitionFrames()
        {
            const string folder = "output/quality-polish/lobby-night";
            Directory.CreateDirectory(folder);
            for (int i = 0; i < maps.Length; i++)
            {
                current.sprite = maps[i]; Fit(current); view.TickForTests(0, mainMenu: false);
                Capture($"{folder}/map-{i}-day.png");
            }
            current.sprite = maps[0]; Fit(current); view.TickForTests(0);
            Capture($"{folder}/sunset-0.png");
            for (int i = 0; i < 10; i++) TapLogo();
            for (int second = 1; second <= Mathf.CeilToInt(LobbyNightSkyView.TransitionSeconds); second++)
            { Advance(1); Capture($"{folder}/sunset-{second}.png"); }
            for (int i = 0; i < maps.Length; i++)
            {
                current.sprite = maps[i]; Fit(current); view.TickForTests(0, mainMenu: false);
                Capture($"{folder}/map-{i}-night.png");
            }
        }

        [Test]
        public void RealLobbyKeepsItsButtonsAndNightOwnerOutsideCanvas()
        {
            var scene = MukJumpSceneBuilder.BuildForTests();
            GameObject clone = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var owners = roots.SelectMany(go => go.GetComponentsInChildren<LobbyNightSkyView>(true)).ToArray();
                Assert.That(owners.Length, Is.EqualTo(1));
                Assert.That(owners[0].GetComponent<MapBackgroundView>(), Is.Not.Null);
                Assert.That(owners[0].GetComponentsInChildren<Graphic>().Length, Is.Zero);
                var source = roots.SelectMany(go => go.GetComponentsInChildren<LobbyView>(true)).Single();
                clone = Object.Instantiate(source.gameObject);
                clone.transform.SetParent(root.transform, false);
                var lobby = clone.GetComponent<LobbyView>();
                lobby.RefreshResponsiveLayout();
                lobby.enabled = false;
                Canvas canvas = clone.GetComponent<Canvas>();
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var rect = (RectTransform)canvas.transform;
                rect.sizeDelta = new Vector2(1080,1920);
                rect.position = Vector3.zero;
                rect.localScale = Vector3.one * 0.01f;
                var group = canvas.GetComponent<CanvasGroup>();
                group.alpha = 1; group.interactable = group.blocksRaycasts = true;
                foreach (Transform node in clone.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Canvas.ForceUpdateCanvases();
                Night();
                Assert.That(lobby.StartButton.interactable, Is.True);
                Assert.That(lobby.GrowthButton.interactable, Is.True);
                Assert.That(lobby.OptionsButton.interactable, Is.True);
                const string folder = "output/quality-polish/lobby-night";
                Directory.CreateDirectory(folder);
                Capture(folder + "/lobby-night.png");
                for (int i = 0; i < 10; i++) TapLogo();
                Advance(LobbyNightSkyView.TransitionSeconds + .1f);
                Capture(folder + "/lobby-day.png");
                Directory.CreateDirectory(folder + "/frames");
                Capture(folder + "/frames/frame-000.png");
                int frame = 1;
                for (int tap = 1; tap <= 10; tap++)
                {
                    TapLogo();
                    for (int sample = 0; sample < 2; sample++, frame++)
                    {
                        Advance(1f / 12f);
                        Capture($"{folder}/frames/frame-{frame:000}.png");
                    }
                }
                for (; frame <= 96; frame++)
                {
                    Advance(1f / 12f);
                    Capture($"{folder}/frames/frame-{frame:000}.png");
                }
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                MukJumpSceneBuilder.CloseTestScene(scene);
            }
        }

        void Capture(string path, int width = 432, int height = 768)
        {
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            Texture2D capture = null;
            try
            {
                target.Create(); camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                capture = new Texture2D(width,height,TextureFormat.RGB24,false);
                capture.ReadPixels(new Rect(0,0,width,height),0,0); capture.Apply();
                File.WriteAllBytes(path,capture.EncodeToPNG());
                Assert.That(capture.GetPixels32().Any(pixel => pixel.r < 180), Is.True);
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                if (capture != null) Object.DestroyImmediate(capture);
                target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
