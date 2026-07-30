using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 씬 빌더가 구성한 로비 Canvas의 표시와 시작·성장·도감·옵션 진입을 담당한다.
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasGroup))]
    public class LobbyView : MonoBehaviour
    {
        [SerializeField] Text bestText;
        [SerializeField] Button startButton;
        [SerializeField] Button growthButton;
        [SerializeField] Button codexButton;
        [SerializeField] Button optionsButton;

        LobbyCollectionView collectionView;
        PermanentGrowthView permanentGrowthView;
        LobbyOptionsView optionsView;
        LobbyScreenNavigator screenNavigator;
        CanvasGroup canvasGroup;
        bool listenersBound;
        bool lastVisible = true;
        bool lastInteractive = true;
        bool navigationVisible = true;
        bool navigationInteractive = true;
        int lastDisplayedBest = int.MinValue;

        public Button StartButton => startButton;
        public Button GrowthButton => growthButton;
        public Button CodexButton => codexButton;
        public Button OptionsButton => optionsButton;
        public bool IsInteractive =>
            canvasGroup != null && canvasGroup.blocksRaycasts;
        public bool IsVisible =>
            canvasGroup != null && canvasGroup.alpha > 0.001f;

        void OnEnable()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            ApplyUiFont();
            if (Application.isPlaying)
            {
                EnsureMenuLayout();
                BindListeners();
            }
        }

        void Start()
        {
            EnsureMenuLayout();
            BindListeners();
            RefreshBest();
        }

        void OnDisable()
        {
            UnbindListeners();
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            // 자기 GameObject를 비활성화하면 Update가 멈춰 로비로 돌아와도 다시 켤 수 없다.
            // CanvasGroup으로만 숨겨 입력 차단과 재활성화를 같은 활성 객체에서 처리한다.
            bool lobbyState = GameManager.Instance == null ||
                              GameManager.Instance.State == GameState.Lobby;
            bool show = lobbyState && navigationVisible;
            SetVisible(show, show && navigationInteractive);
            if (!show) return;
            RefreshBest();
        }

        void BindListeners()
        {
            if (listenersBound) return;
            startButton?.onClick.AddListener(HandleStartPressed);
            growthButton?.onClick.AddListener(HandleGrowthPressed);
            codexButton?.onClick.AddListener(HandleCodexPressed);
            optionsButton?.onClick.AddListener(HandleOptionsPressed);
            listenersBound = true;
        }

        void UnbindListeners()
        {
            if (!listenersBound) return;
            startButton?.onClick.RemoveListener(HandleStartPressed);
            growthButton?.onClick.RemoveListener(HandleGrowthPressed);
            codexButton?.onClick.RemoveListener(HandleCodexPressed);
            optionsButton?.onClick.RemoveListener(HandleOptionsPressed);
            listenersBound = false;
        }

        void HandleStartPressed()
        {
            LobbyScreenNavigator navigator = ResolveScreenNavigator();
            if (navigator != null && !navigator.CanStartGame)
                return;
            collectionView?.Close();
            permanentGrowthView?.Close();
            optionsView?.Close();
            GameManager.Instance?.StartGameFromMenu();
        }

        void HandleGrowthPressed()
        {
            ResolveOptionsView()?.Close();
            LobbyScreenNavigator navigator = ResolveScreenNavigator();
            if (navigator != null)
            {
                navigator.OpenGrowth();
                return;
            }
            ResolveCollectionView()?.Close();
            ResolvePermanentGrowthView()?.Open();
        }

        void HandleCodexPressed()
        {
            ResolveOptionsView()?.Close();
            LobbyScreenNavigator navigator = ResolveScreenNavigator();
            if (navigator != null)
            {
                navigator.OpenCodex();
                return;
            }
            ResolvePermanentGrowthView()?.Close();
            ResolveCollectionView()?.OpenCodex();
        }

        void HandleOptionsPressed()
        {
            LobbyScreenNavigator navigator = ResolveScreenNavigator();
            if (navigator != null && !navigator.CanStartGame)
                return;
            ResolvePermanentGrowthView()?.Close();
            ResolveCollectionView()?.Close();
            ResolveOptionsView()?.Open();
        }

        LobbyCollectionView ResolveCollectionView()
        {
            if (collectionView == null)
                collectionView = FindFirstObjectByType<LobbyCollectionView>();
            return collectionView;
        }

        PermanentGrowthView ResolvePermanentGrowthView()
        {
            if (permanentGrowthView == null)
                permanentGrowthView = FindFirstObjectByType<PermanentGrowthView>();
            return permanentGrowthView;
        }

        LobbyOptionsView ResolveOptionsView()
        {
            if (optionsView == null)
                optionsView = FindFirstObjectByType<LobbyOptionsView>();
            return optionsView;
        }

        LobbyScreenNavigator ResolveScreenNavigator()
        {
            if (screenNavigator == null)
            {
                screenNavigator = LobbyScreenNavigator.Instance != null
                    ? LobbyScreenNavigator.Instance
                    : FindFirstObjectByType<LobbyScreenNavigator>();
            }
            return screenNavigator;
        }

        public void SetNavigationPresentation(bool visible, bool interactive)
        {
            navigationVisible = visible;
            navigationInteractive = interactive;
            bool lobbyState = GameManager.Instance == null ||
                              GameManager.Instance.State == GameState.Lobby;
            bool show = lobbyState && navigationVisible;
            SetVisible(show, show && navigationInteractive);
        }

        /// 실행 중이던 구버전 Main 백업이 복원돼도 네 메뉴가 즉시 같은 규칙을 쓴다.
        /// 옵션 버튼 자체가 없는 구버전 씬은 도감 버튼의 수묵 그래픽을 한 번 복제한다.
        void EnsureMenuLayout()
        {
            if (optionsButton == null)
            {
                optionsButton = transform.Find("OptionsButton")
                    ?.GetComponent<Button>();
            }
            if (optionsButton == null)
            {
                Button source = codexButton != null
                    ? codexButton
                    : growthButton != null
                        ? growthButton
                        : startButton;
                if (source != null && source.transform.parent != null)
                {
                    GameObject clone = Instantiate(
                        source.gameObject,
                        source.transform.parent);
                    clone.name = "OptionsButton";
                    optionsButton = clone.GetComponent<Button>();
                    optionsButton?.onClick.RemoveAllListeners();
                    clone.transform.SetAsLastSibling();
                }
            }

            LobbyMenuLayout.ApplyRecord(bestText);
            LobbyMenuLayout.ApplyButton(
                startButton,
                "시작",
                LobbyMenuLayout.StartAnchor,
                primary: true);
            LobbyMenuLayout.ApplyButton(
                growthButton,
                "성장",
                LobbyMenuLayout.GrowthAnchor,
                primary: false);
            LobbyMenuLayout.ApplyButton(
                codexButton,
                "도감",
                LobbyMenuLayout.CodexAnchor,
                primary: false);
            LobbyMenuLayout.ApplyButton(
                optionsButton,
                "옵션",
                LobbyMenuLayout.OptionsAnchor,
                primary: false);
        }

#if UNITY_EDITOR
        public void ApplyMenuLayoutForTests()
        {
            EnsureMenuLayout();
        }
#endif

        void RefreshBest()
        {
            int best = ScoreManager.Instance != null ? ScoreManager.Instance.Best : 0;
            if (bestText == null || best == lastDisplayedBest) return;
            lastDisplayedBest = best;
            bestText.text = $"최고 {best}";
        }

        void SetVisible(bool visible, bool interactive)
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            if (canvasGroup == null ||
                lastVisible == visible &&
                lastInteractive == interactive &&
                canvasGroup.blocksRaycasts == interactive)
                return;
            lastVisible = visible;
            lastInteractive = interactive;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = interactive;
            canvasGroup.blocksRaycasts = interactive;
        }

        void ApplyUiFont()
        {
            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = InkPalette.UiFont;
                texts[i].fontStyle = FontStyle.Bold;
                texts[i].resizeTextForBestFit = false;
                texts[i].alignByGeometry = true;
            }
        }
    }
}
