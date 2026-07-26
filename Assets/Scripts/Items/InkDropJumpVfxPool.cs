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

        void OnEnable()
        {
            if (Instance != null && Instance != this && Instance.isActiveAndEnabled)
            {
                // 비활성 서비스가 뒤늦게 켜져 현재 공용 풀을 덮어쓰지 않게 한다.
                enabled = false;
                return;
            }
            Instance = this;
        }

        void OnDisable()
        {
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
                service = FindFirstObjectByType<InkDropJumpVfxPool>(
                    FindObjectsInactive.Exclude);
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

            // 게임 전체에서 가장 오래된 묶음을 반납해 분신 수와 무관한 고정 상한을 지킨다.
            if (active.Count >= PoolCapacity)
                ReleaseAt(0);

            var instance = pool.Acquire();
            instance.gameObject.name = "VFX_InkDropJump_Pickup";
            instance.ReleaseRequested -= OnReleaseRequested;
            instance.ReleaseRequested += OnReleaseRequested;
            active.Add(instance);
            owners[instance] = owner;
            instance.Play(player, playerRenderer, ground, height, maximumStrokeLength);
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
            configured = true;
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

        void ReleaseAt(int index)
        {
            var instance = active[index];
            active.RemoveAt(index);
            if (object.ReferenceEquals(instance, null)) return;
            owners.Remove(instance);
            if (instance != null)
                instance.ReleaseRequested -= OnReleaseRequested;
            pool?.Release(instance);
        }

        void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                ReleaseAt(i);
            owners.Clear();
        }
    }
}
