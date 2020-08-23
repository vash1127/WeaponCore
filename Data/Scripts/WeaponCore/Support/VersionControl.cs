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
                    Core.Session.UiInput.ActionKey = Core.Session.KeyMap[xmlData.ActionKey];
                    Core.Session.UiInput.MouseButtonMenu = Core.Session.MouseMap[xmlData.MenuButton];
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
                        CorruptionCheck();
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

        internal void UpdateClientCfgFile()
        {
            MyAPIGateway.Utilities.DeleteFileInGlobalStorage(Session.ClientCfgName);
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
        private void CorruptionCheck()
        {
            if (Core.Enforcement.AreaDamageModifer < 0)
                Core.Enforcement.AreaDamageModifer = 1f;
            if (Core.Enforcement.DirectDamageModifer < 0)
                Core.Enforcement.DirectDamageModifer = 1f;

            if (Core.Enforcement.ShipSizes == null || Core.Enforcement.ShipSizes.Length < 7)
            {
                Core.Enforcement.ShipSizes = new[]
                {
                    new CoreSettings.ServerSettings.ShipSize {Name = "Scout", BlockCount = 0, LargeGrid = false},
                    new CoreSettings.ServerSettings.ShipSize {Name = "Fighter", BlockCount = 2000, LargeGrid = false},
                    new CoreSettings.ServerSettings.ShipSize {Name = "Frigate", BlockCount = 0, LargeGrid = true},
                    new CoreSettings.ServerSettings.ShipSize {Name = "Destroyer", BlockCount = 3000, LargeGrid = true},
                    new CoreSettings.ServerSettings.ShipSize {Name = "Cruiser", BlockCount = 6000, LargeGrid = true},
                    new CoreSettings.ServerSettings.ShipSize {Name = "Battleship", BlockCount = 12000, LargeGrid = true},
                    new CoreSettings.ServerSettings.ShipSize {Name = "Capital", BlockCount = 24000, LargeGrid = true},
                };
            }

            if (Core.Enforcement.BlockModifers == null)
            {
                Core.Enforcement.BlockModifers = new[]
                {
                    new CoreSettings.ServerSettings.BlockModifer {SubTypeId = "TestSubId1", DirectDamageModifer = 0.5f, AreaDamageModifer = 0.1f},
                    new CoreSettings.ServerSettings.BlockModifer { SubTypeId = "TestSubId2", DirectDamageModifer = -1f, AreaDamageModifer = 0f }
                };
            }
        }
    }
}
