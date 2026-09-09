using System.Collections.Generic;
using UnityEngine;
using MukJump.AI;
using MukJump.Core;
using MukJump.Player;

namespace MukJump.Drawing
{
    /// 풍맥과 함께, 맵 장애물처럼 자연스럽게 다음 안전지대 발판을 예약한다.
    /// 기존 씬·빌더 호환을 위해 클래스 이름은 유지한다.
    public class RestPlatformSpawner : MonoBehaviour
    {
        const float MinWindHeightInterval = 1f;
        const int MaxCatchUpSpawnsPerFrame = 8;
        public const int MaxRetainedMapRests = 2;
        public const float DefaultMapRestWidth = 4.8f;
        public const float DefaultMapRestHazardClearance = 8f;

        [SerializeField] Vector2 windHeightIntervalRange = new(82f, 128f);
        [Header("맵 안전지대 발판")]
        [SerializeField] Vector2 firstRestHeightRange = new(22f, 28f);
        [SerializeField] Vector2 restHeightIntervalRange = new(28f, 38f);
        [SerializeField, Min(2f)] float restPlatformWidth = DefaultMapRestWidth;
        [SerializeField] Vector2 restHorizontalOffsetRange = new(-0.9f, 0.9f);
        [SerializeField, Min(0f)] float restHazardClearance =
            DefaultMapRestHazardClearance;
        [SerializeField, Min(2f)] float spawnAheadHeight = 8f;
        [SerializeField, Min(1f)] float cleanupBelowCamera = 8f;

        public static RestPlatformSpawner Instance { get; private set; }

        readonly List<PlatformCollider> spawned = new();
        readonly List<MapRestRecord> activeMapRests = new();
        readonly List<PlayerController> livingPlayers =
            new(GameManager.MaxLivingPlayers);
        Camera worldCamera;
        float nextWindHeight;
        float nextMapRestHeight = float.PositiveInfinity;
        float activeMapRestHeight = float.PositiveInfinity;
        PlatformCollider activeMapRestPlatform;
        int platformIndex;
        int scheduledSessionVersion = -1;

        sealed class MapRestRecord
        {
            public PlatformCollider Platform;
            public float Height;
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            ClearSpawnedPlatforms();
        }

        void Start()
        {
            worldCamera = Camera.main;
        }

        void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameplayTicking)
                return;

            EnsureSessionSchedule();
            if (worldCamera == null)
                worldCamera = Camera.main;
            float fallbackHeight = ScoreManager.Instance != null
                ? ScoreManager.Instance.Height
                : 0f;
            float cameraTopHeight = worldCamera != null
                ? GameHeightAtWorldY(
                    worldCamera.transform.position.y +
                    worldCamera.orthographicSize)
                : fallbackHeight;
            float cameraCutoffWorldY = worldCamera != null
                ? worldCamera.transform.position.y -
                  worldCamera.orthographicSize - cleanupBelowCamera
                : WorldYAtGameHeight(fallbackHeight - cleanupBelowCamera);
            float restCutoffWorldY = ResolveCleanupCutoffWorldY(
                cameraCutoffWorldY);
            float cutoffHeight = GameHeightAtWorldY(restCutoffWorldY);
            CleanupOldPlatforms(cameraCutoffWorldY, restCutoffWorldY);
            TrySpawnMapRest(cameraTopHeight, cutoffHeight);

