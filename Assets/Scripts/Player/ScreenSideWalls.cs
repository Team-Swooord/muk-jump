using UnityEngine;

namespace MukJump.Player
{
    /// 카메라 좌우 가장자리에 함께 이동하는 보이지 않는 충돌 벽을 만든다.
    [RequireComponent(typeof(Camera))]
    public class ScreenSideWalls : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float wallThickness = 0.6f;
        [SerializeField, Min(5f)] float wallHeight = 30f;

        Camera worldCamera;
        Transform leftWall;
        Transform rightWall;

        void Awake()
        {
            worldCamera = GetComponent<Camera>();
            leftWall = CreateWall("LeftScreenWall");
            rightWall = CreateWall("RightScreenWall");
            UpdateWalls();
        }

        void FixedUpdate() => UpdateWalls();

        Transform CreateWall(string wallName)
        {
            var wall = new GameObject(wallName);
            wall.transform.SetParent(transform, false);
            wall.AddComponent<ScreenSideWall>();
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(wallThickness, wallHeight);
            return wall.transform;
        }

        void UpdateWalls()
        {
            if (worldCamera == null || leftWall == null || rightWall == null) return;
            float halfWidth = worldCamera.orthographicSize * worldCamera.aspect;
            float edge = halfWidth + wallThickness * 0.5f;
            leftWall.localPosition = new Vector3(-edge, 0f, 0f);
            rightWall.localPosition = new Vector3(edge, 0f, 0f);
        }

        void OnValidate()
        {
            wallThickness = Mathf.Max(0.1f, wallThickness);
            wallHeight = Mathf.Max(5f, wallHeight);
        }
    }

    /// 플레이어가 화면 경계 충돌만 구분하기 위한 표식 컴포넌트.
    public class ScreenSideWall : MonoBehaviour { }
}
