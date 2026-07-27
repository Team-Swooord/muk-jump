using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;

namespace MukJump.Items
{
    /// 모든 먹분신이 공유하는 먹물점프 합성 VFX 풀.
    /// 플레이어별 풀을 만들지 않아 분신 수가 늘어도 합성 오브젝트 총량은 세 묶음으로 제한된다.
    [DisallowMultipleComponent]
    public sealed class InkDropJumpVfxPool : MonoBehaviour
    {
        const int PoolCapacity = 3;

        public static InkDropJumpVfxPool Instance { get; private set; }

        readonly List<InkDropJumpVfxInstance> active = new(PoolCapacity);
        readonly Dictionary<InkDropJumpVfxInstance, InkDropJumpVfx> owners = new();
        ComponentPool<InkDropJumpVfxInstance> pool;
        InkDropJumpVfxInstance.AssetSet assets;
        int sprayCount;
        int residualDropCount;
        bool configured;
        int prewarmedCount;

        public int ActiveCount => active.Count;

        void OnEnable()
        {
            if (Instance != null && Instance != this && Instance.isActiveAndEnabled)
            {
                // 비활성 서비스가 뒤늦게 켜져 현재 공용 풀을 덮어쓰지 않게 한다.
                enabled = false;
                return;
            }
            Instance = this;
            RegisterQualityListener();
        }

        void OnDisable()
        {
            UnregisterQualityListener();
            ReleaseAll();
            if (Instance == this) Instance = null;
        }

        public static InkDropJumpVfxPool GetOrCreate(InkDropJumpVfxInstance.AssetSet assets,
            int sprayCount, int residualDropCount)
        {
            var service = Instance;
            if (service == null)
            {
                // EditMode 검증이나 Play 중 스크립트 리로드에서는 OnEnable보다 static
                // 참조가 먼저 사라질 수 있다. 계층에 남은 서비스를 우선 복구해야
                // 분신마다 별도 풀이 생기지 않는다.
                var candidates = FindObjectsByType<InkDropJumpVfxPool>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (!candidates[i].isActiveAndEnabled) continue;
                    service = candidates[i];
                    break;
                }
                if (service == null)
                {
                    var go = new GameObject("InkDropJumpVfxPool");
                    if (GameManager.Instance != null)
                        go.transform.SetParent(GameManager.Instance.transform, false);
                    service = go.AddComponent<InkDropJumpVfxPool>();
                }

                Instance = service;
            }

            service.Configure(assets, sprayCount, residualDropCount);
            return service;
        }

        public void Play(InkDropJumpVfx owner, Transform player, SpriteRenderer playerRenderer,
            Vector3 ground, float height, float maximumStrokeLength)
        {
            if (owner == null || player == null || pool == null) return;

            // 품질별 소프트 상한을 넘으면 가장 오래된 장식 묶음을 반납한다.
            // 풀 자체의 3개 하드 상한은 유지해 품질을 올려도 재할당하지 않는다.
            int activeLimit = Mathf.Clamp(
                VfxQualityRuntime.Profile.CompositeConcurrentLimit,
                1,
                PoolCapacity);
            while (active.Count >= activeLimit)
            {
                ReleaseAt(0);
                VfxRuntimeMonitor.Instance?.RecordCompositeReclaimed();
            }

            var instance = pool.Acquire();
            instance.gameObject.name = "VFX_InkDropJump_Pickup";
            instance.ReleaseRequested -= OnReleaseRequested;
            instance.ReleaseRequested += OnReleaseRequested;
            active.Add(instance);
            owners[instance] = owner;
            VfxRuntimeMonitor.Instance?.ReportCompositeUsage(active.Count);
            instance.Play(player, playerRenderer, ground, height, maximumStrokeLength);
        }

        /// 첫 아이템 획득 프레임에 수십 개 자식 렌더러가 한꺼번에 생기는 hitch를
        /// 피하도록 로비에서 현재 품질의 동시 상한까지 미리 만든다.
        public void PrewarmForCurrentTier()
        {
            if (pool == null) return;
            int targetCount = Mathf.Clamp(
                VfxQualityRuntime.Profile.CompositeConcurrentLimit,
                1,
                PoolCapacity);
            if (prewarmedCount >= targetCount) return;

            var borrowed = new InkDropJumpVfxInstance[targetCount];
            for (int i = 0; i < targetCount; i++)
                borrowed[i] = pool.Acquire();
            for (int i = borrowed.Length - 1; i >= 0; i--)
                pool.Release(borrowed[i]);
            prewarmedCount = targetCount;
        }

