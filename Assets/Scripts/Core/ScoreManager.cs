using System;
using UnityEngine;

namespace MukJump.Core
{
    public interface IScoreStore
    {
        int LoadBest();
        void SaveBest(int value);
    }

    sealed class PlayerPrefsScoreStore : IScoreStore
    {
        const string BestKey = "MukJump.BestHeight";

        public int LoadBest() => PlayerPrefs.GetInt(BestKey, 0);

        public void SaveBest(int value)
        {
            PlayerPrefs.SetInt(BestKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }

    /// 점수 = 시작 지점 대비 도달한 최고 고도(월드 단위). 최고 기록은 PlayerPrefs에 저장.
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }
        static IScoreStore scoreStore = new PlayerPrefsScoreStore();
        static int uncertainBestCandidate;

        public int Height { get; private set; }
        public int Best { get; private set; }
        public int RunBestToBeat { get; private set; }
        public bool IsNewBestThisRun { get; private set; }
        public bool RecordsAllowed { get; private set; } = true;
        public int DisplayBest => Mathf.Max(Best, Height);

        /// 이전 최고 기록을 처음 넘어선 순간에만 한 판에 한 번 발생한다.
        public event Action<int, int> NewBestReached;

        Transform target;
        float startY;
        bool bestLoadValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            scoreStore = new PlayerPrefsScoreStore();
            uncertainBestCandidate = 0;
        }

        // OnEnable: Play 중 스크립트 재컴파일로 static이 초기화돼도 다시 할당된다
        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Awake()
        {
            try
            {
                Best = Mathf.Max(0, scoreStore.LoadBest());
                bestLoadValid = true;
            }
            catch (Exception exception)
            {
                Best = 0;
                bestLoadValid = false;
                Debug.LogWarning(
                    $"최고 기록을 읽지 못해 이번 세션은 0m부터 표시합니다: " +
                    exception.Message);
            }
            RunBestToBeat = Best;
        }

        void Start()
        {
            var player = FindFirstObjectByType<Player.PlayerController>();
            if (player != null)
            {
                target = player.transform;
                startY = target.position.y;
            }
        }

        void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameplayTicking)
                return;
            var livingPlayer = GameManager.Instance.HighestLivingPlayer;
            if (livingPlayer != null) target = livingPlayer.transform;
            if (target == null) return;
            SampleWorldHeight(target.position.y);
        }

        /// 물리 콜백에서 마지막 먹방울이가 죽은 프레임도 Update 샘플을 놓치지 않는다.
        public void SampleWorldHeight(float worldY)
        {
            Height = Mathf.Max(Height, Mathf.RoundToInt(worldY - startY));
            if (bestLoadValid && RecordsAllowed && !IsNewBestThisRun &&
                BeatsRecord(Height, RunBestToBeat))
            {
                IsNewBestThisRun = true;
                NewBestReached?.Invoke(Height, RunBestToBeat);
            }
        }

        public static bool BeatsRecord(int height, int record)
        {
            return height > 0 && height > record;
        }

        public void SaveBest()
        {
            TrySaveBest();
        }

        public bool TrySaveBest()
        {
            if (!RecordsAllowed)
                return true;

            if (!TryEnsureBestLoaded())
                return false;

            if (Height <= Best && uncertainBestCandidate <= 0)
                return true;

            int previousBest = Best;
            int candidate = Mathf.Max(
                Best,
                Mathf.Max(Height, uncertainBestCandidate));
            try
            {
                scoreStore.SaveBest(candidate);
                int persisted = Mathf.Max(0, scoreStore.LoadBest());
                if (persisted < candidate)
                    return false;
                Best = Mathf.Max(previousBest, persisted);
                uncertainBestCandidate = 0;
                return true;
            }
            catch (Exception exception)
            {
                // SetInt 뒤 flush 예외에서는 같은 프로세스 readback도 메모리 값일 수
                // 있어 내구 저장을 증명하지 못한다. 후보를 남겨 같은 값을 재저장한다.
                uncertainBestCandidate = Mathf.Max(
                    uncertainBestCandidate,
                    candidate);
                bestLoadValid = false;
                Best = previousBest;
                Debug.LogWarning(
                    $"최고 기록 저장에 실패했지만 결과 화면은 계속 표시합니다: " +
                    exception.Message);
                return false;
            }
        }

        public bool TryEnsureBestLoaded()
        {
            if (bestLoadValid)
                return true;
            try
            {
                Best = Mathf.Max(Best, Mathf.Max(0, scoreStore.LoadBest()));
                RunBestToBeat = Mathf.Max(RunBestToBeat, Best);
                IsNewBestThisRun = BeatsRecord(Height, RunBestToBeat);
                bestLoadValid = true;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"최고 기록 원본을 확인하지 못해 기록·보상 저장을 막았습니다: " +
                    exception.Message);
                return false;
            }
        }

        /// 모호한 flush 결과 자체를 되돌릴 수는 없으므로, 아직 메모리에만 남은
        /// 재시도 후보만 폐기한다. 이미 store에 반영된 단조 최고기록은 보존한다.
        public void StopPendingBestSaveRetry()
        {
            uncertainBestCandidate = 0;
        }

        /// 로비에서 선택한 시작 발판으로 이동한 직후 그 위치를 이번 도전의 0m로 삼는다.
        public void ResetOrigin(float worldY)
        {
            startY = worldY;
            Height = 0;
            RunBestToBeat = Best;
            IsNewBestThisRun = false;
            RecordsAllowed = true;
        }

        public float HeightAt(float worldY) => worldY - startY;

        public void DebugSetHeight(int height, Transform newTarget)
        {
            InvalidateCurrentRunForRecords();
            target = newTarget;
            Height = Mathf.Max(0, height);
            if (target != null)
                startY = target.position.y - Height;
        }

        /// 무적·아이템 지급·순간이동을 사용한 판은 로컬 최고 기록에 저장하지 않는다.
        public void InvalidateCurrentRunForRecords()
        {
            RecordsAllowed = false;
            IsNewBestThisRun = false;
        }

#if UNITY_EDITOR
        public static void UseStoreForTests(IScoreStore testStore)
        {
            scoreStore = testStore ?? new PlayerPrefsScoreStore();
            uncertainBestCandidate = 0;
        }

        public static void RestoreDefaultStoreForTests()
        {
            scoreStore = new PlayerPrefsScoreStore();
            uncertainBestCandidate = 0;
        }
#endif
    }

#if UNITY_EDITOR
    public sealed class MemoryScoreStore : IScoreStore
    {
        public int Best { get; set; }
        public bool ThrowOnLoad { get; set; }
        public bool ThrowOnSave { get; set; }
        public bool ApplyBeforeThrow { get; set; }
        public int SaveCount { get; private set; }

        public int LoadBest()
        {
            if (ThrowOnLoad)
                throw new InvalidOperationException("Injected score read failure");
            return Best;
        }

        public void SaveBest(int value)
        {
            if (ThrowOnSave)
            {
                if (ApplyBeforeThrow)
                    Best = Mathf.Max(0, value);
                throw new InvalidOperationException("Injected score write failure");
            }
            Best = Mathf.Max(0, value);
            SaveCount++;
        }
    }
#endif
}
