using System.Collections;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using MukJump.Player;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class LobbySkinPreviewTests
    {
        [Test, Category("LobbyDrift")]
        public void SlowDriftBouncesWithoutTouchingGameRandom()
        {
            var state = Random.state;
            Vector2 position = Vector2.zero, velocity = new Vector2(3, 4).normalized * LobbySkinPreview.DriftSpeed;
            Vector2 limits = new(100, 200);
            for (int i = 0; i < 36000; i++)
            {
                var before = position;
                LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, 1f / 60f);
                Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(limits.x));
                Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(limits.y));
                Assert.That(Vector2.Distance(before, position), Is.LessThanOrEqualTo(LobbySkinPreview.DriftSpeed / 60f + .0001f));
                Assert.That(velocity.magnitude, Is.EqualTo(LobbySkinPreview.DriftSpeed).Within(.001f));
            }
            Assert.That(Random.state, Is.EqualTo(state));
        }

        [Test, Category("LobbyDrift")]
        public void WallCornerAndLongFrameReflectWithoutEscaping()
        {
            Vector2 position = new(9, 19), velocity = new(10, 10), limits = new(10, 20);
            LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, .2f);
            Assert.That(position, Is.EqualTo(new Vector2(9, 19)));
            Assert.That(velocity, Is.EqualTo(new Vector2(-10, -10)));
            LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, 100f);
            Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(10));
            Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(20));
            limits = Vector2.zero;
            LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, 1);
            Assert.That(position, Is.EqualTo(Vector2.zero));
            limits = Vector2.one * 10; position = new Vector2(-10, 10); velocity = new(-5, 5);
            LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, 0);
            Assert.That(velocity, Is.EqualTo(new Vector2(5, -5)));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LobbyDriftTouchesBounceAndPauseWithoutStartingGame()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Touchscreen touch = null, previousTouch = Touchscreen.current;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var preview = Object.FindAnyObjectByType<LobbySkinPreview>();
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                var body = preview.transform.Find("Drift/Artwork/OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                Assert.That(body.GetComponents<UnityEngine.UI.BaseMeshEffect>(), Is.Empty, "본체 메시 변형을 사용하지 않는다.");
                Assert.That(preview.Area.parent.name, Is.EqualTo("SafeAreaRoot"));
                var safe = (RectTransform)preview.Area.parent;
                Assert.That(preview.Area.rect.width, Is.EqualTo(safe.rect.width).Within(.01));
                Assert.That(preview.Area.rect.height, Is.GreaterThan(safe.rect.height * .7f));
                Assert.That(preview.Area.anchoredPosition.y + preview.Area.rect.yMax,
                    Is.LessThanOrEqualTo(safe.rect.yMax - MobileUiLayout.ReferenceHeight * LobbyAdLayout.TopInsetFraction + .1f));
                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                foreach (var id in new[] { InkSkinId.Ink, InkSkinId.RedHeadband, InkSkinId.Astronaut, InkSkinId.RedShoes, InkSkinId.CraneWings, InkSkinId.DragonTail })
                {
                    Assert.That(InkSkinPreferences.TryEquip(id), Is.True); yield return Wait(.1f);
                    Assert.That(body.rectTransform.rect.width, Is.LessThanOrEqualTo(360f * .85f));
                    Canvas.ForceUpdateCanvases(); CheckBounds(preview);
                    Assert.That(body.canvasRenderer.GetMesh().vertexCount, Is.EqualTo(4));
                    Assert.That(preview.Drift.localRotation, Is.EqualTo(Quaternion.identity));
                    Assert.That(preview.Drift.localScale, Is.EqualTo(Vector3.one));
                }
                InkSkinPreferences.TryEquip(InkSkinId.RedShoes); yield return Wait(.1f);
                CheckTouchTargets(preview);
                Assert.That(preview.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
                Assert.That(preview.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                var before = preview.Drift.anchoredPosition;
                yield return Wait(.4f);
                float distance = Vector2.Distance(before, preview.Drift.anchoredPosition);
                Assert.That(distance, Is.InRange(1f, 20f), "천천히 이동해야 한다.");
                yield return Capture("slow-free-drift");

                // 실제 Input System → UI 모듈 → 레이캐스트 → 드래그 경로. 시작 버튼 위에서 손을 떼도 시작하지 않는다.
                touch = InputSystem.AddDevice<Touchscreen>();
                LobbySettingsProfile.SetReducedMotionEnabled(true); yield return Wait(.1f);
                Vector2 origin = ScreenPoint(preview, preview.Drift.anchoredPosition);
                yield return CheckRaycast(origin, hit => Assert.That(hit, Is.EqualTo(preview.Drift.gameObject)));
                yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Began, origin);
                Assert.That(preview.IsDragging, Is.True);
                Assert.That(body.overrideSprite, Is.SameAs(InkAccessoryMotion.Held.Frame(0)));
                Vector2 target = new(-preview.Travel.x * .7f, preview.Travel.y * .5f);
                yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Moved, ScreenPoint(preview, target));
                Assert.That(Vector2.Distance(preview.Drift.anchoredPosition, target), Is.LessThan(1));
                foreach (var corner in new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                {
                    yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Moved,
                        ScreenPoint(preview, Vector2.Scale(corner, preview.Area.sizeDelta)));
                    Canvas.ForceUpdateCanvases(); CheckBounds(preview);
                    Assert.That(Vector2.Distance(preview.Drift.anchoredPosition, Vector2.Scale(corner, preview.Travel)), Is.LessThan(1));
                }
                yield return Capture("drag-bottom-left");
                Vector2 startPoint = RectTransformUtility.WorldToScreenPoint(null, lobby.StartButton.transform.position);
                yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Moved, startPoint);
                yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Ended, startPoint);
                Assert.That(preview.IsDragging, Is.False);
                Assert.That(body.overrideSprite, Is.SameAs(body.sprite));
                Assert.That(body.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                yield return CheckRaycast(startPoint, hit => Assert.That(hit.GetComponentInParent<UnityEngine.UI.Button>(), Is.EqualTo(lobby.StartButton),
                    "먹이 겹쳐도 시작 버튼의 탭이 우선이다."));

                // 두 번째 손가락은 먼저 잡은 손가락을 가로채지 못한다.
                var e = new PointerEventData(EventSystem.current) { pointerId = 41, position = ScreenPoint(preview, preview.Drift.anchoredPosition) };
                preview.OnPointerDown(e); var held = preview.Drift.anchoredPosition;
                var other = new PointerEventData(EventSystem.current) { pointerId = 42, position = Vector2.zero };
                preview.OnPointerDown(other); preview.OnDrag(other); preview.OnPointerUp(other);
                Assert.That(preview.IsDragging, Is.True);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(held));
                MobileApplicationLifecycle.SetPlatformVisibility(false);
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                e.position += Vector2.one * 100; preview.OnDrag(e);
                Assert.That(preview.IsDragging, Is.False);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(held));
                // 모션 줄이기 상태에서도 수동 이동은 가능하고 손을 떼면 그 자리에 남는다.
                yield return Wait(.2f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(held));
                LobbySettingsProfile.SetReducedMotionEnabled(false);
                preview.OnPointerDown(e);
                var caughtFrame = body.overrideSprite;
                float heldDeadline = Time.realtimeSinceStartup + 1;
                while (body.overrideSprite == caughtFrame && Time.realtimeSinceStartup < heldDeadline) yield return null;
                Assert.That(body.overrideSprite, Is.Not.SameAs(caughtFrame), "잡고 있으면 실제 원화 프레임이 바뀐다.");
                yield return Capture("held-red-shoes");
                preview.OnPointerUp(e);
                Assert.That(body.overrideSprite, Is.SameAs(body.sprite));
                var edge = new PointerEventData(EventSystem.current) { pointerId = 43, position = ScreenPoint(preview, held) };
                preview.OnPointerDown(edge);
                edge.position = ScreenPoint(preview, preview.Area.sizeDelta); preview.OnDrag(edge); preview.OnPointerUp(edge);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(preview.Travel));
                yield return Wait(.2f);
                Assert.That(preview.Velocity.x, Is.LessThan(0)); Assert.That(preview.Velocity.y, Is.LessThan(0));
                Canvas.ForceUpdateCanvases(); CheckBounds(preview);
                yield return Capture("top-right-bounce");

                lobby.OptionsButton.onClick.Invoke(); yield return Wait(.5f);
                before = preview.Drift.anchoredPosition; yield return Wait(.3f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(before));
                Assert.That(preview.Drift.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.False);
                Object.FindAnyObjectByType<LobbyOptionsView>().Close(); yield return Wait(.5f);
                MobileApplicationLifecycle.SetPlatformVisibility(false); yield return Wait(.1f);
                before = preview.Drift.anchoredPosition; yield return Wait(.2f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(before));
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                lobby.SetNavigationPresentation(false, false); yield return Wait(.1f);
                Assert.That(preview.IsVisible, Is.False);
            }
            finally
            {
                if (touch != null) InputSystem.RemoveDevice(touch);
                previousTouch?.MakeCurrent();
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true); Application.runInBackground = background;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static Vector2 ScreenPoint(LobbySkinPreview preview, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(null, preview.Area.TransformPoint(local));

        [UnityTest, Timeout(90000)]
        public IEnumerator HeldArtRegistrationKeepsAccessoriesOnEveryDrawnPose()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                SkinDebugSession.SetAllUnlocked(true);
                var preview = Object.FindAnyObjectByType<LobbySkinPreview>();
                var artwork = (RectTransform)preview.Drift.Find("Artwork");
                var body = artwork.Find("OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                var art = artwork.Find("Accessory").GetComponent<InkAccessoryImage>();
                var clock = typeof(LobbySkinPreview).GetField("heldTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var present = typeof(LobbySkinPreview).GetMethod("PresentHeldPose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                // 검수 화면만 확대한다. 씬/저장값/실제 로비 배율에는 반영하지 않는다.
                preview.transform.SetAsLastSibling();
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                lobby.LogoRect.gameObject.SetActive(false);
                foreach (var button in lobby.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    foreach (var graphic in button.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.enabled = false;
                foreach (var id in new[] { InkSkinId.Ink, InkSkinId.RedHeadband, InkSkinId.Astronaut, InkSkinId.SpiderMask, InkSkinId.RedShoes, InkSkinId.CloudBoots })
                {
                    preview.enabled = true; artwork.localScale = Vector3.one;
                    Assert.That(InkSkinPreferences.TryEquip(id), Is.True); yield return Wait(.1f);
                    preview.enabled = false;
                    preview.Drift.anchoredPosition = new Vector2(0, 100);
                    artwork.localScale = Vector3.one * 4;
                    var e = new PointerEventData(EventSystem.current) { pointerId = 87, position = ScreenPoint(preview, preview.Drift.anchoredPosition) };
                    preview.OnPointerDown(e); Assert.That(preview.IsDragging, Is.True);
                    for (int frame = 0; frame < 4; frame++)
                    {
                        clock.SetValue(preview, (frame + .1f) / InkAccessoryMotion.Held.FramesPerSecond);
                        present.Invoke(preview, null); Canvas.ForceUpdateCanvases();
                        Assert.That(body.overrideSprite, Is.SameAs(InkAccessoryMotion.Held.Frame(frame)));
                        Assert.That(art.sprite == InkSkinCatalog.Sprite(id), Is.True);
                        yield return Capture("held-fit-" + id + "-" + frame);
                    }
                    preview.OnPointerUp(e);
                    Assert.That(body.overrideSprite, Is.SameAs(body.sprite));
                    Assert.That(body.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                }
            }
            finally
            {
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true); Application.runInBackground = background;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckRaycast(Vector2 point, System.Action<GameObject> assertion)
        {
            // EditMode 테스트의 바깥 콜백은 Screen 크기가 Test Runner 창 기준이다.
            // 실제 Game View의 Player 입력 프레임에서만 UI 레이캐스트를 수행한다.
            bool sampled = false; GameObject target = null; System.Exception failure = null;
            System.Action after = () =>
            {
                if (sampled || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                sampled = true;
                try { target = TopTarget(point); } catch (System.Exception e) { failure = e; }
            };
            InputSystem.onAfterUpdate += after;
            try
            {
                float deadline = Time.realtimeSinceStartup + 3;
                while (!sampled && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(sampled, Is.True);
                Assert.That(failure, Is.Null, failure?.ToString());
                assertion(target);
            }
            finally { InputSystem.onAfterUpdate -= after; }
        }
        static GameObject TopTarget(Vector2 point)
        {
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits, Is.Not.Empty, $"point={point}, screen={Screen.width}x{Screen.height}, raycasters=" +
                string.Join(";", Object.FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsSortMode.None)
                    .Select(r => $"{r.name}:{r.isActiveAndEnabled}:{r.GetComponent<Canvas>().renderMode}")) + "; graphics=" +
                string.Join(";", Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None)
                    .Where(g => g.raycastTarget && g.GetComponentInParent<LobbyView>() != null)
                    .Select(g => $"{g.name}:depth={g.depth},cull={g.canvasRenderer.cull},rect={RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform,point)},filter={g.Raycast(point,null)},pos={g.transform.position}")));
            return hits[0].gameObject;
        }
        static IEnumerator Touch(Touchscreen touch, int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 point)
        {
            bool queued = false;
            System.Action before = () =>
            {
                if (queued || UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                queued = true;
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = phase, position = point });
            };
            InputSystem.onBeforeUpdate += before;
            try
            {
                float deadline = Time.realtimeSinceStartup + 3;
                while (!queued && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(queued, Is.True);
                yield return null; yield return null;
            }
            finally { InputSystem.onBeforeUpdate -= before; }
        }
        static void CheckTouchTargets(LobbySkinPreview preview)
        {
            var targets = preview.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Where(g => g.raycastTarget).ToArray();
            Assert.That(targets.Length, Is.EqualTo(1), "먹 외곽만 터치할 수 있고 빈 화면은 막지 않는다.");
            Assert.That(targets[0].transform, Is.EqualTo(preview.Drift));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EquippedSkinFloatsOnlyInLobbyAndNeverStartsGame()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                var preview = Object.FindAnyObjectByType<LobbySkinPreview>();
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.IsVisible, Is.True);
                CheckTouchTargets(preview);
                Assert.That(preview.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
                Assert.That(preview.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(preview.GetComponentsInChildren<PlayerController>(true), Is.Empty);
                int players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length;
                var initial = preview.Drift.anchoredPosition;
                yield return Wait(1);
                Assert.That(Vector2.Distance(initial, preview.Drift.anchoredPosition), Is.GreaterThan(1));
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length, Is.EqualTo(players));

                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                for (int i = 0; i < InkSkinCatalog.Count; i++)
                {
                    var id = (InkSkinId)i;
                    Assert.That(InkSkinPreferences.TryEquip(id), Is.True); yield return Wait(.1f);
                    Assert.That(preview.DisplayedSkin, Is.EqualTo(id));
                    var art = preview.transform.Find("Drift/Artwork/Accessory").GetComponent<InkAccessoryImage>();
                    // Unity의 빈 오브젝트 참조도 기본 먹의 null 스프라이트와 동일하게 비교한다.
                    Assert.That(art.sprite == InkSkinCatalog.Sprite(id), Is.True, id + " accessory");
                    CheckBounds(preview);
                    Assert.That(art.transform.GetSiblingIndex(), Is.EqualTo(InkSkinCatalog.BehindBody(id) ? 0 : 1));
                }
                InkSkinPreferences.TryEquip(InkSkinId.RedHeadband); yield return Wait(.1f);
                yield return Capture("lobby-floating-headband");
                yield return Wait(2); yield return Capture("lobby-floating-drift");
                InkSkinPreferences.TryEquip(InkSkinId.CraneWings); yield return Wait(.1f);
                yield return Capture("lobby-floating-wings");
                InkSkinPreferences.TryEquip(InkSkinId.RedShoes); yield return Wait(.1f);
                yield return Capture("lobby-floating-boots");
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(),Is.True);
                yield return WaitNavigation();
                var wardrobe=Object.FindAnyObjectByType<InkWardrobeView>();
                Assert.That(wardrobe.IsOpen,Is.True);
                foreach(var language in new[]{GameLanguage.Korean,GameLanguage.English,GameLanguage.Japanese})
                {
                    GameLocalization.SetLanguage(language);
                    wardrobe.Select(language==GameLanguage.Korean ? InkSkinId.RedShoes : InkSkinId.SpiritHalo);
                    Assert.That(wardrobe.Selected,Is.EqualTo(language==GameLanguage.Korean ? InkSkinId.RedShoes : InkSkinId.SpiritHalo));
                    yield return Wait(.5f); yield return Capture("wardrobe-polish-"+language);
                }
                wardrobe.ToggleCollection(); Assert.That(wardrobe.CollectionExpanded,Is.True);
                yield return Wait(.2f); yield return Capture("wardrobe-polish-groups");
                wardrobe.ToggleCollection();
                GameLocalization.SetLanguage(GameLanguage.Korean);
                wardrobe.BackButton.onClick.Invoke(); yield return WaitNavigation();
                Assert.That(lobby.IsInteractive,Is.True);

                MobileApplicationLifecycle.SetPlatformVisibility(false); yield return Wait(.1f);
                var paused = preview.Drift.anchoredPosition; yield return Wait(.15f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(paused));
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                LobbySettingsProfile.SetReducedMotionEnabled(true); yield return Wait(.1f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(paused));
                Assert.That(preview.Drift.localRotation, Is.EqualTo(Quaternion.identity));
                LobbySettingsProfile.SetReducedMotionEnabled(false);
                lobby.SetActiveMenu(LobbyMenuSelection.Options);
                // 실제 옵션 창을 열어 메뉴 상태가 다음 프레임에도 유지되게 한다.
                lobby.OptionsButton.onClick.Invoke(); yield return Wait(.5f);
                paused = preview.Drift.anchoredPosition; yield return Wait(.15f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(paused));
                Object.FindAnyObjectByType<LobbyOptionsView>().Close(); yield return Wait(.5f);
                lobby.SetNavigationPresentation(false, false); yield return Wait(.1f);
                Assert.That(preview.IsVisible, Is.False);
                lobby.SetNavigationPresentation(true, true); yield return Wait(.1f);
                Assert.That(preview.IsVisible, Is.True);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(LobbyScreenNavigator.Instance.CanStartGame,Is.True);
                lobby.StartButton.onClick.Invoke();
                float playDeadline=Time.realtimeSinceStartup+20;
                while(GameManager.Instance.State==GameState.Lobby && Time.realtimeSinceStartup<playDeadline) yield return null;
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
                Assert.That(preview.IsVisible, Is.False);
            }
            finally
            {
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground = previousBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static void CheckBounds(LobbySkinPreview preview)
        {
            var corners = new Vector3[4];
            foreach (var image in preview.GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                if (!image.enabled) continue;
                image.rectTransform.GetWorldCorners(corners);
                if (image.name == "OriginalInkBody")
                {
                    Rect r = preview.IsDragging ? LobbySkinPreview.HeldDrawingBounds : LobbySkinPreview.BodyDrawingBounds;
                    if (preview.IsDragging) r.position -= LobbySkinPreview.HeldArtOffset;
                    float size = image.rectTransform.rect.width;
                    corners[0] = image.transform.TransformPoint(new Vector3(r.xMin, r.yMin) * size);
                    corners[1] = image.transform.TransformPoint(new Vector3(r.xMin, r.yMax) * size);
                    corners[2] = image.transform.TransformPoint(new Vector3(r.xMax, r.yMax) * size);
                    corners[3] = image.transform.TransformPoint(new Vector3(r.xMax, r.yMin) * size);
                }
                // 신발은 빈 이미지 사각형 대신 실제 좌우 신발 메시를 검사한다.
                if (image is InkAccessoryImage && InkSkinCatalog.IsFootwear(preview.DisplayedSkin))
                {
                    var mesh = image.canvasRenderer.GetMesh();
                    Assert.That(mesh.vertexCount, Is.EqualTo(8));
                    foreach (var vertex in mesh.vertices)
                        Assert.That(preview.Area.rect.Contains(preview.Area.InverseTransformPoint(image.transform.TransformPoint(vertex))),
                            Is.True, preview.DisplayedSkin + " footwear overflow");
                    continue;
                }
                foreach (var c in corners)
                {
                    var p = preview.Area.InverseTransformPoint(c);
                    Assert.That(preview.Area.rect.Contains(p), Is.True, preview.DisplayedSkin + " overflow");
                }
            }
        }
        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }
        static IEnumerator WaitNavigation()
        {
            float until=Time.realtimeSinceStartup+20;
            while(LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup<until) yield return null;
            Assert.That(LobbyScreenNavigator.Instance.IsTransitioning,Is.False,"붓 전환 완료 후 촬영한다.");
            yield return null;
        }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/sprite-animation-2026-09-13");
            File.Copy(path, "output/qa/sprite-animation-2026-09-13/" + name + ".png", true);
            File.Delete(path);
        }
    }
}
