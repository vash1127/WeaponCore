using System;
using Sandbox.ModAPI;

namespace CoreSystems.Platform
{
    public partial class Phantom 
    {
        internal class PhantomCompData : CompData
        {
            internal readonly PhantomComponent Comp;
            internal ProtoPhantomRepo Repo;

            internal PhantomCompData(PhantomComponent comp)
            {
                Init(comp);
                Comp = comp;
            }

            internal void Load()
            {
                if (Comp.CoreEntity.Storage == null) return;

                ProtoPhantomRepo load = null;
                string rawData;
                bool validData = false;
                if (Comp.CoreEntity.Storage.TryGetValue(Comp.Session.CompDataGuid, out rawData))
                {
                    try
                    {
                        var base64 = Convert.FromBase64String(rawData);
                        load = MyAPIGateway.Utilities.SerializeFromBinary<ProtoPhantomRepo>(base64);
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

                    for (int i = 0; i < Comp.Platform.Phantoms.Count; i++)
                    {
                        var p = Comp.Platform.Phantoms[i];

                        p.PartState = Repo.Values.State.Phantoms[i];

                        if (Comp.Session.IsServer)
                        {
                        }
                    }
                }
                else
                {
                    Repo = new ProtoPhantomRepo
                    {
                        Values = new ProtoPhantomComp
                        {
                            State = new ProtoPhantomState { Phantoms = new ProtoPhantomPartState[Comp.Platform.Support.Count] },
                            Set = new ProtoPhantomSettings(),
                        },
                    };

                    for (int i = 0; i < Comp.Platform.Phantoms.Count; i++)
                    {
                        var state = Repo.Values.State.Phantoms[i] = new ProtoPhantomPartState();
                        var p = Comp.Platform.Phantoms[i];

                        if (p != null)
                        {
                            p.PartState = state;
                        }
                    }

                    Repo.Values.Set.Range = -1;
                }
                ProtoRepoBase = Repo;
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
