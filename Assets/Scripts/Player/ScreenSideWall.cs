using UnityEngine;

namespace MukJump.Player
{
    /// 플레이어가 화면 경계 충돌만 구분하기 위한 표식 컴포넌트.
    public class ScreenSideWall : MonoBehaviour
    {
        [SerializeField] ScreenSideWalls owner;
        [SerializeField] bool isLeft;

        public ScreenSideWalls Owner => owner;
        public bool IsLeft => isLeft;

        public void Initialize(ScreenSideWalls newOwner, bool left)
        {
            owner = newOwner;
            isLeft = left;
        }
    }
}
