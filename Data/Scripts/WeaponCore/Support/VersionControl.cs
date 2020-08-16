using System.IO;
using Sandbox.ModAPI;
using VRage.Game.VisualScripting;
using VRage.Input;
using WeaponCore.Support;

namespace WeaponCore.Settings
{
    internal class VersionControl
    {
        public CoreSettings Core;
        public VersionControl(CoreSettings core)
        {
            Core = core;
        }

        public void InitSettings()
        {
            if (MyAPIGateway.Utilities.FileExistsInGlobalStorage(Session.ClientCfgName)) {
                
                var writer = MyAPIGateway.Utilities.ReadFileInGlobalStorage(Session.ClientCfgName);
                var xmlData = MyAPIGateway.Utilities.SerializeFromXML<CoreSettings.ClientSettings>(writer.ReadToEnd());
                writer.Dispose();

                if (xmlData?.Version == Session.ClientCfgVersion) {
                    Core.ClientConfig = xmlData;
                    Core.Session.UiInput.ActionKey = Core.Session.KeyMap[xmlData.ActionButton];
                    Core.Session.UiInput.MouseKey = Core.Session.MouseMap[xmlData.MenuButton];
                }
                else
                    WriteNewClientCfg();
            }
            else WriteNewClientCfg();

            if (Core.Session.IsServer) {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Session.ServerCfgName, typeof(CoreSettings.ServerSettings))) {

                    var writer = MyAPIGateway.Utilities.ReadFileInWorldStorage(Session.ServerCfgName, typeof(CoreSettings.ServerSettings)); 
                    var xmlData = MyAPIGateway.Utilities.SerializeFromXML<CoreSettings.ServerSettings>(writer.ReadToEnd());
                    writer.Dispose();

                    if (xmlData?.Version == Session.ServerCfgVersion) {
                        Core.Enforcement = xmlData;
                        if (Core.Enforcement.AreaDamageModifer < 0)
                            Core.Enforcement.AreaDamageModifer = 1f;
                        if (Core.Enforcement.DirectDamageModifer < 0)
                            Core.Enforcement.DirectDamageModifer = 1f;
                    }
                    else
                        WriteNewServerCfg();
                }
                else WriteNewServerCfg();
            }
        }

        public void UpdateClientEnforcements(CoreSettings.ServerSettings data)
        {
            Core.Enforcement = data;
            Core.ClientWaiting = false;
        }

        private void WriteNewServerCfg()
        {
            MyAPIGateway.Utilities.DeleteFileInWorldStorage(Session.ServerCfgName, typeof(CoreSettings.ServerSettings));
            Core.Enforcement = new CoreSettings.ServerSettings {Version = Session.ServerCfgVersion};
            var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Session.ServerCfgName, typeof(CoreSettings.ServerSettings));
            var data = MyAPIGateway.Utilities.SerializeToXML(Core.Enforcement);
            Write(writer, data);
        }

        private void WriteNewClientCfg()
        {
            MyAPIGateway.Utilities.DeleteFileInGlobalStorage(Session.ClientCfgName);
            Core.ClientConfig = new CoreSettings.ClientSettings {Version = Session.ClientCfgVersion};
            var writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(Session.ClientCfgName);
            var data = MyAPIGateway.Utilities.SerializeToXML(Core.ClientConfig);
            Write(writer, data);
        }

        private static void Write(TextWriter writer, string data)
        {
            writer.Write(data);
            writer.Flush();
            writer.Dispose();
        }
    }
}
