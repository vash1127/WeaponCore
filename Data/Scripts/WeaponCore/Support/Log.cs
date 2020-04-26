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
                var filename = name + ".log";
                if (_instances.ContainsKey(name)) return;
                RenameFileInLocalStorage(filename, name + $"-{DateTime.Now:MM-dd-yy_HH-mm-ss}.log", typeof(LogInstance));

                if (defaultInstance) _defaultInstance = name;
                var instance = _logPool.Get();
                _instances[name] = instance;
                instance.TextWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(filename, typeof(LogInstance));
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowNotification(e.Message, 5000);
            }
        }

        public static void RenameFileInLocalStorage(string oldName, string newName, Type anyObjectInYourMod)
        {
            if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(oldName, anyObjectInYourMod))
                return;

            if (MyAPIGateway.Utilities.FileExistsInLocalStorage(newName, anyObjectInYourMod))
                return;

            using (var read = MyAPIGateway.Utilities.ReadFileInLocalStorage(oldName, anyObjectInYourMod))
            {
                using (var write = MyAPIGateway.Utilities.WriteFileInLocalStorage(newName, anyObjectInYourMod))
                {
                    write.Write(read.ReadToEnd());
                    write.Flush();
                    write.Dispose();
                }
            }

            MyAPIGateway.Utilities.DeleteFileInLocalStorage(oldName, anyObjectInYourMod);
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
            _threadedLineQueue.Enqueue(new string[] { $"Threaded Time:  {DateTime.Now:HH-mm-ss-fff} - ", logLine });
            MyAPIGateway.Utilities.InvokeOnGameThread(WriteLog);
        }

        private static void WriteLog() {
            string[] line;

            var instance = _instances[_defaultInstance];
            if (instance.TextWriter != null)
                Init("debugdevelop.log");

            instance = _instances[_defaultInstance];           

            while (_threadedLineQueue.TryDequeue(out line))
            {
                if (instance.TextWriter != null)
                {
                    instance.TextWriter.WriteLine(line[0] + line[1]);
                    instance.TextWriter.Flush();
                }
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
