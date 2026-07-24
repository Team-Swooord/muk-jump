using UnityEngine;

namespace MukJump.Core
{
    /// 클라이밍 게임 카메라: 플레이어를 따라 위로만 올라가고, 절대 내려오지 않는다.
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [Tooltip("플레이어를 화면 중앙보다 얼마나 아래에 둘지 (월드 단위)")]
        [SerializeField] float lookAhead = 3f;
        [SerializeField] float smoothSpeed = 4f;

        float highestY;

        public void SetTarget(Transform t) => target = t;

        public void DebugSnapTo(Transform t)
        {
            if (t == null) return;
            target = t;
            highestY = t.position.y + lookAhead;
            var position = transform.position;
            position.y = highestY;
            transform.position = position;
        }

        void Start()
        {
            highestY = transform.position.y;
        }

        void LateUpdate()
        {
            var livingPlayer = GameManager.Instance != null
                ? GameManager.Instance.HighestLivingPlayer
                : null;
            if (livingPlayer != null) target = livingPlayer.transform;
            if (target == null) return;

            // 사망 팝 연출을 따라 카메라까지 올라가면 게임오버 배경과 다음 도전의 기준점이
            // 흔들린다. 마지막 플레이 위치에서 카메라를 고정해 죽음 연출만 화면 안에서 보인다.
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
                return;

            float desired = target.position.y + lookAhead;
            highestY = Mathf.Max(highestY, desired);

            var pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, highestY, smoothSpeed * Time.deltaTime);
            transform.position = pos;
        }
    }
}
