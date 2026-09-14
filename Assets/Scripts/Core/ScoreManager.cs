using System;
using UnityEngine;

namespace MukJump.Core
{
    public interface IScoreStore
    {
        int LoadBest();
        int LoadBestTime(int height);
        void SaveBest(int value, int seconds = -1);
    }

    sealed class PlayerPrefsScoreStore : IScoreStore
    {
        const string BestKey = "MukJump.BestHeight";
        public const string TimeKey = "MukJump.BestRunRecord";

        public int LoadBest() => PlayerPrefs.GetInt(BestKey, 0);

        public int LoadBestTime(int height)
        {
            BestRunRecord record = BestRunRecord.Decode(PlayerPrefs.GetString(TimeKey, ""));
            return record.Height == height ? record.Seconds : -1;
        }

        public void SaveBest(int value, int seconds = -1)
        {
            PlayerPrefs.SetString(TimeKey, new BestRunRecord(value, seconds).Encode());
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
        static int uncertainBestSeconds = -1;

        public int Height { get; private set; }
        public int Best { get; private set; }
        public int BestTimeSeconds { get; private set; } = -1;
        public int HeightTimeSeconds { get; private set; } = -1;
        public BestRunRecord BestRecord => new(Best, BestTimeSeconds);
        public static BestRunRecord StoredRecord
        {
            get
            {
                int height = Mathf.Max(0, scoreStore.LoadBest());
                return new BestRunRecord(height, scoreStore.LoadBestTime(height));
            }
        }
        public int RunBestToBeat { get; private set; }
        public bool IsNewBestThisRun { get; private set; }
        public bool RecordsAllowed { get; private set; } = true;
        public int DisplayBest => Mathf.Max(Best, Height);
        public bool HasConfirmedBest => bestLoadValid;
        public bool HasPendingBestSaveRetry => uncertainBestCandidate > 0;

        /// 이전 최고 기록을 처음 넘어선 순간에만 한 판에 한 번 발생한다.
        public event Action<int, int> NewBestReached;
        /// 로컬 저장소 write/readback까지 끝난 최고 기록만 알린다.
        public event Action<int> BestCommitted;

        Transform target;
        float startY;
        bool bestLoadValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            scoreStore = new PlayerPrefsScoreStore();
            uncertainBestCandidate = 0;
            uncertainBestSeconds = -1;
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
#if UNITY_WEBGL && !UNITY_EDITOR
            // Apps in Toss에서는 현재 토스 사용자와 이 브라우저의 기록 소유권을
            // 대조하기 전까지 이전 사용자의 최고 기록을 읽거나 표시하지 않는다.
            if (!AppsInTossIdentityPolicy.HasVerifiedIdentity)
            {
                Best = 0;
                RunBestToBeat = 0;
                bestLoadValid = false;
                return;
            }
#endif
            try
            {
                Best = Mathf.Max(0, scoreStore.LoadBest());
                BestTimeSeconds = scoreStore.LoadBestTime(Best);
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
            var player = FindAnyObjectByType<Player.PlayerController>();
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
            int sampled = Mathf.Max(0, Mathf.RoundToInt(worldY - startY));
            if (sampled > Height)
            {
                Height = sampled;
                HeightTimeSeconds = GameManager.Instance != null
                    ? Mathf.FloorToInt(GameManager.Instance.ActiveGameplaySeconds) : -1;
            }
            if (bestLoadValid && RecordsAllowed && !IsNewBestThisRun &&
                BeatsRecord(Height, RunBestToBeat))
            {
                IsNewBestThisRun = true;
                NotifyNewBestReached(Height, RunBestToBeat);
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

        public bool TrySaveBest() => TryCommitBestCandidate(Height);

        /// 광고 선택이나 성장 정산보다 먼저 현재 판의 후보를 내구 저장한다.
        /// 판의 Height·RunBestToBeat는 건드리지 않아 부활 뒤 같은 판을 이어갈 수 있다.
        public bool TryCommitBestCandidate(int candidateHeight)
        {
            if (!RecordsAllowed)
                return true;

            if (!TryEnsureBestLoaded())
                return false;

            int requestedCandidate = Mathf.Max(0, candidateHeight);
            BestRunRecord requested = new(requestedCandidate,
                requestedCandidate == Height ? HeightTimeSeconds : -1);
            if (!requested.IsBetterThan(BestRecord) && uncertainBestCandidate <= 0)
                return true;

            int previousBest = Best;
            int previousSeconds = BestTimeSeconds;
            bool wasPendingRetry = uncertainBestCandidate > 0;
            BestRunRecord selected = BestRunRecord.Better(BestRecord,
                BestRunRecord.Better(requested, new BestRunRecord(uncertainBestCandidate, uncertainBestSeconds)));
            int candidate = selected.Height;
            try
            {
                scoreStore.SaveBest(candidate, selected.Seconds);
                int persisted = Mathf.Max(0, scoreStore.LoadBest());
                BestRunRecord persistedRecord = new(persisted, scoreStore.LoadBestTime(persisted));
                if (selected.IsBetterThan(persistedRecord))
                {
                    // 저장 API가 예외 없이 끝나도 readback이 후보보다 낮으면
                    // 내구 저장을 확인한 것이 아니다. 다음 재시도까지 후보를 보존한다.
                    uncertainBestCandidate = Mathf.Max(
                        uncertainBestCandidate,
                        candidate);
                    uncertainBestSeconds = selected.Seconds;
                    return false;
                }
                Best = Mathf.Max(previousBest, persisted);
                BestTimeSeconds = persistedRecord.Seconds;
                uncertainBestCandidate = 0;
                uncertainBestSeconds = -1;
                NotifyBestCommittedIfChanged(previousBest, wasPendingRetry || previousSeconds != BestTimeSeconds);
                return true;
            }
            catch (Exception exception)
            {
                // SetInt 뒤 flush 예외에서는 같은 프로세스 readback도 메모리 값일 수
                // 있어 내구 저장을 증명하지 못한다. 후보를 남겨 같은 값을 재저장한다.
                uncertainBestCandidate = Mathf.Max(
                    uncertainBestCandidate,
                    candidate);
                uncertainBestSeconds = selected.Seconds;
                bestLoadValid = false;
                Best = previousBest;
                BestTimeSeconds = previousSeconds;
                Debug.LogWarning(
                    $"최고 기록 저장에 실패했지만 결과 화면은 계속 표시합니다: " +
                    exception.Message);
                return false;
            }
        }

        void NotifyBestCommittedIfChanged(
            int previousBest,
            bool forceNotification = false)
        {
            if (!forceNotification && Best == previousBest)
                return;
            Action<int> listeners = BestCommitted;
            if (listeners != null)
            {
                foreach (Action<int> listener in listeners.GetInvocationList())
                {
                    try
                    {
                        listener(Best);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[MukJump] 최고 기록 완료 알림 구독자 예외를 격리했습니다: " +
                            exception.Message,
                            this);
                    }
                }
            }

            try
            {
                MukJumpAccountRuntime.Instance?.NotifyBestCommitted(Best);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 최고 기록 외부 동기화 알림 예외를 격리했습니다: " +
                    exception.Message,
                    this);
            }
        }

        void NotifyNewBestReached(int height, int previousBest)
        {
            Action<int, int> listeners = NewBestReached;
            if (listeners == null)
                return;

            foreach (Action<int, int> listener in listeners.GetInvocationList())
            {
                try
                {
                    listener(height, previousBest);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[MukJump] 신기록 알림 구독자 예외를 격리했습니다: " +
                        exception.Message,
                        this);
                }
            }
        }

