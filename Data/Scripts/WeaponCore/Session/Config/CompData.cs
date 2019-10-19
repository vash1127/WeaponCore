using SpaceEngineers.Game.ModAPI;
using WeaponCore.Support;

namespace WeaponCore
{
    using System;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;

    public class LogicState
    {
        public LogicStateValues Value = new LogicStateValues();
        public readonly WeaponComponent Comp;
        public readonly IMyConveyorSorter AiOnlyTurret;
        public readonly IMyLargeMissileTurret ControllableTurret;
        public readonly bool IsAiOnlyTurret;
        public LogicState(WeaponComponent comp)
        {
            Comp = comp;
            AiOnlyTurret = comp.AiOnlyTurret;
            ControllableTurret = comp.ControllableTurret;
            IsAiOnlyTurret = comp.IsAiOnlyTurret;
            Value.Weapons = new WeaponStateValues[Comp.Platform.Weapons.Length];
            for (int i = 0; i < Comp.Platform.Weapons.Length; i++)
                if (Value.Weapons[i] == null) Value.Weapons[i] = new WeaponStateValues();
        }

        public void StorageInit()
        {
            if (IsAiOnlyTurret)
            {
                if (AiOnlyTurret.Storage == null)
                {
                    AiOnlyTurret.Storage = new MyModStorageComponent { [Comp.Ai.Session.LogicSettingsGuid] = "" };
                }
            }
            else
            {
                if (ControllableTurret.Storage == null)
                {
                    ControllableTurret.Storage = new MyModStorageComponent { [Comp.Ai.Session.LogicSettingsGuid] = "" };
                }
            }
        }

        public void SaveState(bool createStorage = false)
        {
            if (IsAiOnlyTurret) { 
                if (AiOnlyTurret.Storage == null) return;

                var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
                AiOnlyTurret.Storage[Comp.Ai.Session.LogicStateGuid] = Convert.ToBase64String(binary);
            }
            else
            {
                if (ControllableTurret.Storage == null) return;

                var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
                ControllableTurret.Storage[Comp.Ai.Session.LogicStateGuid] = Convert.ToBase64String(binary);
            }
        }

        public bool LoadState()
        {
            if ((IsAiOnlyTurret && AiOnlyTurret.Storage == null) || (!IsAiOnlyTurret && ControllableTurret.Storage == null)) return false;

            byte[] base64;
            LogicStateValues loadedState = null;
            string rawData;
            bool loadedSomething = false;

            if (IsAiOnlyTurret)
            {
                if (AiOnlyTurret.Storage.TryGetValue(Comp.Ai.Session.LogicStateGuid, out rawData))
                {
                    base64 = Convert.FromBase64String(rawData);
                    loadedState = MyAPIGateway.Utilities.SerializeFromBinary<LogicStateValues>(base64);
                }
            }
            else
            {
                if (ControllableTurret.Storage.TryGetValue(Comp.Ai.Session.LogicStateGuid, out rawData))
                {
                    base64 = Convert.FromBase64String(rawData);
                    loadedState = MyAPIGateway.Utilities.SerializeFromBinary<LogicStateValues>(base64);
                }
            }

            if (loadedState != null)
            {
                Value = loadedState;
                loadedSomething = true;
            }
            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {

            if (Comp.Ai.Session.IsServer)
            {
                Value.MId++;
                if(IsAiOnlyTurret)
                    Comp.Ai.Session.PacketizeToClientsInRange(AiOnlyTurret, new DataLogicState(AiOnlyTurret.EntityId, Value)); // update clients with server's state
                else
                    Comp.Ai.Session.PacketizeToClientsInRange(ControllableTurret, new DataLogicState(ControllableTurret.EntityId, Value));
            }
        }
        #endregion
    }

    public class LogicSettings
    {
        public LogicSettingsValues Value = new LogicSettingsValues();
        public readonly WeaponComponent Comp;
        public readonly IMyConveyorSorter AiOnlyTurret;
        public readonly IMyLargeMissileTurret ControllableTurret;
        public readonly bool isAiOnlyTurret;
        public LogicSettings(WeaponComponent comp)
        {
            Comp = comp;
            AiOnlyTurret = comp.AiOnlyTurret;
            ControllableTurret = comp.ControllableTurret;
            isAiOnlyTurret = comp.IsAiOnlyTurret;
            Value.Weapons = new WeaponSettingsValues[Comp.Platform.Weapons.Length];
            for (int i = 0; i < Comp.Platform.Weapons.Length; i++)
                if (Value.Weapons[i] == null) Value.Weapons[i] = new WeaponSettingsValues();
        }

        public void SaveSettings(bool createStorage = false)
        {
            if ((isAiOnlyTurret && AiOnlyTurret.Storage == null) || (!isAiOnlyTurret && ControllableTurret.Storage == null)) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);

            if(isAiOnlyTurret)
                AiOnlyTurret.Storage[Comp.Ai.Session.LogicSettingsGuid] = Convert.ToBase64String(binary);
            else
                ControllableTurret.Storage[Comp.Ai.Session.LogicSettingsGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadSettings()
        {
            if ((isAiOnlyTurret && AiOnlyTurret.Storage == null) || (!isAiOnlyTurret && ControllableTurret.Storage == null)) return false;
            string rawData;
            bool loadedSomething = false;
            byte[] base64;
            LogicSettingsValues loadedSettings = null;


            if (isAiOnlyTurret && AiOnlyTurret.Storage.TryGetValue(Comp.Ai.Session.LogicSettingsGuid, out rawData))
            {
                base64 = Convert.FromBase64String(rawData);
                loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<LogicSettingsValues>(base64);
                
            }
            else if (ControllableTurret.Storage.TryGetValue(Comp.Ai.Session.LogicSettingsGuid, out rawData))
            {
                base64 = Convert.FromBase64String(rawData);
                loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<LogicSettingsValues>(base64);
            }

            if (loadedSettings != null && loadedSettings.Weapons != null)
            {
                Value = loadedSettings;
                loadedSomething = true;
            }
            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {
            Value.MId++;
            if (Comp.Ai.Session.IsServer)
            {
                if(isAiOnlyTurret)
                    Comp.Ai.Session.PacketizeToClientsInRange(AiOnlyTurret, new DataLogicSettings(AiOnlyTurret.EntityId, Value)); // update clients with server's settings
                else
                    Comp.Ai.Session.PacketizeToClientsInRange(ControllableTurret, new DataLogicSettings(ControllableTurret.EntityId, Value));
            }
            else // client, send settings to server
            {
                byte[] bytes = null;
                if(isAiOnlyTurret)
                    MyAPIGateway.Utilities.SerializeToBinary(new DataLogicSettings(AiOnlyTurret.EntityId, Value));
                else
                    MyAPIGateway.Utilities.SerializeToBinary(new DataLogicSettings(ControllableTurret.EntityId, Value));

                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID, bytes);
            }
        }
        #endregion
    }
}
