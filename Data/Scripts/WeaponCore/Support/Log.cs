using System;
using System.Collections.Concurrent;
using System.IO;
using Sandbox.ModAPI;
using VRage.Collections;

namespace WeaponCore.Support
{
    public static class Log
    {
        private static MyConcurrentPool<LogInstance> _logPool = new MyConcurrentPool<LogInstance>(128);
        private static ConcurrentDictionary<string, LogInstance> _instances = new ConcurrentDictionary<string, LogInstance>();
        private static ConcurrentQueue<string[]> _threadedLineQueue = new ConcurrentQueue<string[]>();
        private static string _defaultInstance;

        public class LogInstance
        {
            internal TextWriter TextWriter = null;

            internal void Clean()
            {
                TextWriter = null;
            }
        }

        public static void Init(string name, bool defaultInstance = true)
        {
            try
            {
                if (defaultInstance) _defaultInstance = name;
                var instance = _logPool.Get();
                _instances[name] = instance;
                MyAPIGateway.Utilities.ShowNotification(name, 5000);
                instance.TextWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(name, typeof(LogInstance));
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowNotification(e.Message, 5000);
            }
        }

        public static void Line(string text, string instanceName = null)
        {
            try
            {
                var name  = instanceName ?? _defaultInstance;
                var instance = _instances[name];
                if (instance.TextWriter != null)
                {
                    instance.TextWriter.WriteLine($"{DateTime.Now:MM-dd-yy_HH-mm-ss-fff} - " + text);
                    instance.TextWriter.Flush();
                }
            }
            catch (Exception e)
            {
            }
        }

        public static void LineShortDate(string text, string instanceName = null)
        {
            try
            {
                var name = instanceName ?? _defaultInstance;
                var instance = _instances[name];
                if (instance.TextWriter != null)
                {
                    instance.TextWriter.WriteLine($"{DateTime.Now:HH-mm-ss-fff} - "  + text);
                    instance.TextWriter.Flush();
                }
            }
            catch (Exception e)
            {
            }
        }
        public static void Chars(string text, string instanceName = null)
        {
            try
            {
                var name = instanceName ?? _defaultInstance;
                var instance = _instances[name];
                if (instance.TextWriter != null)
                {
                    instance.TextWriter.Write(text);
                    instance.TextWriter.Flush();
                }
            }
            catch (Exception e)
            {
            }
        }

        public static void CleanLine(string text, string instanceName = null)
        {
            try
            {
                var name = instanceName ?? _defaultInstance;
                var instance = _instances[name];
                if (instance.TextWriter != null)
                {
                    instance.TextWriter.WriteLine(text);
                    instance.TextWriter.Flush();
                }
            }
            catch (Exception e)
            {
            }
        }

        public static void ThreadedWrite(string logLine)
        {
            _threadedLineQueue.Enqueue(new string[] { $"Actual Time: {DateTime.Now:MM-dd-yy_HH-mm-ss-fff -} ", logLine });
            MyAPIGateway.Utilities.InvokeOnGameThread(WriteLog);
        }

        private static void WriteLog() {
            string[] line;
            while (_threadedLineQueue.TryDequeue(out line))
            {
                Line(line[0] + line[1]);
            }

        }

        public static void Close()
        {
            try
            {
                _threadedLineQueue.Clear();
                foreach (var pair in _instances)
                {
                    pair.Value.TextWriter.Flush();
                    pair.Value.TextWriter.Close();
                    pair.Value.TextWriter.Dispose();
                    pair.Value.Clean();

                    _logPool.Return(pair.Value);

                }
                _instances.Clear();
                _logPool.Clean();
                _logPool = null;
                _instances = null;
                _threadedLineQueue = null;
                _defaultInstance = null;
            }
            catch (Exception e)
            {
            }
        }
    }
}
