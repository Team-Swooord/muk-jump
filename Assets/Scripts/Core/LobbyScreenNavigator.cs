using System.Collections;
using UnityEngine;

namespace MukJump.Core
{
    /// 로비와 전용 영구 성장 화면 사이의 표시·입력·먹붓 전환을 한 곳에서 소유한다.
    /// 별도 Unity 씬을 로드하지 않아 음악과 런타임 싱글톤 상태를 그대로 보존한다.
    [DisallowMultipleComponent]
    public sealed class LobbyScreenNavigator : MonoBehaviour
    {
        public enum LobbySection
        {
            Lobby,
            PermanentGrowth,
            Wardrobe,
        }

        public static LobbyScreenNavigator Instance { get; private set; }

        GameManager manager;
        BrushTransitionView transitionView;
        LobbyView lobbyView;
        PermanentGrowthView growthView;
        LobbyOptionsView optionsView;
        InkWardrobeView wardrobeView;
        Coroutine revealWaitRoutine;
        LobbySection previousSection;
        int transitionVersion;

        public LobbySection CurrentSection { get; private set; } =
            LobbySection.Lobby;
        public LobbySection PendingSection { get; private set; } =
            LobbySection.Lobby;
        public bool IsTransitioning { get; private set; }
        public bool CanShowTopBanner => !IsTransitioning &&
            (CurrentSection == LobbySection.Lobby ||
             CurrentSection == LobbySection.Wardrobe &&
             (wardrobeView == null || !wardrobeView.IsSkillDetailsOpen) ||
             CurrentSection == LobbySection.PermanentGrowth &&
             (growthView == null || !growthView.HasBlockingOverlay));
        public bool CanStartGame =>
            !InkPremiumShopView.IsOpen && !SkinDebugSession.MenuOpen &&
            !IsTransitioning &&
            CurrentSection == LobbySection.Lobby &&
            !PermanentGrowthProfile.RequiresRecovery &&
            (MukJumpAccountRuntime.Instance == null ||
             !MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync);

        void Awake()
        {
            ResolveDependencies();
        }

        void OnEnable()
        {
            Instance = this;
            ResolveDependencies();
            BindManager();
            ResetToLobbyImmediate();
        }

        IEnumerator Start()
        {
            ResolveDependencies();
            ResetToLobbyImmediate();
            object account = SkinUnlockSceneController.ConsumeWardrobeReturn();
            if (account == null) yield break;
            yield return null; // 같은 프레임의 로비 뷰 Start가 끝난 뒤 가림 아래에서 전용 화면을 준비한다.
            // 공개 씬에서 돌아올 때 로비가 잠깐 보였다가 다시 가려지지 않게, 첫 가림 아래 꾸미기를 준비한다.
            CurrentSection = PendingSection = LobbySection.Wardrobe;
            IsTransitioning = true;
            int version = ++transitionVersion;
            ApplySection(CurrentSection, interactive: false); ApplyMenuSelection(CurrentSection);
            while (IsCurrentTransition(version) && transitionView != null && transitionView.IsPlaying) yield return null;
            if (!IsCurrentTransition(version)) yield break;
            if (!ReferenceEquals(account, PermanentGrowthProfile.SkinAccountToken) || manager == null || manager.State != GameState.Lobby)
            { ResetToLobbyImmediate(); yield break; }
            FinishTransition(version);
        }

        void OnDisable()
        {
            transitionVersion++;
            IsTransitioning = false;
            if (revealWaitRoutine != null)
            {
                StopCoroutine(revealWaitRoutine);
                revealWaitRoutine = null;
            }
            UnbindManager();
            if (Instance == this)
                Instance = null;
        }

        public bool OpenGrowth()
        {
            return RequestSection(LobbySection.PermanentGrowth);
        }

        public bool OpenWardrobe() => RequestSection(LobbySection.Wardrobe);

        public bool ReturnToLobby()
        {
            return RequestSection(LobbySection.Lobby);
        }

        void ResolveDependencies()
        {
            if (manager == null)
                manager = GameManager.Instance != null
                    ? GameManager.Instance
                    : GetComponent<GameManager>();
            if (transitionView == null)
                transitionView = GetComponent<BrushTransitionView>();
            if (transitionView == null)
                transitionView = FindAnyObjectByType<BrushTransitionView>();
            if (lobbyView == null)
                lobbyView = FindAnyObjectByType<LobbyView>();
            if (growthView == null)
                growthView = GetComponent<PermanentGrowthView>();
            if (growthView == null)
                growthView = FindAnyObjectByType<PermanentGrowthView>();
            if (optionsView == null)
                optionsView = GetComponent<LobbyOptionsView>();
            if (optionsView == null)
                optionsView = FindAnyObjectByType<LobbyOptionsView>();
            if (wardrobeView == null)
                wardrobeView = GetComponent<InkWardrobeView>();
        }

        void BindManager()
        {
            GameManager next = GameManager.Instance != null
                ? GameManager.Instance
                : GetComponent<GameManager>();
            if (ReferenceEquals(manager, next))
            {
                if (manager != null)
                {
                    manager.StateChanged -= HandleGameStateChanged;
                    manager.StateChanged += HandleGameStateChanged;
                }
                return;
            }

            UnbindManager();
            manager = next;
            if (manager != null)
                manager.StateChanged += HandleGameStateChanged;
        }

