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
        CanvasGroup canvasGroup;
        bool listenersBound;
        bool lastVisible = true;
        int lastDisplayedBest = int.MinValue;

        public Button StartButton => startButton;
        public Button GrowthButton => growthButton;
        public Button CodexButton => codexButton;
        public Button OptionsButton => optionsButton;
        public bool IsInteractive =>
            canvasGroup != null && canvasGroup.blocksRaycasts;

        void OnEnable()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            ApplyUiFont();
            if (Application.isPlaying)
                BindListeners();
        }

        void Start()
        {
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
            bool show = GameManager.Instance == null ||
                        GameManager.Instance.State == GameState.Lobby;
            SetVisible(show);
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
            collectionView?.Close();
            permanentGrowthView?.Close();
            optionsView?.Close();
            GameManager.Instance?.StartGameFromMenu();
        }

        void HandleGrowthPressed()
        {
            ResolveCollectionView()?.Close();
            ResolveOptionsView()?.Close();
            ResolvePermanentGrowthView()?.Open();
        }

        void HandleCodexPressed()
        {
            ResolvePermanentGrowthView()?.Close();
            ResolveOptionsView()?.Close();
            ResolveCollectionView()?.OpenCodex();
        }

        void HandleOptionsPressed()
        {
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

        void RefreshBest()
        {
            int best = ScoreManager.Instance != null ? ScoreManager.Instance.Best : 0;
            if (bestText == null || best == lastDisplayedBest) return;
            lastDisplayedBest = best;
            bestText.text = $"최고 {best}";
        }

        void SetVisible(bool visible)
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            if (canvasGroup == null || lastVisible == visible &&
                canvasGroup.blocksRaycasts == visible)
                return;
            lastVisible = visible;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
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
