using UnityEngine;
using UnityEngine.Events;

namespace MukJump.Core
{
    /// <summary>
    /// 도달 고도를 점수로 환산하고 하이스코어를 저장한다.
    /// 하이스코어 경쟁형 반복 도전 루프의 핵심.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        private const string HighScoreKey = "muk_jump_high_score";

        [Tooltip("월드 Y 1유닛당 점수 배율")]
        [SerializeField] private float scorePerUnit = 10f;

        [SerializeField] private UnityEvent<int> onScoreChanged;
        [SerializeField] private UnityEvent<int> onHighScoreChanged;

        public float StartY { get; private set; }
        public float MaxHeight { get; private set; }
        public int CurrentScore { get; private set; }
        public int HighScore { get; private set; }

        private void Awake()
        {
            HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        }

        public void ResetScore()
        {
            StartY = 0f;
            MaxHeight = 0f;
            CurrentScore = 0;
            onScoreChanged?.Invoke(CurrentScore);
        }

        /// <summary>
        /// 시작 기준점(StartY)을 실제 플레이어 스폰 위치로 맞추고 싶을 때 호출.
        /// </summary>
        public void SetStartY(float y)
        {
            StartY = y;
            MaxHeight = 0f;
        }

        public void ReportHeight(float worldY)
        {
            float height = worldY - StartY;
            if (height <= MaxHeight) return;

            MaxHeight = height;
            CurrentScore = Mathf.RoundToInt(MaxHeight * scorePerUnit);
            onScoreChanged?.Invoke(CurrentScore);

            if (CurrentScore > HighScore)
            {
                HighScore = CurrentScore;
                PlayerPrefs.SetInt(HighScoreKey, HighScore);
                onHighScoreChanged?.Invoke(HighScore);
            }
        }
    }
}