        void UnbindManager()
        {
            if (manager != null)
                manager.StateChanged -= HandleGameStateChanged;
        }

        bool RequestSection(LobbySection destination)
        {
            ResolveDependencies();
            BindManager();
            if (destination != LobbySection.Lobby &&
                MukJumpAccountRuntime.Instance != null &&
                MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
            {
                optionsView?.OpenAccountForRequiredSync();
                return false;
            }
            if (destination == LobbySection.Wardrobe && wardrobeView == null)
                return false;
            if (manager == null ||
                manager.State != GameState.Lobby ||
                IsTransitioning ||
                manager.IsTransitioning ||
                transitionView != null && transitionView.IsPlaying ||
                destination == CurrentSection)
                return false;

            previousSection = CurrentSection;
            PendingSection = destination;
            IsTransitioning = true;
            int version = ++transitionVersion;
            optionsView?.Close();
            PointerInput.SuppressUntilRelease();
            ApplySection(CurrentSection, interactive: false);
            ApplyMenuSelection(destination);

            if (!Application.isPlaying)
                return true;

            if (transitionView == null)
            {
                HandleCovered(version);
                FinishTransition(version);
                return true;
            }

            if (!transitionView.TryPlay(
                    () => HandleCovered(version),
                    () => HandleFailure(version)))
            {
                HandleFailure(version);
                return false;
            }
            revealWaitRoutine = StartCoroutine(
                WaitForRevealCompletion(version));
            return true;
        }

        IEnumerator WaitForRevealCompletion(int version)
        {
            while (version == transitionVersion &&
                   transitionView != null &&
                   transitionView.IsPlaying)
                yield return null;

            revealWaitRoutine = null;
            FinishTransition(version);
        }

        void HandleCovered(int version)
        {
            if (!IsCurrentTransition(version)) return;
            if (PendingSection == LobbySection.Wardrobe && manager.TryOpenLobbySkinUnlock()) return;
            CurrentSection = PendingSection;
            ApplySection(CurrentSection, interactive: false);
        }

        void FinishTransition(int version)
        {
            if (!IsCurrentTransition(version)) return;
            IsTransitioning = false;
            PendingSection = CurrentSection;
            ApplySection(CurrentSection, interactive: true);
        }

        void HandleFailure(int version)
        {
            if (!IsCurrentTransition(version)) return;
            if (revealWaitRoutine != null)
            {
                StopCoroutine(revealWaitRoutine);
                revealWaitRoutine = null;
            }
            CurrentSection = previousSection;
            PendingSection = previousSection;
            IsTransitioning = false;
            ApplySection(CurrentSection, interactive: true);
        }

        bool IsCurrentTransition(int version)
        {
            return IsTransitioning && version == transitionVersion;
        }

        void HandleGameStateChanged(GameState previous, GameState current)
        {
            if (current != GameState.Lobby)
                ResetToLobbyImmediate();
        }

        void ResetToLobbyImmediate()
        {
            transitionVersion++;
            IsTransitioning = false;
            previousSection = LobbySection.Lobby;
            CurrentSection = LobbySection.Lobby;
            PendingSection = LobbySection.Lobby;
            if (revealWaitRoutine != null)
            {
                StopCoroutine(revealWaitRoutine);
                revealWaitRoutine = null;
            }
            ResolveDependencies();
            ApplySection(LobbySection.Lobby, interactive: true);
        }

        void ApplySection(LobbySection section, bool interactive)
        {
            if (interactive)
                MukJumpAnalytics.Screen(section switch
                {
                    LobbySection.PermanentGrowth => AnalyticsScreen.Growth,
                    LobbySection.Wardrobe => AnalyticsScreen.Wardrobe,
                    _ => AnalyticsScreen.Lobby,
                });
            ApplyMenuSelection(section);
            bool showLobby = section == LobbySection.Lobby;
            bool showGrowth = section == LobbySection.PermanentGrowth;

            lobbyView?.SetNavigationPresentation(
                showLobby,
                showLobby && interactive);
            growthView?.SetNavigationPresentation(
                showGrowth,
                showGrowth && interactive);
            wardrobeView?.SetNavigationPresentation(
                section == LobbySection.Wardrobe,
                section == LobbySection.Wardrobe && interactive);
        }

        void ApplyMenuSelection(LobbySection section)
        {
            if (lobbyView == null) return;
            lobbyView.SetActiveMenu(section switch
            {
                LobbySection.PermanentGrowth => LobbyMenuSelection.Growth,
                LobbySection.Wardrobe => LobbyMenuSelection.Wardrobe,
                _ => LobbyMenuSelection.Start,
            });
        }

#if UNITY_EDITOR
        public void BuildForTests()
        {
            ResolveDependencies();
            BindManager();
            ResetToLobbyImmediate();
        }

        public void CompleteCoverForTests()
        {
            HandleCovered(transitionVersion);
        }

        public void CompleteRevealForTests()
        {
            FinishTransition(transitionVersion);
        }

        public void FailTransitionForTests()
        {
            HandleFailure(transitionVersion);
        }
#endif
    }
}
