using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.BlockType;

namespace WeaponCore
{
    using System;
    using Sandbox.Game.Entities;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;
    using static Session;

    public class CompState
    {
        public CompStateValues Value;
        public readonly WeaponComponent Comp;
        public readonly MyCubeBlock Block;

        public CompState(WeaponComponent comp)
        {
            Comp = comp;
            Block = comp.MyCube;
        }

        public void StorageInit()
        {
            if (Block.Storage == null)
            {
                Block.Storage = new MyModStorageComponent { [Comp.Session.LogicSettingsGuid] = "" };
            }            
        }

        public void SaveState(bool createStorage = false)
        {
            if (Block.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
            Block.Storage[Comp.Session.LogicStateGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadState()
        {
            if (Block.Storage == null) return false;

            CompStateValues loadedState = null;
            string rawData;
            bool loadedSomething = false;

            if (Block.Storage.TryGetValue(Comp.Session.LogicStateGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    loadedState = MyAPIGateway.Utilities.SerializeFromBinary<CompStateValues>(base64);
                }
                catch(Exception e)
                {
                    //Log.Line("Invalid State Loaded, Re-init");
                }
            }

            if (loadedState != null && loadedState.Version == VersionControl)
            {
                Value = loadedState;
                loadedSomething = true;
            }
            else
            {
                Value = new CompStateValues { Weapons = new WeaponStateValues[Comp.Platform.Weapons.Length] };
                for (int i = 0; i < Value.Weapons.Length; i++)
                {
                    Value.Weapons[i] = new WeaponStateValues {Sync = new WeaponSyncValues() { WeaponId = i} };
                }
                Value.CurrentPlayerControl = new PlayerControl();
            }

            for(int i = 0; i < Comp.Platform.Weapons.Length; i++)
                Comp.Platform.Weapons[i].State = Value.Weapons[i];

            return loadedSomething;
        }
    }

    public class CompSettings
    {
        public CompSettingsValues Value;
        public readonly WeaponComponent Comp;
        public readonly MyCubeBlock Block;

        public CompSettings(WeaponComponent comp)
        {
            Comp = comp;
            Block = comp.MyCube;
        }

        public void SaveSettings(bool createStorage = false)
        {
            if (Block?.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
            Comp.MyCube.Storage[Comp.Session.LogicSettingsGuid] = Convert.ToBase64String(binary);
            
        }

        public bool LoadSettings()
        {
            if (Block?.Storage == null) return false;
            string rawData;
            bool loadedSomething = false;
            CompSettingsValues loadedSettings = null;


            if (Block.Storage.TryGetValue(Comp.Session.LogicSettingsGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<CompSettingsValues>(base64);
                }
                catch (Exception e)
                {
                    //Log.Line("Invalid Stettings Loaded, Re-init");
                }
            }

            if (loadedSettings?.Weapons != null && loadedSettings.Version == VersionControl)
            {
                Value = loadedSettings;
                loadedSomething = true;
            }
            else
            {
                Value = new CompSettingsValues {Weapons = new WeaponSettingsValues[Comp.Platform.Weapons.Length]};
                for (int i = 0; i < Value.Weapons.Length; i++) Value.Weapons[i] = new WeaponSettingsValues();

                Value.Range = -1;
            }
            return loadedSomething;
        }        
    }
}
