using System;
using System.Collections.Generic;

namespace WeaponCore.Support
{
    internal class FutureEvents
    {
        internal struct FutureAction
        {
            internal Action<object> Callback;
            internal object Arg1;

            internal FutureAction(Action<object> callBack, object arg1)
            {
                Callback = callBack;
                Arg1 = arg1;
            }
        }

        internal FutureEvents()
        {
            for (int i = 0; i < _maxDelay; i++) _callbacks[i] = new List<FutureAction>();
        }

        private volatile bool Active = true;
        private const int _maxDelay = 1800;
        private readonly List<FutureAction>[] _callbacks = new List<FutureAction>[_maxDelay]; // and fill with list instances
        private uint _offset = 0;
        internal void Schedule(Action<object> callback, object arg1, uint delay)
        {
            lock (_callbacks)
            {
                //Log.Line($"BeforeEvent offset:{_offset} - delay:{delay}");
                //Log.Line($"(_offset + delay) % _maxDelay: {(_offset + delay) % _maxDelay}");
                if (Active) _callbacks[(_offset + delay) % _maxDelay].Add(new FutureAction(callback, arg1));
            }
        }

        internal void Tick(uint tick)
        {
            if (_callbacks.Length > 0 && Active)
            {
                lock (_callbacks)
                {
                    var index = tick % _maxDelay;
                    //if (_callbacks[index].Count > 0) Log.Line($"Tick oldOffSet:{_offset} - Tick:{tick}");
                    foreach (var e in _callbacks[index]) e.Callback(e.Arg1);
                    _callbacks[index].Clear();
                    _offset = tick + 1;
                }
            }
        }

        internal void Purge()
        {
            lock (_callbacks)
            {
                if (_callbacks.Length > 0 && Active)
                {
                    Active = false;
                    foreach (var list in _callbacks)
                    {
                        //foreach (var call in list)
                            //call.Callback(call.Arg1);
                        list.Clear();
                    }
                }

            }
        }
    }
}
