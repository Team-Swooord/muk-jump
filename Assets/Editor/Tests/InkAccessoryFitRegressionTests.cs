using System;
using System.Collections;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    [Category("AccessoryFrameFit")]
    public sealed class InkAccessoryFitRegressionTests
    {
        public static readonly InkSkinId[] Reported = {
            InkSkinId.ZodiacTiger, InkSkinId.ZodiacDragon, InkSkinId.ZodiacRooster,
            InkSkinId.LuckyDice, InkSkinId.DragonTail, InkSkinId.MoonClouds,
            InkSkinId.TeaCupHat, InkSkinId.CatEars, InkSkinId.WindFan,
            InkSkinId.CraneCrest, InkSkinId.PaperLanterns, InkSkinId.StormScarf };

        [TestCase("paper_lanterns"), TestCase("dragon_tail"), TestCase("storm_scarf")]
        [TestCase("moon_clouds"), TestCase("blue_wisps"), TestCase("spirit_halo")]
        [TestCase("origami_wings"), TestCase("crane_wings"), TestCase("butterfly_wings")]
        public void AuthoredFramesKeepEveryPixelOnceAndNeverCutVisibleEdges(string key)
        {
            string path = InkSpriteAnimationBuilder.ArtRoot + "/" + key + "_sheet.png";
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), Is.True);
                int w = texture.width, h = texture.height;
                var pixels = texture.GetPixels32();
                var rects = Enumerable.Range(0, 4).Select(i => InkSpriteAnimationBuilder.SourceRect(key, i, w, h)).ToArray();
                var factories = new SpriteDataProviderFactories(); factories.Init();
                var provider = factories.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(path));
                provider.InitSpriteEditorDataProvider();
                var imported = provider.GetSpriteRects().OrderBy(r => r.name, StringComparer.Ordinal).ToArray();
                Assert.That(imported.Length, Is.EqualTo(4));
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(imported[i].rect, Is.EqualTo(rects[i]), "빌더 규칙이 실제 임포터에 반영돼야 한다");
                    Assert.That(rects[i].size, Is.EqualTo(rects[0].size), "프레임마다 크기가 달라지면 안 된다");
                    Assert.That(rects[i].xMin >= 0 && rects[i].yMin >= 0 && rects[i].xMax <= w && rects[i].yMax <= h, Is.True);
                }
                int missing = 0, duplicate = 0, cutEdge = 0;
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a <= 32) continue;
                    int owners = 0;
                    foreach (var r in rects)
                    {
                        if (!r.Contains(new Vector2(x, y))) continue;
                        owners++;
                        if (x < r.xMin + 3 || y < r.yMin + 3 || x >= r.xMax - 3 || y >= r.yMax - 3) cutEdge++;
                    }
                    if (owners == 0) missing++;
                    if (owners > 1) duplicate++;
                }
                Assert.That(missing, Is.Zero, "원화의 술·끈·꼬리 픽셀을 빠뜨리면 안 된다");
                Assert.That(duplicate, Is.Zero, "옆 프레임의 그림이 섞이면 안 된다");
                Assert.That(cutEdge, Is.Zero, "모바일 필터링 여백까지 확보해야 한다");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void StaticThumbnailsAndReducedMotionUseTheSameAuthoredPoster()
        {
            for (int i = 0; i < InkSkinCatalog.Count; i++)
            {
                var id = (InkSkinId)i;
                if (!InkAccessoryMotion.Supports(id)) continue;
                var clip = InkAccessoryMotion.Animation(id);
                Assert.That(InkSkinCatalog.Sprite(id), Is.SameAs(clip.Frame(0)), id.ToString());
                Assert.That(InkSkinCatalog.Sprite(id).rect.size, Is.EqualTo(clip.Frame(3).rect.size));
            }
        }

        [Test]
        public void HeadRootsAreInsetAndPairedHornsStayCentered()
        {
            foreach (var id in new[] { InkSkinId.ZodiacDragon, InkSkinId.ZodiacRooster, InkSkinId.CatEars })
                Assert.That(InkSkinCatalog.BehindBody(id), Is.True, id.ToString());
            foreach (var id in new[] { InkSkinId.ZodiacTiger, InkSkinId.ZodiacDragon, InkSkinId.ZodiacRooster,
                InkSkinId.LuckyDice, InkSkinId.TeaCupHat, InkSkinId.CatEars, InkSkinId.CraneCrest })
            {
                Assert.That(InkSkinCatalog.Offset(id).x, Is.Zero, id + " 머리 중앙 기준");
                Assert.That(InkSkinCatalog.Width(id), Is.InRange(.45f, .87f), id + " 몸통에 맞는 장식 폭");
            }
            Assert.That(InkSkinCatalog.Offset(InkSkinId.ZodiacDragon).y, Is.InRange(.33f, .37f));
            Assert.That(InkSkinCatalog.Offset(InkSkinId.DragonTail).x, Is.LessThan(.32f), "꼬리 뿌리는 옆구리 안으로 들어간다");
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator ReportedAccessoriesFitInWardrobeAndAllFourFramesRemainVisible()
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
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
                GameLocalization.SetLanguage(GameLanguage.Korean);
                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                float deadline = Time.realtimeSinceStartup + 20;
                while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(LobbyScreenNavigator.Instance.IsTransitioning, Is.False);
                var wardrobe = Object.FindFirstObjectByType<InkWardrobeView>();
                var stage = (RectTransform)wardrobe.Paper.Find("ShowroomStage");
                var art = stage.Find("Preview/Accessory").GetComponent<InkAccessoryImage>();
                foreach (var id in Reported)
                {
                    wardrobe.Select(id); yield return Wait(.85f);
                    var clip = InkAccessoryMotion.Animation(id);
                    if (clip == null) { yield return Capture(id.ToString()); continue; }
                    // 시간 기반 프레임을 그대로 관찰한다. 캡처를 위해 실제 재생 코드를 바꾸지 않는다.
                    for (int f = 0; f < 4; f++)
                    {
                        deadline = Time.realtimeSinceStartup + 3;
                        while (art.overrideSprite != clip.Frame(f) && Time.realtimeSinceStartup < deadline) yield return null;
                        Assert.That(art.overrideSprite, Is.SameAs(clip.Frame(f)));
                        foreach (var vertex in art.canvasRenderer.GetMesh().vertices)
                            Assert.That(stage.rect.Contains(stage.InverseTransformPoint(art.transform.TransformPoint(vertex))), Is.True, id + " stage clip");
                        yield return Capture(id + "-" + f);
                    }
                }
            }
            finally
            {
                SkinDebugSession.Reset(); Application.runInBackground = background;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float end = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/accessory-fit");
            File.Copy(path, "output/qa/accessory-fit/" + name + ".png", true); File.Delete(path);
        }
    }
}
