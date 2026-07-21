using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;

namespace MukJump.Drawing
{
    /// 터치/마우스 스트로크를 월드 좌표 점열로 캡처한다.
    /// 손을 떼면 BezierSmoother로 다듬어 PlatformCollider 발판을 생성한다.
    public class StrokeCapture : MonoBehaviour
    {
        [Tooltip("이 간격(월드 단위) 이상 움직였을 때만 점 추가")]
        [SerializeField] float minPointDistance = 0.15f;
        [Tooltip("한 획의 최대 길이. 넘치면 그 지점에서 획을 끊고 이어 그린다")]
        [SerializeField] float maxStrokeLength = 6f;
        [Tooltip("이보다 짧은 획은 발판으로 만들지 않는다")]
        [SerializeField] float minStrokeLength = 0.6f;
        [SerializeField] float previewWidth = 0.4f;
        [Tooltip("캐릭터에서 이 거리 안에 획이 들어오면 발판을 만들지 않는다 (물리 밀어내기 악용 방지)")]
        [SerializeField] float playerClearance = 0.75f;

        [Header("먹(잉크) 자원 — 무한 드로잉 방지")]
        [Tooltip("먹 총량 (월드 단위 길이). 그은 만큼 소모된다")]
        [SerializeField] float inkCapacity = 12f;
        [Tooltip("초당 먹 회복량 (그리는 중에는 회복하지 않음)")]
        [SerializeField] float inkRegenPerSecond = 1.8f;
        [Tooltip("먹이 이보다 적으면 새 획을 시작할 수 없다")]
        [SerializeField] float minInkToStart = 0.8f;

        readonly List<Vector2> points = new();
        Camera cam;
        Transform player;
        bool drawing;
        float strokeLength;
        float ink;
        LineRenderer preview;
        float unlimitedInkUntil;

        /// HUD 먹 게이지용: 전체 먹 잔량 비율
        public bool HasUnlimitedInk => Time.time < unlimitedInkUntil;
        public float InkRemaining01 => HasUnlimitedInk ? 1f : Mathf.Clamp01(ink / inkCapacity);

        public void ActivateUnlimitedInk(float duration)
        {
            unlimitedInkUntil = Mathf.Max(unlimitedInkUntil, Time.time + duration);
        }

        void Start()
        {
            cam = Camera.main;
            if (cam == null)
                Debug.LogError("[MukJump] MainCamera를 찾을 수 없어 드로잉 좌표를 변환할 수 없습니다.", this);
            ink = inkCapacity;
            var pc = FindFirstObjectByType<Player.PlayerController>();
            if (pc != null) player = pc.transform;
        }

        void Update()
        {
            if (cam == null) return;

            if (GameManager.Instance == null)
            {
                if (drawing) CancelStroke();
                return;
            }

            if (GameManager.Instance.State == GameState.Lobby)
            {
                UpdateLobbyStroke();
                return;
            }

            if (GameManager.Instance.State != GameState.Playing)
            {
                if (drawing) CancelStroke();
                return;
            }

            if (!drawing)
                ink = Mathf.Min(inkCapacity, ink + inkRegenPerSecond * Time.deltaTime);

            if (PointerInput.TryGetPressed(out var screenPos))
            {
                if (GameplayHudView.IsPointerOverItemTestControls(screenPos))
                {
                    if (drawing) CancelStroke();
                    return;
                }

                if (drawing)
                    ContinueStroke(screenPos);
                else if (HasUnlimitedInk || ink >= minInkToStart)
                    BeginStroke(screenPos);
            }
            else if (drawing)
            {
                EndStroke();
            }
        }

        void UpdateLobbyStroke()
        {
            if (PointerInput.TryGetPressed(out var screenPos))
            {
                if (drawing)
                    ContinueStroke(screenPos);
                else
                    BeginStroke(screenPos);
            }
            else if (drawing)
            {
                EndStroke(startGame: true);
            }
        }

        Vector2 ToWorld(Vector2 screenPos)
        {
            return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        }

        void BeginStroke(Vector2 screenPos) => BeginStrokeAtWorld(ToWorld(screenPos));

