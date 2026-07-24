using UnityEngine;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;

namespace MukJump.Items
{
    public enum ItemType
    {
        InkDrop,
        GoldenBrush,
        InkShield,
        InkClone,
        InkReserve,
    }

    /// 실제 픽업과 테스트 버튼이 동일한 아이템 효과를 사용하도록 모아 둔 진입점.
    public static class ItemEffect
    {
        public static void Apply(ItemType type, PlayerController player = null)
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            player ??= GameManager.Instance.HighestLivingPlayer;
            if (player == null) return;
            GameFeedbackController.Instance?.PlayItemPickup(player.transform.position, type);

            switch (type)
            {
                case ItemType.InkDrop:
                    player.LaunchInkDrop(50f);
                    player.GetComponent<InkDropJumpVfx>()?.Play();
                    break;
                case ItemType.GoldenBrush:
                    Object.FindFirstObjectByType<StrokeCapture>()?.ActivateUnlimitedInk(8f);
                    break;
                case ItemType.InkShield:
                    player.GrantShield();
                    break;
                case ItemType.InkClone:
                    GameManager.Instance.TryCreateInkClone(player);
                    break;
                case ItemType.InkReserve:
                    Object.FindFirstObjectByType<StrokeCapture>()?.AddInkReserve(0.35f);
                    break;
            }
        }
    }

    /// 닿는 즉시 효과를 적용하는 아이템. 임시 비주얼은 종류별 색상으로 구분한다.
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField] ItemType type;
        [SerializeField] float bobAmount = 0.18f;
        [SerializeField] float bobSpeed = 2f;

        Vector3 origin;
        float phase;
        bool collected;

        public void Configure(ItemType itemType, float phaseOffset)
        {
            type = itemType;
            phase = phaseOffset;
            origin = transform.position;
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            transform.position = origin + Vector3.up *
                (Mathf.Sin(Time.time * bobSpeed + phase) * bobAmount);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (collected) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            collected = true;
            ItemEffect.Apply(type, player);
            Destroy(gameObject);
        }
    }
}
