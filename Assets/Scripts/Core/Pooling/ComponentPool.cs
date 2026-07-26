using System;
using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Core.Pooling
{
    /// 기능별 스포너가 소유하는 지연 생성 컴포넌트 풀.
    /// 대여 중인 인스턴스를 추적해 중복 반납과 같은 생명주기 오류를 막는다.
    public sealed class ComponentPool<T> where T : Component, IPoolableEntity
    {
        readonly Func<T> factory;
        readonly Stack<T> available = new();
        readonly HashSet<T> leased = new();
        readonly int maxRetained;

        public int AvailableCount => available.Count;
        public int LeasedCount
        {
            get
            {
                leased.RemoveWhere(IsDestroyed);
                return leased.Count;
            }
        }

        public ComponentPool(Func<T> factory, int maxRetained = 16)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.maxRetained = Mathf.Max(1, maxRetained);
        }

        public T Acquire()
        {
            T instance = null;
            while (available.Count > 0 && instance == null)
                instance = available.Pop();

            if (instance == null)
            {
                instance = factory();
                if (instance == null)
                    throw new InvalidOperationException(
                        $"{typeof(T).Name} 풀 팩토리가 null을 반환했습니다.");
                instance.gameObject.SetActive(false);
            }

            if (!leased.Add(instance))
                throw new InvalidOperationException(
                    $"{typeof(T).Name} 인스턴스가 이미 대여 중입니다.");

            instance.OnPoolAcquire();
            instance.gameObject.SetActive(true);
            return instance;
        }

        /// Play 중 스크립트 재컴파일 뒤 Hierarchy에는 남았지만 managed 풀이 사라진
        /// 비활성 인스턴스를 새 풀에 다시 편입한다.
        public bool Adopt(T instance)
        {
            if (instance == null || leased.Contains(instance) || available.Contains(instance))
                return false;

            instance.OnPoolRelease();
            instance.gameObject.SetActive(false);
            if (available.Count < maxRetained)
                available.Push(instance);
            else
                UnityEngine.Object.Destroy(instance.gameObject);
            return true;
        }

        /// 실제 대여 중인 인스턴스만 한 번 반납한다. 중복 반납은 상태를 건드리지 않는다.
        public bool Release(T instance)
        {
            // UnityEngine.Object는 파괴 뒤 C# 참조가 남아 있어도 == null이 된다.
            // 먼저 HashSet에서 제거해야 파괴된 대여 객체 참조가 풀에 영구 잔류하지 않는다.
            if (object.ReferenceEquals(instance, null) || !leased.Remove(instance))
                return false;
            if (instance == null)
                return true;

            instance.OnPoolRelease();
            instance.gameObject.SetActive(false);
            if (available.Count < maxRetained)
                available.Push(instance);
            else
                UnityEngine.Object.Destroy(instance.gameObject);
            return true;
        }

        static bool IsDestroyed(T instance) => instance == null;
    }
}
