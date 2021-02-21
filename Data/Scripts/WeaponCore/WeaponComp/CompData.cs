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
        public Repo Repo;

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

            Repo load = null;
            string rawData;
            bool validData = false;
            if (Comp.MyCube.Storage.TryGetValue(Comp.Session.CompDataGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    load = MyAPIGateway.Utilities.SerializeFromBinary<Repo>(base64);
                    validData = (load?.Base != null && load.Ammos != null);
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
                    Repo.Base.Targets = new TransferTarget[Comp.Platform.Weapons.Length];

                for (int i = 0; i < Comp.Platform.Weapons.Length; i++) {
                    var w = Comp.Platform.Weapons[i];
                    
                    w.State = Repo.Base.State.Weapons[i];
                    w.Reload = Repo.Base.Reloads[i];
                    w.Ammo = w.Comp.Data.Repo.Ammos[i];

                    if (Comp.Session.IsServer)  {
                        Repo.Base.Targets[i] = new TransferTarget();
                        w.TargetData = Repo.Base.Targets[i];
                        w.TargetData.WeaponRandom = new WeaponRandomGenerator();
                        w.TargetData.WeaponInit(w);
                    }
                    else
                    {
                        w.Ammo = w.Comp.Data.Repo.Ammos[i];
                        w.ClientStartId = w.Reload.StartId;
                        w.ClientEndId = w.Reload.EndId;
                        w.TargetData = w.Comp.Data.Repo.Base.Targets[i];
                        w.TargetData.WeaponRefreshClient(w);
                    }
                }
            }
            else {

                Repo = new Repo {
                    Base = new CompBaseValues
                    {
                        State = new CompStateValues { Weapons = new WeaponStateValues[Comp.Platform.Weapons.Length] },
                        Set = new CompSettingsValues(),
                        Targets = new TransferTarget[Comp.Platform.Weapons.Length],
                        Reloads = new WeaponReloadValues[Comp.Platform.Weapons.Length],
                    },
                    Ammos = new AmmoValues[Comp.Platform.Weapons.Length],

                };

                for (int i = 0; i < Comp.Platform.Weapons.Length; i++) {
                    var state = Repo.Base.State.Weapons[i] = new WeaponStateValues();
                    var reload = Repo.Base.Reloads[i] = new WeaponReloadValues();
                    var ammo = Repo.Ammos[i] = new AmmoValues();
                    var w = Comp.Platform.Weapons[i];
                    
                    w.State = state;
                    w.Reload = reload;
                    w.Ammo = ammo;

                    Repo.Base.Targets[i] = new TransferTarget();
                    w.TargetData = Repo.Base.Targets[i];
                    w.TargetData.WeaponRandom = new WeaponRandomGenerator();
                    w.TargetData.WeaponInit(w);
                }

                Repo.Base.Set.Range = -1;
            }
        }
    }
}
