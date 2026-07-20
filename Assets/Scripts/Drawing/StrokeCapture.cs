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
        [Tooltip("먹 잔량: 한 획의 최대 길이. 넘치면 자동으로 획이 끝난다")]
        [SerializeField] float maxStrokeLength = 6f;
        [Tooltip("이보다 짧은 획은 발판으로 만들지 않는다")]
        [SerializeField] float minStrokeLength = 0.6f;
        [SerializeField] float previewWidth = 0.12f;
        [Tooltip("캐릭터에서 이 거리 안에 획이 들어오면 발판을 만들지 않는다 (물리 밀어내기 악용 방지)")]
        [SerializeField] float playerClearance = 0.75f;

        readonly List<Vector2> points = new();
        Camera cam;
        Transform player;
        bool drawing;
        float strokeLength;
        LineRenderer preview;

        /// HUD 먹 게이지용: 남은 잉크 비율 (획을 긋는 동안 소모)
        public float InkRemaining01 => drawing ? 1f - Mathf.Clamp01(strokeLength / maxStrokeLength) : 1f;

        void Start()
        {
            cam = Camera.main;
            var pc = FindFirstObjectByType<Player.PlayerController>();
            if (pc != null) player = pc.transform;
        }

        void Update()
        {
            if (GameManager.Instance.State != GameState.Playing)
            {
                if (drawing) CancelStroke();
                return;
            }

            if (PointerInput.TryGetPressed(out var screenPos))
            {
                if (drawing)
                    ContinueStroke(screenPos);
                else
                    BeginStroke(screenPos);
            }
            else if (drawing)
            {
                EndStroke();
            }
        }

        Vector2 ToWorld(Vector2 screenPos)
        {
            return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        }

        void BeginStroke(Vector2 screenPos)
        {
            drawing = true;
            strokeLength = 0f;
            points.Clear();
            points.Add(ToWorld(screenPos));
            CreatePreview();
        }

        void ContinueStroke(Vector2 screenPos)
        {
            Vector2 world = ToWorld(screenPos);
            float step = Vector2.Distance(points[^1], world);
            if (step < minPointDistance) return;

            // 먹 잔량 소진 시 그 지점에서 획을 끊는다
            if (strokeLength + step > maxStrokeLength)
            {
                EndStroke();
                return;
            }

            strokeLength += step;
            points.Add(world);
            UpdatePreview();
        }

        void EndStroke()
        {
            drawing = false;
            DestroyPreview();

            if (points.Count < 2 || strokeLength < minStrokeLength) return;

            var smoothed = BezierSmoother.Smooth(points);
            if (smoothed.Count < 2) return;

            // 캐릭터와 겹치거나 너무 가까운 획은 무효 — 콜라이더 밀어내기로 캐릭터를
            // 튕겨 올리는 악용(반복 드로잉 탈출)을 막는다
            if (TooCloseToPlayer(smoothed)) return;

            PlatformCollider.Spawn(smoothed);
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
