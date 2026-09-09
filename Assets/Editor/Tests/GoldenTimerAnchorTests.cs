using System.IO;
using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class GoldenTimerAnchorTests
    {
        Scene preview;
        GameManager manager;
        PrototypeHud hud;
        Camera camera;
        RenderTexture target;
        Sprite playerSprite;
        float originalTimeScale;
        float originalFixedDeltaTime;
        bool originalAudioPause;
        readonly Rect safe = new(0, 0, 540, 540);

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            originalFixedDeltaTime = Time.fixedDeltaTime;
            originalAudioPause = AudioListener.pause;
            preview = EditorSceneManager.NewPreviewScene();
            target = new RenderTexture(540, 540, 24);
            target.Create();
            camera = CreateObject("TimerAnchorCamera").AddComponent<Camera>();
            camera.scene = preview;
            camera.cameraType = CameraType.Preview;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = InkPalette.Paper;
            manager = CreateObject("TimerAnchorManager").AddComponent<GameManager>();
            // 프리뷰의 개체만 등록한다. 열린 Main 씬의 플레이어는 수정하지 않는다.
            Invoke(manager, "SetState", GameState.Playing);
            hud = CreateObject("TimerAnchorHud").AddComponent<PrototypeHud>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(
                         "Assets/Art/Character/Player/muk_spritesheet.png"))
                if (asset is Sprite sprite && sprite.name == "idle") playerSprite = sprite;
            Assert.That(playerSprite, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(preview);
            target.Release();
            Object.DestroyImmediate(target);
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDeltaTime;
            AudioListener.pause = originalAudioPause;
        }

        [Test]
        public void RingFollowsCameraRepresentativeAndTransfersAfterDeath()
        {
            var first = CreatePlayer(new Vector3(-1, -.8f));
            var leader = CreatePlayer(new Vector3(.7f, .3f));
            Assert.That(TryResolve(out Rect initial), Is.True);
            Assert.That(Owner(), Is.SameAs(leader));
            Assert.That(manager.TryGetSwarmCameraFrame(out var representative, out _, out _), Is.True);
            Assert.That(Owner(), Is.SameAs(representative));

            leader.transform.position += new Vector3(.4f, .2f);
            Assert.That(TryResolve(out Rect moved), Is.True);
            Assert.That(moved.center.x - initial.center.x, Is.EqualTo(36f).Within(.1f));
            Assert.That(moved.center.y - initial.center.y, Is.EqualTo(-18f).Within(.1f));
            SetDead(leader);
            Assert.That(TryResolve(out _), Is.True);
            Assert.That(Owner(), Is.SameAs(first));
            SetDead(first);
            Assert.That(TryResolve(out _), Is.False);
        }

        [Test]
        public void RingKeepsScreenSizeWhileCameraPansAndZooms()
        {
            CreatePlayer(Vector3.zero);
            Assert.That(TryResolve(out Rect original), Is.True);
            camera.transform.position += new Vector3(.4f, .2f);
            Assert.That(TryResolve(out Rect panned), Is.True);
            Assert.That(panned.x - original.x, Is.EqualTo(-36f).Within(.1f));
            Assert.That(panned.y - original.y, Is.EqualTo(18f).Within(.1f));
            camera.orthographicSize = 4.5f;
            Assert.That(TryResolve(out Rect zoomed), Is.True);
            Assert.That(zoomed.size, Is.EqualTo(original.size));
        }

        [Test]
        public void RingAvoidsHealthEvenWhenBodyIsScaledAndRotated()
        {
            var player = CreatePlayer(Vector3.zero);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 0, 35));
            player.transform.localScale = Vector3.one * 1.4f;
            SpriteRenderer health = ShowHealth(player);
            Assert.That(TryResolve(out Rect rect), Is.True);
            Rect frame = Project(player.GetComponent<SpriteRenderer>().bounds);
            Assert.That(rect.xMin, Is.GreaterThan(frame.xMax - frame.width * .12f));
            Assert.That(rect.Overlaps(Project(health.bounds)), Is.False);
        }

        [Test]
        public void RingHidesWithoutVisiblePlayerAndOutsideGameplay()
        {
            var player = CreatePlayer(Vector3.zero);
            Assert.That(TryResolve(out _), Is.True);
            var body = player.GetComponent<SpriteRenderer>();
            body.enabled = false;
            Assert.That(TryResolve(out _), Is.False);
            body.enabled = true;
            player.transform.position = new Vector3(20, 0);
            Assert.That(TryResolve(out _), Is.False, "화면 밖 캐릭터의 링만 가장자리에 남지 않습니다.");
            player.transform.position = new Vector3(0, 0, -20);
            Assert.That(TryResolve(out _), Is.False, "카메라 뒤쪽 좌표는 반전 투영하지 않습니다.");
            player.transform.position = Vector3.zero;
            player.gameObject.SetActive(false);
            Assert.That(TryResolve(out _), Is.False);
            player.gameObject.SetActive(true);
            Invoke(manager, "SetState", GameState.Lobby);
            Assert.That(TryResolve(out _), Is.False);
            Invoke(manager, "SetState", GameState.GameOver);
            Assert.That(TryResolve(out _), Is.False);
        }

        [TestCase(190f, 220f, 90f, 180f)]
        [TestCase(475f, 220f, 470f, 210f)]
        [TestCase(-10f, 300f, -20f, 290f)]
        public void HealthAndScreenEdgesNeverOverlapRing(float bodyX, float bodyY,
            float healthX, float healthY)
        {
            var body = new Rect(bodyX, bodyY, 60, 60);
            var health = new Rect(healthX, healthY, 100, 10);
            object[] args = { body, health, safe, default(Rect) };
            Assert.That((bool)typeof(PrototypeHud).GetMethod("TryCalculateGoldenTimerRect",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True);
            var ring = (Rect)args[3];
            Assert.That(ring.Overlaps(body), Is.False);
            Assert.That(ring.Overlaps(health), Is.False);
            Assert.That(ring.xMin, Is.GreaterThanOrEqualTo(8));
            Assert.That(ring.xMax, Is.LessThanOrEqualTo(532));
            Assert.That(ring.yMin, Is.GreaterThanOrEqualTo(8));
            Assert.That(ring.yMax, Is.LessThanOrEqualTo(532));
        }

        [Test]
        public void RingHidesDuringPauseAndBlackTransitionThenReturnsOnResume()
        {
            CreatePlayer(Vector3.zero);
            Assert.That(TryResolve(out _), Is.True);
            Assert.That(manager.PauseGame(), Is.True);
            Assert.That(TryResolve(out _), Is.False);
            Assert.That(manager.ResumeGame(), Is.True);
            Assert.That(TryResolve(out _), Is.True);
            typeof(GameManager).GetField("transitionInProgress", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, true);
            Assert.That(TryResolve(out _), Is.False);
            typeof(GameManager).GetField("transitionInProgress", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, false);
            Assert.That(TryResolve(out _), Is.True);
        }

        [Test]
        public void RingSitsBetweenHeadAndHealthWithoutChangingHorizontalPositionOrDiameter()
        {
            var body = new Rect(190, 220, 60, 60);
            var health = new Rect(190, 195, 60, 10);
            object[] args = { body, health, safe, default(Rect) };
            Assert.That((bool)typeof(PrototypeHud).GetMethod("TryCalculateGoldenTimerRect",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True);
            var ring = (Rect)args[3];
            float diameter = 540f * .0476f;
            Assert.That(ring.width, Is.EqualTo(diameter).Within(.001f));
            Assert.That(ring.xMin - body.xMax, Is.EqualTo(diameter * .06f).Within(.001f));
            Assert.That(ring.center.y, Is.EqualTo((body.yMin + health.yMax) * .5f).Within(.001f));
            Assert.That(ring.center.y, Is.GreaterThan(health.yMax).And.LessThan(body.yMin));
            Assert.That(ring.Overlaps(body) || ring.Overlaps(health), Is.False);
        }

        [Test]
        public void SpritePaddingDoesNotLeaveRingFarFromVisibleCharacter()
        {
            var player = CreatePlayer(Vector3.zero);
            var health = ShowHealth(player);
            Assert.That(TryResolve(out Rect ring), Is.True);
            Rect frame = Project(player.GetComponent<SpriteRenderer>().bounds);
            float insetX = frame.width * .12f, insetY = frame.height * .12f;
            Assert.That(ring.xMin, Is.EqualTo(frame.xMax - insetX + ring.width * .06f).Within(.01f));
            Assert.That(ring.center.y, Is.EqualTo((frame.yMin + insetY + Project(health.bounds).yMax) * .5f).Within(.01f));
            Assert.That(ring.xMin, Is.LessThan(frame.xMax), "투명 여백 안으로 들어와 실제 몸통에 가까이 붙습니다.");
        }

        [Test]
        public void WideHealthBarOnlyAdjustsHeightWithoutPushingRingSideways()
        {
            var body = new Rect(190, 220, 60, 60);
            var health = new Rect(170, 195, 150, 10);
            object[] args = { body, health, safe, default(Rect) };
            var calculate = typeof(PrototypeHud).GetMethod("TryCalculateGoldenTimerRect",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That((bool)calculate.Invoke(null, args), Is.True);
            var ring = (Rect)args[3];
            Assert.That(ring.xMin - body.xMax, Is.LessThan(2f));
            Assert.That(ring.Overlaps(health), Is.False);
            float originalX = ring.xMin;
            health.y = 205;
            args[1] = health;
            Assert.That((bool)calculate.Invoke(null, args), Is.True);
            ring = (Rect)args[3];
            Assert.That(ring.xMin, Is.EqualTo(originalX));
            Assert.That(ring.yMin, Is.GreaterThan(health.yMax));
            Assert.That(ring.Overlaps(health), Is.False);
        }

        [Test]
        public void RenderActualCharacterHealthAndGoldenCountdownPlacement()
        {
            var player = CreatePlayer(new Vector3(-.4f, -.1f));
            ShowHealth(player);
            Assert.That(TryResolve(out Rect rect), Is.True);
            Invoke(hud, "OnEnable");
            var material = (Material)typeof(PrototypeHud).GetField("goldenTimerMaterial",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
            material.SetFloat("_Remaining", .68f);
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            Texture2D capture = null;
            try
            {
                RenderTexture.active = target;
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0, 540, 540, 0);
                    Graphics.DrawTexture(rect, Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                        0, 0, 0, 0, Color.white, material);
                }
                finally { GL.PopMatrix(); }
                capture = new Texture2D(540, 540, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, 540, 540), 0, 0);
                capture.Apply();
                int gold = 0;
                for (int y = Mathf.CeilToInt(rect.yMin); y < rect.yMax; y++)
                    for (int x = Mathf.CeilToInt(rect.xMin); x < rect.xMax; x++)
                    {
                        Color pixel = capture.GetPixel(x, 539 - y);
                        if (pixel.r > pixel.g + .06f && pixel.g > pixel.b + .18f) gold++;
                    }
                Assert.That(gold, Is.GreaterThan(20), "계산한 캐릭터 옆 위치에 실제 노란 링을 렌더합니다.");
                const string folder = "output/quality-polish/golden-timer-anchor";
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "character-ring.png"), capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (capture != null) Object.DestroyImmediate(capture);
            }
        }

        GameObject CreateObject(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, preview);
            return go;
        }

        PlayerController CreatePlayer(Vector3 position)
        {
            var go = CreateObject("RingOwner");
            go.transform.position = position;
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<CircleCollider2D>();
            go.AddComponent<SpriteRenderer>().sprite = playerSprite;
            var player = go.AddComponent<PlayerController>();
            manager.RegisterPlayer(player);
            return player;
        }

        SpriteRenderer ShowHealth(PlayerController player)
        {
            var billboard = player.gameObject.AddComponent<PlayerHealthBillboard>();
            Invoke(billboard, "OnEnable");
            var health = billboard.HealthRenderer;
            Bounds body = player.GetComponent<SpriteRenderer>().bounds;
            health.transform.SetPositionAndRotation(new Vector3(body.center.x, body.max.y + .12f),
                Quaternion.identity);
            health.enabled = true;
            return health;
        }

        bool TryResolve(out Rect rect)
        {
            object[] args = { manager, camera, safe, 540f, default(Rect) };
            bool result = (bool)Invoke(hud, "TryGetGoldenTimerRect", args);
            rect = (Rect)args[4];
            return result;
        }

        PlayerController Owner() => (PlayerController)typeof(PrototypeHud).GetField(
            "goldenTimerOwner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);

        Rect Project(Bounds bounds)
        {
            Vector3 min = camera.WorldToScreenPoint(bounds.min);
            Vector3 max = camera.WorldToScreenPoint(bounds.max);
            return Rect.MinMaxRect(min.x, 540f - max.y, max.x, 540f - min.y);
        }

        static void SetDead(PlayerController player) => typeof(PlayerController).GetField(
            "<IsDead>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, true);

        static object Invoke(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
