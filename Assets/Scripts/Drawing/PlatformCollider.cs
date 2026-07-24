using System.Collections.Generic;
using UnityEngine;
using MukJump.AI;
using MukJump.Core;

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
        [SerializeField] float lifetime = 6.5f;
        [SerializeField] float fadeDuration = 1.2f;

        public float Length { get; private set; }
        public LineRenderer Line { get; private set; }
        EdgeCollider2D edge;
        Vector2[] originalPoints;
        float age;
        bool removalRequested;

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

            originalPoints = localPoints.ToArray();
        }

        void Update()
        {
            if (removalRequested) return;
            if (lifetime <= 0f) return; // 영구 발판

            age += Time.deltaTime;
            float remaining = lifetime - age;

            if (remaining <= fadeDuration)
            {
                float t = 1f - Mathf.Clamp01(remaining / fadeDuration);
                FadeVisual(t);
                TrimCollider(t);
            }

            if (remaining <= 0f)
                Destroy(gameObject);
        }

        /// 낙하 위험물에 맞은 발판을 자연 소멸과 같은 등록 해제 규칙으로 안전하게 제거한다.
        public bool BreakFromHazard()
        {
            if (!TryBeginHazardRemoval()) return false;
            Destroy(gameObject);
            return true;
        }

        bool TryBeginHazardRemoval()
        {
            if (removalRequested) return false;
            removalRequested = true;
            if (edge != null) edge.enabled = false;
            active.Remove(this);
            return true;
        }

        /// 처음 붓을 댄 쪽(t=0 지점)부터 투명해지는 알파 스윕 — 선의 길이·두께는 그대로,
        /// 먹이 마르며 스며들 듯 투명도만 쓸려나간다
        void FadeVisual(float t)
        {
            const float feather = 0.3f; // 투명↔불투명 경계의 부드러운 폭

            var ink = InkPalette.Ink;
            float front = Mathf.Lerp(0f, 1f + feather, t); // t=1이면 끝까지 완전히 투명

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(ink, 0f),
                    new GradientColorKey(ink, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0f, Mathf.Clamp01(front - feather)),
                    new GradientAlphaKey(0.96f, Mathf.Clamp01(front)),
                    new GradientAlphaKey(0.96f, 1f),
                });
            Line.colorGradient = gradient;
        }

        /// 투명해진 구간은 밟을 수 없도록 콜라이더도 같은 진행도로 잘라낸다 (비주얼은 그대로)
        void TrimCollider(float t)
        {
            if (originalPoints == null || originalPoints.Length < 2) return;

            int cutoff = Mathf.Clamp(Mathf.FloorToInt(t * (originalPoints.Length - 1)), 0, originalPoints.Length - 2);
            var remainingPoints = new Vector2[originalPoints.Length - cutoff];
            for (int i = 0; i < remainingPoints.Length; i++)
                remainingPoints[i] = originalPoints[cutoff + i];
            edge.points = remainingPoints; // 배열 전체를 한 번에 대입해야 콜라이더에 반영된다
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
