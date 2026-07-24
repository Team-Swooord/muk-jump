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
            if (target == null) return;
            startY = worldY;
            Height = 0;
        }

        public float HeightAt(float worldY) => worldY - startY;
    }
}
