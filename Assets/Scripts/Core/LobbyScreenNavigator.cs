using System.Collections;
using UnityEngine;

namespace MukJump.Core
{
    /// 로비와 전용 성장·도감 화면 사이의 표시·입력·먹붓 전환을 한 곳에서 소유한다.
    /// 별도 Unity 씬을 로드하지 않아 음악과 런타임 싱글톤 상태를 그대로 보존한다.
    [DisallowMultipleComponent]
    public sealed class LobbyScreenNavigator : MonoBehaviour
    {
        public enum LobbySection
        {
            Lobby,
            PermanentGrowth,
            Codex,
        }

        public static LobbyScreenNavigator Instance { get; private set; }

        GameManager manager;
        BrushTransitionView transitionView;
        LobbyView lobbyView;
        PermanentGrowthView growthView;
        LobbyCollectionView codexView;
        LobbyOptionsView optionsView;
        Coroutine revealWaitRoutine;
        LobbySection previousSection;
        int transitionVersion;

        public LobbySection CurrentSection { get; private set; } =
            LobbySection.Lobby;
        public LobbySection PendingSection { get; private set; } =
            LobbySection.Lobby;
        public bool IsTransitioning { get; private set; }
        public bool CanStartGame =>
            !IsTransitioning && CurrentSection == LobbySection.Lobby;

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

        void Start()
        {
            ResolveDependencies();
            ResetToLobbyImmediate();
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

        public bool OpenCodex()
        {
            return RequestSection(LobbySection.Codex);
        }

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
                transitionView = FindFirstObjectByType<BrushTransitionView>();
            if (lobbyView == null)
                lobbyView = FindFirstObjectByType<LobbyView>();
            if (growthView == null)
                growthView = GetComponent<PermanentGrowthView>();
            if (growthView == null)
                growthView = FindFirstObjectByType<PermanentGrowthView>();
            if (codexView == null)
                codexView = GetComponent<LobbyCollectionView>();
            if (codexView == null)
                codexView = FindFirstObjectByType<LobbyCollectionView>();
            if (optionsView == null)
                optionsView = GetComponent<LobbyOptionsView>();
            if (optionsView == null)
                optionsView = FindFirstObjectByType<LobbyOptionsView>();
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
            bool showLobby = section == LobbySection.Lobby;
            bool showGrowth = section == LobbySection.PermanentGrowth;
            bool showCodex = section == LobbySection.Codex;

            lobbyView?.SetNavigationPresentation(
                showLobby,
                showLobby && interactive);
            growthView?.SetNavigationPresentation(
                showGrowth,
                showGrowth && interactive);
            codexView?.SetNavigationPresentation(
                showCodex,
                showCodex && interactive);
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
