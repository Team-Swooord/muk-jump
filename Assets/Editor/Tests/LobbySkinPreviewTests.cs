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
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class LobbySkinPreviewTests
    {
        [Test] public void DriftIsContinuousAndBoundedWithoutTouchingGameRandom()
        {
            var state = Random.state;
            float distance = 0f;
            for (int i = 0; i < 3600; i++)
            {
                float t = i / 30f;
                var p = LobbySkinPreview.DriftAt(t);
                var next = LobbySkinPreview.DriftAt(t + 1f / 60f);
                Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(1));
                Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(1));
                distance += Vector2.Distance(p, next);
                Assert.That(Vector2.Distance(p, next), Is.LessThan(.06f));
                Assert.That(Mathf.Abs(LobbySkinPreview.TiltAt(t)), Is.LessThanOrEqualTo(24));
                Assert.That(Vector2.Distance(p, LobbySkinPreview.DriftAt(t, 981)), Is.GreaterThan(.00001f));
            }
            Assert.That(distance, Is.GreaterThan(25f), "기존 느린 진동보다 활발하게 이동해야 한다.");
            Assert.That(Random.state, Is.EqualTo(state));
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
                Assert.That(preview.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g => !g.raycastTarget), Is.True);
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
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(Vector2.zero));
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
                    Rect r = LobbySkinPreview.BodyDrawingBounds;
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
            Directory.CreateDirectory("output/qa/lobby-skin-preview");
            File.Copy(path, "output/qa/lobby-skin-preview/" + name + ".png", true);
            File.Delete(path);
        }
    }
}
