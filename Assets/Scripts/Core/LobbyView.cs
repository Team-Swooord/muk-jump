using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 씬 빌더가 구성한 로비 Canvas의 표시와 시작·성장·도감 진입을 담당한다.
    [ExecuteAlways]
    public class LobbyView : MonoBehaviour
    {
        [SerializeField] Text bestText;
        [SerializeField] Button startButton;
        [SerializeField] Button growthButton;
        [SerializeField] Button codexButton;

        LobbyCollectionView collectionView;
        bool listenersBound;

        public Button StartButton => startButton;
        public Button GrowthButton => growthButton;
        public Button CodexButton => codexButton;

        void OnEnable()
        {
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

            bool show = GameManager.Instance != null && GameManager.Instance.State == GameState.Lobby;
            if (gameObject.activeSelf != show)
            {
                gameObject.SetActive(show);
                return;
            }
            if (!show) return;
            RefreshBest();
        }

        void BindListeners()
        {
            if (listenersBound) return;
            startButton?.onClick.AddListener(HandleStartPressed);
            growthButton?.onClick.AddListener(HandleGrowthPressed);
            codexButton?.onClick.AddListener(HandleCodexPressed);
            listenersBound = true;
        }

        void UnbindListeners()
        {
            if (!listenersBound) return;
            startButton?.onClick.RemoveListener(HandleStartPressed);
            growthButton?.onClick.RemoveListener(HandleGrowthPressed);
            codexButton?.onClick.RemoveListener(HandleCodexPressed);
            listenersBound = false;
        }

        void HandleStartPressed()
        {
            collectionView?.Close();
            GameManager.Instance?.StartGameFromMenu();
        }

        void HandleGrowthPressed()
        {
            ResolveCollectionView()?.OpenGrowth();
        }

        void HandleCodexPressed()
        {
            ResolveCollectionView()?.OpenCodex();
        }

        LobbyCollectionView ResolveCollectionView()
        {
            if (collectionView == null)
                collectionView = FindFirstObjectByType<LobbyCollectionView>();
            return collectionView;
        }

        void RefreshBest()
        {
            int best = ScoreManager.Instance != null ? ScoreManager.Instance.Best : 0;
            if (bestText != null)
                bestText.text = $"최고 {best}";
        }

        void ApplyUiFont()
        {
            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = InkPalette.UiFont;
                texts[i].resizeTextForBestFit = false;
                texts[i].alignByGeometry = true;
            }
        }
    }
}
