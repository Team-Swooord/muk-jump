using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MukJump.EditorTools
{
    public static class MukJumpEditModeTestRunner
    {
        static TestRunnerApi runner;
        static ResultLogger resultLogger;
        static double nextRequestPollTime;
        static bool runScheduled;
        static bool requestRefreshed;
        // 외부 검증은 이 요청 파일을 만든 뒤 에셋 새로고침으로 실행한다.
        const string RequestPath = "Temp/MukJumpRunAllTests.request";
        const string CancelRequestPath =
            "Temp/MukJumpCancelEditModeTests.request";
        const string CurrentTestPath =
            "Temp/MukJumpCurrentEditModeTest.txt";
        const string ActiveRunPath =
            "Temp/MukJumpRunAllTests.active";
        const string ResultPath = "Temp/MukJumpRunAllTests.result";
        const string FailurePath = "Temp/MukJumpRunAllTests.failures";

        [InitializeOnLoadMethod]
        static void InstallRequestWatcher()
        {
            InstallResultLoggerIfActive();
            EditorApplication.update -= PollRequestedTests;
            EditorApplication.update += PollRequestedTests;
            PollRequestedTests();
        }

        static void PollRequestedTests()
        {
            if (EditorApplication.timeSinceStartup < nextRequestPollTime)
                return;
            nextRequestPollTime = EditorApplication.timeSinceStartup + 1d;
            if (File.Exists(CancelRequestPath))
            {
                File.Delete(CancelRequestPath);
                DeleteIfExists(RequestPath);
                EditorApplication.delayCall += CancelAllRunningTests;
                return;
            }
            if (!File.Exists(RequestPath))
                return;
            // Play 중 TestRunner가 임시 부트스트랩 씬을 만들면 실행 전체가 깨진다.
            // 다만 요청 파일 자체가 명시적인 자동 검증 의사이므로 Play를 종료하고,
            // 요청은 그대로 둔 채 다음 폴링에서 안전하게 소비한다.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.isPlaying)
                    EditorApplication.isPlaying = false;
                return;
            }
            if (runScheduled)
                return;
            runScheduled = true;
            EditorApplication.delayCall += RunRequestedTests;
        }

        [MenuItem("MukJump/Diagnostics/Resume Requested Validation")]
        static void RunRequestedTests()
        {
            runScheduled = false;
            if (!File.Exists(RequestPath))
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            // 외부 파일 수정 뒤 이전 DLL로 검증하지 않도록 먼저 새로고침한다.
            // 재컴파일/도메인 리로드 동안 요청 파일은 남겨 새 코드로 다시 예약한다.
            if (!requestRefreshed)
            {
                requestRefreshed = true;
                AssetDatabase.Refresh();
                // Refresh 직후에는 컴파일 예약만 있고 isCompiling이 아직 false일 수 있다.
                // 다음 에디터 폴링까지 양보해 이전 DLL로 테스트하는 경합을 막는다.
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            // 실제 실행 직전에만 요청 파일을 지운다. 패키지 초기화나 도메인
            // 리로드가 delayCall보다 먼저 발생하면 요청 파일이 남아 다음 로드에서
            // 다시 예약되므로 테스트 요청이 사라지지 않는다.
            string requestScope = File.ReadAllText(RequestPath).Trim();
            requestRefreshed = false;
            bool renderOnly = requestScope == "render-fixture-only";
            File.Delete(RequestPath);
            // CLI 인증이 막혀도 동일한 읽기 전용 감사를 로컬 요청으로 실행한다.
            if (requestScope == "agent-audit")
            {
                File.WriteAllText("Temp/MukJumpAgentAudit.json",
                    Newtonsoft.Json.JsonConvert.SerializeObject(MukJumpAgentAudit.Audit(),
                        Newtonsoft.Json.Formatting.Indented));
                return;
            }
            if (requestScope == "agent-regression")
            {
                RunAgentToolingRegression();
                return;
            }
            if (requestScope == "release-targeted-recheck")
            {
                Run(new[] { @"^MukJump\.EditorTests\.ReleaseGameplayAuditTests\.(AccountDeletionReturnsThroughSplashBeforePausedTutorial|MainSceneNightFinalBand)$" });
                return;
            }
            if (requestScope == "capture-haetae" || requestScope == "capture-first-run" || requestScope == "capture-lobby-ranking" || requestScope == "capture-logo-night" || requestScope == "capture-result-growth" || requestScope == "capture-blackout")
            {
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                EditorApplication.ExecuteMenuItem(requestScope == "capture-haetae"
                    ? "MukJump/검증/해태 양쪽 벽 실제 촬영" : requestScope == "capture-first-run"
                        ? "MukJump/검증/첫 실행 스포트라이트 실제 촬영" : requestScope == "capture-lobby-ranking"
                            ? "MukJump/검증/로비 순위 아이콘과 닉네임 배치 촬영" : requestScope == "capture-logo-night"
                                ? "MukJump/검증/로고 탭 낮과 밤 실제 촬영" : requestScope == "capture-result-growth"
                                    ? "MukJump/검증/결과 먹빛 누적 애니메이션 촬영" : "MukJump/검증/화면 전환 완전 암전 촬영");
                return;
            }
            if (requestScope == "rebuild-main-scene")
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    {
                        Debug.LogError("[MukJump] 저장하지 않은 씬 변경이 있어 자동 재생성을 중단했습니다.");
                        return;
                    }
                MukJumpSceneBuilder.Build();
                return;
            }
            if (renderOnly) Run(new[] { @"^MukJump\.EditorTools\.LeaderboardRenderFixtureTests\." });
            else if (requestScope == "account-legacy-recovery") Run(new[] {
                @"^MukJump\.EditorTests\.(MukJumpAccount|PermanentGrowthProfile|PermanentGrowthV8|PermanentGrowthTreeProfile|NicknameIdentity)Tests\." });
            else if (requestScope == "haetae-facing") Run(new[] { @"^HaetaeObstacleTests\.(EveryFrameFacesDown|PoolReuseResets|TelegraphLocksSelectedWall|SideWarningMatchesSelectedWall)" });
            else if (requestScope == "live-leaderboard") Run(new[] {
                @"^MukJump\.EditorTests\.(MukJumpAccount|NicknameIdentity)Tests\.", @"^SettingsScrollTests\." });
            else if (requestScope == "account-deletion-targeted") Run(new[] {
                @"^MukJump\.EditorTests\.MukJumpAccountDeletionCleanupTests\.",
                @"^MukJump\.EditorTests\.MukJumpAccountTests\.(GuestLogoutNeverCopiesProgressOrChangesAccount|WithdrawnOrInvalidRankNeverAppearsAsZeroMetres|QueuedLogoutRunsOnceAfterSaveUnlessAccountSessionChanged|AppleDeletionDuplicateCallbacksAndRetriesWithdrawExactlyOnce|AppleDeletionWrongStoredOwnerCannotStartAnyRemoteRequest)" });
            else if (requestScope == "settings-compact-footer") Run(new[] {
                @"^SettingsScrollTests\.(MainSettingsHasThreeCompactTogglesNoMotionOptionAndReadableLabels|SettingsHeaderAndCaptionsFitBothLanguagesWithoutTouchingTheRoll|SettingsFooterKeepsSaveWarningsReadableAboveLegalText|EmptySettingsFooterCollapsesWithoutMovingUpperControls|SettingsGroupsKeepBreathingRoomWithoutOverlappingLowerContent|LegalFooterUsesSmallUnderlinedTextWithFullTouchArea|ExternalCloseUsesSmallHanjiCrossWithFullHitAreaBelowTheRoll|TutorialSharedGeometryKeepsRollAndFooterInsideSafeArea|SettingsAndTutorialShareScaleAndTopRollPosition)" });
            else if (requestScope == "bottom-fall-only") Run(new[] { @"^MukJump\.EditorTests\.BottomFallRecoveryTests\." });
            else if (requestScope == "bottom-fall-regression") Run(new[] {
                @"^MukJump\.EditorTests\.(BottomFallRecovery|PlayerHealth|PlayerSafety)Tests\." });
            else if (requestScope == "shared-hanji-ui") Run(new[] { @"^MukJump\.EditorTests\.SharedHanjiUiTests\.", @"^(MukJump\.EditorTests\.)?(ActionButtonStyle|GameplayHudView)Tests\." });
            else if (requestScope == "analytics-only") Run(new[] { @"^MukJump\.EditorTests\.FirebaseAnalyticsTests\." });
            else if (requestScope == "analytics-regression") Run(new[] {
                @"^MukJump\.EditorTests\.(FirebaseAnalytics|FirstRunTutorial|PermanentGrowthV8|PermanentGrowthProfile|EnglishLocalization|GoogleMobileAdsConfiguration|MonetizationPolicy|PlayerHealth|CodeBoundaryRegression)Tests\.",
                @"^(SettingsScroll|HanjiScrollClose|HanjiScrollCloseRuntime|GameOverPresentation|PlayerHealth)Tests\." });
            else if (requestScope == "nickname-identity") Run(new[] { @"^MukJump\.EditorTests\.NicknameIdentityTests\.", @"^SettingsScrollTests\." });
            else if (requestScope == "tutorial-final-hint") Run(new[] {
                @"^MukJump\.EditorTests\.FirstRunTutorialTests\.(LastStepHasNoTapHint|SpotlightBlocksTheWholeScreen)" });
            else if (requestScope == "first-run-spotlight") Run(new[] {
                @"^MukJump\.EditorTests\.(FirstRunTutorial|PopupTypography|StartupBrandSplash|NicknameIdentity|ApplicationPauseBoundary)Tests\.",
                @"^MukJump\.EditorTests\.EnglishLocalizationTests\.SpotlightAndReview",
                @"^SettingsScrollTests\.(ReviewRetains|Nickname)",
                @"^PauseMenuViewTests\.FirstRunTutorialPause" });
            else if (requestScope == "nickname-icon-only") Run(new[] {
                @"^SettingsScrollTests\.(NicknameMenuIcon|PaintedSettingsIcons|SettingsHeaderAndCaptions|RenderSettingsSpacingFromActualUi)",
                @"^MukJump\.EditorTests\.NicknameIdentityTests\.SettingsShowNicknameBesideUidAndMatchingButtonAboveAccount" });
            else if (requestScope == "lobby-record-button") Run(new[] {
                @"^MukJump\.EditorTests\.LobbyMenuTests\.(LegacyLobbyBackup|MainLeaderboard)" });
            else if (requestScope == "lobby-ranking-entry") Run(new[] {
                @"^SettingsScrollTests\.(NicknameMenuIcon|PaintedSettingsIcons|SettingsHeaderAndCaptions|RenderSettingsSpacingFromActualUi|IconMenuUses|SettingsGroups|RankingDim|TossOptionsOmit)",
                @"^MukJump\.EditorTests\.LobbyMenuTests\.(LegacyLobbyBackup|MainLeaderboard)",
                @"^MukJump\.EditorTests\.NicknameIdentityTests\.SettingsShowNicknameBesideUidAndMatchingButtonAboveAccount" });
            else if (requestScope == "settings-ui-only") Run(new[] { @"^SettingsScrollTests\." });
            else if (requestScope == "settings-transition-only") Run(new[] { @"^SettingsScrollTests\.", @"^HanjiScrollCloseTests\.", @"^HanjiScrollCloseRuntimeTests\." });
            else if (requestScope == "ui-spacing-only") Run(new[] { @"^SettingsScrollTests\.", @"^MukJump\.EditorTests\.PermanentGrowth(V8|V8View|KeystoneView)Tests\." });
            else if (requestScope == "starter-and-wash-only") Run(new[] { @"^MukJump\.EditorTests\.LobbyWorldSetupTests\.", @"^(MukJump\.EditorTests\.)?ActionButtonStyleTests\." });
            else if (requestScope == "fall-recovery-only") Run(new[] { @"^MukJump\.EditorTests\.PlayerHealthTests\." });
            else if (requestScope == "special-platform-only") Run(new[] { @"^SpecialPlatformTests\." });
            else if (requestScope == "rest-platform-edges-only") Run(new[] { @"^SpecialPlatformTests\.", @"^MukJump\.EditorTests\.RestPlatformEdgeTests\." });
            else if (requestScope == "release-lifecycle-only") Run(new[] { @"^MukJump\.EditorTests\.ReleaseGameplayAuditTests\.(Every|Growth)" });
            else if (requestScope == "release-main-maps") Run(new[] { @"^MukJump\.EditorTests\.ReleaseGameplayAuditTests\.MainScene" });
            else if (requestScope == "compatibility-regression") Run(new[] {
                @"^(MukJump\.EditorTests\.)?(MobileUiLayout|GameplayBannerLayout|ApplicationPauseBoundary|StrokeCapture|StrokeOverlapRegression|RuntimePhysicsIntegration|PlayerHealth|HaetaeObstacle|LobbyNightSky|BrushTransitionView|GoldenTimerAnchor|FirstRunTutorial|NicknameIdentity|PauseMenuView|GrowthUnlockPresentation|GameOverPresentation|GoogleMobileAdsConfiguration|AppsInTossRelease)Tests\.",
                @"^MukJump\.EditorTests\.LobbyMenuTests\.(LegacyLobbyBackup|MainLeaderboard)" });
            else if (requestScope == "release-remaining-regression") Run(new[] {
                @"^HanjiScrollClose(Runtime)?Tests\.", @"^PauseMenuViewTests\.",
                @"^MukJump\.EditorTests\.(LobbyNightSky|CodeBoundaryRegression|BrushTransitionView)Tests\." });
            else if (requestScope == "release-final-verification") Run(new[] {
                @"^MukJump\.EditorTests\.(ReleaseGameplayAudit|HudSoftEdge|SharedHanjiUi)Tests\." });
            else if (requestScope == "release-final-fixes") Run(new[] {
                @"^FallingInkRockTests\.SceneBuilderCreatesSingleConfiguredSpawner$",
                @"^GameplayHudViewTests\.", @"^PauseMenuViewTests\.BuildsPauseIconWithNoRenderedPanel$",
                @"^MukJump\.EditorTests\.(GrowthUnlockPresentationTests\.PermanentGrowth_|LobbyDedicatedScreenTests\.GrowthBuilds|PermanentGrowthKeystoneViewTests\.(GrowthScreenUsesLargeType|OrnamentsUseSharedArt)|PermanentGrowthV8ViewTests\.CurrencyBadge|StrokeOverlapRegressionTests\.ReleasedPointer|ReleaseGameplayAuditTests\.(Every|Growth))" });
            else if (requestScope == "ink-gauge-only") Run(new[] { @"^ItemSpawnerBalanceTests\." });
            else if (requestScope == "golden-timer-only") Run(new[] { @"^ItemSpawnerBalanceTests\.", @"^MukJump\.EditorTests\.GoldenTimerAnchorTests\." });
            else if (requestScope == "golden-timer-placement-only") Run(new[] { @"^MukJump\.EditorTests\.GoldenTimerAnchorTests\.", @"^ItemSpawnerBalanceTests\.GoldenBrushRingIsSmallRoundAndInsideSafeArea" });
            else if (requestScope == "golden-gauge-vfx-only") Run(new[] { @"^ItemSpawnerBalanceTests\.", @"^MukJump\.EditorTests\.(GoldenGaugeVfxTests|GoldenTimerAnchorTests|MobileFeedbackPolishTests)\." });
            else if (requestScope == "pause-button-only") Run(new[] { @"^PauseMenuViewTests\.(PauseButtonOccupiesFormerRecordSlot|Builds|PauseOverlay|PauseAndResume|Restores)", @"^GameplayHudViewTests\." });
            else if (requestScope == "pause-icon-only") Run(new[] { @"^PauseMenuViewTests\.(BuildsReadableBlockingControls|BuildsPauseIconWithNoRenderedPanel|RestoresIconOnlyPauseButton|PauseButtonOccupiesFormerRecordSlot|PauseOverlay|PauseAndResume)" });
            else if (requestScope == "pause-navigation-only") Run(new[] { @"^PauseMenuViewTests\.(PauseExit|PauseNavigation|PauseOverlay|PauseAndResume|BuildsReadable|PauseButtonOccupiesFormerRecordSlot)", @"^HanjiScrollCloseTests\.", @"^MukJump\.EditorTests\.BrushTransitionViewTests\." });
            else if (requestScope == "pause-exit-only") Run(new[] { @"^PauseMenuViewTests\.(PauseExit|PauseNavigation|PauseOverlay|PauseAndResume|BuildsReadable)", @"^MukJump\.EditorTests\.GoldenTimerAnchorTests\." });
            else if (requestScope == "startup-regression") Run(new[] { @"^MukJump\.EditorTests\.(StartupBrandSplash|FirstRunTutorial|EnglishLocalization)Tests\.", @"^GameplayHudViewTests\.", @"^PauseMenuViewTests\." });
            else if (requestScope == "startup-brand-only") Run(new[] { @"^MukJump\.EditorTests\.StartupBrandSplashTests\." });
            else if (requestScope == "startup-and-moon") Run(new[] { @"^MukJump\.EditorTests\.StartupBrandSplashTests\.", @"^MukJump\.EditorTests\.LobbyNightSkyTests\.(HanjiMoon|SameSizeDiscs|EveryLogo|RedCharge)" });
            else if (requestScope == "recent-surface-polish") Run(new[] { @"^MukJump\.EditorTests\.StartupBrandSplashTests\.", @"^MukJump\.EditorTests\.LobbyNightSkyTests\.(HanjiMoon|SameSizeDiscs|EveryLogo|RedCharge)", @"^MukJump\.EditorTests\.PermanentGrowthKeystoneViewTests\.(Ornaments|PaperRibbon|RenderBorderless|SelectionWash|ExactlyOneSelected|TabsAndActions|CardClickOnlySelects)" });
            else if (requestScope == "banner-regression") Run(new[] { @"^MukJump\.EditorTests\.(GameplayBannerLayout|MonetizationPolicy|MobileUiLayout|AppsInTossRelease|GoogleMobileAdsConfiguration)Tests\.", @"^GameplayHudViewTests\.", @"^PauseMenuViewTests\." });
            else if (requestScope == "distance-experience") Run(new[] { @"^MukJump\.EditorTests\.(DistanceExperience|PermanentGrowthProfile|PermanentGrowthV8|PermanentGrowthTreeProfile|MukJumpAccount)Tests\." });
            else if (requestScope == "result-growth-animation") Run(new[] { @"^GameOverPresentationTests\.", @"^MukJump\.EditorTests\.DistanceExperienceTests\." });
            else if (requestScope == "result-growth-settlement") Run(new[] { @"^PauseMenuViewTests\.(UnsettledGrowthGauge|AmbiguousPreRevive|RecordWriteRetry|RecoverySettlement|GrowthSaveFailure|ScoreSaveFailure)" });
            else if (requestScope == "brush-blackout") Run(new[] {
                @"^MukJump\.EditorTests\.BrushTransitionViewTests\.",
                @"^MukJump\.EditorTests\.LobbyDedicatedScreenTests\.(ScreenEntry|BackUses|RepeatedRequests|RequestIsRejected)" });
            else if (requestScope == "record-hud-regression") Run(new[] { @"^GameplayHudViewTests\.", @"^PauseMenuViewTests\.", @"^MukJump\.EditorTests\.EnglishLocalizationTests\." });
            else if (requestScope == "growth-selection-only") Run(new[] { @"^MukJump\.EditorTests\.PermanentGrowth(V8|V8View|KeystoneView)Tests\." });
            else if (requestScope == "growth-names-mini-brush") Run(new[] {
                @"^MukJump\.EditorTests\.PermanentGrowthKeystoneViewTests\.(GrowthNamesAndMiniBrush|FocusTextIsReadable|LargeEffectLines|TypographyRemainsLarge)",
                @"^MukJump\.EditorTests\.PermanentGrowthArtTests\.MiniBrush",
                @"^MukJump\.EditorTests\.PermanentGrowthV8Tests\.CatalogHasFourPlainChoices",
                @"^MukJump\.EditorTests\.PermanentGrowthTreeCatalogTests\.ChoiceCopy",
                @"^MukJump\.EditorTests\.LobbyMenuTests\.EveryGrowthNodeOpensReadable" });
            else if (requestScope == "english-only") Run(new[] { @"^MukJump\.EditorTests\.EnglishLocalizationTests\." });
            else if (requestScope == "english-regression") Run(new[] { @"^MukJump\.EditorTests\.(EnglishLocalization|PopupTypography|PermanentGrowthV8|PermanentGrowthV8View|PermanentGrowthKeystoneView|MukJumpAccount)Tests\.", @"(^|\.)(SettingsScroll|LobbyMenu|FirstRunTutorial|GameOverPresentation|GameplayHudView|WindWeatherController|PauseMenuView)Tests\." });
            else if (requestScope == "growth-progression-only") Run(new[] { @"^MukJump\.EditorTests\.PermanentGrowth.*Tests\.", @"(^|\.)PlayerHealthTests\.", @"(^|\.)MukJumpAccountTests\." });
            else if (requestScope == "top-hud-only") Run(new[] { @"^GameplayHudViewTests\.", @"^MukJump\.EditorTests\.WindWeatherControllerTests\." });
            else if (requestScope == "night-sky-only") Run(new[] { @"^MukJump\.EditorTests\.LobbyNightSkyTests\." });
            else if (requestScope == "logo-night-easteregg") Run(new[] { @"^MukJump\.EditorTests\.LobbyNightSkyTests\.(EveryLogo|RedCharge|TenCompleted|DragLong|OnlyVisible|CancelledPartial|Reinitialization|RapidTaps|SameSize|PauseAnd|ReducedMotion|LogoTap|LogoUi|LogoRaycast|OutgoingDisc|CelestialLane|LostProperty|AllSeven)" });
            else if (requestScope == "cloud-motion-only") Run(new[] { @"^MukJump\.EditorTests\.(AmbientCloudView|AmbientCloudTheme|AmbientCloudRuntime|LobbyNightSky)Tests\." });
            else if (requestScope == "night-sky-regression") Run(new[] { @"^MukJump\.EditorTests\.(LobbyNightSky|AmbientCloudView|AmbientCloudTheme|LobbyMenu|LobbyDedicatedScreen|BrushTransitionView|EnglishLocalization)Tests\." });
            else if (requestScope == "hud-soft-edges-only") Run(new[] { @"^GameplayHudViewTests\.", @"^MukJump\.EditorTests\.(HudSoftEdgeTests|WindWeatherControllerTests)\." });
            else if (requestScope == "start-regression-only") Run(new[] { @"^MukJump\.EditorTests\.(PermanentGrowthProfileTests|LobbyMenuTests|LobbyDedicatedScreenTests)\." });
            else if (requestScope == "code-boundary-only") Run(new[] { @"^MukJump\.EditorTests\.CodeBoundaryRegressionTests\.", @"^MukJump\.EditorTests\.MukJumpAccountTests\.(CloudReadAndSaveCannotOverlapAndSaveResumesAfterRead|DisableInvalidatesOldSaveReplyAndAllowsNewSave|DisabledCloudReadCannotCreateServerDataAndCanResume|DisabledCloudReadCannotResumeUnderChangedOrMissingOwner)" });
            else if (requestScope == "code-audit-only") Run(new[] { @"^MukJump\.EditorTests\.(CodeBoundaryRegressionTests|MukJumpAccountTests|LobbyMenuTests|LobbyDedicatedScreenTests|MobileFeedbackPolishTests)\.", @"^(SettingsScrollTests|ItemSpawnerBalanceTests)\." });
            else if (requestScope == "recent-fixes-only") Run(new[] { @"^SettingsScrollTests\.", @"^(MukJump\.EditorTests\.)?ActionButtonStyleTests\.", @"^MukJump\.EditorTests\.(LobbyWorldSetupTests|PlayerHealthTests|PermanentGrowth(V8|V8View|KeystoneView)Tests)\." });
            else if (requestScope == "prerelease-reset") Run(new[] {
                @"^MukJump\.EditorTests\.(PrereleasePlayerReset|IosReleaseBuild|IosLocalizationBuild|MukJumpAccount|NicknameIdentity|FirstRunTutorial|StartupBrandSplash|GoogleMobileAdsConfiguration)Tests\." });
            else if (requestScope == "all") RunAll();
            else
            {
                // 새 요청을 이전 DLL이 읽어도 미지정 범위를 전체 검사로 확대하지 않는다.
                File.WriteAllText(ResultPath, "unsupported-scope=" + requestScope);
                Debug.LogWarning("[MukJump] 알 수 없는 테스트 범위는 실행하지 않습니다: " + requestScope);
            }
        }

        static void CancelAllRunningTests()
        {
            // Unity Test Framework의 공개 API는 실행 GUID가 있어야 중지할 수 있다.
            // 도메인 리로드 뒤 복원된 작업의 GUID는 공개되지 않으므로, 복원된 러너만
            // 리플렉션으로 찾아 각 러너의 공개 CancelRun 메서드를 호출한다.
            Assembly assembly = typeof(TestRunnerApi).Assembly;
            Type holderType = assembly.GetType(
                "UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder");
            PropertyInfo instanceProperty = holderType?.GetProperty(
                "instance",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            object holder = instanceProperty?.GetValue(null);
            MethodInfo getAllRunners = holderType?.GetMethod(
                "GetAllRunners",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (getAllRunners?.Invoke(holder, null) is not System.Array runners)
            {
                Debug.LogWarning("[MukJump] 중지할 Unity 테스트 실행을 찾지 못했습니다.");
                DeleteIfExists(ActiveRunPath);
                DeleteIfExists(CurrentTestPath);
                return;
            }

            int canceled = 0;
            foreach (object activeRunner in runners)
            {
                MethodInfo cancel = activeRunner?.GetType().GetMethod(
                    "CancelRun",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (cancel?.Invoke(activeRunner, null) is true)
                    canceled++;
            }
            Debug.Log($"[MukJump] Unity 테스트 실행 {canceled}개 중지 요청 완료");
            DeleteIfExists(ActiveRunPath);
            DeleteIfExists(CurrentTestPath);
        }

        [MenuItem("MukJump/Diagnostics/Run All EditMode Tests %#t")]
        static void RunAllFromMenu() => RunAll();

        [MenuItem("MukJump/Diagnostics/Run Release Remaining Regression")]
        static void RunReleaseRemaining() => Run(new[] {
            @"^HanjiScrollClose(Runtime)?Tests\.", @"^PauseMenuViewTests\.",
            @"^MukJump\.EditorTests\.(LobbyNightSky|CodeBoundaryRegression|BrushTransitionView)Tests\." });

        [MenuItem("MukJump/Diagnostics/Run Release Map Matrix")]
        static void RunReleaseMapMatrix() => Run(new[] { @"^MukJump\.EditorTests\.ReleaseGameplayAuditTests\." });

        [MenuItem("MukJump/Diagnostics/Run Lobby Return Regression")]
        static void RunLobbyReturnRegression() => Run(new[] {
            @"^MukJump\.EditorTests\.FirstRunTutorialTests\.",
            @"^MukJump\.EditorTests\.ReleaseGameplayAuditTests\.IncompleteTutorialNeverAutoRestarts" });

        public static void RunAll() => Run(null);

        [MenuItem("MukJump/Diagnostics/Run Brand Splash Regression")]
        static void RunBrandSplashRegression() => Run(new[] {
            @"^MukJump\.EditorTests\.(StartupBrandSplash|FirstRunTutorial|IosReleaseBuild|ApplicationPauseBoundary)Tests\.",
            @"^MukJump\.EditorTests\.ReleaseGameplayAuditTests\.AccountDeletionReturnsThroughSplashBeforePausedTutorial$" });

        [MenuItem("MukJump/Diagnostics/Run Guest Leaderboard Regression")]
        static void RunGuestLeaderboardRegression() => Run(new[] {
            @"^MukJump\.EditorTests\.MukJumpAccountTests\." });

        [MenuItem("MukJump/Diagnostics/Run Guest Nickname Regression")]
        static void RunGuestNicknameRegression() => Run(new[] {
            @"^MukJump\.EditorTests\.(NicknameIdentity|FirstRunTutorial)Tests\.",
            @"^SettingsScrollTests\." });

        [MenuItem("MukJump/Diagnostics/Run Tracking Consent Regression")]
        static void RunTrackingConsentRegression() => Run(new[] {
            @"^MukJump\.EditorTests\.(GoogleMobileAdsConfiguration|FirebaseAnalytics)Tests\." });

        [MenuItem("MukJump/Diagnostics/Run Drawing Regression Tests")]
        public static void RunDrawingRegression() => Run(new[]
        {
            @"^(MukJump\.EditorTests\.)?(StrokeOverlapRegressionTests|StrokeCaptureTests|DrawingResourceTests|BezierSmootherTests|ItemSpawnerBalanceTests|PlayerSafetyTests|SpecialPlatformTests|RuntimePhysicsIntegrationTests|PauseMenuViewTests)\."
        });

        [MenuItem("MukJump/Diagnostics/Run Gameplay Audit Tests")]
        public static void RunGameplayAudit() => Run(new[]
        {
            @"^(MukJump\.EditorTests\.)?(ApplicationPauseBoundaryTests|GameplayLoopAuditTests|StrokeOverlapRegressionTests|PauseMenuViewTests|AmbientCloudRuntimeTests)\."
        });

        [MenuItem("MukJump/Diagnostics/Run Agent Tooling Regression")]
        public static void RunAgentToolingRegression() => Run(new[]
        {
            @"^MukJump\.EditorTests\.(AgentTooling|StartupBrandSplash|FirstRunTutorial|NicknameIdentity|CloudSaveFailure|JapaneseLocalization|DeviceRegion|IosReleaseBuild|IosLocalizationBuild|CodeBoundaryRegression|ReleaseGameplayAudit)Tests\."
        });

        static void Run(string[] groups)
        {
            if (runner != null || File.Exists(ActiveRunPath))
            {
                Debug.LogWarning("[MukJump] EditMode 테스트가 이미 실행 중입니다.");
                return;
            }

            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            DeleteIfExists(CurrentTestPath);
            DeleteIfExists(ResultPath);
            DeleteIfExists(FailurePath);
            File.WriteAllText(ActiveRunPath, "scope=" +
                (groups == null ? "all" : string.Join("|", groups)) + "\n" + DateTime.UtcNow.ToString("O"));
            InstallResultLoggerIfActive();
            runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                groupNames = groups
            }));
            Debug.Log(groups == null ? "[MukJump] 전체 EditMode 테스트를 시작합니다." :
                "[MukJump] 선택 범위 테스트를 시작합니다: " + string.Join("|", groups));
        }

        static void InstallResultLoggerIfActive()
        {
            if (!File.Exists(ActiveRunPath) || resultLogger != null)
                return;

            // EnterPlayMode 기반 EditMode 테스트는 도메인을 다시 로드한다.
            // plain managed callback은 이때 사라지므로 실행 표식을 기준으로
            // 매 도메인에서 새 ScriptableObject callback을 다시 등록한다.
            resultLogger = ScriptableObject.CreateInstance<ResultLogger>();
            resultLogger.hideFlags = HideFlags.HideAndDontSave;
            TestRunnerApi.RegisterTestCallback(resultLogger);
        }

        sealed class ResultLogger : ScriptableObject, ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result,
                    Path.GetFullPath("Temp/MukJumpEditModeResults.xml"));
                int total = result.PassCount + result.FailCount +
                            result.SkipCount + result.InconclusiveCount;
                Debug.Log(
                    $"[MukJump] EditMode 테스트 완료: " +
                    $"{result.PassCount}/{total} 통과, " +
                    $"실패 {result.FailCount}, 건너뜀 {result.SkipCount}");
                File.WriteAllText(
                    ResultPath,
                    $"pass={result.PassCount}\n" +
                    $"fail={result.FailCount}\n" +
                    $"skip={result.SkipCount}\n" +
                    $"inconclusive={result.InconclusiveCount}\n" +
                    $"total={total}\n" +
                    (File.Exists(ActiveRunPath) ? File.ReadAllLines(ActiveRunPath)[0] : "scope=unknown") + "\n");
                DeleteIfExists(CurrentTestPath);
                DeleteIfExists(ActiveRunPath);
                TestRunnerApi.UnregisterTestCallback(this);
                runner = null;
                resultLogger = null;
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                        DestroyImmediate(this);
                };
            }

            public void TestStarted(ITestAdaptor test)
            {
                if (test != null)
                    File.WriteAllText(CurrentTestPath, test.FullName ?? test.Name);
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result == null || result.FailCount <= 0)
                    return;
                File.AppendAllText(
                    FailurePath,
                    $"{result.FullName}\n{result.Message}\n{result.StackTrace}\n\n");
            }
        }

        static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