        public void ReleaseOwner(InkDropJumpVfx owner)
        {
            if (object.ReferenceEquals(owner, null)) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var instance = active[i];
                if (object.ReferenceEquals(instance, null) || instance == null)
                {
                    ReleaseAt(i);
                    continue;
                }
                if (owners.TryGetValue(instance, out var candidate) &&
                    object.ReferenceEquals(candidate, owner))
                    ReleaseAt(i);
            }
        }

        void Configure(InkDropJumpVfxInstance.AssetSet newAssets, int newSprayCount,
            int newResidualDropCount)
        {
            // EditMode 검증과 Domain Reload 복원에서는 GetOrCreate가 OnEnable보다
            // 먼저 공용 서비스를 되찾을 수 있으므로 정적 품질 이벤트도 함께 복구한다.
            RegisterQualityListener();
            // Unity의 플레이 중 스크립트 리로드에서는 bool은 복원되지만,
            // 직렬화되지 않는 ComponentPool 캐시는 사라진다. 두 상태를 함께 확인한다.
            if (configured && pool != null) return;

            // 합성 인스턴스의 renderer 배열은 managed 캐시라 스크립트 리로드 뒤 복원할 수 없다.
            // 이전 자식은 즉시 숨기고 정리해 새 풀이 중복 자식을 계속 쌓지 않게 한다.
            var staleInstances = GetComponentsInChildren<InkDropJumpVfxInstance>(true);
            for (int i = 0; i < staleInstances.Length; i++)
            {
                staleInstances[i].gameObject.SetActive(false);
                Destroy(staleInstances[i].gameObject);
            }

            assets = newAssets;
            sprayCount = Mathf.Max(0, newSprayCount);
            residualDropCount = Mathf.Max(0, newResidualDropCount);
            pool = new ComponentPool<InkDropJumpVfxInstance>(CreatePooledInstance, PoolCapacity);
            prewarmedCount = 0;
            configured = true;
        }

        void RegisterQualityListener()
        {
            // SubsystemRegistration이 정적 event만 초기화한 Fast Enter Play 조합에서도
            // bool 상태에 기대지 않고 항상 정확히 한 번 등록한다.
            VfxQualityRuntime.Changed -= HandleQualityChanged;
            VfxQualityRuntime.Changed += HandleQualityChanged;
        }

        void UnregisterQualityListener()
        {
            VfxQualityRuntime.Changed -= HandleQualityChanged;
        }

        InkDropJumpVfxInstance CreatePooledInstance()
        {
            var go = new GameObject("Pooled_InkDropJumpVfx");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var instance = go.AddComponent<InkDropJumpVfxInstance>();
            instance.Initialize(transform, assets, sprayCount, residualDropCount);
            return instance;
        }

        void OnReleaseRequested(InkDropJumpVfxInstance instance)
        {
            int index = active.IndexOf(instance);
            if (index >= 0)
                ReleaseAt(index);
        }

        void HandleQualityChanged(
            VfxQualityTier tier,
            VfxQualityChangeReason reason)
        {
            int activeLimit = Mathf.Clamp(
                VfxQualityRuntime.GetProfile(tier).CompositeConcurrentLimit,
                1,
                PoolCapacity);
            while (active.Count > activeLimit)
            {
                ReleaseAt(0);
                VfxRuntimeMonitor.Instance?.RecordCompositeReclaimed();
            }
        }

        void ReleaseAt(int index)
        {
            var instance = active[index];
            active.RemoveAt(index);
            if (!object.ReferenceEquals(instance, null))
            {
                owners.Remove(instance);
                if (instance != null)
                {
                    instance.ReleaseRequested -= OnReleaseRequested;
                    pool?.Release(instance);
                }
            }
            VfxRuntimeMonitor.Instance?.ReportCompositeUsage(active.Count);
        }

        void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                ReleaseAt(i);
            owners.Clear();
            VfxRuntimeMonitor.Instance?.ReportCompositeUsage(0);
        }
    }
}