        public bool TryEnsureBestLoaded()
        {
            if (bestLoadValid)
                return true;
            try
            {
                int previousBest = Best;
                int previousSeconds = BestTimeSeconds;
                BestRunRecord loaded = BestRunRecord.Better(BestRecord, StoredRecord);
                Best = loaded.Height;
                BestTimeSeconds = loaded.Seconds;
                bestLoadValid = true;
                // flush 예외 뒤 PlayerPrefs 메모리 readback은 내구 저장 증거가 아니다.
                // 보류 후보가 있으면 실제 재쓰기 성공 때만 완료 이벤트를 보낸다.
                if (!HasPendingBestSaveRetry)
                    NotifyBestCommittedIfChanged(previousBest, previousSeconds != BestTimeSeconds);
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

        /// 새 판의 기준 기록은 읽기 성공뿐 아니라 이전 판에서 남은 모호한
        /// 저장 후보의 재쓰기·readback까지 확인된 뒤에만 확정한다.
        public bool TryPrepareRunBaseline()
        {
            if (!TryEnsureBestLoaded())
                return false;
            if (HasPendingBestSaveRetry &&
                !TryCommitBestCandidate(Best))
                return false;
            return bestLoadValid && !HasPendingBestSaveRetry;
        }

        /// 모호한 flush 결과 자체를 되돌릴 수는 없으므로, 아직 메모리에만 남은
        /// 재시도 후보만 폐기한다. 이미 store에 반영된 단조 최고기록은 보존한다.
        public void StopPendingBestSaveRetry()
        {
            uncertainBestCandidate = 0;
        }

        /// 서버 동기화에서 확인된 최고 기록을 로컬의 단조 기록에 합친다.
        /// 낮은 서버 값이나 손상된 음수 값은 기존 기록을 낮추지 못한다.
        public bool TryMergeVerifiedBest(int verifiedBest, int verifiedSeconds = -1)
        {
            if (!TryEnsureBestLoaded())
                return false;

            BestRunRecord selected = BestRunRecord.Better(BestRecord, new BestRunRecord(verifiedBest, verifiedSeconds));
            int candidate = selected.Height;
            if (!selected.IsBetterThan(BestRecord))
                return !HasPendingBestSaveRetry ||
                       TryCommitBestCandidate(candidate);

            int previousHeight = Height;
            int previousBest = Best;
            try
            {
                scoreStore.SaveBest(candidate, selected.Seconds);
                int persisted = Mathf.Max(0, scoreStore.LoadBest());
                BestRunRecord persistedRecord = new(persisted, scoreStore.LoadBestTime(persisted));
                if (selected.IsBetterThan(persistedRecord))
                    return false;
                Best = Mathf.Max(previousBest, persisted);
                BestTimeSeconds = persistedRecord.Seconds;
                NotifyBestCommittedIfChanged(previousBest, true);
                return true;
            }
            catch (Exception exception)
            {
                Best = previousBest;
                Height = previousHeight;
                bestLoadValid = false;
                Debug.LogWarning(
                    $"검증된 최고 기록을 로컬에 합치지 못했습니다: {exception.Message}");
                return false;
            }
        }

        /// 씬의 ScoreManager가 아직 생성되기 전에도 서버 최고 기록을 안전하게
        /// 로컬 저장소에 합친다. BeforeSceneLoad 계정 동기화에서 사용한다.
        public static bool TryMergeVerifiedBestIntoStore(int verifiedBest, int verifiedSeconds = -1)
        {
            try
            {
                BestRunRecord selected = BestRunRecord.Better(StoredRecord, new BestRunRecord(verifiedBest, verifiedSeconds));
                scoreStore.SaveBest(selected.Height, selected.Seconds);
                return !selected.IsBetterThan(StoredRecord);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"검증된 최고 기록을 로컬 저장소에 합치지 못했습니다: {exception.Message}");
                return false;
            }
        }

        /// 계정을 명시적으로 전환할 때만 사용한다. 다른 계정의 로컬 최고 기록이
        /// 새 계정에 섞이지 않도록 서버에서 검증한 값으로 정확히 교체한다.
        public static bool TryReplaceVerifiedBestForAccountSwitch(
            int verifiedBest, int verifiedSeconds = -1)
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.State != GameState.Lobby)
            {
                Debug.LogWarning(
                    "진행 중인 판에서는 계정 전환 최고 기록을 교체하지 않습니다.");
                return false;
            }
            try
            {
                int candidate = Mathf.Max(0, verifiedBest);
                BestRunRecord replacement = new(candidate, verifiedSeconds);
                scoreStore.SaveBest(candidate, replacement.Seconds);
                if (StoredRecord.RankScore != replacement.RankScore)
                    return false;

                uncertainBestCandidate = 0;
                if (Instance != null)
                {
                    int previousBest = Instance.Best;
                    Instance.Best = candidate;
                    Instance.BestTimeSeconds = replacement.Seconds;
                    Instance.HeightTimeSeconds = -1;
                    Instance.Height = 0;
                    Instance.RunBestToBeat = candidate;
                    Instance.IsNewBestThisRun = false;
                    Instance.bestLoadValid = true;
                    Instance.NotifyBestCommittedIfChanged(previousBest);
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"계정 전환 최고 기록을 교체하지 못했습니다: {exception.Message}");
                return false;
            }
        }

