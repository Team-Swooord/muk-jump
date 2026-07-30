using MukJump.Core;
using UnityEngine;

namespace MukJump.Player
{
    /// 로비의 영구 먹선 위를 먹방울이가 천천히 왕복하며 메뉴 화면을 살아 있게 만든다.
    /// 게임 시작 순간에는 개입을 멈추고 PlayerController의 실제 물리에 소유권을 넘긴다.
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class LobbyCharacterWander : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float moveSpeed = 0.82f;
        [SerializeField, Min(0f)] float edgePadding = 0.62f;
        [SerializeField, Min(0f)] float bobHeight = 0.045f;
        [SerializeField, Min(0.1f)] float bobFrequency = 4.2f;
        [SerializeField, Min(0f)] float rollAngle = 1.8f;
        [SerializeField] float fallbackHalfWidth =
            LobbyWorldSetup.StarterPlatformHalfWidth;

        Rigidbody2D body;
        SpriteRenderer spriteRenderer;
        Camera worldCamera;
        float direction = 1f;
        float lobbyBaseY;
        bool wasInLobby;

        public float MoveSpeed => moveSpeed;
        public float FallbackHalfWidth => fallbackHalfWidth;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            lobbyBaseY = body.position.y;
        }

        void Start()
        {
            worldCamera = Camera.main;
            lobbyBaseY = body.position.y;
        }

        void FixedUpdate()
        {
            var manager = GameManager.Instance;
            if (manager == null || manager.State != GameState.Lobby)
            {
                if (wasInLobby)
                {
                    body.linearVelocity = Vector2.zero;
                    body.rotation = 0f;
                }
                wasInLobby = false;
                return;
            }

            wasInLobby = true;
            if (body.bodyType != RigidbodyType2D.Kinematic)
                body.bodyType = RigidbodyType2D.Kinematic;

            float halfWidth = Mathf.Max(1f, fallbackHalfWidth);
            if (worldCamera != null)
            {
                halfWidth = Mathf.Min(
                    halfWidth,
                    worldCamera.orthographicSize * worldCamera.aspect);
            }
            float limit = Mathf.Max(0.5f, halfWidth - edgePadding);
            float nextX = body.position.x +
                          direction * moveSpeed * Time.fixedUnscaledDeltaTime;
            if (nextX >= limit)
            {
                nextX = limit;
                direction = -1f;
            }
            else if (nextX <= -limit)
            {
                nextX = -limit;
                direction = 1f;
            }

            float phase = Time.unscaledTime * bobFrequency;
            float nextY = lobbyBaseY + Mathf.Abs(Mathf.Sin(phase)) * bobHeight;
            body.MovePosition(new Vector2(nextX, nextY));
            body.MoveRotation(Mathf.Sin(phase) * rollAngle * direction);
            body.linearVelocity = new Vector2(direction * moveSpeed, 0f);
            spriteRenderer.flipX = direction < 0f;
        }

        void OnValidate()
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            edgePadding = Mathf.Max(0f, edgePadding);
            bobHeight = Mathf.Max(0f, bobHeight);
            bobFrequency = Mathf.Max(0.1f, bobFrequency);
            rollAngle = Mathf.Max(0f, rollAngle);
            fallbackHalfWidth = Mathf.Max(1f, fallbackHalfWidth);
        }
    }
}
