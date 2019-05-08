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

        private const int _maxDelay = 1800;
        private readonly List<FutureAction>[] _callbacks = new List<FutureAction>[_maxDelay]; // and fill with list instances
        private uint _offset = 0;
        internal void Schedule(Action<object> callback, object arg1, uint delay)
        {
            lock (_callbacks)
            {
                //Log.Line($"BeforeEvent offset:{_offset} - delay:{delay}");
                _callbacks[(_offset + delay) % _maxDelay].Add(new FutureAction(callback, arg1));
            }
        }

        internal void Tick(uint tick)
        {
            lock (_callbacks)
            {
                if (_callbacks[tick].Count > 0) Log.Line($"Tick oldOffSet:{_offset} - Tick:{tick}");
                foreach (var e in _callbacks[tick]) e.Callback(e.Arg1);
                _callbacks[tick].Clear();
                _offset = tick + 1;
            }
        }
    }
}
