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
        PlayerController player;
        InkSkinId displayed = (InkSkinId)(-1);
        public SpriteRenderer AccessoryRenderer => accessory;

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
        }

        void OnDisable() { if (accessory != null) accessory.enabled = false; }

        void LateUpdate()
        {
            if (body == null || accessory == null) return;
            InkSkinId id = RunGrowthController.Instance != null
                ? RunGrowthController.Instance.SkinSnapshot : InkSkinPreferences.Equipped;
            if (displayed != id)
            {
                displayed = id;
                string path = InkSkinCatalog.ResourcePath(id);
                accessory.sprite = path.Length == 0 ? null : Resources.Load<Sprite>(path);
            }
            accessory.enabled = body.enabled && body.sprite != null && accessory.sprite != null &&
                                (player == null || !player.IsDead);
            if (!accessory.enabled) return;
            Sprite frame = body.sprite;
            Vector2 frameSize = frame.rect.size / frame.pixelsPerUnit;
            Vector2 center = (frame.rect.size * 0.5f - frame.pivot) / frame.pixelsPerUnit;
            Vector2 offset = InkSkinCatalog.Offset(id) * frameSize;
            accessory.transform.localPosition = center + offset;
            float scale = frameSize.x * InkSkinCatalog.Width(id) / accessory.sprite.bounds.size.x;
            accessory.transform.localScale = Vector3.one * scale;
            accessory.transform.localRotation = Quaternion.identity;
            accessory.flipX = body.flipX;
            accessory.flipY = body.flipY;
            accessory.color = body.color;
            accessory.sortingLayerID = body.sortingLayerID;
            accessory.sortingOrder = body.sortingOrder + 1;
        }
    }
}
