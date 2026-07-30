using MukJump.Drawing;
using MukJump.Player;
using UnityEngine;

namespace MukJump.Core
{
    /// 저장된 최신 씬과 실행 중 복원된 구버전 씬이 같은 로비 지형을 사용하게 한다.
    /// 시작 먹선은 화면 폭 전체를 덮고, 로비 먹방울은 시작 전까지 그 위를 왕복한다.
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

        void OnEnable()
        {
            Apply();
        }

        void Start()
        {
            // 다른 씬 오브젝트의 Awake 순서와 무관하게 첫 프레임 전에 한 번 더 동기화한다.
            Apply();
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
                if (player.GetComponent<LobbyCharacterWander>() == null)
                    player.gameObject.AddComponent<LobbyCharacterWander>();
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
#endif
    }
}
