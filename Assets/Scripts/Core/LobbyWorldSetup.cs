using MukJump.Drawing;
using MukJump.Player;
using UnityEngine;

namespace MukJump.Core
{
    /// 저장된 최신 씬과 실행 중 복원된 구버전 씬이 같은 로비 지형을 사용하게 한다.
    /// 시작 물리는 보존하되 로비에서는 플레이어와 먹선을 숨기고 게임 시작 때만 표시한다.
    [DisallowMultipleComponent]
    public sealed class LobbyWorldSetup : MonoBehaviour
    {
        public const string StarterPlatformObjectName = "StarterInkPlatform";
        public const float StarterPlatformHalfWidth = 5.35f;
        public const float StarterPlatformYOffset = 0.42f;

        static readonly Vector2[] StarterPlatformPoints =
        {
            new(-StarterPlatformHalfWidth, 0f),
            new(StarterPlatformHalfWidth, 0f),
        };

        GameManager boundManager;

        void OnEnable()
        {
            BindManager();
            Apply();
        }

        void Start()
        {
            // 다른 씬 오브젝트의 Awake 순서와 무관하게 첫 프레임 전에 한 번 더 동기화한다.
            BindManager();
            Apply();
        }

        void OnDisable()
        {
            UnbindManager();
        }

        void Apply()
        {
            PlayerController firstPlayer = null;
            var players = FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || !BelongsToSetupScene(player.gameObject))
                    continue;

                firstPlayer ??= player;
                var legacyWander = player.GetComponent<LobbyCharacterWander>();
                if (legacyWander != null)
                    legacyWander.enabled = false;
            }

            var starter = FindStarterPlatform();
            if (starter == null && firstPlayer != null)
                starter = CreateStarterPlatform(firstPlayer.transform.position);
            if (starter == null)
                return;

            int platformLayer = LayerMask.NameToLayer("Platform");
            if (platformLayer >= 0)
                starter.layer = platformLayer;

            var line = starter.GetComponent<LineRenderer>();
            if (line == null)
                line = starter.AddComponent<LineRenderer>();
            line.useWorldSpace = false;

            if (starter.GetComponent<EdgeCollider2D>() == null)
                starter.AddComponent<EdgeCollider2D>();
            var platform = starter.GetComponent<PlatformCollider>();
            if (platform == null)
                platform = starter.AddComponent<PlatformCollider>();
            platform.ConfigurePermanentInkLine(StarterPlatformPoints);
            GameState currentState =
                boundManager == null ? GameState.Lobby : boundManager.State;
            if (currentState == GameState.Lobby)
                ResetLobbyPlayersToStarter(starter.transform.position);
            ApplyPresentation(currentState);
        }

        void BindManager()
        {
            var current = GameManager.Instance;
            if (boundManager == current)
                return;

            UnbindManager();
            boundManager = current;
            if (boundManager != null)
                boundManager.StateChanged += HandleStateChanged;
        }

        void UnbindManager()
        {
            if (boundManager != null)
                boundManager.StateChanged -= HandleStateChanged;
            boundManager = null;
        }

        void HandleStateChanged(GameState _, GameState nextState)
        {
            ApplyPresentation(nextState);
        }

        void ApplyPresentation(GameState state)
        {
            bool showGameplayWorld = state != GameState.Lobby;
            var players = FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || !BelongsToSetupScene(player.gameObject))
                    continue;

                var renderer = player.GetComponent<SpriteRenderer>();
                if (renderer != null)
                    renderer.enabled = showGameplayWorld;
            }

            var starter = FindStarterPlatform();
            if (starter == null)
                return;
            var line = starter.GetComponent<LineRenderer>();
            if (line != null)
                line.enabled = showGameplayWorld;
        }

        void ResetLobbyPlayersToStarter(Vector3 starterPosition)
        {
            Vector2 playerPosition = starterPosition +
                                     Vector3.up * StarterPlatformYOffset;
            var players = FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || !BelongsToSetupScene(player.gameObject))
                    continue;

                var body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    player.transform.position = new Vector3(
                        playerPosition.x,
                        playerPosition.y,
                        player.transform.position.z);
                    body.position = playerPosition;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                    body.rotation = 0f;
                }
                else
                {
                    player.transform.position = playerPosition;
                }

                var renderer = player.GetComponent<SpriteRenderer>();
                if (renderer != null)
                    renderer.flipX = false;
            }
        }

        GameObject FindStarterPlatform()
        {
            var platforms = FindObjectsByType<PlatformCollider>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < platforms.Length; i++)
            {
                var candidate = platforms[i];
                if (candidate != null &&
                    candidate.name == StarterPlatformObjectName &&
                    BelongsToSetupScene(candidate.gameObject))
                {
                    return candidate.gameObject;
                }
            }

            var named = GameObject.Find(StarterPlatformObjectName);
            return named != null && BelongsToSetupScene(named) ? named : null;
        }

        GameObject CreateStarterPlatform(Vector3 playerPosition)
        {
            var starter = new GameObject(StarterPlatformObjectName);
            starter.transform.position = new Vector3(
                playerPosition.x,
                playerPosition.y - StarterPlatformYOffset,
                0f);
            starter.AddComponent<LineRenderer>().sortingOrder = 2;
            starter.AddComponent<EdgeCollider2D>();
            return starter;
        }

        bool BelongsToSetupScene(GameObject candidate)
        {
            return candidate != null &&
                   (!gameObject.scene.IsValid() ||
                    candidate.scene == gameObject.scene);
        }

#if UNITY_EDITOR
        public void ApplyForTests()
        {
            Apply();
        }

        public void ApplyPresentationForTests(GameState state)
        {
            ApplyPresentation(state);
        }
#endif
    }
}
