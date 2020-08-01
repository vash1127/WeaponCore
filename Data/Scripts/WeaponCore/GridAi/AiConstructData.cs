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
        internal GridAi Ai;
        public ConstructDataValues Repo;


        internal void Init(GridAi ai)
        {
            Ai = ai;

            StorageInit();
            Load();

            if (Ai.Session.IsServer)
            {
                Repo.FocusData = new FocusData { Target = new long[2], Locked = new FocusData.LockModes[2]};
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
            Ai = null;
        }

        public void StorageInit()
        {
            if (Ai.MyGrid.Storage == null)
                Ai.MyGrid.Storage = new MyModStorageComponent { [Ai.Session.ConstructDataGuid] = "" };
            else if (!Ai.MyGrid.Storage.ContainsKey(Ai.Session.ConstructDataGuid))
                Ai.MyGrid.Storage[Ai.Session.ConstructDataGuid] = "";
        }

        public void Save()
        {
            if (Ai.MyGrid.Storage == null)  return;
            var binary = MyAPIGateway.Utilities.SerializeToBinary(Repo);
            Ai.MyGrid.Storage[Ai.Session.ConstructDataGuid] = Convert.ToBase64String(binary);
        }

        public void Load()
        {
            if (Ai.MyGrid.Storage == null) return;

            ConstructDataValues load = null;
            string rawData;
            bool validData = false;

            if (Ai.MyGrid.Storage.TryGetValue(Ai.Session.ConstructDataGuid, out rawData))
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
            else Log.Line($"Storage didn't contain ConstructDataGuid");

            if (validData && load.Version == Session.VersionControl)
                Repo = load;
            else Repo = new ConstructDataValues();

            if (Repo.FocusData == null)
                Repo.FocusData = new FocusData {Target = new long[2], Locked = new FocusData.LockModes[2] };
        }
    }
}
