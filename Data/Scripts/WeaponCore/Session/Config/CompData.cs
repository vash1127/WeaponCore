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
        public readonly IMyConveyorSorter SorterBase;
        public readonly IMyLargeMissileTurret ControllableTurret;
        public readonly bool IsSorterTurret;
        public LogicState(WeaponComponent comp)
        {
            Comp = comp;
            SorterBase = comp.SorterBase;
            ControllableTurret = comp.MissileBase;
            IsSorterTurret = comp.IsSorterTurret;
            Value.Weapons = new WeaponStateValues[Comp.Platform.Weapons.Length];
            for (int i = 0; i < Comp.Platform.Weapons.Length; i++)
                if (Value.Weapons[i] == null) Value.Weapons[i] = new WeaponStateValues();
        }

        public void StorageInit()
        {
            if (IsSorterTurret)
            {
                if (SorterBase.Storage == null)
                {
                    SorterBase.Storage = new MyModStorageComponent { [Comp.Ai.Session.LogicSettingsGuid] = "" };
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
            if (IsSorterTurret) { 
                if (SorterBase.Storage == null) return;

                var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
                SorterBase.Storage[Comp.Ai.Session.LogicStateGuid] = Convert.ToBase64String(binary);
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
            if ((IsSorterTurret && SorterBase.Storage == null) || (!IsSorterTurret && ControllableTurret.Storage == null)) return false;

            byte[] base64;
            LogicStateValues loadedState = null;
            string rawData;
            bool loadedSomething = false;

            if (IsSorterTurret)
            {
                if (SorterBase.Storage.TryGetValue(Comp.Ai.Session.LogicStateGuid, out rawData))
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
                if(IsSorterTurret)
                    Comp.Ai.Session.PacketizeToClientsInRange(SorterBase, new DataLogicState(SorterBase.EntityId, Value)); // update clients with server's state
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
        public readonly IMyConveyorSorter SorterBase;
        public readonly IMyLargeMissileTurret MissileBase;
        public readonly bool IsSorterTurret;
        public LogicSettings(WeaponComponent comp)
        {
            Comp = comp;
            SorterBase = comp.SorterBase;
            MissileBase = comp.MissileBase;
            IsSorterTurret = comp.IsSorterTurret;
            Value.Weapons = new WeaponSettingsValues[Comp.Platform.Weapons.Length];
            for (int i = 0; i < Comp.Platform.Weapons.Length; i++)
                if (Value.Weapons[i] == null) Value.Weapons[i] = new WeaponSettingsValues();
        }

        public void SaveSettings(bool createStorage = false)
        {
            if ((IsSorterTurret && SorterBase.Storage == null) || (!IsSorterTurret && MissileBase.Storage == null)) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);

            if(IsSorterTurret)
                SorterBase.Storage[Comp.Ai.Session.LogicSettingsGuid] = Convert.ToBase64String(binary);
            else
                MissileBase.Storage[Comp.Ai.Session.LogicSettingsGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadSettings()
        {
            if ((IsSorterTurret && SorterBase.Storage == null) || (!IsSorterTurret && MissileBase.Storage == null)) return false;
            string rawData;
            bool loadedSomething = false;
            byte[] base64;
            LogicSettingsValues loadedSettings = null;


            if (IsSorterTurret && SorterBase.Storage.TryGetValue(Comp.Ai.Session.LogicSettingsGuid, out rawData))
            {
                base64 = Convert.FromBase64String(rawData);
                loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<LogicSettingsValues>(base64);
                
            }
            else if (MissileBase.Storage.TryGetValue(Comp.Ai.Session.LogicSettingsGuid, out rawData))
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
                if(IsSorterTurret)
                    Comp.Ai.Session.PacketizeToClientsInRange(SorterBase, new DataLogicSettings(SorterBase.EntityId, Value)); // update clients with server's settings
                else
                    Comp.Ai.Session.PacketizeToClientsInRange(MissileBase, new DataLogicSettings(MissileBase.EntityId, Value));
            }
            else // client, send settings to server
            {
                byte[] bytes = null;
                if(IsSorterTurret)
                    MyAPIGateway.Utilities.SerializeToBinary(new DataLogicSettings(SorterBase.EntityId, Value));
                else
                    MyAPIGateway.Utilities.SerializeToBinary(new DataLogicSettings(MissileBase.EntityId, Value));

                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID, bytes);
            }
        }
        #endregion
    }
}
