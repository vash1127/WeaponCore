using System;
using Sandbox.ModAPI;

namespace WeaponCore.Platform
{
    public partial class Weapon : Part
    {
        internal class WeaponCompData : CompData
        {
            internal readonly WeaponComponent Comp;
            internal WeaponRepo Repo;
            internal WeaponCompData(WeaponComponent comp)
            {
                base.Init(comp);
                Comp = comp;
                Repo = (WeaponRepo)RepoBase;
            }

            internal void Load()
            {
                if (Comp.CoreEntity.Storage == null) return;

                WeaponRepo load = null;
                string rawData;
                bool validData = false;
                if (Comp.CoreEntity.Storage.TryGetValue(Comp.Session.CompDataGuid, out rawData))
                {
                    try
                    {
                        var base64 = Convert.FromBase64String(rawData);
                        load = MyAPIGateway.Utilities.SerializeFromBinary<WeaponRepo>(base64);
                        validData = load != null;
                    }
                    catch (Exception e)
                    {
                        //Log.Line("Invalid State Loaded, Re-init");
                    }
                }

                if (validData && load.Version == Session.VersionControl)
                {
                    Repo = load;
                    if (Comp.Session.IsServer)
                        Repo.Base.Targets = new TransferTarget[Comp.Platform.Weapons.Count];

                    for (int i = 0; i < Comp.Platform.Weapons.Count; i++)
                    {
                        var w = Comp.Platform.Weapons[i];

                        w.State = Repo.Base.State.Weapons[i];
                        w.Reload = Repo.Base.Reloads[i];
                        w.Ammo = Repo.Ammos[i];

                        if (Comp.Session.IsServer)
                        {
                            Repo.Base.Targets[i] = new TransferTarget();
                            w.TargetData = Repo.Base.Targets[i];
                            w.TargetData.WeaponRandom = new WeaponRandomGenerator();
                            w.TargetData.WeaponInit(w);
                        }
                        else
                        {
                            w.Ammo = Repo.Ammos[i];
                            w.ClientStartId = w.Reload.StartId;
                            w.ClientEndId = w.Reload.EndId;
                            w.TargetData = Repo.Base.Targets[i];
                            w.TargetData.PartRefreshClient(w);
                        }

                    }
                }
                else
                {
                    Repo = new WeaponRepo
                    {
                        Base = new CompBaseValues
                        {
                            State = new CompStateValues { Weapons = new WeaponStateValues[Comp.Platform.Weapons.Count] },
                            Set = new CompSettingsValues(),
                            Targets = new TransferTarget[Comp.Platform.Weapons.Count],
                            Reloads = new WeaponReloadValues[Comp.Platform.Weapons.Count],
                        },
                        Ammos = new AmmoValues[Comp.Platform.Weapons.Count],

                    };

                    for (int i = 0; i < Comp.Platform.Weapons.Count; i++)
                    {
                        var state = Repo.Base.State.Weapons[i] = new WeaponStateValues();
                        var reload = Repo.Base.Reloads[i] = new WeaponReloadValues();
                        var ammo = Repo.Ammos[i] = new AmmoValues();
                        var w = Comp.Platform.Weapons[i];

                        if (w != null)
                        {
                            w.State = state;
                            w.Reload = reload;
                            w.Ammo = ammo;

                            Repo.Base.Targets[i] = new TransferTarget();
                            w.TargetData = Repo.Base.Targets[i];
                            w.TargetData.WeaponRandom = new WeaponRandomGenerator();
                            w.TargetData.WeaponInit(w);
                        }
                    }

                    Repo.Base.Set.Range = -1;
                }
            }
        }

    }
}
