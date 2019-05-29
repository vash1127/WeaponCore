using VRage.Collections;
using VRage.Library.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using VRage.Game.Entity;

namespace WeaponCore.Support
{


    internal class BlockPriority : IComparer<BlockInfo>
    {
        public int Compare(BlockInfo x, BlockInfo y)
        {
            var compareVolume = x.Entity.PositionComp.WorldAABB.Volume.CompareTo(y.Entity.PositionComp.WorldAABB.Volume);
            if (compareVolume != 0) return compareVolume;
            /*
            var compareBlocks = x.BlocksCount.CompareTo(y.BlocksCount);
            if (compareBlocks != 0) return compareBlocks;
            */
            return x.Entity.EntityId.CompareTo(y.Entity.EntityId);
        }
    }

    internal static class ConcurrentQueueExtensions
    {
        public static void Clear<T>(this ConcurrentQueue<T> queue)
        {
            T item;
            while (queue.TryDequeue(out item)) { }
        }
    }

    class FiniteFifoQueueSet<T1, T2>
    {
        private readonly T1[] _nodes;
        private readonly Dictionary<T1, T2> _backingDict;
        private int _nextSlotToEvict;

        public FiniteFifoQueueSet(int size)
        {
            _nodes = new T1[size];
            _backingDict = new Dictionary<T1, T2>(size + 1);
            _nextSlotToEvict = 0;
        }

        public void Enqueue(T1 key, T2 value)
        {
            try
            {
                _backingDict.Remove(_nodes[_nextSlotToEvict]);
                _nodes[_nextSlotToEvict] = key;
                _backingDict.Add(key, value);

                _nextSlotToEvict++;
                if (_nextSlotToEvict >= _nodes.Length) _nextSlotToEvict = 0;
            }
            catch (Exception ex) { Log.Line($"Exception in Enqueue: {ex}"); }
        }

        public bool Contains(T1 value)
        {
            return _backingDict.ContainsKey(value);
        }

        public bool TryGet(T1 value, out T2 hostileEnt)
        {
            return _backingDict.TryGetValue(value, out hostileEnt);
        }
    }

    public class DsUniqueListFastRemove<T>
    {
        private List<T> _list = new List<T>();
        private Dictionary<T, int> _dictionary = new Dictionary<T, int>();
        private int _index;

        /// <summary>O(1)</summary>
        public int Count
        {
            get
            {
                return _list.Count;
            }
        }

        /// <summary>O(1)</summary>
        public T this[int index]
        {
            get
            {
                return _list[index];
            }
        }

        /// <summary>O(1)</summary>
        public bool Add(T item)
        {
            if (_dictionary.ContainsKey(item))
                return false;
            _dictionary.Add(item, _index);
            _list.Add(item);
            _index++;
            return true;
        }

        /// <summary>O(1)</summary>
        public bool Remove(T item)
        {
            if (!_dictionary.ContainsKey(item)) return false;

            var oldIndex = _dictionary[item];
            _dictionary.Remove(item);
            if (_index != oldIndex)
            {
                _list[oldIndex - 1] = _list[_index - 1];
                _list.RemoveAt(_index - 1);
            }
            else _list.RemoveAt(_index - 1);

            _index--;

            return true;
        }

        public void Clear()
        {
            _list.Clear();
            _dictionary.Clear();
        }

        /// <summary>O(1)</summary>
        public bool Contains(T item)
        {
            return _dictionary.ContainsKey(item);
        }

        public UniqueListReader<T> Items
        {
            get
            {
                return new UniqueListReader<T>();
            }
        }

        public ListReader<T> ItemList
        {
            get
            {
                return new ListReader<T>(_list);
            }
        }

