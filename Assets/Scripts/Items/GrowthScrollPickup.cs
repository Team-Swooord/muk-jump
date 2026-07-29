using System;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Player;
using UnityEngine;

namespace MukJump.Items
{
    /// 성장 선택 화면을 여는 월드 두루마리.
    /// 일반 아이템과 수명 주기가 다르므로 전용 스포너의 단일 슬롯 풀만 사용한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public sealed class GrowthScrollPickup : MonoBehaviour, IPoolableEntity
    {
        [SerializeField, Min(0f)] float bobAmount = 0.12f;
        [SerializeField, Min(0f)] float bobSpeed = 1.7f;
        [SerializeField, Range(0f, 8f)] float tiltAngle = 2.5f;

        SpriteRenderer spriteRenderer;
        CircleCollider2D trigger;
        Vector3 origin;
        Vector3 baseScale = Vector3.one;
        float phase;
        bool collected;

        /// 선택 화면이 실제로 열렸을 때만 소유 스포너에 반납을 요청한다.
        public event Action<GrowthScrollPickup> ReleaseRequested;

        public bool IsCollected => collected;

        void Awake()
        {
            EnsureComponents();
        }

        /// 풀에서 꺼낸 두루마리의 월드 기준 상태를 한 번에 초기화한다.
        public void Configure(float phaseOffset = 0f)
        {
            EnsureComponents();
            origin = transform.position;
            baseScale = transform.localScale;
            phase = phaseOffset;
            collected = false;
            spriteRenderer.enabled = spriteRenderer.sprite != null;
            trigger.enabled = true;
            transform.rotation = Quaternion.identity;
        }

        void Update()
        {
            var manager = GameManager.Instance;
            if (collected || manager == null || !manager.IsGameplayTicking)
                return;

            float wave = Time.time * bobSpeed + phase;
            transform.position = origin + Vector3.up * (Mathf.Sin(wave) * bobAmount);
            transform.rotation = Quaternion.Euler(
                0f, 0f, Mathf.Sin(wave * 0.73f) * tiltAngle);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (collected) return;

            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.IsDead) return;

            var growth = RunGrowthController.Instance;
            if (growth == null || !growth.RequestChoice()) return;

            // RequestChoice가 성공한 뒤에만 소비한다. 같은 물리 프레임에 여러 분신이
            // 닿더라도 콜라이더를 먼저 꺼 선택 패널이 중복으로 열리지 않게 한다.
            collected = true;
            trigger.enabled = false;
            ReleaseRequested?.Invoke(this);
        }

        public void OnPoolAcquire()
        {
            EnsureComponents();
            collected = false;
            spriteRenderer.enabled = spriteRenderer.sprite != null;
            trigger.enabled = true;
            transform.rotation = Quaternion.identity;
        }

        public void OnPoolRelease()
        {
            EnsureComponents();
            collected = true;
            spriteRenderer.enabled = false;
            trigger.enabled = false;
            transform.position = origin;
            transform.localScale = baseScale == Vector3.zero ? Vector3.one : baseScale;
            transform.rotation = Quaternion.identity;
        }

        void EnsureComponents()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (trigger == null)
                trigger = GetComponent<CircleCollider2D>();

            trigger.isTrigger = true;
        }

        void OnValidate()
        {
            bobAmount = Mathf.Max(0f, bobAmount);
            bobSpeed = Mathf.Max(0f, bobSpeed);
            tiltAngle = Mathf.Clamp(tiltAngle, 0f, 8f);
        }
    }
}
