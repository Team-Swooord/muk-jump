using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;
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
        /// 효과가 실제로 적용됐을 때만 true를 반환한다. 물리 콜백 순서상 같은 프레임에
        /// 사망한 플레이어나 필수 시스템이 없는 경우 픽업을 소비하지 않는다.
        public static bool Apply(ItemType type, PlayerController player = null)
        {
            var manager = GameManager.Instance;
            if (manager == null || !manager.IsGameplayTicking)
                return false;

            player ??= manager.HighestLivingPlayer;
            if (player == null || player.IsDead) return false;

            switch (type)
            {
                case ItemType.InkDrop:
                    player.LaunchInkDrop(50f);
                    player.GetComponent<InkDropJumpVfx>()?.Play();
                    break;
                case ItemType.GoldenBrush:
                    var strokeCapture =
                        UnityEngine.Object.FindFirstObjectByType<StrokeCapture>();
                    if (strokeCapture == null) return false;
                    strokeCapture.ActivateUnlimitedInk(8f);
                    player.GetComponent<ItemEffectView>()?
                        .RequestSharedGoldenBrush(strokeCapture);
                    break;
                case ItemType.InkShield:
                    player.GrantShield();
                    break;
                case ItemType.InkClone:
                    if (!manager.TryCreateInkClone(player)) return false;
                    break;
                case ItemType.InkReserve:
                    var reserveTarget =
                        UnityEngine.Object.FindFirstObjectByType<StrokeCapture>();
                    if (reserveTarget == null) return false;
                    reserveTarget.AddInkReserve(0.35f);
                    break;
                default:
                    return false;
            }

            GameFeedbackController.Instance?.PlayItemPickup(player.transform.position, type);
            return true;
        }
    }

    /// 닿는 즉시 효과를 적용하는 아이템. 임시 비주얼은 종류별 색상으로 구분한다.
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class ItemPickup : MonoBehaviour, IPoolableEntity
    {
        [SerializeField] ItemType type;
        [SerializeField] float bobAmount = 0.18f;
        [SerializeField] float bobSpeed = 2f;

        Vector3 origin;
        Vector3 baseScale;
        float phase;
        float telegraphTime;
        bool collected;
        bool telegraphed;
        Camera worldCamera;
        SpriteRenderer spriteRenderer;
        CircleCollider2D trigger;

        /// 획득된 아이템을 Destroy하지 않고 소유 스포너가 명시적으로 반납한다.
        public event System.Action<ItemPickup> ReleaseRequested;

        void Awake()
        {
            EnsureComponents();
        }

        public void Configure(ItemType itemType, float phaseOffset)
        {
            EnsureComponents();
            type = itemType;
            phase = phaseOffset;
            origin = transform.position;
            baseScale = transform.localScale;
            collected = false;
            telegraphed = false;
            telegraphTime = 0f;
            transform.localScale = baseScale * 0.86f;
            worldCamera = Camera.main;
            spriteRenderer.enabled = true;
            trigger.enabled = true;
        }

        void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameplayTicking)
                return;

            transform.position = origin + Vector3.up *
                (Mathf.Sin(Time.time * bobSpeed + phase) * bobAmount);

            if (!telegraphed && worldCamera != null)
            {
                Vector3 viewport = worldCamera.WorldToViewportPoint(transform.position);
                if (viewport.z > 0f && viewport.y is >= 0.78f and <= 1.06f)
                {
                    telegraphed = true;
                    telegraphTime = 0.38f;
                    GameFeedbackController.Instance?.PlayItemTelegraph(transform.position, type);
                }
            }

            if (!telegraphed) return;
            if (telegraphTime <= 0f)
            {
                transform.localScale = baseScale;
                return;
            }

            telegraphTime -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(telegraphTime / 0.38f);
            float scale = t < 0.55f
                ? Mathf.Lerp(0.86f, 1.09f, Smooth01(t / 0.55f))
                : Mathf.Lerp(1.09f, 1f, Smooth01((t - 0.55f) / 0.45f));
            transform.localScale = baseScale * scale;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (collected) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            if (!ItemEffect.Apply(type, player)) return;

            collected = true;
            trigger.enabled = false;
            if (ReleaseRequested != null)
                ReleaseRequested.Invoke(this);
            else
                Destroy(gameObject);
        }

        public void OnPoolAcquire()
        {
            EnsureComponents();
            collected = false;
            telegraphed = false;
            telegraphTime = 0f;
            phase = 0f;
            worldCamera = Camera.main;
            spriteRenderer.enabled = true;
            trigger.enabled = true;
        }

        public void OnPoolRelease()
        {
            EnsureComponents();
            collected = true;
            telegraphed = false;
            telegraphTime = 0f;
            transform.localScale = baseScale == Vector3.zero ? Vector3.one : baseScale;
            spriteRenderer.enabled = false;
            trigger.enabled = false;
        }

        void EnsureComponents()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (trigger == null) trigger = GetComponent<CircleCollider2D>();
        }
    }
}
