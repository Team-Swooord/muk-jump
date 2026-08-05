using UnityEngine;
using MukJump.AI;
using MukJump.Core;

namespace MukJump.Player
{
    /// 카메라 좌우 가장자리에 함께 이동하는 충돌 벽과 먹선 금지 띠를 만든다.
    [RequireComponent(typeof(Camera))]
    public class ScreenSideWalls : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float wallThickness = 0.6f;
        [SerializeField, Min(5f)] float wallHeight = 30f;
        [Tooltip("화면 경계와 급경사 먹선 사이의 무한 상승을 막는 월드 단위 먹선 금지 폭")]
        [SerializeField, Min(0.45f)] float drawExclusionMargin = 0.95f;
        [Tooltip("플레이 중 화면 가장자리에 보이는 붉은 먹벽의 투명도")]
        [SerializeField, Range(0f, 0.5f)] float wallVisualAlpha = 0.16f;

        Camera worldCamera;
        [SerializeField, HideInInspector] Rigidbody2D leftWall;
        [SerializeField, HideInInspector] Rigidbody2D rightWall;
        [SerializeField, HideInInspector] PhysicsMaterial2D wallMaterial;

        void Awake()
        {
            worldCamera = GetComponent<Camera>();
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            EnsureWalls();
            UpdateWalls(true);
        }

        void FixedUpdate()
        {
            EnsureWalls();
            UpdateWalls(false);
            UpdateWallVisuals();
        }

        void EnsureWalls()
        {
            if (worldCamera == null) worldCamera = GetComponent<Camera>();
            if (wallMaterial == null)
            {
                wallMaterial = new PhysicsMaterial2D("ScreenSideWall_Frictionless")
                {
                    friction = 0f,
                    bounciness = 0f,
                };
            }
            if (leftWall == null)
                leftWall = FindOwnedWall(true) ?? CreateWall("LeftScreenWall", true);
            if (rightWall == null)
                rightWall = FindOwnedWall(false) ?? CreateWall("RightScreenWall", false);
            ConfigureWall(leftWall);
            ConfigureWall(rightWall);
        }

        Rigidbody2D CreateWall(string wallName, bool isLeft)
        {
            var wall = new GameObject(wallName);
            wall.AddComponent<ScreenSideWall>().Initialize(this, isLeft);
            var body = wall.AddComponent<Rigidbody2D>();
            wall.AddComponent<BoxCollider2D>();
            ConfigureWall(body);
            return body;
        }

