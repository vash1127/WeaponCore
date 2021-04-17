using System;
using System.Collections.Generic;
using System.IO;
using CoreSystems.Support;
using Sandbox.ModAPI;
using static CoreSystems.Settings.CoreSettings.ServerSettings;

namespace CoreSystems.Settings
{
    internal class VersionControl
    {
        public CoreSettings Core;
        private readonly Dictionary<WeaponDefinition.AmmoDef, AmmoModifer> _tmpAmmoModiferMap = new Dictionary<WeaponDefinition.AmmoDef, AmmoModifer>();
        public bool VersionChange;
        public VersionControl(CoreSettings core)
        {
            Core = core;
        }

        public void InitSettings()
        {
            if (MyAPIGateway.Utilities.FileExistsInGlobalStorage(Session.ClientCfgName))
            {

                var writer = MyAPIGateway.Utilities.ReadFileInGlobalStorage(Session.ClientCfgName);
                var xmlData = MyAPIGateway.Utilities.SerializeFromXML<CoreSettings.ClientSettings>(writer.ReadToEnd());
                writer.Dispose();

                if (xmlData?.Version == Session.ClientCfgVersion)
                {

                    Core.ClientConfig = xmlData;
                    Core.Session.UiInput.ActionKey = Core.Session.KeyMap[xmlData.ActionKey];
                    Core.Session.UiInput.MouseButtonMenu = Core.Session.MouseMap[xmlData.MenuButton];
                }
                else
                    WriteNewClientCfg();
            }
            else WriteNewClientCfg();

            if (Core.Session.IsServer)
            {

                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Session.ServerCfgName, typeof(CoreSettings.ServerSettings)))
                {

                    var writer = MyAPIGateway.Utilities.ReadFileInWorldStorage(Session.ServerCfgName, typeof(CoreSettings.ServerSettings));

                    CoreSettings.ServerSettings xmlData = null;

                    try { xmlData = MyAPIGateway.Utilities.SerializeFromXML<CoreSettings.ServerSettings>(writer.ReadToEnd()); }
                    catch (Exception e) { writer.Dispose(); }

                    writer.Dispose();

                    if (xmlData?.Version == Session.ServerCfgVersion)
                    {
                        Core.Enforcement = xmlData;
                        CorruptionCheck(true);
                    }
                    else
                        GenerateConfig(xmlData);
                }
                else GenerateConfig();


                GenerateBlockDmgMap();
                GenerateAmmoDmgMap();
            }

