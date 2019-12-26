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

            internal void Purge()
            {
                Callback = null;
                Arg1 = null;
            }
        }

        internal FutureEvents()
        {
            for (int i = 0; i <= _maxDelay; i++) _callbacks[i] = new List<FutureAction>();
        }

        private volatile bool Active = true;
        private const int _maxDelay = 7200;
        private List<FutureAction>[] _callbacks = new List<FutureAction>[_maxDelay + 1]; // and fill with list instances
        private uint _offset = 0;
        internal void Schedule(Action<object> callback, object arg1, uint delay)
        {
            lock (_callbacks)
            {
                _callbacks[(_offset + delay) % _maxDelay].Add(new FutureAction(callback, arg1));
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
                    for (int i = 0; i < _callbacks[index].Count; i++) _callbacks[index][i].Callback(_callbacks[index][i].Arg1);
                    _callbacks[index].Clear();
                    _offset = tick + 1;
                }
            }
        }

        internal void Purge(int tick)
        {
            for (int i = tick; i < tick + 7200; i++)
                Tick((uint)i);

            lock (_callbacks)
            {
                Active = false;
                foreach (var list in _callbacks)
                {
                    foreach (var call in list)
                        call.Purge();
                    list.Clear();
                }

                _callbacks = null;
            }
        }
    }
}
