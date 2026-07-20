using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace MukJump.AI
{
    /// <summary>
    /// 플레이어가 그린 발판 스케치를 실시간으로 '수묵화 붓질' 텍스처로 변환 요청하는 서비스.
    ///
    /// 설계 원칙 (CLAUDE.md 6장):
    ///   - 비동기 처리로 게임 진행을 막지 않는다.
    ///   - API 지연/실패 시 지정한 시간(timeoutSeconds) 안에 콜백에 null을 넘겨,
    ///     호출부(DrawnPlatform)가 이미 적용해둔 폴백 먹 텍스처 셰이더를 그대로 유지하게 한다.
    ///   - 실제 엔드포인트/모델(예: img2img + ControlNet-scribble)은 팀 결정에 따라
    ///     SendRequest() 내부만 교체하면 되도록 인터페이스를 분리해두었다.
    /// </summary>
    public class SketchToInkService : MonoBehaviour
    {
        public static SketchToInkService Instance { get; private set; }

        [Header("API 설정 (실제 값은 커밋하지 말고 로컬 설정/환경변수로 주입할 것)")]
        [SerializeField] private string apiEndpoint = "";
        [SerializeField] private string apiKey = "";

        [Header("Timing")]
        [Tooltip("이 시간(초) 안에 응답이 없으면 실패로 간주하고 폴백 유지")]
        [SerializeField] private float timeoutSeconds = 1.5f;

        [Header("Sketch Rasterize")]
        [SerializeField] private int sketchTextureSize = 256;
        [SerializeField] private float strokeWidthPixels = 6f;

        [Header("Cache (동일/유사 스트로크 재사용으로 체감 지연 최소화)")]
        [SerializeField] private bool useCachePresets = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// 스무딩된 스트로크를 스케치 텍스처로 만든 뒤 AI 변환을 요청한다.
        /// 성공하면 콜백에 변환된 텍스처를, 실패/타임아웃이면 null을 전달한다.
        /// </summary>
        public IEnumerator RequestInkConversion(Vector2[] smoothedWorldPoints, Action<Texture2D> onComplete)
        {
            if (string.IsNullOrEmpty(apiEndpoint))
            {
                // 엔드포인트 미설정 상태(로컬 개발 등)에서는 즉시 폴백 유지
                onComplete?.Invoke(null);
                yield break;
            }

            Texture2D sketch = RasterizeStroke(smoothedWorldPoints, sketchTextureSize, strokeWidthPixels);

            bool finished = false;
            Texture2D result = null;

            var requestRoutine = StartCoroutine(SendRequest(sketch, tex =>
            {
                result = tex;
                finished = true;
            }));

            float elapsed = 0f;
            while (!finished && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!finished)
            {
                StopCoroutine(requestRoutine);
                Debug.LogWarning("[SketchToInkService] AI 변환 타임아웃 — 폴백 셰이더 유지");
                onComplete?.Invoke(null);
                yield break;
            }

            onComplete?.Invoke(result);
        }

        private IEnumerator SendRequest(Texture2D sketch, Action<Texture2D> onDone)
        {
            byte[] pngBytes = sketch.EncodeToPNG();

            // TODO: 실제 img2img API 스펙(예: ControlNet-scribble)에 맞춰 요청 바디 구성.
            // 아래는 흔한 형태(멀티파트/JSON base64)를 가정한 뼈대 예시.
            var form = new WWWForm();
            form.AddBinaryData("image", pngBytes, "sketch.png", "image/png");
            form.AddField("prompt", "ink wash painting texture, sumi-e brush stroke, monochrome");

            using UnityWebRequest req = UnityWebRequest.Post(apiEndpoint, form);
            if (!string.IsNullOrEmpty(apiKey))
            {
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            }

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SketchToInkService] 요청 실패: {req.error}");
                onDone?.Invoke(null);
                yield break;
            }

            var resultTex = new Texture2D(2, 2);
            if (resultTex.LoadImage(req.downloadHandler.data))
            {
                onDone?.Invoke(resultTex);
            }
            else
            {
                onDone?.Invoke(null);
            }
        }

        /// <summary>스무딩된 월드 포인트를 정사각 텍스처 위에 선으로 래스터화한다 (AI 입력용 스케치).</summary>
        private Texture2D RasterizeStroke(Vector2[] worldPoints, int size, float widthPx)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            tex.SetPixels32(pixels);

            if (worldPoints.Length < 2)
            {
                tex.Apply();
                return tex;
            }

            // 포인트들의 바운딩 박스를 텍스처 전체에 맞춰 정규화
            Vector2 min = worldPoints[0], max = worldPoints[0];
            foreach (var p in worldPoints)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            Vector2 size2 = Vector2.Max(max - min, Vector2.one * 0.001f);

            Vector2 ToPixel(Vector2 p)
            {
                Vector2 n = (p - min) / size2;
                return new Vector2(n.x * (size - 1), n.y * (size - 1));
            }

            for (int i = 0; i < worldPoints.Length - 1; i++)
            {
                DrawLine(tex, ToPixel(worldPoints[i]), ToPixel(worldPoints[i + 1]), widthPx, Color.black);
            }

            tex.Apply();
            return tex;
        }

        private static void DrawLine(Texture2D tex, Vector2 a, Vector2 b, float width, Color color)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                int r = Mathf.CeilToInt(width * 0.5f);
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int x = Mathf.Clamp(Mathf.RoundToInt(p.x) + dx, 0, tex.width - 1);
                        int y = Mathf.Clamp(Mathf.RoundToInt(p.y) + dy, 0, tex.height - 1);
                        if (dx * dx + dy * dy <= r * r)
                            tex.SetPixel(x, y, color);
                    }
                }
            }
        }
    }
}
