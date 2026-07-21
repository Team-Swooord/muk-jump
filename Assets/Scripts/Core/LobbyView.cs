using UnityEngine;

namespace MukJump.Core
{
    /// 하이어라키에서 직접 편집할 수 있는 로비 Canvas의 표시와 붓 안내 연출을 담당한다.
    public class LobbyView : MonoBehaviour
    {
        [SerializeField] RectTransform brushGuide;
        [SerializeField] CanvasGroup brushCanvasGroup;
        [SerializeField] RectTransform canvasRect;

        Transform player;
        Camera cam;

        void Start()
        {
            var controller = FindFirstObjectByType<Player.PlayerController>();
            if (controller != null) player = controller.transform;
            cam = Camera.main;
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
    }
}
