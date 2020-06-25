using WeaponCore.Support;

namespace WeaponCore
{
    using System;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;
    using static Session;

    public class CompData
    {
        public readonly WeaponComponent Comp;
        public CompDataValues Repo;

        public CompData(WeaponComponent comp)
        {
            Comp = comp;
        }

        public void StorageInit()
        {
            if (Comp.MyCube.Storage == null)
            {
                Comp.MyCube.Storage = new MyModStorageComponent { [Comp.Session.LogicSettingsGuid] = "" };
            }
        }

        public void Save()
        {
            if (Comp.MyCube.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Repo);
            Comp.MyCube.Storage[Comp.Session.DataGuid] = Convert.ToBase64String(binary);
        }

        public bool Load()
        {
            if (Comp.MyCube.Storage == null) return false;

            CompDataValues loadedState = null;
            string rawData;
            bool loadedSomething = false;

            if (Comp.MyCube.Storage.TryGetValue(Comp.Session.DataGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    loadedState = MyAPIGateway.Utilities.SerializeFromBinary<CompDataValues>(base64);
                }
                catch (Exception e)
                {
                    //Log.Line("Invalid State Loaded, Re-init");
                }
            }

            if (loadedState != null && loadedState.Version == VersionControl)
            {
                Repo = loadedState;
                loadedSomething = true;
            }
            else {

                Repo = new CompDataValues {
                    State = new CompStateValues {

                        Weapons = new WeaponStateValues[Comp.Platform.Weapons.Length],
                        CurrentPlayerControl = new PlayerControl()
                    },
                    WepVal = new WeaponValues(),
                    Set = new CompSettingsValues {Weapons = new WeaponSettingsValues[Comp.Platform.Weapons.Length]}
                };

                for (int i = 0; i < Comp.Platform.Weapons.Length; i++) {
                    Repo.State.Weapons[i] = new WeaponStateValues { Sync = new WeaponSyncValues { WeaponId = i } };
                    Repo.Set.Weapons[i] = new WeaponSettingsValues();
                }

                Repo.Set.Range = -1;

                if (Comp.Session.IsServer)
                    WeaponValues.Init(Comp);
                else WeaponValues.RefreshClient(Comp);
            }

            return loadedSomething;
        }
    }
    /*
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
        */
}
