using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;

namespace MukJump.Items
{
    /// 카메라 위쪽에 아이템을 일정 간격으로 미리 생성하고 지나간 아이템을 정리한다.
    public class ItemSpawner : MonoBehaviour
    {
        [SerializeField] Sprite placeholderSprite;
        [Tooltip("먹물방울 정식 스프라이트. 비어 있으면 placeholderSprite를 사용한다.")]
        [SerializeField] Sprite inkDropSprite;
        [Tooltip("황금 붓 스프라이트. 비어 있으면 placeholderSprite를 사용한다.")]
        [SerializeField] Sprite goldenBrushSprite;
        [Tooltip("먹 방어막 스프라이트. 비어 있으면 placeholderSprite를 사용한다.")]
        [SerializeField] Sprite inkShieldSprite;
        [Tooltip("먹분신 스프라이트. 비어 있으면 placeholderSprite를 사용한다.")]
        [SerializeField] Sprite inkCloneSprite;
        [SerializeField] Vector2 verticalSpacing = new(15f, 25f);
        [SerializeField] Vector2 horizontalRange = new(-4f, 4f);
        [SerializeField] float firstSpawnHeight = 12f;
        [SerializeField] float spawnAhead = 12f;
        [SerializeField] float despawnBelow = 10f;
        // 씬에 저장된 예전 직렬화 값과 무관하게 네 아이템의 크기를 항상 동일하게 유지한다.
        const float ItemWorldWidth = 0.9f;

        readonly List<ItemPickup> active = new();
        Camera cam;
        float nextSpawnY;

        void Start()
        {
            cam = Camera.main;
            nextSpawnY = firstSpawnHeight;
        }

        void Update()
        {
            if (cam == null || placeholderSprite == null || GameManager.Instance == null ||
                GameManager.Instance.State != GameState.Playing) return;

            float cameraTop = cam.transform.position.y + cam.orthographicSize;
            while (nextSpawnY <= cameraTop + spawnAhead)
            {
                Spawn(nextSpawnY);
                nextSpawnY += Random.Range(verticalSpacing.x, verticalSpacing.y);
            }

            float cutoff = cam.transform.position.y - cam.orthographicSize - despawnBelow;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i] == null)
                {
                    active.RemoveAt(i);
                    continue;
                }
                if (active[i].transform.position.y >= cutoff) continue;
                Destroy(active[i].gameObject);
                active.RemoveAt(i);
            }
        }

        void Spawn(float y)
        {
            var type = (ItemType)Random.Range(0, 4);
            Sprite sprite = SpriteFor(type);
            bool usesDedicatedSprite = sprite != placeholderSprite;
            var go = new GameObject($"Item_{type}")
            {
                layer = LayerMask.NameToLayer("Item"),
            };
            go.transform.position = new Vector3(Random.Range(horizontalRange.x, horizontalRange.y), y, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 4;
            renderer.color = usesDedicatedSprite ? Color.white : ColorFor(type);
            float width = sprite.bounds.size.x;
            go.transform.localScale = Vector3.one * (width > 0f ? ItemWorldWidth / width : 1f);

            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Min(sprite.bounds.extents.x, sprite.bounds.extents.y) * 0.72f;

            var pickup = go.AddComponent<ItemPickup>();
            pickup.Configure(type, Random.Range(0f, Mathf.PI * 2f));
            active.Add(pickup);
        }

        Sprite SpriteFor(ItemType type)
        {
            return type switch
            {
                ItemType.InkDrop when inkDropSprite != null => inkDropSprite,
                ItemType.GoldenBrush when goldenBrushSprite != null => goldenBrushSprite,
                ItemType.InkShield when inkShieldSprite != null => inkShieldSprite,
                ItemType.InkClone when inkCloneSprite != null => inkCloneSprite,
                _ => placeholderSprite,
            };
        }

        static Color ColorFor(ItemType type)
        {
            return type switch
            {
                ItemType.InkDrop => new Color(0.42f, 0.62f, 0.72f),
                ItemType.GoldenBrush => new Color(0.95f, 0.72f, 0.2f),
                ItemType.InkShield => new Color(0.72f, 0.18f, 0.28f),
                _ => new Color(0.2f, 0.18f, 0.16f),
            };
        }
    }
}
