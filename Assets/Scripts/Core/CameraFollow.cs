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

        void Start()
        {
            highestY = transform.position.y;
        }

        void LateUpdate()
        {
            if (target == null) return;

            float desired = target.position.y + lookAhead;
            highestY = Mathf.Max(highestY, desired);

            var pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, highestY, smoothSpeed * Time.deltaTime);
            transform.position = pos;
        }
    }
}
