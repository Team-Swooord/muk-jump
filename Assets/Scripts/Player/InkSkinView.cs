using MukJump.Core;
using UnityEngine;

namespace MukJump.Player
{
    /// 몸체 프레임·물리 크기를 바꾸지 않고 장식 렌더러 한 개만 포개어 재사용한다.
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(150)]
    public sealed class InkSkinView : MonoBehaviour
    {
        const string AccessoryName = "InkSkinAccessory";
        SpriteRenderer body;
        SpriteRenderer accessory;
        SpriteRenderer rightShoe;
        float motionTime;
        PlayerController player;
        InkSkinId displayed = (InkSkinId)(-1);
        public SpriteRenderer AccessoryRenderer => accessory;
        public SpriteRenderer RightShoeRenderer => rightShoe;

        void OnEnable()
        {
            body = GetComponent<SpriteRenderer>();
            player = GetComponent<PlayerController>();
            // 분신 복제/재컴파일에서도 자신의 자식만 다시 연결한다.
            accessory = transform.Find(AccessoryName)?.GetComponent<SpriteRenderer>();
            if (accessory == null)
            {
                accessory = new GameObject(AccessoryName, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                accessory.transform.SetParent(transform, false);
            }
            displayed = (InkSkinId)(-1);
            accessory.enabled = false;
            rightShoe=transform.Find("InkSkinRightShoe")?.GetComponent<SpriteRenderer>();
            if(rightShoe==null)
            {
                rightShoe=new GameObject("InkSkinRightShoe",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                rightShoe.transform.SetParent(transform,false);
            }
            rightShoe.enabled=false;
        }

        void OnDisable() { if (accessory != null) accessory.enabled = false; if(rightShoe!=null) rightShoe.enabled=false; }

        void LateUpdate()
        {
            if (body == null || accessory == null) return;
            accessory.forceRenderingOff = false;
            InkSkinId id = RunGrowthController.Instance != null
                ? RunGrowthController.Instance.SkinSnapshot : InkSkinPreferences.Equipped;
            if (displayed != id)
            {
                displayed = id;
                accessory.sprite = InkSkinCatalog.IsFootwear(id) ? InkFootwearPose.Part(id,false) : InkSkinCatalog.Sprite(id);
                rightShoe.sprite = InkSkinCatalog.IsFootwear(id) ? InkFootwearPose.Part(id,true) : null;
            }
            accessory.enabled = body.enabled && body.sprite != null && accessory.sprite != null &&
                                (player == null || !player.IsDead);
            rightShoe.enabled=accessory.enabled && InkSkinCatalog.IsFootwear(id);
            if (!accessory.enabled) return;
            Sprite frame = body.sprite;
            Vector2 frameSize = frame.rect.size / frame.pixelsPerUnit;
            Vector2 center = (frame.rect.size * 0.5f - frame.pivot) / frame.pixelsPerUnit;
            if(InkSkinCatalog.IsFootwear(id))
            {
                ApplyShoe(accessory,id,frame,frameSize,center,false);
                ApplyShoe(rightShoe,id,frame,frameSize,center,true);
                return;
            }
            // 프레임은 장착할 때 캐시하고, 화면에 보이는 동안만 시계를 진행한다.
            if (InkAccessoryMotion.Supports(id))
            {
                if (body.isVisible && MobileApplicationLifecycle.IsApplicationActive && Time.timeScale > 0f &&
                    (GameManager.Instance == null || !GameManager.Instance.IsPaused))
                    motionTime += Mathf.Min(.05f, Time.deltaTime);
                accessory.sprite = InkAccessoryMotion.Strength > 0f
                    ? InkAccessoryMotion.Sample(id, motionTime + Mathf.Abs(GetEntityId().GetHashCode() % 19) * .17f)
                        ?? InkSkinCatalog.Sprite(id)
                    : InkSkinCatalog.Sprite(id);
            }
            Vector2 offset = InkSkinCatalog.Offset(id) * frameSize;
            // 매듭 때문에 비대칭인 머리띠도 방향 반전 시 띠 중심이 먹의 이마에 남는다.
            if (body.flipX) offset.x = -offset.x;
            accessory.transform.localPosition = center + offset;
            float scale = frameSize.x * InkSkinCatalog.Width(id) / accessory.sprite.bounds.size.x;
            accessory.transform.localScale = Vector3.one * scale;
            accessory.transform.localRotation = Quaternion.identity;
            accessory.flipX = body.flipX;
            accessory.flipY = body.flipY;
            accessory.color = body.color;
            accessory.sortingLayerID = body.sortingLayerID;
            accessory.sortingOrder = body.sortingOrder + (InkSkinCatalog.BehindBody(id) ? -1 : 1);
        }
        void ApplyShoe(SpriteRenderer shoe,InkSkinId id,Sprite frame,Vector2 frameSize,Vector2 center,bool right)
        {
            var anchor=InkFootwearPose.Anchor(frame,right)*frameSize;
            if(body.flipX) anchor.x=-anchor.x; if(body.flipY) anchor.y=-anchor.y;
            shoe.transform.localPosition=center+anchor;
            float scale=frameSize.x*InkFootwearPose.ArtScale*.5f/shoe.sprite.bounds.size.x;
            shoe.transform.localScale=Vector3.one*scale; shoe.transform.localRotation=Quaternion.identity;
            shoe.flipX=body.flipX; shoe.flipY=body.flipY; shoe.color=body.color;
            shoe.sortingLayerID=body.sortingLayerID; shoe.sortingOrder=body.sortingOrder+1;
        }
    }
}