        void BeginStrokeAtWorld(Vector2 worldPos)
        {
            drawing = true;
            strokeLength = 0f;
            points.Clear();
            points.Add(worldPos);
            CreatePreview();
        }

        void ContinueStroke(Vector2 screenPos)
        {
            bool lobbyStroke = GameManager.Instance != null &&
                               GameManager.Instance.State == GameState.Lobby;
            Vector2 world = ToWorld(screenPos);
            float step = Vector2.Distance(points[^1], world);
            if (step < minPointDistance) return;

            // 먹이 다 떨어지면 그 지점에서 획이 끝난다 — 회복될 때까지 더 그릴 수 없다
            if (!lobbyStroke && !HasUnlimitedInk && ink <= 0f)
            {
                EndStroke();
                return;
            }

            // 한 획의 최대 길이 초과 시 그 지점에서 끊고, 손을 떼지 않았다면 바로 그
            // 지점에서 새 획을 이어 시작한다 (끊지 않으면 다음 프레임의 이동분만큼 틈이
            // 생겨 일직선으로 길게 그은 발판 중간이 붕 뜨는 문제가 있었음)
            if (strokeLength + step > maxStrokeLength)
            {
                // 로비의 시작선은 한 획만 인정하므로 최대 길이에 도달하면 손을 뗄 때까지 유지한다.
                if (lobbyStroke) return;

                Vector2 seam = points[^1];
                EndStroke();
                BeginStrokeAtWorld(seam);
                return;
            }

            strokeLength += step;
            if (!lobbyStroke && !HasUnlimitedInk)
                ink = Mathf.Max(0f, ink - step);
            points.Add(world);
            UpdatePreview();
        }

        void EndStroke(bool startGame = false)
        {
            drawing = false;
            DestroyPreview();

            if (points.Count < 2 || strokeLength < minStrokeLength) return;

            var smoothed = BezierSmoother.Smooth(points);
            if (smoothed.Count < 2) return;

            // 캐릭터와 겹치거나 너무 가까운 획은 무효 — 콜라이더 밀어내기로 캐릭터를
            // 튕겨 올리는 악용(반복 드로잉 탈출)을 막는다
            if (!startGame && TooCloseToPlayer(smoothed)) return;

            PlatformCollider.Spawn(smoothed);
            if (startGame)
                GameManager.Instance?.StartGameFromStroke();
        }

        void CancelStroke()
        {
            drawing = false;
            DestroyPreview();
        }

        bool TooCloseToPlayer(List<Vector2> strokePoints)
        {
            if (player == null) return false;
            Vector2 center = player.position;
            foreach (var p in strokePoints)
            {
                if (Vector2.Distance(p, center) < playerClearance)
                    return true;
            }
            return false;
        }

        // ---- 그리는 동안 옅은 먹선 미리보기 ----

        void CreatePreview()
        {
            var go = new GameObject("StrokePreview");
            preview = go.AddComponent<LineRenderer>();
            preview.useWorldSpace = true;
            preview.startWidth = preview.endWidth = previewWidth;
            preview.material = AI.FallbackInkStyle.SharedInkMaterial;
            var faint = InkPalette.Ink;
            faint.a = 0.35f;
            preview.startColor = preview.endColor = faint;
            preview.numCapVertices = 4;
            preview.sortingOrder = 10;
            UpdatePreview();
        }

        void UpdatePreview()
        {
            if (preview == null) return;

            if (points.Count == 1)
            {
                // 손이 닿은 즉시 붓점이 찍히는 느낌: 점 하나로는 선이 그려지지 않으므로
                // 같은 위치를 두 번 찍어 둥근 캡만 있는 점으로 보이게 한다
                preview.positionCount = 2;
                preview.SetPosition(0, points[0]);
                preview.SetPosition(1, points[0]);
                return;
            }

            preview.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                preview.SetPosition(i, points[i]);
        }

        void DestroyPreview()
        {
            if (preview != null) Destroy(preview.gameObject);
            preview = null;
        }
    }
}
