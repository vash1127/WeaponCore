using System.Collections.Generic;
using VRage.Collections;
using VRage.Library.Threading;
using WeaponCore.Projectiles;

namespace WeaponCore.Support
{
    internal class ProjectilePool
    {
        private SpinLockRef _activeLock = new SpinLockRef();
        private MyConcurrentQueue<Projectiles.Projectile> _unused;
        private HashSet<Projectiles.Projectile> _active;
        private HashSet<Projectiles.Projectile> _marked;
        private Projectile _activator;
        private int _baseCapacity;
        private int _poolId;

        internal SpinLockRef ActiveLock
        {
            get
            {
                return _activeLock;
            }
        }

        internal HashSetReader<Projectiles.Projectile> ActiveWithoutLock
        {
            get
            {
                return new HashSetReader<Projectiles.Projectile>(_active);
            }
        }

        internal HashSetReader<Projectiles.Projectile> Active
        {
            get
            {
                using (_activeLock.Acquire())
                    return new HashSetReader<Projectiles.Projectile>(_active);
            }
        }

        internal HashSetReader<Projectiles.Projectile> Marked
        {
            get
            {
                using (_activeLock.Acquire())
                    return new HashSetReader<Projectiles.Projectile>(_marked);
            }
        }

        internal int ActiveCount
        {
            get
            {
                using (_activeLock.Acquire())
                    return _active.Count;
            }
        }

        internal int BaseCapacity
        {
            get
            {
                return _baseCapacity;
            }
        }

        internal int Capacity
        {
            get
            {
                using (_activeLock.Acquire())
                    return _unused.Count + _active.Count;
            }
        }

        internal ProjectilePool(int baseCapacity, Projectiles.Projectiles parent, int poolId)
        {
            _poolId = poolId;
            //_activator = new Projectiles.Projectile(parent, _poolId);
            _baseCapacity = baseCapacity;
            _unused = new MyConcurrentQueue<Projectiles.Projectile>(_baseCapacity);
            _active = new HashSet<Projectiles.Projectile>();
            _marked = new HashSet<Projectiles.Projectile>();
            for (int index = 0; index < _baseCapacity; ++index)
                _unused.Enqueue(_activator);
        }

        /// <summary>Returns true when new item was allocated</summary>
        internal bool AllocateOrCreate(out Projectiles.Projectile item)
        {
            bool flag = false;
            using (_activeLock.Acquire())
            {
                flag = _unused.Count == 0;
                item = !flag ? _unused.Dequeue() : _activator;
                _active.Add(item);
            }
            return flag;
        }

        internal Projectiles.Projectile Allocate(bool nullAllowed = false)
        {
            Projectiles.Projectile obj = default(Projectiles.Projectile);
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

        internal void Deallocate(Projectiles.Projectile item)
        {
            using (_activeLock.Acquire())
            {
                _active.Remove(item);
                _unused.Enqueue(item);
            }
        }

        internal void MarkForDeallocate(Projectiles.Projectile item)
        {
            using (_activeLock.Acquire())
                _marked.Add(item);
        }

        internal void MarkAllActiveForDeallocate()
        {
            using (_activeLock.Acquire())
                _marked.UnionWith((IEnumerable<Projectiles.Projectile>)_active);
        }

        internal void DeallocateAllMarked()
        {
            using (_activeLock.Acquire())
            {
                foreach (Projectiles.Projectile instance in _marked)
                {
                    _active.Remove(instance);
                    _unused.Enqueue(instance);
                }
                _marked.Clear();
            }
        }

        internal void DeallocateAll()
        {
            using (_activeLock.Acquire())
            {
                foreach (Projectiles.Projectile instance in _active)
                    _unused.Enqueue(instance);
                _active.Clear();
                _marked.Clear();
            }
        }
    }
}