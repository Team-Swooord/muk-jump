using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 하이어라키에서 직접 편집할 수 있는 로비 Canvas의 표시와 붓 안내 연출을 담당한다.
    public class LobbyView : MonoBehaviour
    {
        [SerializeField] RectTransform brushGuide;
        [SerializeField] CanvasGroup brushCanvasGroup;
        [SerializeField] RectTransform canvasRect;
        [SerializeField] Text bestText;
        [SerializeField] Text rankingText;
        [SerializeField] Button rankingButton;
        [SerializeField] GameObject rankingPopup;
        [SerializeField] Button rankingPopupCloseButton;
        [SerializeField] Button rankingPopupBackdropButton;
        [SerializeField] Text rankingPopupBestText;

        Transform player;
        Camera cam;
        InkPopupAnimator rankingPopupAnimator;

        void OnEnable()
        {
            rankingButton?.onClick.AddListener(OpenRankingPopup);
            rankingPopupCloseButton?.onClick.AddListener(CloseRankingPopup);
            rankingPopupBackdropButton?.onClick.AddListener(CloseRankingPopup);
        }

        void OnDisable()
        {
            rankingButton?.onClick.RemoveListener(OpenRankingPopup);
            rankingPopupCloseButton?.onClick.RemoveListener(CloseRankingPopup);
            rankingPopupBackdropButton?.onClick.RemoveListener(CloseRankingPopup);
        }

        void Start()
        {
            var controller = FindFirstObjectByType<Player.PlayerController>();
            if (controller != null) player = controller.transform;
            cam = Camera.main;
            if (rankingPopup != null)
            {
                rankingPopupAnimator = rankingPopup.GetComponent<InkPopupAnimator>();
                if (rankingPopupAnimator == null)
                    rankingPopupAnimator = rankingPopup.AddComponent<InkPopupAnimator>();
            }
        }

        void Update()
        {
            bool show = GameManager.Instance != null && GameManager.Instance.State == GameState.Lobby;
            if (gameObject.activeSelf != show)
            {
                gameObject.SetActive(show);
                return;
            }
            if (!show || brushGuide == null || canvasRect == null || player == null || cam == null) return;

            int best = ScoreManager.Instance != null ? ScoreManager.Instance.Best : 0;
            if (bestText != null) bestText.text = $"최고 {best}";
            if (rankingText != null) rankingText.text = "랭킹";
            if (rankingPopupBestText != null)
                rankingPopupBestText.text = best > 0 ? $"1위   최고 {best}m" : "아직 기록이 없습니다";

            Vector3 world = player.position + Vector3.down * 0.85f;
            Vector2 screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);

            float travel = canvasRect.rect.width * 0.22f;
            float offset = Mathf.PingPong(Time.unscaledTime * canvasRect.rect.width * 0.12f,
                travel * 2f) - travel;
            brushGuide.anchoredPosition = local + new Vector2(offset, 55f);

            if (brushCanvasGroup != null)
                brushCanvasGroup.alpha = 0.46f + 0.12f * Mathf.Sin(Time.unscaledTime * 3.2f);
        }

        void OpenRankingPopup()
        {
            if (rankingPopupAnimator != null) rankingPopupAnimator.Show();
            else if (rankingPopup != null) rankingPopup.SetActive(true);
        }

        void CloseRankingPopup()
        {
            if (rankingPopupAnimator != null) rankingPopupAnimator.Hide();
            else if (rankingPopup != null) rankingPopup.SetActive(false);
        }
    }
}
