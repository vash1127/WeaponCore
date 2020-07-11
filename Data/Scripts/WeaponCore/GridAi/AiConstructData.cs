using Sandbox.Game.Entities;
using VRage.Game.Components;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
namespace WeaponCore
{
    using System;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;

    public class ConstructData
    {
        internal Constructs Construct;
        public ConstructDataValues Repo;


        internal void Init(Constructs construct)
        {
            Construct = construct;
            if (!Construct.RootAi.MyGrid.Components.Has<AiComponent>())
                Construct.RootAi.MyGrid.Components.Add(new AiComponent(Construct.RootAi.Session, Construct.RootAi.MyGrid));

            StorageInit();
            Load();

            if (Construct.RootAi.Session.IsServer)
            {
                Repo.FocusData = new FocusData { Target = new long[2] };
                foreach (var bg in Repo.BlockGroups)
                {
                    bg.Value.CompIds.Clear();
                    bg.Value.ChangeState = GroupInfo.ChangeStates.None;
                }
            }
        }

        public void Clean()
        {
            Repo.FocusData = null;
            Repo = null;
            Construct = null;
        }

        public void StorageInit()
        {
            if (Construct.RootAi.MyGrid.Storage == null)
            {
                Construct.RootAi.MyGrid.Storage = new MyModStorageComponent { [Construct.RootAi.Session.ConstructDataGuid] = "" };
            }
        }

        public void Save()
        {
            if (Construct.RootAi.MyGrid.Storage == null) return;
            Construct.RootAi.LastAiDataSave = Construct.RootAi.Session.Tick;
            var binary = MyAPIGateway.Utilities.SerializeToBinary(Repo);
            Construct.RootAi.MyGrid.Storage[Construct.RootAi.Session.ConstructDataGuid] = Convert.ToBase64String(binary);
        }

        public void Load()
        {
            if (Construct.RootAi.MyGrid.Storage == null) return;

            ConstructDataValues load = null;
            string rawData;
            bool validData = false;

            if (Construct.RootAi.MyGrid.Storage.TryGetValue(Construct.RootAi.Session.ConstructDataGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    load = MyAPIGateway.Utilities.SerializeFromBinary<ConstructDataValues>(base64);
                    validData = load != null;
                }
                catch (Exception e)
                {
                    //Log.Line("Invalid State Loaded, Re-init");
                }
            }

            Repo = validData ? load : new ConstructDataValues();
            if (Repo.FocusData == null)
                Repo.FocusData = new FocusData {Target = new long[2]};
        }
    }
}
