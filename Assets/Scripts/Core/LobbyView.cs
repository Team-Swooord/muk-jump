using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 하이어라키에서 직접 편집할 수 있는 로비 Canvas의 표시와 붓 안내 연출을 담당한다.
    [ExecuteAlways]
    public class LobbyView : MonoBehaviour
    {
        static readonly string[] DummyRankingNames = { "먹선비", "구름붓", "나", "묵방울", "한지새" };
        static readonly int[] DummyScoreOffsets = { 72, 31, 0, -18, -43 };

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
        Text[] embeddedRankingRows;
        Image currentRankingHighlight;
        int displayedRankingBest = -1;

        void OnEnable()
        {
            if (!Application.isPlaying)
            {
                ConfigureEmbeddedRankingBoard();
                return;
            }
            rankingButton?.onClick.AddListener(OpenRankingPopup);
            rankingPopupCloseButton?.onClick.AddListener(CloseRankingPopup);
            rankingPopupBackdropButton?.onClick.AddListener(CloseRankingPopup);
        }

        void OnDisable()
        {
            if (!Application.isPlaying) return;
            rankingButton?.onClick.RemoveListener(OpenRankingPopup);
            rankingPopupCloseButton?.onClick.RemoveListener(CloseRankingPopup);
            rankingPopupBackdropButton?.onClick.RemoveListener(CloseRankingPopup);
        }

        void Start()
        {
            var controller = FindFirstObjectByType<Player.PlayerController>();
            if (controller != null) player = controller.transform;
            cam = Camera.main;
            ConfigureEmbeddedRankingBoard();
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                if (embeddedRankingRows == null) ConfigureEmbeddedRankingBoard();
                UpdateEmbeddedRankingBoard(ScoreManager.Instance != null ? ScoreManager.Instance.Best : 0);
                return;
            }

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
            UpdateEmbeddedRankingBoard(best);

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
            // 랭킹은 로비에 항상 표시하므로 별도 팝업을 열지 않는다.
        }

        void CloseRankingPopup()
        {
            // 임베디드 랭킹 보드는 로비가 보이는 동안 닫히지 않는다.
        }

        void ConfigureEmbeddedRankingBoard()
        {
            if (rankingButton != null) rankingButton.gameObject.SetActive(false);
            if (rankingPopup == null) return;

            rankingPopup.SetActive(true);
            var overlayRect = rankingPopup.GetComponent<RectTransform>();
            if (overlayRect != null)
            {
                overlayRect.anchorMin = overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
                overlayRect.anchoredPosition = new Vector2(12f, -310f);
                overlayRect.sizeDelta = new Vector2(790f, 540f);
            }
            var overlayImage = rankingPopup.GetComponent<Image>();
            if (overlayImage != null) overlayImage.color = Color.clear;
            if (rankingPopupBackdropButton != null) rankingPopupBackdropButton.enabled = false;
            if (rankingPopupCloseButton != null) rankingPopupCloseButton.gameObject.SetActive(false);
            if (rankingPopupBestText != null) rankingPopupBestText.gameObject.SetActive(false);

            var panel = rankingPopup.transform.Find("PaperPanel") as RectTransform;
            if (panel == null) return;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(760f, 510f);
            panel.localScale = Vector3.one;
            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
                panelImage.color = new Color(InkPalette.Paper.r, InkPalette.Paper.g, InkPalette.Paper.b, 0.96f);

            var title = panel.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = "랭킹";
                title.rectTransform.anchoredPosition = new Vector2(0f, 205f);
                title.fontSize = 48;
                title.color = InkPalette.Ink;
            }
            var notice = panel.Find("Notice");
            if (notice != null) notice.gameObject.SetActive(false);

            var rowsRoot = panel.Find("EmbeddedRows") as RectTransform;
            if (rowsRoot == null)
            {
                var rowsObject = new GameObject("EmbeddedRows", typeof(RectTransform));
                rowsRoot = rowsObject.GetComponent<RectTransform>();
                rowsRoot.SetParent(panel, false);
                rowsRoot.anchorMin = rowsRoot.anchorMax = new Vector2(0.5f, 0.5f);
                rowsRoot.anchoredPosition = new Vector2(0f, -22f);
                rowsRoot.sizeDelta = new Vector2(670f, 350f);
            }

            embeddedRankingRows = new Text[5];
            for (int i = 0; i < embeddedRankingRows.Length; i++)
            {
                string rowName = $"RankRow{i + 1}";
                var row = rowsRoot.Find(rowName)?.GetComponent<Text>();
                if (row == null)
                {
                    var rowObject = new GameObject(rowName, typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(Text));
                    row = rowObject.GetComponent<Text>();
                    row.rectTransform.SetParent(rowsRoot, false);
                    row.font = title != null ? title.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    row.fontSize = 34;
                    row.fontStyle = i == 2 ? FontStyle.Bold : FontStyle.Normal;
                    row.alignment = TextAnchor.MiddleCenter;
                    row.raycastTarget = false;
                }
                row.rectTransform.anchorMin = row.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                row.rectTransform.anchoredPosition = new Vector2(0f, -35f - i * 66f);
                row.rectTransform.sizeDelta = new Vector2(640f, 58f);
                row.color = i == 2 ? InkPalette.Red : InkPalette.TextDark;
                embeddedRankingRows[i] = row;
            }

            var highlight = rowsRoot.Find("CurrentRankingHighlight")?.GetComponent<Image>();
            if (highlight == null)
            {
                var highlightObject = new GameObject("CurrentRankingHighlight", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                highlight = highlightObject.GetComponent<Image>();
                highlight.rectTransform.SetParent(rowsRoot, false);
                highlight.color = new Color(InkPalette.Gold.r, InkPalette.Gold.g, InkPalette.Gold.b, 0.22f);
                highlight.raycastTarget = false;
            }
            highlight.rectTransform.anchorMin = highlight.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            highlight.rectTransform.anchoredPosition = new Vector2(0f, -35f - 2f * 66f);
            highlight.rectTransform.sizeDelta = new Vector2(660f, 60f);
            highlight.transform.SetAsFirstSibling();
            currentRankingHighlight = highlight;
        }

        void UpdateEmbeddedRankingBoard(int best)
        {
            if (embeddedRankingRows == null || embeddedRankingRows.Length != 5) return;
            if (displayedRankingBest != best)
            {
                displayedRankingBest = best;
                int currentRank = Mathf.Max(3, 48 - best / 10);
                for (int i = 0; i < embeddedRankingRows.Length; i++)
                {
                    int rank = currentRank + i - 2;
                    int score = Mathf.Max(0, best + DummyScoreOffsets[i]);
                    embeddedRankingRows[i].text = i == 2
                        ? $"▶ 현재 랭킹  {rank}위     {DummyRankingNames[i]}     {best}m"
                        : $"{rank}위     {DummyRankingNames[i]}     {score}m";
                }
            }
            if (currentRankingHighlight != null)
            {
                float pulse = 0.16f + 0.07f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.6f));
                currentRankingHighlight.color = new Color(InkPalette.Gold.r, InkPalette.Gold.g,
                    InkPalette.Gold.b, pulse);
            }
        }
    }
}
