﻿using Sandbox.Game.Entities;
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
        public Ai Ai;
        public AiDataValues Repo;


        public void Init(Ai ai)
        {
            Ai = ai;

            StorageInit();
            Load();
            if (Ai.Session.IsServer)
            {
                Repo.ControllingPlayers.Clear();
            }
        }

        public void Clean()
        {
            Ai = null;
            Repo = null;
        }

        public void StorageInit()
        {
            if (Ai.TopEntity.Storage == null)
                Ai.TopEntity.Storage = new MyModStorageComponent { [Ai.Session.AiDataGuid] = "" };
            else if (!Ai.TopEntity.Storage.ContainsKey(Ai.Session.AiDataGuid))
                Ai.TopEntity.Storage[Ai.Session.AiDataGuid] = "";
        }

        public void Save()
        {
            if (Ai.TopEntity.Storage == null) return;
            Ai.LastAiDataSave = Ai.Session.Tick;
            var binary = MyAPIGateway.Utilities.SerializeToBinary(Repo);
            Ai.TopEntity.Storage[Ai.Session.AiDataGuid] = Convert.ToBase64String(binary);
        }

        public void Load()
        {
            if (Ai.TopEntity.Storage == null) return;

            AiDataValues load = null;
            string rawData;
            bool validData = false;

            if (Ai.TopEntity.Storage.TryGetValue(Ai.Session.AiDataGuid, out rawData))
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
                Repo = load;
            else 
                Repo = new AiDataValues();
        }
    }
}