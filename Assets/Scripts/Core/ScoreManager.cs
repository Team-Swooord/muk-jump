using System;
using UnityEngine;

namespace MukJump.Core
{
    /// 점수 = 시작 지점 대비 도달한 최고 고도(월드 단위). 최고 기록은 PlayerPrefs에 저장.
    public class ScoreManager : MonoBehaviour
    {
        const string BestKey = "MukJump.BestHeight";

        public static ScoreManager Instance { get; private set; }

        public int Height { get; private set; }
        public int Best { get; private set; }
        public int RunBestToBeat { get; private set; }
        public bool IsNewBestThisRun { get; private set; }
        public int DisplayBest => Mathf.Max(Best, Height);

        /// 이전 최고 기록을 처음 넘어선 순간에만 한 판에 한 번 발생한다.
        public event Action<int, int> NewBestReached;

        Transform target;
        float startY;

        // OnEnable: Play 중 스크립트 재컴파일로 static이 초기화돼도 다시 할당된다
        void OnEnable()
        {
            Instance = this;
        }

        void Awake()
        {
            Best = PlayerPrefs.GetInt(BestKey, 0);
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
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;
            var livingPlayer = GameManager.Instance.HighestLivingPlayer;
            if (livingPlayer != null) target = livingPlayer.transform;
            if (target == null) return;
            Height = Mathf.Max(Height, Mathf.RoundToInt(target.position.y - startY));
            if (!IsNewBestThisRun && BeatsRecord(Height, RunBestToBeat))
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
            if (Height <= Best) return;
            Best = Height;
            PlayerPrefs.SetInt(BestKey, Best);
            PlayerPrefs.Save();
        }

        /// 로비에서 선택한 시작 발판으로 이동한 직후 그 위치를 이번 도전의 0m로 삼는다.
        public void ResetOrigin(float worldY)
        {
            startY = worldY;
            Height = 0;
            RunBestToBeat = Best;
            IsNewBestThisRun = false;
        }

        public float HeightAt(float worldY) => worldY - startY;

        public void DebugSetHeight(int height, Transform newTarget)
        {
            target = newTarget;
            Height = Mathf.Max(0, height);
            if (target != null)
                startY = target.position.y - Height;
            IsNewBestThisRun = BeatsRecord(Height, RunBestToBeat);
        }
    }
}
