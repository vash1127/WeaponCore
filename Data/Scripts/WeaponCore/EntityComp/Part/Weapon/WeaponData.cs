using System;
using Sandbox.ModAPI;

namespace WeaponCore.Platform
{
    public partial class Weapon : Part
    {
        internal class WeaponCompData : CompData
        {
            internal readonly WeaponComponent Comp;
            internal ProtoWeaponRepo Repo;
            internal WeaponCompData(WeaponComponent comp)
            {
                base.Init(comp);
                Comp = comp;
                Repo = (ProtoWeaponRepo)ProtoRepoBase;
            }

            internal void Load()
            {
                if (Comp.CoreEntity.Storage == null) return;

                ProtoWeaponRepo load = null;
                string rawData;
                bool validData = false;
                if (Comp.CoreEntity.Storage.TryGetValue(Comp.Session.CompDataGuid, out rawData))
                {
                    try
                    {
                        var base64 = Convert.FromBase64String(rawData);
                        load = MyAPIGateway.Utilities.SerializeFromBinary<ProtoWeaponRepo>(base64);
                        validData = load != null;
                    }
                    catch (Exception e)
                    {
                        //Log.Line("Invalid PartState Loaded, Re-init");
                    }
                }

                if (validData && load.Version == Session.VersionControl)
                {
                    Repo = load;
                    if (Comp.Session.IsServer)
                        Repo.Values.Targets = new ProtoWeaponTransferTarget[Comp.Platform.Weapons.Count];

                    for (int i = 0; i < Comp.Platform.Weapons.Count; i++)
                    {
                        var w = Comp.Platform.Weapons[i];

                        w.PartState = Repo.Values.State.Weapons[i];
                        w.Reload = Repo.Values.Reloads[i];
                        w.ProtoWeaponAmmo = Repo.Ammos[i];

                        if (Comp.Session.IsServer)
                        {
                            Repo.Values.Targets[i] = new ProtoWeaponTransferTarget();
                            w.TargetData = Repo.Values.Targets[i];
                            w.TargetData.WeaponRandom = new WeaponRandomGenerator();
                            w.TargetData.WeaponInit(w);
                        }
                        else
                        {
                            w.ProtoWeaponAmmo = Repo.Ammos[i];
                            w.ClientStartId = w.Reload.StartId;
                            w.ClientEndId = w.Reload.EndId;
                            w.TargetData = Repo.Values.Targets[i];
                            w.TargetData.PartRefreshClient(w);
                        }

                    }
                }
                else
                {
                    Repo = new ProtoWeaponRepo
                    {
                        Values = new ProtoWeaponComp
                        {
                            State = new ProtoWeaponState { Weapons = new ProtoWeaponPartState[Comp.Platform.Weapons.Count] },
                            Set = new ProtoWeaponSettings(),
                            Targets = new ProtoWeaponTransferTarget[Comp.Platform.Weapons.Count],
                            Reloads = new ProtoWeaponReload[Comp.Platform.Weapons.Count],
                        },
                        Ammos = new ProtoWeaponAmmo[Comp.Platform.Weapons.Count],

                    };

                    for (int i = 0; i < Comp.Platform.Weapons.Count; i++)
                    {
                        var state = Repo.Values.State.Weapons[i] = new ProtoWeaponPartState();
                        var reload = Repo.Values.Reloads[i] = new ProtoWeaponReload();
                        var ammo = Repo.Ammos[i] = new ProtoWeaponAmmo();
                        var w = Comp.Platform.Weapons[i];

                        if (w != null)
                        {
                            w.PartState = state;
                            w.Reload = reload;
                            w.ProtoWeaponAmmo = ammo;

                            Repo.Values.Targets[i] = new ProtoWeaponTransferTarget();
                            w.TargetData = Repo.Values.Targets[i];
                            w.TargetData.WeaponRandom = new WeaponRandomGenerator();
                            w.TargetData.WeaponInit(w);
                        }
                    }

                    Repo.Values.Set.Range = -1;
                }
            }
        }

    }
}
