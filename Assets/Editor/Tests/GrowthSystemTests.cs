using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Items;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    /// 성장 두루마리의 규칙, 모달 소유권, 풀링, 피해 우선순위와
    /// 고정 비용 VFX 계약을 함께 지키는 회귀 테스트.
    public sealed class GrowthSystemTests
    {
        const string ScrollAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_scroll.png";
        const string VitalityAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_vitality.png";
        const string JumpAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_jump.png";

        readonly List<UnityEngine.Object> cleanup = new();
        readonly List<Camera> retaggedMainCameras = new();
        float originalTimeScale;
        float originalFixedDeltaTime;
        bool originalAudioPause;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            originalFixedDeltaTime = Time.fixedDeltaTime;
            originalAudioPause = AudioListener.pause;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();

            for (int i = 0; i < retaggedMainCameras.Count; i++)
                if (retaggedMainCameras[i] != null)
                    retaggedMainCameras[i].gameObject.tag = "MainCamera";
            retaggedMainCameras.Clear();

            // GameManager.OnDisable이 일시정지 상태를 복구한 뒤 원래 테스트 환경으로 되돌린다.
            AudioListener.pause = originalAudioPause;
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDeltaTime;
        }

        [Test]
        public void GrowthPauseHasIndependentOwnershipFromUserPause()
        {
            var manager = CreatePlayingManager(out _);

            Assert.That(manager.BeginGrowthChoicePause(), Is.True);
            Assert.That(manager.IsPaused, Is.True);
            Assert.That(manager.PauseReason,
                Is.EqualTo(GameplayPauseReason.GrowthChoice));
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            Assert.That(manager.ResumeGame(), Is.False,
                "일시정지판의 재개 입력이 성장 선택 모달을 닫으면 안 됩니다.");
            Assert.That(manager.PauseGame(), Is.False,
                "성장 선택 중 사용자 일시정지판이 겹쳐 열리면 안 됩니다.");
            Assert.That(manager.IsPaused, Is.True);
            Assert.That(manager.EndGrowthChoicePause(), Is.True);
            Assert.That(manager.IsPaused, Is.False);

            Assert.That(manager.PauseGame(), Is.True);
            Assert.That(manager.PauseReason,
                Is.EqualTo(GameplayPauseReason.UserMenu));
            Assert.That(manager.EndGrowthChoicePause(), Is.False,
                "성장 모달의 닫기 경로가 사용자 일시정지를 풀면 안 됩니다.");
            Assert.That(manager.IsPaused, Is.True);
            Assert.That(manager.ResumeGame(), Is.True);
            Assert.That(manager.PauseReason, Is.EqualTo(GameplayPauseReason.None));
        }

        [Test]
        public void GrowthRequestPausesPlayingUntilAppliedChoiceFinishes()
        {
            var manager = CreatePlayingManager(out var growth);
            int requestCount = 0;
            GrowthUpgradeType? selected = null;
            growth.ChoiceRequested += () => requestCount++;
            growth.UpgradeSelected += type => selected = type;

            Assert.That(growth.RequestChoice(), Is.True);
            Assert.That(requestCount, Is.EqualTo(1));
            Assert.That(growth.HasPendingChoice, Is.True);
            Assert.That(manager.PauseReason,
                Is.EqualTo(GameplayPauseReason.GrowthChoice));
            Assert.That(Time.timeScale, Is.Zero);

            Assert.That(growth.TrySelectUpgrade(GrowthUpgradeType.Vitality), Is.True);
            Assert.That(selected, Is.EqualTo(GrowthUpgradeType.Vitality));
            Assert.That(growth.VitalityLevel, Is.EqualTo(1));
            Assert.That(growth.VitalityCharges, Is.EqualTo(1));
            Assert.That(growth.HasSelectedPendingChoice, Is.True);
            Assert.That(manager.IsPaused, Is.True,
                "선택 직후가 아니라 두루마리 닫힘 연출 뒤에 시간이 재개되어야 합니다.");

            Assert.That(growth.FinishChoice(), Is.True);
            Assert.That(growth.HasPendingChoice, Is.False);
            Assert.That(manager.IsPaused, Is.False);
            Assert.That(manager.IsGameplayTicking, Is.True);
        }

        [Test]
        public void PendingGrowthChoiceBlocksQueuedObstacleHit()
        {
            var manager = CreatePlayingManager(out var growth);
            ApplyChoice(growth, GrowthUpgradeType.Vitality);
            var player = CreatePlayer(
                "PausedGrowthCollisionTarget",
                withEffectView: true);
            manager.RegisterPlayer(player);
            player.GrantShield();
            SetField(player, "damageInvulnerableUntil", Time.time - 1f);

            Assert.That(growth.RequestChoice(), Is.True);
            Assert.That(manager.IsGameplayTicking, Is.False);

            // 두루마리 OnTrigger가 시간을 멈춘 같은 물리 스텝의 잔여 충돌을 흉내 낸다.
            player.TakeHit();

            Assert.That(player.IsDead, Is.False);
            Assert.That(player.HasShield, Is.True,
                "선택판 뒤에서 방어막이 소모되면 안 됩니다.");
            Assert.That(growth.VitalityCharges, Is.EqualTo(1),
                "선택판 뒤에서 공유 먹두께가 소모되면 안 됩니다.");
            Assert.That(growth.CancelChoice(), Is.True);
        }

        [Test]
        public void CancelledGrowthChoiceImmediatelyReleasesModalRaycasts()
        {
            CreatePlayingManager(out var growth);
            var host = Track(new GameObject("CancelledGrowthChoiceView"));
            var view = host.AddComponent<GrowthChoiceView>();
            Invoke(view, "BuildIfNeeded");
            Invoke(view, "BindController", growth);

            Assert.That(growth.RequestChoice(), Is.True);
            SetProperty(view, "IsOpen", true);
            Invoke(view, "ApplyRevealPose", 1f);
            var canvasGroup = host.transform
                .Find("GrowthChoiceCanvas")
                .GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;

            Assert.That(growth.CancelChoice(), Is.True);

            Assert.That(view.IsOpen, Is.False);
            Assert.That(canvasGroup.blocksRaycasts, Is.False,
                "게임 상태가 선택을 취소하면 두루마리가 다음 화면 입력을 막으면 안 됩니다.");
            Assert.That(canvasGroup.interactable, Is.False);
        }

        [Test]
        public void VitalityIsSharedCappedAndNeverBecomesNegative()
        {
            CreatePlayingManager(out var growth);
            var player = CreatePlayer("VitalityTarget");

            for (int i = 0; i < RunGrowthController.MaxVitalityLevel; i++)
                ApplyChoice(growth, GrowthUpgradeType.Vitality);

            Assert.That(growth.VitalityLevel,
                Is.EqualTo(RunGrowthController.MaxVitalityLevel));
            Assert.That(growth.VitalityCharges,
                Is.EqualTo(RunGrowthController.MaxVitalityLevel));
            Assert.That(growth.CanSelectUpgrade(GrowthUpgradeType.Vitality),
                Is.False);

            for (int expected = RunGrowthController.MaxVitalityLevel - 1;
                 expected >= 0;
                 expected--)
            {
                Assert.That(growth.TryAbsorbObstacleHit(player), Is.True);
                Assert.That(growth.VitalityCharges, Is.EqualTo(expected));
            }

            Assert.That(growth.TryAbsorbObstacleHit(player), Is.False);
            Assert.That(growth.VitalityCharges, Is.Zero);
        }

        [Test]
        public void ShieldIsConsumedBeforeSharedVitalityInTakeHit()
        {
            var manager = CreatePlayingManager(out var growth);
            ApplyChoice(growth, GrowthUpgradeType.Vitality);
            var player = CreatePlayer("ShieldPriorityTarget", withEffectView: true);
            manager.RegisterPlayer(player);
            Vector3 rootScale = player.transform.localScale;
            var collider = player.GetComponent<CircleCollider2D>();
            float colliderRadius = collider.radius;
            Vector2 colliderOffset = collider.offset;

            player.GrantShield();
            SetField(player, "damageInvulnerableUntil", Time.time - 1f);
            player.TakeHit();

            Assert.That(player.HasShield, Is.False);
            Assert.That(growth.VitalityCharges, Is.EqualTo(1),
                "방어막이 남아 있을 때 공유 먹두께를 먼저 쓰면 안 됩니다.");
            Assert.That(player.IsDead, Is.False);

            SetField(player, "damageInvulnerableUntil", Time.time - 1f);
            player.TakeHit();

            Assert.That(growth.VitalityCharges, Is.Zero);
            Assert.That(player.IsDead, Is.False);
            Assert.That(player.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(collider.radius, Is.EqualTo(colliderRadius));
            Assert.That(collider.offset, Is.EqualTo(colliderOffset));
            Assert.That(collider.enabled, Is.True);
        }

        [Test]
        public void FallDeathDoesNotConsumeVitalityCharge()
        {
            RetagExistingMainCameras();
            var cameraObject = Track(new GameObject("GrowthFallCamera"));
            cameraObject.tag = "MainCamera";
            var worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 5f;

            var manager = CreatePlayingManager(out var growth);
            ApplyChoice(growth, GrowthUpgradeType.Vitality);
            var fallingPlayer = CreatePlayer("FallingGrowthPlayer");
            var survivingPlayer = CreatePlayer("SurvivingGrowthPlayer");
            manager.RegisterPlayer(fallingPlayer);
            manager.RegisterPlayer(survivingPlayer);
            fallingPlayer.transform.position = Vector3.down * 20f;
            SetField(fallingPlayer, "cam", worldCamera);
            SetField(fallingPlayer, "camHalfHeight", worldCamera.orthographicSize);

            Invoke(fallingPlayer, "FixedUpdate");

            Assert.That(fallingPlayer.IsDead, Is.True);
            Assert.That(growth.VitalityCharges, Is.EqualTo(1),
                "먹두께는 장애물 완충이며 화면 아래 추락 목숨까지 막으면 안 됩니다.");
            Assert.That(manager.State, Is.EqualTo(GameState.Playing),
                "다른 먹분신이 살아 있으면 추락 테스트도 게임을 계속해야 합니다.");
        }

        [Test]
        public void JumpGrowthAddsFourPercentPerLevelAndCapsAtTwentyPercent()
        {
            CreatePlayingManager(out var growth);

            ApplyChoice(growth, GrowthUpgradeType.JumpPower);
            Assert.That(growth.JumpLevel, Is.EqualTo(1));
            Assert.That(growth.JumpPowerMultiplier, Is.EqualTo(1.04f).Within(0.0001f));

            for (int i = 1; i < RunGrowthController.MaxJumpLevel; i++)
                ApplyChoice(growth, GrowthUpgradeType.JumpPower);

            Assert.That(growth.JumpLevel,
                Is.EqualTo(RunGrowthController.MaxJumpLevel));
            Assert.That(growth.JumpPowerMultiplier,
                Is.EqualTo(1.20f).Within(0.0001f));
            Assert.That(growth.CanSelectUpgrade(GrowthUpgradeType.JumpPower),
                Is.False);
        }

        [Test]
        public void ChoiceViewBuildsOneBlockingScrollWithTwoResourceCards()
        {
            var vitalitySprite = LoadGrowthSprite(
                VitalityAssetPath, "MukJump/UI/Growth/growth_vitality");
            var jumpSprite = LoadGrowthSprite(
                JumpAssetPath, "MukJump/UI/Growth/growth_jump");
            Assert.That(LoadGrowthSprite(
                ScrollAssetPath, "MukJump/UI/Growth/growth_scroll"), Is.Not.Null);

            var host = Track(new GameObject("GrowthChoiceViewHost"));
            var view = host.AddComponent<GrowthChoiceView>();
            view.SetSprites(vitalitySprite, jumpSprite);
            Invoke(view, "BuildIfNeeded");
            Invoke(view, "BuildIfNeeded");

            Assert.That(CountDirectChildren(
                host.transform, "GrowthChoiceCanvas"), Is.EqualTo(1));
            var canvasRoot = host.transform.Find("GrowthChoiceCanvas");
            Assert.That(canvasRoot, Is.Not.Null);
            Assert.That(canvasRoot.GetComponent<Canvas>().sortingOrder, Is.EqualTo(3000));
            Assert.That(canvasRoot.GetComponent<Canvas>().pixelPerfect, Is.True);

            var backdrop = canvasRoot.Find("InkWash") as RectTransform;
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(backdrop.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(backdrop.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(backdrop.GetComponent<Image>().raycastTarget, Is.True);

            var panel = canvasRoot.Find(
                "SafeAreaRoot/GrowthScrollPopup") as RectTransform;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Find("ScrollBody/ScrollPaper"), Is.Not.Null);
            Assert.That(panel.Find("ScrollBody/PaperCore"), Is.Not.Null);
            Assert.That(panel.Find("TopRoll"), Is.Not.Null);
            Assert.That(panel.Find("BottomRoll"), Is.Not.Null);

            var content = panel.Find("GrowthContent");
            var vitalityCard = content.Find("VitalityChoice");
            var jumpCard = content.Find("JumpChoice");
            Assert.That(vitalityCard.GetComponent<Button>(), Is.Not.Null);
            Assert.That(jumpCard.GetComponent<Button>(), Is.Not.Null);
            Assert.That(vitalityCard.Find("Icon").GetComponent<Image>().sprite,
                Is.SameAs(vitalitySprite));
            Assert.That(jumpCard.Find("Icon").GetComponent<Image>().sprite,
                Is.SameAs(jumpSprite));

            var reveal = (IEnumerator)Invoke(view, "RevealRoutine");
            Assert.That(reveal.MoveNext(), Is.True);
            var rootGroup = canvasRoot.GetComponent<CanvasGroup>();
            Assert.That(rootGroup.blocksRaycasts, Is.True,
                "모달이 열리는 첫 프레임부터 뒤 게임 입력을 막아야 합니다.");

            var growthHost = Track(new GameObject("MaxedGrowthState"));
            var growth = growthHost.AddComponent<RunGrowthController>();
            Invoke(growth, "OnEnable");
            SetProperty(growth, "VitalityLevel",
                RunGrowthController.MaxVitalityLevel);
            SetProperty(growth, "VitalityCharges",
                RunGrowthController.MaxVitalityLevel);
            SetProperty(growth, "JumpLevel",
                RunGrowthController.MaxJumpLevel);
            SetField(view, "boundController", growth);
            SetProperty(view, "IsOpen", true);
            rootGroup.interactable = true;
            Invoke(view, "RefreshCards");

            Assert.That(vitalityCard.GetComponent<Button>().interactable, Is.False);
            Assert.That(jumpCard.GetComponent<Button>().interactable, Is.False);
            Assert.That(vitalityCard.GetComponent<CanvasGroup>().alpha,
                Is.LessThan(0.7f));
            Assert.That(jumpCard.GetComponent<CanvasGroup>().alpha,
                Is.LessThan(0.7f));
            StringAssert.Contains(
                "완성", vitalityCard.Find("Status").GetComponent<Text>().text);
            StringAssert.Contains(
                "완성", jumpCard.Find("Status").GetComponent<Text>().text);
        }

        [Test]
        public void GrowthSpawnerUsesGuaranteedScheduleAndOneReusablePickup()
        {
            Assert.That(GrowthScrollSpawner.DefaultFirstHeight, Is.EqualTo(45f));
            Assert.That(GrowthScrollSpawner.DefaultInterval, Is.EqualTo(180f));
            Assert.That(GrowthScrollSpawner.NextScheduleAtOrAbove(44f),
                Is.EqualTo(45f));
            Assert.That(GrowthScrollSpawner.NextScheduleAtOrAbove(45f),
                Is.EqualTo(45f));
            Assert.That(GrowthScrollSpawner.NextScheduleAtOrAbove(46f),
                Is.EqualTo(225f));
            Assert.That(GrowthScrollSpawner.NextScheduleAtOrAbove(1000f),
                Is.EqualTo(1125f));

            RetagExistingMainCameras();
            var cameraObject = Track(new GameObject("GrowthSpawnerCamera"));
            cameraObject.tag = "MainCamera";
            var worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 9.6f;

            var host = Track(new GameObject("GrowthScrollSpawnerHost"));
            host.SetActive(false);
            var spawner = host.AddComponent<GrowthScrollSpawner>();
            var scrollSprite = LoadGrowthSprite(
                ScrollAssetPath, "MukJump/UI/Growth/growth_scroll");
            spawner.Configure(scrollSprite, 45f, 180f);
            SetField(spawner, "worldCamera", worldCamera);
            Invoke(spawner, "EnsurePool");

            Assert.That(spawner.FirstHeight, Is.EqualTo(45f));
            Assert.That(spawner.Interval, Is.EqualTo(180f));
            Assert.That(spawner.PoolAvailableCount, Is.EqualTo(1));
            Assert.That((bool)Invoke(spawner, "TrySpawn", 45f), Is.True);
            var first = spawner.ActivePickup;
            Assert.That(first, Is.Not.Null);
            Assert.That(spawner.PoolLeasedCount, Is.EqualTo(1));
            Assert.That((bool)Invoke(spawner, "TrySpawn", 225f), Is.False,
                "한 화면에 성장 두루마리를 두 개 이상 대여하면 안 됩니다.");

            Invoke(spawner, "ReleaseActive");
            Assert.That(spawner.PoolAvailableCount, Is.EqualTo(1));
            Assert.That(spawner.PoolLeasedCount, Is.Zero);
            Assert.That((bool)Invoke(spawner, "TrySpawn", 225f), Is.True);
            Assert.That(spawner.ActivePickup, Is.SameAs(first),
                "두 번째 일정은 Instantiate 대신 같은 한 슬롯 풀을 재사용해야 합니다.");

            Invoke(spawner, "HandleWorldHeightTeleported", 1000);
            Assert.That(spawner.HasActivePickup, Is.False);
            Assert.That(spawner.NextScheduledHeight, Is.EqualTo(1125f));
            Assert.That(spawner.PoolAvailableCount, Is.EqualTo(1),
                "순간이동으로 건너뛴 과거 슬롯을 한꺼번에 생성하면 안 됩니다.");
        }

        [Test]
        public void VitalityPuffIsSingleReusableCloneExcludedVisual()
        {
            var player = CreatePlayer("VitalityPuffPlayer", withEffectView: true);
            var view = player.GetComponent<ItemEffectView>();
            var renderer = player.GetComponent<SpriteRenderer>();
            renderer.sprite = CreateTestSprite();
            Vector3 rootScale = player.transform.localScale;
            var collider = player.GetComponent<CircleCollider2D>();
            float radius = collider.radius;
            Vector2 offset = collider.offset;

            view.PlayVitalityHit();
            view.PlayVitalityHit();

            Assert.That(CountDirectChildren(
                player.transform, "GrowthVitalityPuff"), Is.EqualTo(1));
            var puff = player.transform.Find("GrowthVitalityPuff");
            Assert.That(puff, Is.Not.Null);
            Assert.That(puff.GetComponent<SpriteRenderer>(), Is.Not.Null);

            var lifecycle = (IRuntimeCloneLifecycle)view;
            lifecycle.PrepareForRuntimeClone();
            Assert.That(puff.parent, Is.Null);
            var clone = Track(UnityEngine.Object.Instantiate(player.gameObject));
            Assert.That(clone.transform.Find("GrowthVitalityPuff"), Is.Null,
                "고정 캐시 VFX가 먹분신 수만큼 복제되면 안 됩니다.");
            lifecycle.RestoreAfterRuntimeClone();

            Assert.That(puff.parent, Is.SameAs(player.transform));
            Assert.That(player.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(collider.radius, Is.EqualTo(radius));
            Assert.That(collider.offset, Is.EqualTo(offset));
        }

        GameManager CreatePlayingManager(out RunGrowthController growth)
        {
            var host = Track(new GameObject("GrowthGameManager"));
            var manager = host.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            SetProperty(manager, "State", GameState.Playing);

            growth = host.GetComponent<RunGrowthController>();
            if (growth == null)
                growth = host.AddComponent<RunGrowthController>();
            Invoke(growth, "OnEnable");
            return manager;
        }

        PlayerController CreatePlayer(
            string objectName,
            bool withEffectView = false)
        {
            var host = Track(new GameObject(objectName));
            host.AddComponent<SpriteRenderer>();
            var body = host.AddComponent<Rigidbody2D>();
            body.gravityScale = 2.2f;
            host.AddComponent<CircleCollider2D>();
            var player = host.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            if (withEffectView)
            {
                var view = host.AddComponent<ItemEffectView>();
                Invoke(view, "Awake");
            }
            return player;
        }

        void ApplyChoice(
            RunGrowthController growth,
            GrowthUpgradeType upgrade)
        {
            Assert.That(growth.RequestChoice(), Is.True);
            Assert.That(growth.TrySelectUpgrade(upgrade), Is.True);
            Assert.That(growth.FinishChoice(), Is.True);
        }

        Sprite CreateTestSprite()
        {
            var texture = Track(new Texture2D(4, 4, TextureFormat.RGBA32, false));
            texture.SetPixels(Enumerable.Repeat(Color.black, 16).ToArray());
            texture.Apply();
            return Track(Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f));
        }

        static Sprite LoadGrowthSprite(
            string assetPath,
            string resourcePath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null,
                $"성장 아이콘 파일이 없습니다: {assetPath}");
            Assert.That(importer.textureType,
                Is.EqualTo(TextureImporterType.Sprite),
                $"Resources.Load<Sprite>를 위해 Sprite 임포트가 필요합니다: {assetPath}");
            Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(1024),
                $"모바일 선택 아이콘은 1024px GPU 예산을 넘기면 안 됩니다: {assetPath}");
            var sprite = Resources.Load<Sprite>(resourcePath);
            Assert.That(sprite, Is.Not.Null,
                $"성장 아이콘 Resources 경로가 올바르지 않습니다: {resourcePath}");
            return sprite;
        }

        void RetagExistingMainCameras()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (!cameras[i].CompareTag("MainCamera")) continue;
                cameras[i].gameObject.tag = "Untagged";
                retaggedMainCameras.Add(cameras[i]);
            }
        }

        T Track<T>(T target) where T : UnityEngine.Object
        {
            cleanup.Add(target);
            return target;
        }

        static int CountDirectChildren(Transform root, string objectName)
        {
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name == objectName)
                    count++;
            return count;
        }

        static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            var method = target.GetType()
                .GetMethods(BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != methodName) return false;
                    var parameters = candidate.GetParameters();
                    if (parameters.Length != arguments.Length) return false;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (arguments[i] == null) continue;
                        if (!parameters[i].ParameterType.IsInstanceOfType(arguments[i]) &&
                            !(parameters[i].ParameterType.IsValueType &&
                              parameters[i].ParameterType == arguments[i].GetType()))
                            return false;
                    }
                    return true;
                });
            Assert.That(method, Is.Not.Null,
                $"{target.GetType().Name}.{methodName} 메서드를 찾을 수 없습니다.");
            return method.Invoke(target, arguments);
        }

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(target, value);
        }

        static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null,
                $"{target.GetType().Name}.{propertyName} 속성을 찾을 수 없습니다.");
            property.SetValue(target, value);
        }
    }
}