        /// 회원 탈퇴 완료 뒤 계정에 연결됐던 로컬 최고 기록도 제거한다.
        public static bool TryClearForAccountDeletion()
        {
            try
            {
                scoreStore.SaveBest(0);
                if (scoreStore.LoadBest() != 0)
                    return false;
                uncertainBestCandidate = 0;
                if (Instance != null)
                {
                    int previousBest = Instance.Best;
                    Instance.Best = 0;
                    Instance.BestTimeSeconds = -1;
                    Instance.HeightTimeSeconds = -1;
                    Instance.Height = 0;
                    Instance.RunBestToBeat = 0;
                    Instance.IsNewBestThisRun = false;
                    Instance.bestLoadValid = true;
                    Instance.NotifyBestCommittedIfChanged(previousBest);
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"회원 탈퇴 후 최고 기록 삭제에 실패했습니다: {exception.Message}");
                return false;
            }
        }

        /// Apps in Toss 사용자 식별이 비동기로 끝난 뒤, 시작 전에 가려 두었던
        /// 최고 기록을 같은 소유자의 저장소에서 다시 읽는다.
        public static bool TryReloadAfterAppsInTossIdentity()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!AppsInTossIdentityPolicy.HasVerifiedIdentity)
                return false;
#endif
            if (GameManager.Instance != null &&
                GameManager.Instance.State != GameState.Lobby)
                return false;

            try
            {
                int persisted = Mathf.Max(0, scoreStore.LoadBest());
                uncertainBestCandidate = 0;
                if (Instance != null)
                {
                    int previousBest = Instance.Best;
                    Instance.Best = persisted;
                    Instance.BestTimeSeconds = scoreStore.LoadBestTime(persisted);
                    Instance.HeightTimeSeconds = -1;
                    Instance.Height = 0;
                    Instance.RunBestToBeat = persisted;
                    Instance.IsNewBestThisRun = false;
                    Instance.RecordsAllowed = true;
                    Instance.bestLoadValid = true;
                    Instance.NotifyBestCommittedIfChanged(
                        previousBest,
                        forceNotification: true);
                }
                return true;
            }
            catch (Exception exception)
            {
                if (Instance != null)
                    Instance.bestLoadValid = false;
                Debug.LogWarning(
                    "토스 사용자 확인 뒤 최고 기록을 다시 읽지 못했습니다: " +
                    exception.Message);
                return false;
            }
        }

        /// 로비에서 선택한 시작 발판으로 이동한 직후 그 위치를 이번 도전의 0m로 삼는다.
        public void ResetOrigin(float worldY)
        {
            startY = worldY;
            Height = 0;
            HeightTimeSeconds = -1;
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
            MukJumpAnalytics.ExcludeDebugRun();
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
        public int Seconds { get; set; } = -1;
        public int? ForcedLoadBest { get; set; }
        public bool ThrowOnLoad { get; set; }
        public bool ThrowOnSave { get; set; }
        public bool ApplyBeforeThrow { get; set; }
        public int SaveCount { get; private set; }

        public int LoadBest()
        {
            if (ThrowOnLoad)
                throw new InvalidOperationException("Injected score read failure");
            return ForcedLoadBest ?? Best;
        }

        public int LoadBestTime(int height) => height == Best ? Seconds : -1;

        public void SaveBest(int value, int seconds = -1)
        {
            if (ThrowOnSave)
            {
                if (ApplyBeforeThrow)
                {
                    Best = Mathf.Max(0, value);
                    Seconds = seconds;
                }
                throw new InvalidOperationException("Injected score write failure");
            }
            Best = Mathf.Max(0, value);
            Seconds = seconds;
            SaveCount++;
        }
    }
#endif
}
