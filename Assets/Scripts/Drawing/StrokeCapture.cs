using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;

namespace MukJump.Drawing
{
    /// <summary>
    /// 손끝(터치/마우스) 입력을 스트로크로 캡처하고, 손을 뗀 시점에
    /// 스무딩 + 발판 생성 파이프라인으로 넘긴다.
    /// 지정한 드로잉 존(한지 여백 영역) 안에서만 입력을 받는다.
    /// </summary>
    public class StrokeCapture : MonoBehaviour
    {
        [Header("Draw Zone (기획서 기준: 배경 하단 ~620px 한지 여백)")]
        [SerializeField] private BoxCollider2D drawZone;

        [Header("Platform Output")]
        [SerializeField] private Transform platformParent;
        [SerializeField] private int platformLayer;
        [SerializeField] private Material fallbackInkMaterial;

        [Header("Capture Tuning")]
        [Tooltip("한 프레임에 이동 거리가 이 값보다 작으면 포인트를 추가하지 않음 (points 폭주 방지)")]
        [SerializeField] private float minCaptureDistance = 0.03f;
        [SerializeField] private int segmentsPerSpan = 8;

        private readonly List<Vector2> rawPoints = new();
        private bool isDrawing;
        private Camera cam;

        private void Awake()
        {
            cam = Camera.main;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing)
            {
                if (isDrawing) CancelStroke();
                return;
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }

        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0)) TryBeginStroke(Input.mousePosition);
            else if (Input.GetMouseButton(0) && isDrawing) ContinueStroke(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0) && isDrawing) EndStroke();
        }

        private void HandleTouchInput()
        {
            if (Input.touchCount == 0) return;
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    TryBeginStroke(touch.position);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (isDrawing) ContinueStroke(touch.position);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (isDrawing) EndStroke();
                    break;
            }
        }

        private void TryBeginStroke(Vector2 screenPos)
        {
            Vector2 worldPos = cam.ScreenToWorldPoint(screenPos);
            if (drawZone != null && !drawZone.OverlapPoint(worldPos)) return;

            rawPoints.Clear();
            rawPoints.Add(worldPos);
            isDrawing = true;
        }

        private void ContinueStroke(Vector2 screenPos)
        {
            Vector2 worldPos = cam.ScreenToWorldPoint(screenPos);

            if (rawPoints.Count > 0 && Vector2.Distance(rawPoints[^1], worldPos) < minCaptureDistance)
                return;

            rawPoints.Add(worldPos);
        }

        private void EndStroke()
        {
            isDrawing = false;

            if (rawPoints.Count >= 2)
            {
                Vector2[] smoothed = BezierSmoother.Smooth(rawPoints, segmentsPerSpan);
                DrawnPlatform.Create(smoothed, platformParent, platformLayer, fallbackInkMaterial);
            }

            rawPoints.Clear();
        }

        private void CancelStroke()
        {
            isDrawing = false;
            rawPoints.Clear();
        }
    }
}
