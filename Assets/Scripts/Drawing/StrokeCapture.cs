using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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

        readonly List<Vector2> points = new();
        Camera cam;
        bool drawing;
        float strokeLength;
        LineRenderer preview;

        void Start()
        {
            cam = Camera.main;
        }

        void Update()
        {
            if (GameManager.Instance.State != GameState.Playing)
            {
                if (drawing) CancelStroke();
                return;
            }

            var pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame)
                BeginStroke(pointer.position.ReadValue());
            else if (drawing && pointer.press.isPressed)
                ContinueStroke(pointer.position.ReadValue());
            else if (drawing && pointer.press.wasReleasedThisFrame)
                EndStroke();
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
            if (smoothed.Count >= 2)
                PlatformCollider.Spawn(smoothed);
        }

        void CancelStroke()
        {
            drawing = false;
            DestroyPreview();
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