        Rigidbody2D FindOwnedWall(bool isLeft)
        {
            var markers = FindObjectsByType<ScreenSideWall>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker.Owner != this || marker.IsLeft != isLeft ||
                    marker.gameObject.scene != gameObject.scene)
                    continue;
                return marker.GetComponent<Rigidbody2D>();
            }
            return null;
        }

        void ConfigureWall(Rigidbody2D body)
        {
            if (body == null) return;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = body.GetComponent<BoxCollider2D>();
            if (collider == null) collider = body.gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(wallThickness, wallHeight);
            collider.sharedMaterial = wallMaterial;
            ConfigureWallVisual(body);
        }

        void ConfigureWallVisual(Rigidbody2D body)
        {
            var marker = body.GetComponent<ScreenSideWall>();
            if (marker == null) return;

            var line = body.GetComponent<LineRenderer>();
            if (line == null) line = body.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 3;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            // 선의 절반만 화면 안으로 들어오므로 실제 안쪽 폭은 drawExclusionMargin이다.
            line.startWidth = line.endWidth = drawExclusionMargin * 2f;
            float innerFaceX = marker.IsLeft
                ? wallThickness * 0.5f
                : -wallThickness * 0.5f;
            line.SetPosition(0, new Vector3(innerFaceX, -wallHeight * 0.5f, 0f));
            line.SetPosition(1, new Vector3(innerFaceX, wallHeight * 0.5f, 0f));
            Color wallColor = InkPalette.ObstaclePaperRed;
            wallColor.a = wallVisualAlpha;
            line.startColor = line.endColor = wallColor;
            line.enabled = ShouldShowWallVisuals();
        }

        void UpdateWallVisuals()
        {
            bool visible = ShouldShowWallVisuals();
            SetWallVisualEnabled(leftWall, visible);
            SetWallVisualEnabled(rightWall, visible);
        }

        static void SetWallVisualEnabled(Rigidbody2D wall, bool visible)
        {
            if (wall == null) return;
            var line = wall.GetComponent<LineRenderer>();
            if (line != null) line.enabled = visible;
        }

        static bool ShouldShowWallVisuals()
        {
            return Application.isPlaying &&
                   GameManager.Instance != null &&
                   GameManager.Instance.State != GameState.Lobby;
        }

        /// 현재 카메라 줌과 비율을 반영한 먹선 생성 가능 가로 범위다.
        public bool TryGetDrawableWorldXRange(out float minimumX, out float maximumX)
        {
            if (worldCamera == null) worldCamera = GetComponent<Camera>();
            if (worldCamera == null || !worldCamera.orthographic)
            {
                minimumX = float.NegativeInfinity;
                maximumX = float.PositiveInfinity;
                return false;
            }

            float halfWidth = Mathf.Max(
                0.01f,
                worldCamera.orthographicSize * worldCamera.aspect);
            float margin = Mathf.Min(drawExclusionMargin, halfWidth * 0.4f);
            float centerX = worldCamera.transform.position.x;
            minimumX = centerX - halfWidth + margin;
            maximumX = centerX + halfWidth - margin;
            return maximumX > minimumX;
        }

        void UpdateWalls(bool immediate)
        {
            if (worldCamera == null || leftWall == null || rightWall == null) return;
            float halfWidth = worldCamera.orthographicSize * worldCamera.aspect;
            float edge = halfWidth + wallThickness * 0.5f;
            Vector2 cameraPosition = worldCamera.transform.position;
            MoveWall(leftWall, cameraPosition + Vector2.left * edge, immediate);
            MoveWall(rightWall, cameraPosition + Vector2.right * edge, immediate);
        }

        static void MoveWall(Rigidbody2D wall, Vector2 position, bool immediate)
        {
            if (immediate)
                wall.position = position;
            else
                wall.MovePosition(position);
        }

        void OnDisable()
        {
            CleanupWalls();
        }

        void OnDestroy()
        {
            CleanupWalls();
        }

        void CleanupWalls()
        {
            DestroyWall(ref leftWall);
            DestroyWall(ref rightWall);
            if (wallMaterial == null) return;
            if (Application.isPlaying)
                Destroy(wallMaterial);
            else
                DestroyImmediate(wallMaterial);
            wallMaterial = null;
        }

        static void DestroyWall(ref Rigidbody2D wall)
        {
            if (wall == null) return;
            var wallObject = wall.gameObject;
            wall.simulated = false;
            var collider = wall.GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;
            wall = null;

            if (Application.isPlaying)
                Destroy(wallObject);
            else
                DestroyImmediate(wallObject);
        }

        void OnValidate()
        {
            wallThickness = Mathf.Max(0.1f, wallThickness);
            wallHeight = Mathf.Max(5f, wallHeight);
            drawExclusionMargin = Mathf.Max(0.45f, drawExclusionMargin);
            wallVisualAlpha = Mathf.Clamp(wallVisualAlpha, 0f, 0.5f);
        }
    }

    /// 플레이어가 화면 경계 충돌만 구분하기 위한 표식 컴포넌트.
    public class ScreenSideWall : MonoBehaviour
    {
        [SerializeField] ScreenSideWalls owner;
        [SerializeField] bool isLeft;

        public ScreenSideWalls Owner => owner;
        public bool IsLeft => isLeft;

        public void Initialize(ScreenSideWalls newOwner, bool left)
        {
            owner = newOwner;
            isLeft = left;
        }
    }
}
