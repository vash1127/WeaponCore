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
                Comp.MyCube.Storage = new MyModStorageComponent { [Comp.Session.CompDataGuid] = "" };
            }
        }

        public void Save()
        {
            if (Comp.MyCube.Storage == null) return;

            if (Repo != null)
            {
                var binary = MyAPIGateway.Utilities.SerializeToBinary(Repo);
                Comp.MyCube.Storage[Comp.Session.CompDataGuid] = Convert.ToBase64String(binary);
            }

        }

        public void Load()
        {
            if (Comp.MyCube.Storage == null) return;

            CompDataValues load = null;
            string rawData;
            bool validData = false;
            if (Comp.MyCube.Storage.TryGetValue(Comp.Session.CompDataGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    load = MyAPIGateway.Utilities.SerializeFromBinary<CompDataValues>(base64);
                    validData = (load != null && load.Set != null && load.State != null);
                }
                catch (Exception e)
                {
                    //Log.Line("Invalid State Loaded, Re-init");
                }
            }

            if (validData && load.Version == VersionControl)
            {
                Repo = load;

                if (Comp.Session.IsServer) {
                    for (int i = 0; i < Comp.Platform.Weapons.Length; i++) {
                        var w = Comp.Platform.Weapons[i];
                        w.State = Repo.State.Weapons[i];
                        w.State.WeaponInit(w);
                    }
                }

            }
            else {

                Repo = new CompDataValues {
                    State = new CompStateValues { Weapons = new WeaponStateValues[Comp.Platform.Weapons.Length]},
                    Set = new CompSettingsValues(),
                };

                for (int i = 0; i < Comp.Platform.Weapons.Length; i++) {
                    var state = Repo.State.Weapons[i] = new WeaponStateValues();
                    var w = Comp.Platform.Weapons[i];
                    w.State = state;

                    if (Comp.Session.IsServer)
                        state.WeaponInit(w);
                    else state.WeaponRefreshClient(w);
                }

                Repo.Set.Range = -1;
            }
        }
    }
}
