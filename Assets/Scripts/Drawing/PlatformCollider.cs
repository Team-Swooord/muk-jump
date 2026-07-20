using System.Collections;
using UnityEngine;
using MukJump.AI;

namespace MukJump.Drawing
{
    /// <summary>
    /// 스무딩된 스트로크 한 개를 실제 플레이 가능한 발판(콜라이더 + 비주얼)으로 만든다.
    /// 생성 즉시 폴백(먹 텍스처) 셰이더로 그려지고, AI 변환이 성공하면 텍스처가 교체된다.
    /// </summary>
    [RequireComponent(typeof(EdgeCollider2D))]
    public class DrawnPlatform : MonoBehaviour
    {
        [Tooltip("착지 후 이 시간(초)이 지나면 자동으로 사라진다. 0 이하면 사라지지 않음")]
        [SerializeField] private float lifetimeAfterLanding = 4f;

        private EdgeCollider2D edgeCollider;
        private LineRenderer lineRenderer;
        private bool landedOnce;

        public float AngleDeg { get; private set; }

        /// <summary>
        /// 스무딩된 월드 좌표 포인트로 새 발판을 생성한다.
        /// </summary>
        public static DrawnPlatform Create(Vector2[] smoothedWorldPoints, Transform parent, int platformLayer, Material fallbackInkMaterial)
        {
            if (smoothedWorldPoints == null || smoothedWorldPoints.Length < 2) return null;

            var go = new GameObject("DrawnPlatform");
            go.transform.SetParent(parent, worldPositionStays: true);
            go.transform.position = smoothedWorldPoints[0];
            go.layer = platformLayer;

            var platform = go.AddComponent<DrawnPlatform>();
            platform.Initialize(smoothedWorldPoints, fallbackInkMaterial);
            return platform;
        }

        private void Initialize(Vector2[] worldPoints, Material fallbackInkMaterial)
        {
            edgeCollider = GetComponent<EdgeCollider2D>();

            // EdgeCollider2D는 로컬 좌표를 요구하므로 원점(worldPoints[0]) 기준으로 변환
            var localPoints = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
            {
                localPoints[i] = worldPoints[i] - worldPoints[0];
            }
            edgeCollider.points = localPoints;

            AngleDeg = BezierSmoother.EstimateAngleDeg(worldPoints);

            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.positionCount = localPoints.Length;
            lineRenderer.SetPositions(System.Array.ConvertAll(localPoints, p => (Vector3)p));
            lineRenderer.widthMultiplier = 0.18f;
            lineRenderer.numCapVertices = 4;
            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.material = fallbackInkMaterial; // 폴백 먹 텍스처 셰이더로 즉시 표시

            var service = SketchToInkService.Instance;
            if (service != null)
            {
                StartCoroutine(service.RequestInkConversion(worldPoints, OnAiTextureReady));
            }
        }

        private void OnAiTextureReady(Texture2D aiTexture)
        {
            if (aiTexture == null || lineRenderer == null) return; // 실패 시 폴백 머티리얼 그대로 유지
            lineRenderer.material.mainTexture = aiTexture;
        }

        /// <summary>PlayerController가 이 발판에 착지했을 때 호출.</summary>
        public void NotifyLanded()
        {
            if (landedOnce) return;
            landedOnce = true;

            if (lifetimeAfterLanding > 0f)
            {
                StartCoroutine(ExpireAfter(lifetimeAfterLanding));
            }
        }

        private IEnumerator ExpireAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            // TODO: 페이드아웃 등 연출 추가 여지
            Destroy(gameObject);
        }
    }
}