            float height = fallbackHeight;
            int spawnedThisFrame = 0;
            while (height + spawnAheadHeight >= nextWindHeight &&
                   spawnedThisFrame < MaxCatchUpSpawnsPerFrame)
            {
                if (!IsHazardHeightBlocked(nextWindHeight))
                    SpawnWindAtGameHeight(nextWindHeight);
                ScheduleNextWind(nextWindHeight);
                spawnedThisFrame++;
            }
            // 손상된 세이브나 큰 디버그 순간이동이 있어도 한 프레임에 과거 예약을
            // 무제한 생성하지 않고 현재 고도 이후로 예약을 재정렬한다.
            if (height + spawnAheadHeight >= nextWindHeight)
                ScheduleNextWind(height + spawnAheadHeight);
        }

        public void DebugResetSchedule(int currentHeight)
        {
            ClearSpawnedPlatforms();
            scheduledSessionVersion = GameplayRandom.SessionVersion;
            ScheduleNextWind(Mathf.Max(0, currentHeight));
            ScheduleNextMapRest(Mathf.Max(0, currentHeight));
        }

        /// 예약된 안전지대 전후에는 새 장애물과 낙묵석을 시작하지 않는다.
        /// 이미 진행 중인 경고나 위험은 강제로 삭제하지 않는다.
        public bool IsHazardHeightBlocked(float gameHeight) =>
            IsHazardBandBlocked(gameHeight, gameHeight);

        public bool IsHazardBandBlocked(
            float minimumGameHeight,
            float maximumGameHeight)
        {
            EnsureSessionSchedule();
            float minimum = Mathf.Min(minimumGameHeight, maximumGameHeight);
            float maximum = Mathf.Max(minimumGameHeight, maximumGameHeight);
            if (!IsFinite(minimum) || !IsFinite(maximum))
                return false;

            PruneMissingMapRests();
            for (int i = 0; i < activeMapRests.Count; i++)
            {
                if (BandOverlapsRestHeight(
                        minimum,
                        maximum,
                        activeMapRests[i].Height))
                    return true;
            }
            return BandOverlapsRestHeight(
                minimum,
                maximum,
                nextMapRestHeight);
        }

        public void DebugSpawnWindNearPlayer()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.HighestLivingPlayer : null;
            if (player == null) return;
            SpawnWindPlatform(new Vector2(player.transform.position.x,
                player.transform.position.y + 3.2f), 3.4f, "DEBUG");
            GameFeedbackController.Instance?.ShowZone("풍맥 발판", "위에 착지해 상승 기류를 타세요");
        }

        void SpawnWindAtGameHeight(float gameHeight)
        {
            float worldY = WorldYAtGameHeight(gameHeight);
            float halfWidth = worldCamera != null
                ? worldCamera.orthographicSize * worldCamera.aspect
                : 4.8f;
            float width = GameplayRandom.Range(
                GameplayRandomStream.Platforms, 2.8f, 3.8f);
            float limit = Mathf.Max(0.2f, halfWidth - width * 0.5f - 0.25f);
            SpawnWindPlatform(new Vector2(
                    GameplayRandom.Range(GameplayRandomStream.Platforms, -limit, limit),
                    worldY),
                width,
                $"{Mathf.RoundToInt(gameHeight)}m");
        }

        void SpawnWindPlatform(Vector2 center, float width, string suffix)
        {
            var points = new List<Vector2>(7);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                points.Add(new Vector2(Mathf.Lerp(center.x - width * 0.5f,
                    center.x + width * 0.5f, t), center.y + Mathf.Sin(t * Mathf.PI) * 0.12f));
            }
            var platform = PlatformCollider.SpawnWindCurrentPlatform(points);
            platform.name = $"WindCurrentPlatform_{++platformIndex:00}_{suffix}";
            spawned.Add(platform);
            AddWindMark(platform.transform);
        }

        bool TrySpawnMapRest(float cameraTopHeight, float cutoffHeight)
        {
            PruneMissingMapRests();
            // 디버그 순간이동·50m 급상승으로 예약을 통째로 지나갔다면
            // 과거 발판을 쌓지 않고 현재 정리선 앞에서 한 번만 재예약한다.
            if (nextMapRestHeight < cutoffHeight)
            {
                ScheduleNextMapRest(cutoffHeight);
                return false;
            }
            if (cameraTopHeight + spawnAheadHeight < nextMapRestHeight)
                return false;

            // 실제 다음 예약이 생성 범위에 들어온 순간에만 기존 최신 슬롯을
            // 교체한다. 미리 없애면 플레이어가 착지한 쉼터가 발밑에서 사라진다.
            if (!MakeRoomForNextMapRest(cutoffHeight))
                return false;

            float spawnHeight = nextMapRestHeight;
            SpawnMapRestAtGameHeight(spawnHeight);
            ScheduleNextMapRest(spawnHeight);
            return activeMapRestPlatform != null;
        }

        bool MakeRoomForNextMapRest(float cutoffHeight)
        {
            if (activeMapRests.Count < MaxRetainedMapRests)
                return true;

            int protectedIndex = -1;
            float protectedHeight = float.PositiveInfinity;
            for (int i = 0; i < activeMapRests.Count; i++)
            {
                float height = activeMapRests[i].Height;
                if (height < cutoffHeight || height >= protectedHeight)
                    continue;
                protectedHeight = height;
                protectedIndex = i;
            }

            int replaceIndex = -1;
            float latestReplaceableHeight = float.NegativeInfinity;
            for (int i = 0; i < activeMapRests.Count; i++)
            {
                if (i == protectedIndex ||
                    activeMapRests[i].Height <= latestReplaceableHeight)
                    continue;
                latestReplaceableHeight = activeMapRests[i].Height;
                replaceIndex = i;
            }
            if (replaceIndex < 0)
                return false;

            DestroyTrackedMapRest(activeMapRests[replaceIndex].Platform);
            return activeMapRests.Count < MaxRetainedMapRests;
        }

        void SpawnMapRestAtGameHeight(float gameHeight)
        {
            float worldY = WorldYAtGameHeight(gameHeight);
            Vector2 offsets = NormalizeOrderedRange(
                restHorizontalOffsetRange,
                -0.9f,
                0.9f);
            float cameraCenterX = worldCamera != null
                ? worldCamera.transform.position.x
                : 0f;
            float centerX = cameraCenterX + GameplayRandom.Range(
                GameplayRandomStream.Platforms,
                offsets.x,
                offsets.y);
            float width = Mathf.Max(2f, restPlatformWidth);
            var points = new List<Vector2>(7);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                points.Add(new Vector2(
                    Mathf.Lerp(centerX - width * 0.5f,
                        centerX + width * 0.5f, t),
                    worldY + Mathf.Sin(t * Mathf.PI) * 0.035f));
            }

            PlatformCollider platform =
                PlatformCollider.SpawnMapRestPlatform(points);
            if (platform == null)
                return;
            platform.name = $"MapRestPlatform_{++platformIndex:00}_" +
                            $"{Mathf.RoundToInt(gameHeight)}m";
            activeMapRestPlatform = platform;
            activeMapRestHeight = gameHeight;
            activeMapRests.Add(new MapRestRecord
            {
                Platform = platform,
                Height = gameHeight,
            });
            spawned.Add(platform);
        }

        static void AddWindMark(Transform parent)
        {
            for (int arc = 0; arc < 3; arc++)
            {
                var mark = new GameObject($"WindArc_{arc + 1}");
                mark.transform.SetParent(parent, false);
                mark.transform.localPosition = new Vector3(0f, 0.25f + arc * 0.22f, 0f);
                var line = mark.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 9;
                line.sharedMaterial = FallbackInkStyle.SharedTintableBrushMaterial;
                line.sortingOrder = 4;
                line.startWidth = line.endWidth = 0.035f;
                var accent = InkPalette.WindAccent;
                accent.a = 0.8f;
                line.startColor = line.endColor = accent;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float t = i / (line.positionCount - 1f);
                    line.SetPosition(i, new Vector3(Mathf.Sin(t * Mathf.PI) * (0.22f + arc * 0.04f),
                        t * 0.18f, 0f));
                }
            }
        }

        void ScheduleNextWind(float fromHeight)
        {
            Vector2 interval = NormalizeHeightInterval(windHeightIntervalRange);
            nextWindHeight = fromHeight +
                             GameplayRandom.Range(
                                 GameplayRandomStream.Platforms,
                                 interval.x,
                                 interval.y);
        }

        void ScheduleFirstMapRest()
        {
            Vector2 range = NormalizeNonNegativeRange(firstRestHeightRange);
            nextMapRestHeight = GameplayRandom.Range(
                GameplayRandomStream.Platforms,
                range.x,
                range.y);
        }

        void ScheduleNextMapRest(float fromHeight)
        {
            Vector2 interval = NormalizeHeightInterval(
                restHeightIntervalRange);
            nextMapRestHeight = Mathf.Max(0f, fromHeight) +
                                GameplayRandom.Range(
                                    GameplayRandomStream.Platforms,
                                    interval.x,
                                    interval.y);
        }

        static Vector2 NormalizeHeightInterval(Vector2 range)
        {
            float first = IsFinite(range.x) ? range.x : MinWindHeightInterval;
            float second = IsFinite(range.y) ? range.y : first;
            float minimum = Mathf.Max(
                MinWindHeightInterval, Mathf.Min(first, second));
            float maximum = Mathf.Max(minimum, Mathf.Max(first, second));
            return new Vector2(minimum, maximum);
        }

        static Vector2 NormalizeNonNegativeRange(Vector2 range)
        {
            Vector2 ordered = NormalizeOrderedRange(range, 22f, 28f);
            float minimum = Mathf.Max(0f, ordered.x);
            return new Vector2(minimum, Mathf.Max(minimum, ordered.y));
        }

        static Vector2 NormalizeOrderedRange(
            Vector2 range,
            float fallbackMinimum,
            float fallbackMaximum)
        {
            float first = IsFinite(range.x) ? range.x : fallbackMinimum;
            float second = IsFinite(range.y) ? range.y : fallbackMaximum;
            return new Vector2(
                Mathf.Min(first, second),
                Mathf.Max(first, second));
        }

        static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        void EnsureSessionSchedule()
        {
            int version = GameplayRandom.SessionVersion;
            if (scheduledSessionVersion == version) return;
            ClearSpawnedPlatforms();
            scheduledSessionVersion = version;
            ScheduleNextWind(25f);
            ScheduleFirstMapRest();
        }

        void CleanupOldPlatforms(
            float cameraCutoffWorldY,
            float restCutoffWorldY)
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] == null)
                {
                    spawned.RemoveAt(i);
                    continue;
                }
                bool isMapRest = spawned[i].IsMapRestPlatform;
                float cutoff = isMapRest
                    ? restCutoffWorldY
                    : cameraCutoffWorldY;
                if (spawned[i].transform.position.y >= cutoff) continue;
                PlatformCollider removed = spawned[i];
                spawned[i].gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(spawned[i].gameObject);
                else
                    DestroyImmediate(spawned[i].gameObject);
                spawned.RemoveAt(i);
                if (isMapRest)
                    RemoveMapRestTracking(removed);
            }
            PruneMissingMapRests();
        }

        /// 선두 사망 직후 카메라는 아직 높은 위치에서 다음 선두로 내려오는 중일 수 있다.
        /// 다음 생존자가 다시 밟을 최근 쉼터는 카메라 재구도가 끝날 때까지 보존한다.
        /// 한 명만 남은 순간에도 사망 프레임의 옛 카메라가 먼저 쉼터를 지우지 않게 하되,
        /// 그 생존자가 쉼터를 지나면 정상 정리한다.
        float ResolveCleanupCutoffWorldY(float cameraCutoffWorldY)
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
                return cameraCutoffWorldY;

            manager.GetLivingPlayersNonAlloc(livingPlayers);
            if (livingPlayers.Count == 0)
                return cameraCutoffWorldY;

            float highestWorldY = float.NegativeInfinity;
            float secondHighestWorldY = float.NegativeInfinity;
            for (int i = 0; i < livingPlayers.Count; i++)
            {
                PlayerController player = livingPlayers[i];
                if (player == null)
                    continue;
                float worldY = player.transform.position.y;
                if (!IsFinite(worldY))
                    continue;
                if (worldY > highestWorldY)
                {
                    secondHighestWorldY = highestWorldY;
                    highestWorldY = worldY;
                }
                else if (worldY > secondHighestWorldY)
                {
                    secondHighestWorldY = worldY;
                }
            }

            float protectedWorldY = IsFinite(secondHighestWorldY)
                ? secondHighestWorldY
                : highestWorldY;
            if (!IsFinite(protectedWorldY))
                return cameraCutoffWorldY;

            float survivorCutoff = protectedWorldY -
                                   Mathf.Max(1f, cleanupBelowCamera);
            return Mathf.Min(cameraCutoffWorldY, survivorCutoff);
        }

        void ClearSpawnedPlatforms()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                var platform = spawned[i];
                if (platform == null) continue;

                // Play Mode에서는 삭제가 프레임 끝까지 지연되므로 즉시 충돌과 렌더를 끈다.
                platform.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(platform.gameObject);
                else
                    DestroyImmediate(platform.gameObject);
            }
            spawned.Clear();
            activeMapRests.Clear();
            activeMapRestPlatform = null;
            activeMapRestHeight = float.PositiveInfinity;
        }

        void RemoveMapRestTracking(PlatformCollider platform)
        {
            for (int i = activeMapRests.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(activeMapRests[i].Platform, platform))
                    activeMapRests.RemoveAt(i);
            }
            RefreshLatestMapRest();
        }

        void DestroyTrackedMapRest(PlatformCollider platform)
        {
            if (platform == null)
            {
                PruneMissingMapRests();
                return;
            }

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(spawned[i], platform))
                    spawned.RemoveAt(i);
            }
            RemoveMapRestTracking(platform);
            platform.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(platform.gameObject);
            else
                DestroyImmediate(platform.gameObject);
        }

        void PruneMissingMapRests()
        {
            bool changed = false;
            for (int i = activeMapRests.Count - 1; i >= 0; i--)
            {
                if (activeMapRests[i].Platform != null)
                    continue;
                activeMapRests.RemoveAt(i);
                changed = true;
            }
            if (changed || (activeMapRestPlatform == null && activeMapRests.Count > 0))
                RefreshLatestMapRest();
        }

        void RefreshLatestMapRest()
        {
            activeMapRestPlatform = null;
            activeMapRestHeight = float.PositiveInfinity;
            float latestHeight = float.NegativeInfinity;
            for (int i = 0; i < activeMapRests.Count; i++)
            {
                MapRestRecord record = activeMapRests[i];
                if (record.Platform == null || record.Height <= latestHeight)
                    continue;
                latestHeight = record.Height;
                activeMapRestPlatform = record.Platform;
                activeMapRestHeight = record.Height;
            }
        }

        bool BandOverlapsRestHeight(
            float minimum,
            float maximum,
            float restHeight)
        {
            if (!IsFinite(restHeight))
                return false;
            float clearance = Mathf.Max(0f, restHazardClearance);
            return maximum >= restHeight - clearance &&
                   minimum <= restHeight + clearance;
        }

        float GameHeightAtWorldY(float worldY)
        {
            return ScoreManager.Instance != null
                ? ScoreManager.Instance.HeightAt(worldY)
                : worldY;
        }

        float WorldYAtGameHeight(float gameHeight)
        {
            if (ScoreManager.Instance == null)
                return gameHeight;
            float anchorY = worldCamera != null
                ? worldCamera.transform.position.y
                : 0f;
            return anchorY + gameHeight -
                   ScoreManager.Instance.HeightAt(anchorY);
        }

        void OnValidate()
        {
            windHeightIntervalRange = NormalizeHeightInterval(windHeightIntervalRange);
            firstRestHeightRange = NormalizeNonNegativeRange(firstRestHeightRange);
            restHeightIntervalRange = NormalizeHeightInterval(
                restHeightIntervalRange);
            restPlatformWidth = Mathf.Max(2f, restPlatformWidth);
            restHorizontalOffsetRange = NormalizeOrderedRange(
                restHorizontalOffsetRange,
                -0.9f,
                0.9f);
            restHazardClearance = Mathf.Max(0f, restHazardClearance);
            spawnAheadHeight = Mathf.Max(2f, spawnAheadHeight);
            cleanupBelowCamera = Mathf.Max(1f, cleanupBelowCamera);
        }
    }
}