            if (VersionChange)
            {
                Core.Session.PlayerMessage = "You may access WeaponCore client settings with the /wc chat command";
            }
        }

        public void UpdateClientEnforcements(CoreSettings.ServerSettings data)
        {
            Core.Enforcement = data;
            Core.ClientWaiting = false;
            GenerateBlockDmgMap();
            GenerateAmmoDmgMap();
        }

        private void GenerateConfig(CoreSettings.ServerSettings oldSettings = null)
        {

            if (oldSettings != null) RebuildConfig(oldSettings);
            else
                Core.Enforcement = new CoreSettings.ServerSettings { Version = Session.ServerCfgVersion };

            CorruptionCheck();
            SaveServerCfg();
            VersionChange = true;
        }

        private void WriteNewClientCfg()
        {
            VersionChange = true;
            MyAPIGateway.Utilities.DeleteFileInGlobalStorage(Session.ClientCfgName);
            Core.ClientConfig = new CoreSettings.ClientSettings { Version = Session.ClientCfgVersion };
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

        private void SaveServerCfg()
        {
            MyAPIGateway.Utilities.DeleteFileInWorldStorage(Session.ServerCfgName, typeof(CoreSettings.ServerSettings));
            var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Session.ServerCfgName, typeof(CoreSettings.ServerSettings));
            var data = MyAPIGateway.Utilities.SerializeToXML(Core.Enforcement);
            Write(writer, data);
        }

        private static void Write(TextWriter writer, string data)
        {
            writer.Write(data);
            writer.Flush();
            writer.Dispose();
        }

        private void RebuildConfig(CoreSettings.ServerSettings oldSettings)
        {
            var oldAmmoModifers = oldSettings.AmmoModifers;
            var oldBlockModifers = oldSettings.BlockModifers;
            var oldShipSizes = oldSettings.ShipSizes;
            var oldSleep = oldSettings.ServerSleepSupport;
            var oldOptimize = oldSettings.ServerOptimizations;
            var oldFocusDist = oldSettings.MinHudFocusDistance;
            var oldDisableAi = oldSettings.DisableAi;

            Core.Enforcement = new CoreSettings.ServerSettings { Version = Session.ServerCfgVersion };

            if (oldAmmoModifers != null)
                Core.Enforcement.AmmoModifers = oldAmmoModifers;

            if (oldBlockModifers != null)
                Core.Enforcement.BlockModifers = oldBlockModifers;

            if (oldShipSizes != null)
                Core.Enforcement.ShipSizes = oldShipSizes;

            Core.Enforcement.ServerSleepSupport = oldSleep;

            Core.Enforcement.ServerOptimizations = oldOptimize;
            Core.Enforcement.MinHudFocusDistance = oldFocusDist;
            Core.Enforcement.DisableAi = oldDisableAi;
        }

        private void CorruptionCheck(bool write = false)
        {
            if (Core.Enforcement.AreaDamageModifer < 0)
                Core.Enforcement.AreaDamageModifer = 1f;

            if (Core.Enforcement.DirectDamageModifer < 0)
                Core.Enforcement.DirectDamageModifer = 1f;

            if (Core.Enforcement.ShipSizes == null || Core.Enforcement.ShipSizes.Length != 7)
            {
                Core.Enforcement.ShipSizes = new[]
                {
                    new ShipSize {Name = "Scout", BlockCount = 0, LargeGrid = false},
                    new ShipSize {Name = "Fighter", BlockCount = 2000, LargeGrid = false},
                    new ShipSize {Name = "Frigate", BlockCount = 0, LargeGrid = true},
                    new ShipSize {Name = "Destroyer", BlockCount = 3000, LargeGrid = true},
                    new ShipSize {Name = "Cruiser", BlockCount = 6000, LargeGrid = true},
                    new ShipSize {Name = "Battleship", BlockCount = 12000, LargeGrid = true},
                    new ShipSize {Name = "Capital", BlockCount = 24000, LargeGrid = true},
                };
            }

            if (Core.Enforcement.BlockModifers == null)
            {
                Core.Enforcement.BlockModifers = new[]
                {
                    new BlockModifer {SubTypeId = "TestSubId1", DirectDamageModifer = 0.5f, AreaDamageModifer = 0.1f},
                    new BlockModifer { SubTypeId = "TestSubId2", DirectDamageModifer = -1f, AreaDamageModifer = 0f }
                };
            }

            if (Core.Enforcement.AmmoModifers == null)
            {
                Core.Enforcement.AmmoModifers = new[]
                {
                    new AmmoModifer {Name = "TestAmmo1", DirectDamageModifer = 1f, AreaDamageModifer = 0.5f, DetonationDamageModifer = 3.5f},
                    new AmmoModifer {Name = "TestAmmo2", DirectDamageModifer = 2f, AreaDamageModifer = 0f, DetonationDamageModifer = 0f },
                };
            }
            if (write)
                SaveServerCfg();
        }


        private void GenerateBlockDmgMap()
        {
            if (Core.Enforcement.BlockModifers == null)
                return;

            foreach (var def in Core.Session.AllDefinitions)
            {
                foreach (var blockModifer in Core.Enforcement.BlockModifers)
                {
                    if ((blockModifer.AreaDamageModifer >= 0 || blockModifer.DirectDamageModifer >= 0) && def.Id.SubtypeId.String == blockModifer.SubTypeId)
                    {
                        Core.Session.GlobalDamageModifed = true;
                        Core.Session.BlockDamageMap[def] = new Session.BlockDamage { DirectModifer = blockModifer.DirectDamageModifer >= 0 ? blockModifer.DirectDamageModifer : 1, AreaModifer = blockModifer.AreaDamageModifer >= 0 ? blockModifer.AreaDamageModifer : 1 };
                    }
                }
            }
        }

        private void GenerateAmmoDmgMap()
        {
            if (Core.Enforcement.AmmoModifers == null)
                return;

            foreach (var modifer in Core.Enforcement.AmmoModifers)
                foreach (var pair in Core.Session.AmmoDamageMap)
                    if (modifer.Name == pair.Key.AmmoRound)
                        _tmpAmmoModiferMap[pair.Key] = modifer;

            foreach (var t in _tmpAmmoModiferMap)
                Core.Session.AmmoDamageMap[t.Key] = t.Value;

            _tmpAmmoModiferMap.Clear();
        }
    }
}
