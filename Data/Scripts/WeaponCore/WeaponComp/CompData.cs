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
                    validData = (load?.Set != null && load.State != null);
                }
                catch (Exception e)
                {
                    //Log.Line("Invalid State Loaded, Re-init");
                }
            }

            if (validData && load.Version == VersionControl)
            {
                Repo = load;
                if (Comp.Session.IsServer)
                    Repo.Targets = new TransferTarget[Comp.Platform.Weapons.Length];

                for (int i = 0; i < Comp.Platform.Weapons.Length; i++) {
                    var w = Comp.Platform.Weapons[i];
                    
                    w.State = Repo.State.Weapons[i];
                    w.Reload = Repo.Reloads[i];

                    if (Comp.Session.IsServer)  {
                        Repo.Targets[i] = new TransferTarget();
                        w.TargetData = Repo.Targets[i];
                        w.TargetData.WeaponRandom = new WeaponRandomGenerator();
                        w.TargetData.WeaponInit(w);
                    }
                    else
                    {
                        w.Reload = w.Comp.Data.Repo.Reloads[w.WeaponId];
                        w.TargetData = w.Comp.Data.Repo.Targets[w.WeaponId];
                        w.TargetData.WeaponRefreshClient(w);
                    }
                }
            }
            else {

                Repo = new CompDataValues {
                    State = new CompStateValues { Weapons = new WeaponStateValues[Comp.Platform.Weapons.Length]},
                    Set = new CompSettingsValues(),
                    Targets = new TransferTarget[Comp.Platform.Weapons.Length],
                    Reloads = new WeaponReloadValues[Comp.Platform.Weapons.Length],
                };

                for (int i = 0; i < Comp.Platform.Weapons.Length; i++) {
                    var state = Repo.State.Weapons[i] = new WeaponStateValues();
                    var reload = Repo.Reloads[i] = new WeaponReloadValues();
                    var w = Comp.Platform.Weapons[i];
                    
                    w.State = state;
                    w.Reload = reload;

                    Repo.Targets[i] = new TransferTarget();
                    w.TargetData = Repo.Targets[i];
                    w.TargetData.WeaponRandom = new WeaponRandomGenerator();
                    w.TargetData.WeaponInit(w);
                }

                Repo.Set.Range = -1;
            }
        }
    }
}
