using System.Collections.Generic;
using UnityEngine;
using MukJump.AI;

namespace MukJump.Drawing
{
    /// 스트로크 점열 하나 = 발판 하나. LineRenderer(붓선 비주얼) + EdgeCollider2D(물리).
    /// 일정 시간이 지나면 먹이 마르듯 서서히 사라진다. 씬에 미리 배치하면(시작 지형) 영구 발판.
    [RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
    public class PlatformCollider : MonoBehaviour
    {
        const int MaxActivePlatforms = 4;
        static readonly List<PlatformCollider> active = new();

        [Tooltip("생성 후 유지 시간(초). 0 이하면 영구 발판")]
        [SerializeField] float lifetime = 9f;
        [SerializeField] float fadeDuration = 1.5f;

        public float Length { get; private set; }
        public LineRenderer Line { get; private set; }

        EdgeCollider2D edge;
        float age;

        /// 스무딩 완료된 월드 좌표 점열로 발판을 생성한다 (런타임 드로잉 경로)
        public static PlatformCollider Spawn(List<Vector2> worldPoints)
        {
            var go = new GameObject("InkPlatform")
            {
                layer = LayerMask.NameToLayer("Platform"),
            };
            var platform = go.AddComponent<PlatformCollider>();
            platform.Build(worldPoints);

            active.Add(platform);
            if (active.Count > MaxActivePlatforms)
                active[0].BeginFade(); // 가장 오래된 발판부터 먹이 마른다

            SketchToInkService.Instance?.Stylize(platform);
            return platform;
        }

        void Awake()
        {
            Line = GetComponent<LineRenderer>();
            edge = GetComponent<EdgeCollider2D>();
        }

        void Start()
        {
            // 씬에 미리 배치된 발판(시작 지형): 에디터에서 넣은 콜라이더 점으로 비주얼만 구성
            if (Length <= 0f && edge.pointCount >= 2)
            {
                var pts = new List<Vector2>(edge.points);
                Length = BezierSmoother.PolylineLength(pts);
                lifetime = 0f;
                ApplyVisual(pts);
                SketchToInkService.Instance?.Stylize(this);
            }
        }

        void Build(List<Vector2> worldPoints)
        {
            // 원점을 스트로크 중심으로 잡고 로컬 좌표로 변환
            var center = Vector2.zero;
            foreach (var p in worldPoints) center += p;
            center /= worldPoints.Count;
            transform.position = center;

            var local = new List<Vector2>(worldPoints.Count);
            foreach (var p in worldPoints) local.Add(p - center);

            Length = BezierSmoother.PolylineLength(local);

            edge = GetComponent<EdgeCollider2D>();
            edge.points = local.ToArray();
            edge.edgeRadius = 0.06f;

            ApplyVisual(local);
        }

        void ApplyVisual(List<Vector2> localPoints)
        {
            Line = GetComponent<LineRenderer>();
            Line.useWorldSpace = false;
            Line.positionCount = localPoints.Count;
            for (int i = 0; i < localPoints.Count; i++)
                Line.SetPosition(i, localPoints[i]);
        }

        void Update()
        {
            if (lifetime <= 0f) return; // 영구 발판

            age += Time.deltaTime;
            float remaining = lifetime - age;

            if (remaining <= fadeDuration)
            {
                float alpha = Mathf.Clamp01(remaining / fadeDuration);
                var c = Line.startColor;
                c.a = alpha;
                Line.startColor = Line.endColor = c;
            }

            if (remaining <= 0f)
                Destroy(gameObject);
        }

        /// 발판 수 초과 시 수명을 앞당겨 페이드아웃 시작
        void BeginFade()
        {
            if (lifetime <= 0f) return;
            age = Mathf.Max(age, lifetime - fadeDuration);
        }

        void OnDestroy()
        {
            active.Remove(this);
        }
    }
}
