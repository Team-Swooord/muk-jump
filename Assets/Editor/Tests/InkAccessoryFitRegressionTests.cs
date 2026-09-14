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
            InkSkinId.CraneCrest, InkSkinId.PaperLanterns };

        [Test, Category("SkinReworkAppearance")]
        public void WearablesAreForegroundButTailsStayBehindAndNewNamesTranslate()
        {
            foreach (var id in new[] { InkSkinId.CatEars, InkSkinId.WindFan, InkSkinId.ZodiacOx,
                InkSkinId.ZodiacTiger, InkSkinId.ZodiacGoat, InkSkinId.HaetaeCrest, InkSkinId.ZodiacPig,
                InkSkinId.ZodiacRat, InkSkinId.ZodiacRabbit, InkSkinId.ZodiacDragon, InkSkinId.ZodiacDog,
                InkSkinId.ZodiacRooster, InkSkinId.ZodiacHorse })
                Assert.That(InkSkinCatalog.BehindBody(id), Is.False, id.ToString());
            foreach (var id in new[] { InkSkinId.ZodiacMonkey, InkSkinId.DragonTail, InkSkinId.NineTails })
                Assert.That(InkSkinCatalog.BehindBody(id), Is.True, id.ToString());
            Assert.That(InkSkinCatalog.Offset(InkSkinId.ZodiacPig).y, Is.LessThan(0), "눈 아래 코");
            Assert.That(InkSkinCatalog.Offset(InkSkinId.ZodiacHorse).y, Is.LessThan(.30f), "갈기가 머리에 걸쳐진다");
            Assert.That(InkSkinCatalog.Name(InkSkinId.NineTails), Is.EqualTo("구미호 꼬리"));
            foreach (var id in new[] { InkSkinId.Gat, InkSkinId.SpiderMask, InkSkinId.NightBat,
                InkSkinId.ZodiacGoat, InkSkinId.ZodiacMonkey, InkSkinId.NineTails })
            {
                var lore = InkSkinLore.Get(id);
                Assert.That(InkSkinTranslations.TryTranslate(lore.ko, false, out var en), Is.True);
                Assert.That(InkSkinTranslations.TryTranslate(lore.ko, true, out var ja), Is.True);
                Assert.That(en, Is.EqualTo(lore.en).And.Not.Empty);
                Assert.That(ja, Is.EqualTo(lore.ja).And.Not.Empty);
            }
        }

        [TestCase(InkSkinId.Gat), TestCase(InkSkinId.CatEars), TestCase(InkSkinId.ZodiacTiger)]
        [TestCase(InkSkinId.HaetaeCrest), TestCase(InkSkinId.ZodiacPig), TestCase(InkSkinId.ZodiacMonkey)]
        [TestCase(InkSkinId.YakshaMask, Category = "MaskStrike")]
        [TestCase(InkSkinId.SpiderMask, Category = "SpiderWeb")]
        [Category("SkinReworkAppearance")]
        public void NewArtHasRealAlphaAndCatHasNoConnectingBand(InkSkinId id)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes("Assets/Resources/" + InkSkinCatalog.ResourcePath(id) + ".png"));
                var pixels = texture.GetPixels32();
                Assert.That(pixels.Count(c => c.a < 8), Is.GreaterThan(pixels.Length * .40f), id + " 투명한 원화");
                for (int x = 0; x < texture.width; x++)
                {
                    Assert.That(pixels[x].a, Is.Zero, "하단 여백");
                    Assert.That(pixels[(texture.height - 1) * texture.width + x].a, Is.Zero, "상단 여백");
                }
                if (id == InkSkinId.CatEars)
                    for (int y = 0; y < texture.height; y++)
                        Assert.That(pixels[y * texture.width + texture.width / 2].a, Is.Zero, "귀 사이에 머리띠 없음");
                Assert.That(InkSkinCatalog.Sprite(id), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test, Category("SpiderWeb")]
        public void SpiderMaskHasRedBlueFabricAndTransparentEyeOpenings()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes("Assets/Resources/" + InkSkinCatalog.ResourcePath(InkSkinId.SpiderMask) + ".png"));
                var pixels = texture.GetPixels32();
                Assert.That(pixels.Count(c => c.a > 200 && c.r > 150 && c.g < 100 && c.b < 100),
                    Is.GreaterThan(pixels.Length * .1f), "흰 탈 대신 붉은 천");
                Assert.That(pixels.Count(c => c.a > 200 && c.b > 150 && c.r < 100),
                    Is.GreaterThan(pixels.Length * .025f), "좌우 푸른 천");
                foreach (float centerX in new[] { .30f, .70f })
                    for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
                        Assert.That(texture.GetPixelBilinear(centerX + (x - 2) * .025f, .39f + (y - 2) * .04f).a,
                            Is.LessThan(.02f), "기존 먹의 눈을 가리는 체크무늬나 불투명 배경이 없어야 한다");
                Assert.That(InkSkinCatalog.BehindBody(InkSkinId.SpiderMask), Is.False);
                Assert.That((int)InkSkinId.SpiderMask, Is.EqualTo(2), "기존 보유·업적 ID 유지");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [TestCase("paper_lanterns"), TestCase("dragon_tail")]
        [TestCase("moon_clouds"), TestCase("blue_wisps"), TestCase("spirit_halo")]
        [TestCase("origami_wings"), TestCase("crane_wings"), TestCase("butterfly_wings")]
        [TestCase("nine_tails")]
        [TestCase("mask_strike", Category = "MaskStrike")]
        [TestCase("angel_wings", Category = "AngelWings")]
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
                Assert.That(InkSkinCatalog.BehindBody(id), Is.False, id + " 착용 장식은 몸 앞");
            foreach (var id in new[] { InkSkinId.ZodiacDragon, InkSkinId.ZodiacRooster,
                InkSkinId.LuckyDice, InkSkinId.TeaCupHat, InkSkinId.CatEars, InkSkinId.CraneCrest })
            {
                Assert.That(InkSkinCatalog.Offset(id).x, Is.Zero, id + " 머리 중앙 기준");
                Assert.That(InkSkinCatalog.Width(id), Is.InRange(.45f, .87f), id + " 몸통에 맞는 장식 폭");
            }
            Assert.That(InkSkinCatalog.Offset(InkSkinId.ZodiacDragon).y, Is.InRange(.33f, .37f));
            Assert.That(InkSkinCatalog.Offset(InkSkinId.DragonTail).x, Is.LessThan(.32f), "꼬리 뿌리는 옆구리 안으로 들어간다");
        }

        [Test, Category("DragonTailRootFit"), Category("SkinAnimationRework")]
        public void ThickTailKeepsEveryPixelAndSharesTheStaticPoster()
        {
            AuthoredFramesKeepEveryPixelOnceAndNeverCutVisibleEdges("dragon_tail");
            Assert.That(InkSkinCatalog.Sprite(InkSkinId.DragonTail),
                Is.SameAs(InkAccessoryMotion.Animation(InkSkinId.DragonTail).Frame(0)));
        }

        [Test, Category("DragonTailRootFit")]
        public void ThickTailRootAndBellyStaySubstantialAndRegisteredAcrossFrames()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(InkSpriteAnimationBuilder.ArtRoot + "/dragon_tail_sheet.png"));
                var pixels = texture.GetPixels32();
                Assert.That(pixels.Count(c => c.a < 8), Is.GreaterThan(pixels.Length * .65f), "가짜 체크무늬 배경은 허용하지 않는다");
                float minCenter = float.MaxValue, maxCenter = float.MinValue;
                for (int frame = 0; frame < 4; frame++)
                {
                    Rect r = InkSpriteAnimationBuilder.SourceRect("dragon_tail", frame, texture.width, texture.height);
                    foreach (int localX in new[] { 70, (int)r.width / 2 })
                    {
                        int bottom = texture.height, top = -1;
                        for (int y = (int)r.yMin; y < r.yMax; y++)
                        {
                            var c = pixels[y * texture.width + (int)r.xMin + localX];
                            if (c.a <= 128 || c.r <= 150) continue;
                            bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                        }
                        Assert.That(top - bottom + 1, Is.GreaterThanOrEqualTo(localX == 70 ? 140 : 130), "뿌리뿐 아니라 노출되는 중간도 충분히 두껍다");
                        if (localX != 70) continue;
                        float center = (top + bottom) * .5f - r.yMin;
                        minCenter = Mathf.Min(minCenter, center); maxCenter = Mathf.Max(maxCenter, center);
                    }
                }
                Assert.That(maxCenter - minCenter, Is.LessThanOrEqualTo(6), "꼬리 끝만 흔들리고 뿌리는 같은 곳에 붙는다");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator ReportedAccessoriesFitInWardrobeAndAllFourFramesRemainVisible() => CaptureAccessories(Reported);

        [UnityTest, Timeout(120000), Category("DragonTailRootFit")]
        public IEnumerator ThickTailRendersAllFourFramesOnTheOriginalBody() => CaptureAccessories(new[] { InkSkinId.DragonTail });

        [UnityTest, Timeout(150000), Category("SkinReworkAppearance")]
        public IEnumerator ReworkedWearablesAndAnimalFeaturesFitTheOriginalBody() => CaptureAccessories(new[] {
            InkSkinId.Gat, InkSkinId.CatEars, InkSkinId.WindFan, InkSkinId.ZodiacOx,
            InkSkinId.ZodiacTiger, InkSkinId.ZodiacHorse, InkSkinId.ZodiacGoat, InkSkinId.HaetaeCrest,
            InkSkinId.ZodiacPig, InkSkinId.ZodiacMonkey, InkSkinId.ZodiacRat, InkSkinId.ZodiacRabbit,
            InkSkinId.ZodiacDragon, InkSkinId.ZodiacDog, InkSkinId.ZodiacRooster, InkSkinId.NineTails });

        [UnityTest, Timeout(120000), Category("SkinRetirement")]
        public IEnumerator PaperWingsReplaceRetiredScarfWithoutAnEmptyCard() => CaptureAccessories(new[] { InkSkinId.OrigamiWings });

        [UnityTest, Timeout(150000), Category("SkinAnimationRework")]
        public IEnumerator ReworkedHaloAndTailsFitAllAuthoredFrames() => CaptureAccessories(new[] { InkSkinId.SpiritHalo, InkSkinId.DragonTail, InkSkinId.NineTails });

        [UnityTest, Timeout(120000), Category("CloudBoots")]
        public IEnumerator CloudBootsPanelFitsAndAnActualMapLandingTriggersTheJump() => CaptureAccessories(new[] { InkSkinId.CloudBoots });

        [UnityTest, Timeout(120000), Category("MaskStrike")]
        public IEnumerator MaskStrikeHasAReadablePanelAndAnAuthoredCombatEffect() => CaptureAccessories(new[] { InkSkinId.YakshaMask });

        [UnityTest, Timeout(120000), Category("AngelWings")]
        public IEnumerator AngelWingsFitTheOriginalBodyAcrossFourDrawnPoses() => CaptureAccessories(new[] { InkSkinId.AngelWings });

        [UnityTest, Timeout(120000), Category("SpiderWeb")]
        public IEnumerator SpiderWebMovesThePlayerUpAnActualScreenWall() => CaptureAccessories(new[] { InkSkinId.SpiderMask });

        IEnumerator CaptureAccessories(InkSkinId[] ids)
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
                foreach (var id in ids)
                {
                    wardrobe.Select(id); yield return Wait(.85f);
                    if (id is InkSkinId.Gat or InkSkinId.WindFan or InkSkinId.CloudBoots)
                    {
                        foreach (string label in new[] { "ItemLore", "Description" })
                        {
                            var text = wardrobe.Paper.Find(label).GetComponent<UnityEngine.UI.Text>();
                            Assert.That(text.color, Is.EqualTo(InkPalette.Ink));
                            Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Bold));
                            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 3));
                        }
                    }
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
                if (ids.Length == 1 && ids[0] == InkSkinId.YakshaMask)
                {
                    Assert.That(InkSkinPreferences.TryEquip(InkSkinId.YakshaMask), Is.True);
                    Assert.That(LobbyScreenNavigator.Instance.ReturnToLobby(), Is.True);
                    deadline = Time.realtimeSinceStartup + 20;
                    while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
                    var manager = GameManager.Instance;
                    typeof(GameManager).GetMethod("BeginPlayingAfterCover", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manager, null);
                    var player = manager.HighestLivingPlayer;
                    Assert.That(player, Is.Not.Null);
                    var vfx = manager.GetComponent<InkSkinStrikeVfx>();
                    Assert.That(vfx, Is.Not.Null);
                    Assert.That(vfx.Play(player, player.transform.position + Vector3.right), Is.True);
                    yield return Capture("MaskStrike-combat");
                }
                if (ids.Length == 1 && ids[0] == InkSkinId.SpiderMask)
                {
                    Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiderMask), Is.True);
                    Assert.That(LobbyScreenNavigator.Instance.ReturnToLobby(), Is.True);
                    deadline = Time.realtimeSinceStartup + 20;
                    while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
                    var manager = GameManager.Instance;
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    typeof(GameManager).GetMethod("BeginPlayingAfterCover", flags).Invoke(manager, null);
                    yield return Wait(.2f);
                    var player = manager.HighestLivingPlayer;
                    float initialY = player.Body.position.y;
                    var growth = manager.GetComponent<RunGrowthController>();
                    var web = manager.GetComponent<InkWebClimb>();
                    Assert.That(web, Is.Not.Null);
                    typeof(RunGrowthController).GetMethod("TickSkinCooldown", flags).Invoke(growth, new object[] { 30f });
                    deadline = Time.realtimeSinceStartup + 3;
                    while (!web.IsClimbing && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(web.IsClimbing, Is.True);
                    yield return Wait(.3f); yield return Capture("SpiderWeb-climb");
                    var webLine = web.transform.Find("SkinWebThread").GetComponent<LineRenderer>();
                    Assert.That(webLine.sharedMaterial.mainTexture, Is.SameAs(Texture2D.whiteTexture), "붓 텍스처의 투명 끝 때문에 줄이 끊겨 보이지 않는다.");
                    Assert.That(Vector3.Distance(webLine.GetPosition(0), player.transform.position), Is.LessThan(.05f), "줄의 시작점이 본체에서 떨어지지 않는다.");
                    deadline = Time.realtimeSinceStartup + 3;
                    while (web.IsClimbing && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(player.IsDead, Is.False);
                    Assert.That(player.Body.position.y - initialY, Is.GreaterThan(10));
                    Assert.That(web.IsClimbing, Is.False);
                }
                if (ids.Length == 1 && ids[0] == InkSkinId.CloudBoots)
                {
                    Assert.That(InkSkinPreferences.TryEquip(InkSkinId.CloudBoots), Is.True);
                    Assert.That(LobbyScreenNavigator.Instance.ReturnToLobby(), Is.True);
                    deadline = Time.realtimeSinceStartup + 20;
                    while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(LobbyScreenNavigator.Instance.IsTransitioning, Is.False);
                    var manager = GameManager.Instance;
                    typeof(GameManager).GetMethod("BeginPlayingAfterCover", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manager, null);
                    var player = manager.HighestLivingPlayer;
                    Assert.That(player, Is.Not.Null);
                    player.GetComponent<MukJump.Player.AutoJump>().enabled = false;
                    player.transform.position = new Vector3(0, 20, 0);
                    player.Body.position = new Vector2(0, 20);
                    player.Body.linearVelocity = Vector2.down * 2;
                    var platform = MukJump.Drawing.PlatformCollider.SpawnMapRestPlatform(
                        new System.Collections.Generic.List<Vector2> { new(-2, 18.5f), new(2, 18.5f) });
                    Assert.That(platform, Is.Not.Null);
                    Physics2D.SyncTransforms();
                    deadline = Time.realtimeSinceStartup + 4;
                    while (!player.IsInkDropBoosted && !player.IsDead && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(player.IsInkDropBoosted, Is.True, "실제 위쪽 접촉 콜백에서 장화 효과가 발동해야 한다");
                    Assert.That(player.Body.linearVelocity.y, Is.GreaterThan(20));
                    yield return Capture("CloudBoots-flight");
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
