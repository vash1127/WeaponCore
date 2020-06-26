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
                    State = new CompStateValues { Weapons = new WeaponStateValues[Comp.Platform.Weapons.Length]},
                    WepVal = new WeaponValues(),
                    Set = new CompSettingsValues {Weapons = new WeaponSettingsValues[Comp.Platform.Weapons.Length]}
                };

                for (int i = 0; i < Comp.Platform.Weapons.Length; i++) {
                    Repo.State.Weapons[i] = new WeaponStateValues();
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
}
