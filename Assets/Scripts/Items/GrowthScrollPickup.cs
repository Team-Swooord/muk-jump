using System;
using MukJump.Core;
using MukJump.Core.Pooling;
using UnityEngine;

namespace MukJump.Items
{
    /// 다음 성장 이정표를 미리 보여 주는 월드 두루마리 연출.
    /// 선택은 GrowthScrollSpawner가 먹떼 진행도를 기준으로 직접 열며,
    /// 이 오브젝트는 플레이어와 충돌하지 않는 한 슬롯 풀 미리보기다.
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

        /// 소유 스포너가 명시적으로 미리보기를 끝낼 때 반납을 요청한다.
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
            trigger.enabled = false;
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

        /// 선택이 열린 뒤 미리보기를 즉시 단일 슬롯 풀로 돌려보낸다.
        public void CompletePreview()
        {
            if (collected) return;

            collected = true;
            trigger.enabled = false;
            ReleaseRequested?.Invoke(this);
        }

        public void OnPoolAcquire()
        {
            EnsureComponents();
            collected = false;
            spriteRenderer.enabled = spriteRenderer.sprite != null;
            trigger.enabled = false;
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
