using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    internal partial class Support : Part
    {
        internal class SupportCompData : CompData
        {
            internal readonly SupportSys.SupportComponent Comp;
            internal ProtoSupportRepo Repo;

            internal SupportCompData(SupportSys.SupportComponent comp)
            {
                base.Init(comp);
                Comp = comp;
                Repo = (ProtoSupportRepo)ProtoRepoBase;
            }

            internal void Load()
            {
                Log.Line($"start support load");
                if (Comp.CoreEntity.Storage == null) return;

                ProtoSupportRepo load = null;
                string rawData;
                bool validData = false;
                if (Comp.CoreEntity.Storage.TryGetValue(Comp.Session.CompDataGuid, out rawData))
                {
                    try
                    {
                        Log.Line($"found something");
                        var base64 = Convert.FromBase64String(rawData);
                        load = MyAPIGateway.Utilities.SerializeFromBinary<ProtoSupportRepo>(base64);
                        validData = load != null;
                    }
                    catch (Exception e)
                    {
                        //Log.Line("Invalid PartState Loaded, Re-init");
                    }
                }

                if (validData && load.Version == Session.VersionControl)
                {
                    Log.Line("loading something");
                    Repo = load;

                    for (int i = 0; i < Comp.Platform.Weapons.Count; i++)
                    {
                        var p = Comp.Platform.Support[i];

                        p.PartState = Repo.Values.State.Support[i];

                        if (Comp.Session.IsServer)
                        {
                        }
                        else
                        {
                        }
                    }
                }
                else
                {
                    Log.Line($"creating something");
                    Repo = new ProtoSupportRepo
                    {
                        Values = new ProtoSupportComp
                        {
                            State = new ProtoSupportState { Support = new ProtoSupportPartState[Comp.Platform.Support.Count] },
                            Set = new ProtoSupportSettings(),
                        },
                    };

                    for (int i = 0; i < Comp.Platform.Support.Count; i++)
                    {
                        var state = Repo.Values.State.Support[i] = new ProtoSupportPartState();
                        var p = Comp.Platform.Support[i];

                        if (p != null)
                        {
                            p.PartState = state;
                        }
                    }

                    Repo.Values.Set.Range = -1;
                }
            }

            internal void Change(DataState state)
            {
                switch (state)
                {
                    case DataState.Load:
                        Load();
                        break;
                    case DataState.Reset:
                        Repo.ResetToFreshLoadState();
                        break;
                }
            }
        }
    }
}
