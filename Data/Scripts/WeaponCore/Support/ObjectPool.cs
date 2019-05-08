using System;
using System.Collections.Generic;
using VRage.Collections;
using VRage.Library.Threading;

namespace WeaponCore.Support
{
    public class ObjectsPool<T> where T : class, new()
    {
        private SpinLockRef _activeLock = new SpinLockRef();
        private MyConcurrentQueue<T> _unused;
        private HashSet<T> _active;
        private HashSet<T> _marked;
        private Func<T> _activator;
        private int _baseCapacity;

        public SpinLockRef ActiveLock
        {
            get
            {
                return _activeLock;
            }
        }

        public HashSetReader<T> ActiveWithoutLock
        {
            get
            {
                return new HashSetReader<T>(_active);
            }
        }

        public HashSetReader<T> Active
        {
            get
            {
                using (_activeLock.Acquire())
                    return new HashSetReader<T>(_active);
            }
        }

        public int ActiveCount
        {
            get
            {
                using (_activeLock.Acquire())
                    return _active.Count;
            }
        }

        public int BaseCapacity
        {
            get
            {
                return _baseCapacity;
            }
        }

        public int Capacity
        {
            get
            {
                using (_activeLock.Acquire())
                    return _unused.Count + _active.Count;
            }
        }

        public ObjectsPool(int baseCapacity, Func<T> activator = null)
        {
            _activator = activator ?? new Func<T>(() => new T());
            _baseCapacity = baseCapacity;
            _unused = new MyConcurrentQueue<T>(_baseCapacity);
            _active = new HashSet<T>();
            _marked = new HashSet<T>();
            for (int index = 0; index < _baseCapacity; ++index)
                _unused.Enqueue(_activator());
        }

        /// <summary>Returns true when new item was allocated</summary>
        public bool AllocateOrCreate(out T item)
        {
            bool flag = false;
            using (_activeLock.Acquire())
            {
                flag = _unused.Count == 0;
                item = !flag ? _unused.Dequeue() : _activator();
                _active.Add(item);
            }
            return flag;
        }

        public T Allocate(bool nullAllowed = false)
        {
            T obj = default(T);
            using (_activeLock.Acquire())
            {
                if (_unused.Count > 0)
                {
                    obj = _unused.Dequeue();
                    _active.Add(obj);
                }
            }
            return obj;
        }

        public void Deallocate(T item)
        {
            using (_activeLock.Acquire())
            {
                _active.Remove(item);
                _unused.Enqueue(item);
            }
        }

        public void MarkForDeallocate(T item)
        {
            using (_activeLock.Acquire())
                _marked.Add(item);
        }

        public void MarkAllActiveForDeallocate()
        {
            using (_activeLock.Acquire())
                _marked.UnionWith((IEnumerable<T>)_active);
        }

        public void DeallocateAllMarked()
        {
            using (_activeLock.Acquire())
            {
                foreach (T instance in _marked)
                {
                    _active.Remove(instance);
                    _unused.Enqueue(instance);
                }
                _marked.Clear();
            }
        }

        public void DeallocateAll()
        {
            using (_activeLock.Acquire())
            {
                foreach (T instance in _active)
                    _unused.Enqueue(instance);
                _active.Clear();
                _marked.Clear();
            }
        }
    }
}