        public List<T>.Enumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }
    }

    public class DsUniqueList<T>
    {
        private List<T> _list = new List<T>();
        private HashSet<T> _hashSet = new HashSet<T>();

        /// <summary>O(1)</summary>
        public int Count
        {
            get
            {
                return _list.Count;
            }
        }

        /// <summary>O(1)</summary>
        public T this[int index]
        {
            get
            {
                return _list[index];
            }
        }

        /// <summary>O(1)</summary>
        public bool Add(T item)
        {
            if (!_hashSet.Add(item))
                return false;
            _list.Add(item);
            return true;
        }

        /// <summary>O(n)</summary>
        public bool Insert(int index, T item)
        {
            if (_hashSet.Add(item))
            {
                _list.Insert(index, item);
                return true;
            }
            _list.Remove(item);
            _list.Insert(index, item);
            return false;
        }

        /// <summary>O(n)</summary>
        public bool Remove(T item)
        {
            if (!_hashSet.Remove(item))
                return false;
            _list.Remove(item);
            return true;
        }

        public void Clear()
        {
            _list.Clear();
            _hashSet.Clear();
        }

        /// <summary>O(1)</summary>
        public bool Contains(T item)
        {
            return _hashSet.Contains(item);
        }

        public UniqueListReader<T> Items
        {
            get
            {
                return new UniqueListReader<T>();
            }
        }

        public ListReader<T> ItemList
        {
            get
            {
                return new ListReader<T>(_list);
            }
        }

        public List<T>.Enumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }
    }

    public class ConcurrentUniqueQueue<T> : IEnumerable<T>
    {
        private readonly MyConcurrentHashSet<T> _hashSet;
        private readonly ConcurrentQueue<T> _queue;
        private SpinLockRef _lock = new SpinLockRef();

        public ConcurrentUniqueQueue()
        {
            _hashSet = new MyConcurrentHashSet<T>();
            _queue = new ConcurrentQueue<T>();
        }


        public int Count
        {
            get
            {
                return _hashSet.Count;
            }
        }

        public void Clear()
        {
            _hashSet.Clear();
            _queue.Clear();
        }


        public bool Contains(T item)
        {
            return _hashSet.Contains(item);
        }


        public void Enqueue(T item)
        {
            if (_hashSet.Add(item))
            {
                _queue.Enqueue(item);
            }
        }

        public T Dequeue()
        {
            T item;
            _queue.TryDequeue(out item);
            _hashSet.Remove(item);
            return item;
        }


        public T Peek()
        {
            T result;
            _queue.TryPeek(out result);
            return result;
        }


        public IEnumerator<T> GetEnumerator()
        {
            return _queue.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _queue.GetEnumerator();
        }
    }

    public class DsConcurrentUniqueList<T>
    {
        private List<T> _list = new List<T>();
        private HashSet<T> _hashSet = new HashSet<T>();
        private SpinLockRef _lock = new SpinLockRef();

        /// <summary>O(1)</summary>
        public int Count
        {
            get
            {
                return _list.Count;
            }
        }

        /// <summary>O(1)</summary>
        public T this[int index]
        {
            get
            {
                return _list[index];
            }
        }

        /// <summary>O(1)</summary>
        public bool Add(T item)
        {
            using (_lock.Acquire())
            {
                if (!_hashSet.Add(item))
                    return false;
                _list.Add(item);
                return true;
            }
        }

        /// <summary>O(n)</summary>
        public bool Insert(int index, T item)
        {
            using (_lock.Acquire())
            {
                if (_hashSet.Add(item))
                {
                    _list.Insert(index, item);
                    return true;
                }
                _list.Remove(item);
                _list.Insert(index, item);
                return false;
            }
        }

        /// <summary>O(n)</summary>
        public bool Remove(T item)
        {
            using (_lock.Acquire())
            {
                if (!_hashSet.Remove(item))
                    return false;
                _list.Remove(item);
                return true;
            }
        }

        public void Clear()
        {
            _list.Clear();
            _hashSet.Clear();
        }

        /// <summary>O(1)</summary>
        public bool Contains(T item)
        {
            return _hashSet.Contains(item);
        }

        public UniqueListReader<T> Items
        {
            get
            {
                return new UniqueListReader<T>();
            }
        }

        public ListReader<T> ItemList
        {
            get
            {
                return new ListReader<T>(_list);
            }
        }

        public List<T>.Enumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }
    }

    internal class DSUtils
    {
        private double _last;
        public Stopwatch Sw { get; } = new Stopwatch();

        public void StopWatchReport(string message, float log)
        {
            Sw.Stop();
            var ticks = Sw.ElapsedTicks;
            var ns = 1000000000.0 * ticks / Stopwatch.Frequency;
            var ms = ns / 1000000.0;
            var s = ms / 1000;
            if (log <= -1) Log.Line($"{message} ms:{(float)ms} last-ms:{(float)_last} s:{(int)s}");
            else
            {
                if (ms >= log) Log.Line($"{message} ms:{(float)ms} last-ms:{(float)_last} s:{(int)s}");
            }
            _last = ms;
            Sw.Reset();
        }
    }

    internal class RunningAverage
    {
        private readonly int _size;
        private readonly int[] _values;
        private int _valuesIndex;
        private int _valueCount;
        private int _sum;

        internal RunningAverage(int size)
        {
            _size = Math.Max(size, 1);
            _values = new int[_size];
        }

        internal int Add(int newValue)
        {
            // calculate new value to add to sum by subtracting the 
            // value that is replaced from the new value; 
            var temp = newValue - _values[_valuesIndex];
            _values[_valuesIndex] = newValue;
            _sum += temp;

            _valuesIndex++;
            _valuesIndex %= _size;

            if (_valueCount < _size)
                _valueCount++;

            return _sum / _valueCount;
        }
    }

    public class UniqueQueue<T> : IEnumerable<T>
    {
        private HashSet<T> hashSet;
        private Queue<T> queue;


        public UniqueQueue()
        {
            hashSet = new HashSet<T>();
            queue = new Queue<T>();
        }


        public int Count
        {
            get
            {
                return hashSet.Count;
            }
        }

        public void Clear()
        {
            hashSet.Clear();
            queue.Clear();
        }


        public bool Contains(T item)
        {
            return hashSet.Contains(item);
        }


        public void Enqueue(T item)
        {
            if (hashSet.Add(item))
            {
                queue.Enqueue(item);
            }
        }

        public T Dequeue()
        {
            T item = queue.Dequeue();
            hashSet.Remove(item);
            return item;
        }


        public T Peek()
        {
            return queue.Peek();
        }


        public IEnumerator<T> GetEnumerator()
        {
            return queue.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return queue.GetEnumerator();
        }
    }
}
