using UnityEngine;

namespace MukJump.Core.Pooling
{
    /// 짧은 피드백 연출이 공유하는 풀 요소. 필요한 렌더러만 지연 생성한다.
    [DisallowMultipleComponent]
    public sealed class TransientVfxElement : MonoBehaviour, IPoolableEntity
    {
        LineRenderer line;
        SpriteRenderer sprite;

        public LineRenderer UseLine()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            if (line == null) line = gameObject.AddComponent<LineRenderer>();
            if (sprite != null) sprite.enabled = false;
            line.enabled = true;
            return line;
        }

        public SpriteRenderer UseSprite()
        {
            if (sprite == null) sprite = GetComponent<SpriteRenderer>();
            if (sprite == null) sprite = gameObject.AddComponent<SpriteRenderer>();
            if (line != null) line.enabled = false;
            sprite.enabled = true;
            return sprite;
        }

        public void OnPoolAcquire()
        {
            RecoverRenderers();
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            if (line != null) line.enabled = false;
            if (sprite != null) sprite.enabled = false;
        }

        public void OnPoolRelease()
        {
            RecoverRenderers();
            if (line != null)
            {
                line.enabled = false;
                line.loop = false;
                line.positionCount = 0;
            }

            if (sprite != null)
            {
                sprite.enabled = false;
                sprite.sprite = null;
                sprite.color = Color.white;
            }

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            gameObject.name = "TransientVfxElement (Pooled)";
        }

        void RecoverRenderers()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        }
    }
}
