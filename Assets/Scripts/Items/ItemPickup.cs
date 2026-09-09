using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Drawing;
using MukJump.Player;

namespace MukJump.Items
{
    public enum ItemType
    {
        InkDrop = 0,
        GoldenBrush = 1,
        InkShield = 2,
        InkClone = 3,
        // 구형 씬 직렬화 호환용 폐기 번호. 다시 사용하거나 스폰하지 않는다.
        InkReserve = 4,
    }

    /// 원화가 없는 픽업과 획득 먹획에 같은 종류별 안료를 사용한다.
    public static class ItemFeedbackPalette
    {
        public static Color For(ItemType type) => type switch
        {
            ItemType.InkDrop => InkPalette.WindPlatform,
            ItemType.GoldenBrush => InkPalette.Gold,
            ItemType.InkShield => InkPalette.Ink,
            _ => InkPalette.Ink,
        };
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
                    if (!manager.LaunchSwarmInkDrop(player, 50f, out var feedbackPlayer))
                        return false;
                    player.ArmInkDropEndShield();
                    var launchVfx = feedbackPlayer.GetComponent<InkDropJumpVfx>();
                    if (launchVfx != null && launchVfx.TryPlay())
                    {
                        // 합성 먹기둥과 전용 소리가 이미 획득을 설명한다.
                        return true;
                    }
                    GameFeedbackController.Instance?.PlayItemPickup(feedbackPlayer.transform.position, type);
                    return true;
                case ItemType.GoldenBrush:
                    var strokeCapture =
                        UnityEngine.Object.FindAnyObjectByType<StrokeCapture>();
                    if (strokeCapture == null) return false;
                    strokeCapture.ActivateUnlimitedInk(8f);
                    PermanentGrowthRunSnapshot snapshot =
                        RunGrowthController.Instance != null
                            ? RunGrowthController.Instance.PermanentSnapshot
                            : PermanentGrowthProfile.CreateRunSnapshot();
                    if (snapshot.HasGoldenBrushShield)
                        player.TryGrantShield();
                    break;
                case ItemType.InkShield:
                    if (!player.TryGrantShield())
                        return false;
                    var shieldView = player.GetComponent<ItemEffectView>();
                    if (shieldView != null && shieldView.isActiveAndEnabled)
                        return true; // 로컬 등장 고리와 획득음 한 번만 사용한다.
                    break;
                case ItemType.InkClone:
                    if (!manager.TryCreateInkClonesFromItem(player)) return false;
                    break;
                case ItemType.InkReserve:
                    return false;
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
        bool collectionAnimating;
        float collectionTime;
        Vector3 collectionOrigin;
        Vector3 collectionScale;
        Color baseColor = Color.white;
        Transform collector;
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
            baseColor = spriteRenderer.color;
            collectionAnimating = false;
            collector = null;
            collected = false;
            telegraphed = false;
            telegraphTime = 0f;
            transform.localScale = baseScale *
                (LobbySettingsProfile.ReducedMotionEnabled ? 1f : 0.86f);
            worldCamera = Camera.main;
            spriteRenderer.enabled = true;
            trigger.enabled = true;
        }

        void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameplayTicking)
                return;

            if (collected)
            {
                AdvanceCollection(Time.deltaTime);
                return;
            }

            transform.position = origin + Vector3.up *
                (LobbySettingsProfile.ReducedMotionEnabled ? 0f :
                    Mathf.Sin(Time.time * bobSpeed + phase) * bobAmount);

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
            transform.localScale = baseScale *
                (LobbySettingsProfile.ReducedMotionEnabled ? 1f : scale);
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
            MukJumpAnalytics.Item(type);
            trigger.enabled = false;
            BeginCollection(player.transform);
        }

        void BeginCollection(Transform target)
        {
            collector = target;
            collectionOrigin = transform.position;
            collectionScale = transform.localScale;
            collectionTime = 0f;
            collectionAnimating = true;
        }

        /// 효과 적용과 충돌 해제는 즉시, 원화의 흡수만 180ms 동안 이어 준다.
        /// 기존 픽업 렌더러를 재사용하므로 분신 동시 획득에도 새 객체가 생기지 않는다.
        void AdvanceCollection(float deltaTime)
        {
            if (!collectionAnimating) return;
            bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
            collectionTime += Mathf.Max(0f, deltaTime);
            float t = Mathf.Clamp01(collectionTime / (reduced ? 0.1f : 0.18f));
            if (!reduced)
            {
                float absorb = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1f, t));
                float scale = t < 0.28f
                    ? Mathf.Lerp(1f, 1.06f, t / 0.28f)
                    : Mathf.Lerp(1.06f, 0.2f, absorb);
                transform.localScale = collectionScale * scale;
                // 50m 급상승 중에도 화면을 가로지르는 긴 흡수 궤적은 만들지 않는다.
                Vector3 offset = collector != null
                    ? Vector3.ClampMagnitude(collector.position - collectionOrigin, 0.7f)
                    : Vector3.up * 0.2f;
                transform.position = collectionOrigin + offset * absorb;
            }
            Color color = baseColor;
            color.a *= 1f - Mathf.SmoothStep(0f, 1f, t);
            spriteRenderer.color = color;
            if (t < 1f) return;
            collectionAnimating = false;
            collector = null;
            if (ReleaseRequested != null)
                ReleaseRequested.Invoke(this);
            else
                Destroy(gameObject);
        }

        public void OnPoolAcquire()
        {
            EnsureComponents();
            collected = false;
            collectionAnimating = false;
            collector = null;
            spriteRenderer.color = baseColor;
            transform.localScale = baseScale == Vector3.zero ? Vector3.one : baseScale;
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
            collectionAnimating = false;
            collector = null;
            spriteRenderer.color = baseColor;
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
