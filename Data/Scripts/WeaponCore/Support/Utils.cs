using VRage.Collections;
using VRage.Library.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace WeaponCore.Support
{
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

    public class NetworkReporter
    {
        public Dictionary<PacketType, List<Report>> ReportData = new Dictionary<PacketType, List<Report>>();
        public readonly MyConcurrentPool<Report> ReportPool = new MyConcurrentPool<Report>(3600, reprot => reprot.Clean());

        public NetworkReporter()
        {
            foreach (var suit in (PacketType[])Enum.GetValues(typeof(PacketType)))
                ReportData.Add(suit, new List<Report>());
        }

        public class Report
        {
            public enum Received
            {
                None,
                Server,
                Client
            }

            public Received Receiver;
            public bool PacketValid;
            public int PacketSize;

            public void Clean()
            {
                Receiver = Received.None;
                PacketValid = false;
                PacketSize = 0;
            }
        }
    }

    public class DsUniqueListFastRemove<T>
    {
        private List<T> _list;
        private Dictionary<T, int> _dictionary;

        public DsUniqueListFastRemove(int capacity)
        {
            _list = new List<T>(capacity);
            _dictionary = new Dictionary<T, int>(capacity);
        }

        public DsUniqueListFastRemove()
        {
            _list = new List<T>();
            _dictionary = new Dictionary<T, int>();
        }

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
            _list.Add(item);
            _dictionary.Add(item, _list.Count - 1);
            return true;
        }

        /// <summary>O(1)</summary>
        public bool Remove(T item)
        {
            int oldPos;
            if (_dictionary.TryGetValue(item, out oldPos))
            {
                
                _dictionary.Remove(item);
                _list.RemoveAtFast(oldPos);
                var count  = _list.Count;
                if (count > 0)
                {
                    count--;
                    if (oldPos <= count)
                        _dictionary[_list[oldPos]] = oldPos;
                    else
                        _dictionary[_list[count]] = count;
                }

                return true;
            }
            return false;
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

        public List<T> ItemList
        {
            get
            {
                return new List<T>(_list);
            }
        }

        public List<T>.Enumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }
    }

    public class DsUniqueConcurrentListFastRemove<T>
    {
        private MyConcurrentList<T> _list = new MyConcurrentList<T>();
        private ConcurrentDictionary<T, int> _dictionary = new ConcurrentDictionary<T, int>();

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
            _list.Add(item);

            return _dictionary.TryAdd(item, _list.Count - 1);
            
        }

        /// <summary>O(1)</summary>
        public bool Remove(T item)
        {
            int oldPos;
            if (_dictionary.TryGetValue(item, out oldPos))
            {

                _dictionary.Remove(item);
                _list.RemoveAtFast(oldPos);
                var count = _list.Count;
                if (count > 0)
                {
                    count--;
                    if (oldPos <= count)
                        _dictionary[_list[oldPos]] = oldPos;
                    else
                        _dictionary[_list[count]] = count;
                }

                return true;
            }
            return false;
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
                return new ListReader<T>(new List<T>(_list));
            }
        }

        public Object GetEnumerator()
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

    internal class StallReporter
    {
        private readonly Stopwatch _watch = new Stopwatch();
        internal string Name;
        internal double MaxMs;

        public void Start(string name, double maxMs)
        {
            Name = name;
            MaxMs = maxMs;
            _watch.Restart();
        }

        public void End()
        {
            _watch.Stop();
            var ticks = _watch.ElapsedTicks;
            var ns = 1000000000.0 * ticks / Stopwatch.Frequency;
            var ms = ns / 1000000.0;
            if (ms > MaxMs)
            {
                var message = $"[Warning] {ms} milisecond delay detected in {Name}: ";
                Log.LineShortDate(message, "perf");
            }
        }
    }

    internal class DSUtils
    {
        internal DSUtils(Session session)
        {
            Session = session;
        }
        internal struct Results
        {
            public double Min;
            public double Max;
            public double Median;
            public uint MaxTick;
        }

        internal class Timings
        {
            public double Max;
            public double Min;
            public double Total;
            public double Average;
            public int Events;
            public uint MaxTick;
            public readonly List<int> Values = new List<int>();
            public int[] TmpArray = new int[1];

            internal void Clean()
            {
                Max = 0;
                Min = 0;
                Total = 0;
                Average = 0;
                Events = 0;
            }
        }

        internal Session Session;
        private double _last;
        private bool _time;
        private bool _showTick;

        private Stopwatch Sw { get; } = new Stopwatch();
        private readonly Dictionary<string, Timings> _timings = new Dictionary<string, Timings>();
        public void Start(string name, bool time = true)
        {
            _time = time;
            Sw.Restart();
        }

        public void Purge()
        {
            Clean();
            Clear();
            Session = null;
        }

        public void Clear()
        {
            _timings.Clear();
        }

        public void Clean()
        {
            foreach (var timing in _timings.Values)
                timing.Clean();
        }

        public Results GetValue(string name)
        {
            Timings times;
            if (_timings.TryGetValue(name, out times) && times.Values.Count > 0)
            {
                var itemCnt = times.Values.Count;
                var tmpCnt = times.TmpArray.Length;
                if (itemCnt != tmpCnt)
                    Array.Resize(ref times.TmpArray, itemCnt);
                for (int i = 0; i < itemCnt; i++)
                    times.TmpArray[i] = times.Values[i];

                times.Values.Clear();
                var median = MathFuncs.GetMedian(times.TmpArray);

                return new Results { Median = median / 1000000.0, Min = times.Min, Max = times.Max, MaxTick = times.MaxTick};
            }

            return new Results();
        }

        public void Complete(string name, bool store, bool display = false)
        {
            Sw.Stop();
            var ticks = Sw.ElapsedTicks;
            var ns = 1000000000.0 * ticks / Stopwatch.Frequency;
            var ms = ns / 1000000.0;
            if (store)
            {
                Timings timings;
                if (_timings.TryGetValue(name, out timings))
                {
                    timings.Total += ms;
                    timings.Values.Add((int)ns);
                    timings.Events++;
                    timings.Average = (timings.Total / timings.Events);
                    if (ms > timings.Max)
                    {
                        timings.Max = ms;
                        timings.MaxTick = Session.Tick;
                    }
                    if (ms < timings.Min || timings.Min <= 0) timings.Min = ms;
                }
                else
                {
                    timings = new Timings();
                    timings.Total += ms;
                    timings.Values.Add((int)ns);
                    timings.Events++;
                    timings.Average = ms;
                    timings.Max = ms;
                    timings.MaxTick = Session.Tick;
                    timings.Min = ms;
                    _timings[name] = timings;
                }
            }
            Sw.Reset();
            if (display)
            {
                var message = $"[{name}] ms:{(float)ms} last-ms:{(float)_last}";
                _last = ms;
                if (_time) Log.LineShortDate(message);
                else Log.CleanLine(message);
            }
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
