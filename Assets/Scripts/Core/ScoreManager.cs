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

        void Awake()
        {
            Instance = this;
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
            if (target == null || GameManager.Instance.State != GameState.Playing) return;
            Height = Mathf.Max(Height, Mathf.RoundToInt(target.position.y - startY));
        }

        public void SaveBest()
        {
            if (Height <= Best) return;
            Best = Height;
            PlayerPrefs.SetInt(BestKey, Best);
            PlayerPrefs.Save();
        }
    }
}
