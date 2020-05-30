using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
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
            internal Session Session;
            internal uint CheckTick;
            internal uint StartTick;
            internal uint Messages;
            internal bool Supress;

            internal bool Paused()
            {
                if (Session.Tick < 240)
                    return false;

                var checkInTime = Session.Tick - CheckTick > 119;
                var threshold = 122;

                if (!Supress && checkInTime && Messages > threshold || !Supress && Messages > threshold * 3)
                    return Pause();

                if (Supress && StartTick - Session.Tick > 0)
                    return true;

                ++Messages;

                if (Supress)
                    UnPause();
                else if (checkInTime) {
                    CheckTick = Session.Tick;
                    Messages = 0;
                }

                return false;
            }

            internal bool Pause()
            {
                Supress = true;
                StartTick = Session.Tick + 7200;
                var message = $"{DateTime.Now:HH-mm-ss-fff} - " + "Debug flooding detected, supressing logs for two minutes.  Please report the first 500 lines of this file";
                TextWriter.WriteLine(message);
                TextWriter.Flush();
                return true;
            }

            internal void UnPause()
            {
                Supress = false;
                Messages = 0;
                CheckTick = Session.Tick;
            }

            internal void Clean()
            {
                CheckTick = Session.Tick;
                StartTick = CheckTick;
                TextWriter = null;
                Session = null;
                Supress = false;
                Messages = 0;
            }
        }

        public static void Init(string name, Session session, bool defaultInstance = true)
        {
            try
            {
                var filename = name + ".log";
                if (_instances.ContainsKey(name)) return;
                RenameFileInLocalStorage(filename, name + $"-{DateTime.Now:MM-dd-yy_HH-mm-ss}.log", typeof(LogInstance));

                if (defaultInstance) _defaultInstance = name;
                var instance = _logPool.Get();

                instance.Session = session;
                _instances[name] = instance;

                instance.TextWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(filename, typeof(LogInstance));
                Line($"Logging Started", name);
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

        public static void NetLogger(Session session, string message, string name)
        {
            switch (name) {
                case "perf":
                    message = "1" + message;
                    break;
                case "stats":
                    message = "2" + message;
                    break;
                case "net":
                    message = "3" + message;
                    break;
                case "custom":
                    message = "4" + message;
                    break;
                default:
                    message = "0" + message;
                    break;
            }

            var test = Encoding.UTF8.GetBytes(message);

            foreach (var a in session.ConnectedAuthors)
                MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(Session.AuthorPacketId, test, a.Value, true);
        }

        public static void Line(string text, string instanceName = null)
        {
            try
            {
                var name  = instanceName ?? _defaultInstance;
                var instance = _instances[name];
                if (instance.TextWriter != null) {

                    if (name == _defaultInstance && instance.Paused())
                        return;

                    var message = $"{DateTime.Now:MM-dd-yy_HH-mm-ss-fff} - " + text;
                    instance.TextWriter.WriteLine(message);
                    instance.TextWriter.Flush();
                    var set = instance.Session.AuthorSettings;
                    var netEnabled = instance.Session.AuthLogging && name == _defaultInstance && set[0] >= 0 || name == "perf" && set[1] >= 0 || name == "stats" && set[2] >= 0 || name == "net" && set[3] >= 0;
                    if (netEnabled)
                        NetLogger(instance.Session, "[R-LOG] " + message, name);
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
                if (instance.TextWriter != null) {

                    if (name == _defaultInstance && instance.Paused())
                        return;

                    var message = $"{DateTime.Now:HH-mm-ss-fff} - " + text;
                    instance.TextWriter.WriteLine(message);
                    instance.TextWriter.Flush();

                    var set = instance.Session.AuthorSettings;
                    var netEnabled = instance.Session.AuthLogging && name == _defaultInstance && set[0] >= 0 || name == "perf" && set[1] >= 0 || name == "stats" && set[2] >= 0 || name == "net" && set[3] >= 0;
                    if (netEnabled)
                        NetLogger(instance.Session, "[R-LOG] " + message, name);
                }
            }
            catch (Exception e)
            {
            }
        }

        public static void NetLog(string text, Session session, int logLevel)
        {
            var set = session.AuthorSettings;
            var netEnabled = session.AuthLogging && set[4] >= 0 && logLevel >= set[5];
            if (netEnabled)
                NetLogger(session, "[R-LOG] " + text, string.Empty);
        }

        public static void Chars(string text, string instanceName = null)
        {
            try
            {
                var name = instanceName ?? _defaultInstance;
                var instance = _instances[name];
                if (instance.TextWriter != null) {

                    if (name == _defaultInstance && instance.Paused())
                        return;

                    instance.TextWriter.Write(text);
                    instance.TextWriter.Flush();

                    var set = instance.Session.AuthorSettings;
                    var netEnabled = instance.Session.AuthLogging && name == _defaultInstance && set[0] >= 0 || name == "perf" && set[1] >= 0 || name == "stats" && set[2] >= 0 || name == "net" && set[3] >= 0;
                    if (netEnabled)
                        NetLogger(instance.Session, "[R-LOG] " + text, name);
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
                if (instance.TextWriter != null) {
                    
                    if (name == _defaultInstance && instance.Paused())
                        return;

                    instance.TextWriter.WriteLine(text);
                    instance.TextWriter.Flush();

                    var set = instance.Session.AuthorSettings;
                    var netEnabled = instance.Session.AuthLogging && name == _defaultInstance && set[0] >= 0 || name == "perf" && set[1] >= 0 || name == "stats" && set[2] >= 0 || name == "net" && set[3] >= 0;
                    if (netEnabled)
                        NetLogger(instance.Session, "[R-LOG] " + text, name);
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
                Init("debugdevelop.log", null);

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
