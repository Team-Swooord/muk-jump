using UnityEngine;
using MukJump.Core;

namespace MukJump.Player
{
    /// <summary>
    /// 캐릭터가 접지 상태일 때 일정 주기마다 자동으로 점프를 발생시킨다.
    /// 플레이어는 점프 자체를 조작하지 않고, 이 타이밍에 맞춰 발판을 그려 대비한다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class AutoJump : MonoBehaviour
    {
        [Tooltip("착지 후 다음 점프까지 대기 시간(초)")]
        [SerializeField] private float jumpInterval = 1.2f;

        [Tooltip("점프 임박 시 UI/이펙트에 활용할 수 있는 예고 구간(초)")]
        [SerializeField] private float telegraphWindow = 0.4f;

        private PlayerController player;
        private float timer;

        public float TimeUntilNextJump => Mathf.Max(0f, jumpInterval - timer);
        public bool IsTelegraphing => player != null && player.IsGrounded && TimeUntilNextJump <= telegraphWindow;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            player.OnLanded += HandleLanded;
        }

        private void OnDisable()
        {
            player.OnLanded -= HandleLanded;
        }

        private void HandleLanded()
        {
            timer = 0f;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameManager.GameState.Playing)
                return;

            if (!player.IsGrounded) return; // 공중에 떠 있는 동안은 타이머 정지

            timer += Time.deltaTime;
            if (timer >= jumpInterval)
            {
                if (player.TryPerformJump())
                {
                    timer = 0f;
                }
            }
        }
    }
}
