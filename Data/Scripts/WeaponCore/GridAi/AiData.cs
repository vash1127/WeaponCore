using Sandbox.Game.Entities;
using VRage.Game.Components;
using WeaponCore.Support;

namespace WeaponCore
{
    using System;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;
    using static Session;

    public class AiData
    {
        public GridAi Ai;
        public AiDataValues Repo;


        public void Init(GridAi ai)
        {
            Ai = ai;
            if (!Ai.MyGrid.Components.Has<AiComponent>())
                Ai.MyGrid.Components.Add(new AiComponent(Ai.Session, Ai.MyGrid));

            StorageInit();
            Load();
        }

        public void Clean()
        {
            Ai = null;
            Repo = null;
        }

        public void StorageInit()
        {
            if (Ai.MyGrid.Storage == null)
            {
                Ai.MyGrid.Storage = new MyModStorageComponent { [Ai.Session.AiDataGuid] = "" };
            }
        }

        public void Save()
        {
            if (Ai.MyGrid.Storage == null) return;
            Ai.LastAiDataSave = Ai.Session.Tick;
            var binary = MyAPIGateway.Utilities.SerializeToBinary(Repo);
            Ai.MyGrid.Storage[Ai.Session.AiDataGuid] = Convert.ToBase64String(binary);
        }

        public void Load()
        {
            if (Ai.MyGrid.Storage == null) return;

            AiDataValues load = null;
            string rawData;
            bool validData = false;

            if (Ai.MyGrid.Storage.TryGetValue(Ai.Session.AiDataGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    load = MyAPIGateway.Utilities.SerializeFromBinary<AiDataValues>(base64);
                    validData = load != null;
                }
                catch (Exception e)
                {
                    //Log.Line("Invalid State Loaded, Re-init");
                }
            }

            if (validData && load.Version == VersionControl)
            {
                Repo = load;
            }
            else {

                Repo = new AiDataValues();
                Repo.ActiveTerminal.MyGridId = Ai.MyGrid.EntityId;

            }

            return;
        }
    }
}
