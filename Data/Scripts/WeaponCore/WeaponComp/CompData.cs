using SpaceEngineers.Game.ModAPI;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.BlockType;

namespace WeaponCore
{
    using System;
    using Sandbox.Game.Entities;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;

    public class CompState
    {
        public CompStateValues Value;
        public readonly WeaponComponent Comp;
        public readonly MyCubeBlock Block;
        public uint LastUpdateTick;

        public CompState(WeaponComponent comp)
        {
            Comp = comp;
            Block = comp.MyCube;
        }

        public void StorageInit()
        {
            if (Block.Storage == null)
            {
                Block.Storage = new MyModStorageComponent { [Comp.Session.LogicSettingsGuid] = "" };
            }            
        }

        public void SaveState(bool createStorage = false)
        {
            if (Block.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
            Block.Storage[Comp.Session.LogicStateGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadState()
        {
            if (Block.Storage == null) return false;

            byte[] base64;
            CompStateValues loadedState = null;
            string rawData;
            bool loadedSomething = false;

            if (Block.Storage.TryGetValue(Comp.Session.LogicStateGuid, out rawData))
            {
                base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<CompStateValues>(base64);
            }

            if (loadedState != null)
            {
                Value = loadedState;
                loadedSomething = true;
            }
            else
            {
                Value = new CompStateValues { Weapons = new WeaponStateValues[Comp.Platform.Weapons.Length] };
                for (int i = 0; i < Value.Weapons.Length; i++) Value.Weapons[i] = new WeaponStateValues();
                Value.ManualControl = new PlayerControl();
            }
            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {
            Value.MId++;
            if (Comp.Session.IsServer || Comp.Session.DedicatedServer)
                    Comp.Session.PacketizeToClientsInRange(Comp.FunctionalBlock, new StatePacket {EntityId = Block.EntityId, SenderId = 0, PType = PacketType.CompStateUpdate, Data = Value});
            else // client, send settings to server
            {
                Log.Line($"Value.ShootOn: {Value.ShootOn}");
                Comp.Session.SendPacketToServer(new StatePacket { EntityId = Block.EntityId, PType = PacketType.CompStateUpdate, SenderId = MyAPIGateway.Multiplayer.MyId, Data = Value});
            }
        }
        #endregion
    }

    public class CompSettings
    {
        public CompSettingsValues Value;
        public CompSettingsValues LastValue;
        public readonly WeaponComponent Comp;
        public readonly MyCubeBlock Block;
        public uint LastUpdateTick;

        public CompSettings(WeaponComponent comp)
        {
            Comp = comp;
            Block = comp.MyCube;
        }

        public void SettingsInit()
        {
            var maxTrajectory = 0f;
            for (int i = 0; i < Comp.Platform.Weapons.Length; i++)
                if (maxTrajectory < Comp.Platform.Weapons[i].System.MaxTrajectory) maxTrajectory = (float)Comp.Platform.Weapons[i].System.MaxTrajectory;

            //TODO change this
            Value.Range =  Comp.BaseType != Turret ? maxTrajectory : Comp.TurretBase.Range;
        }

        public void SaveSettings(bool createStorage = false)
        {
            if (Block == null || Block.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
            Comp.MyCube.Storage[Comp.Session.LogicSettingsGuid] = Convert.ToBase64String(binary);
            
        }

        public bool LoadSettings()
        {
            if (Block == null || Block.Storage == null) return false;
            string rawData;
            bool loadedSomething = false;
            byte[] base64;
            CompSettingsValues loadedSettings = null;


            if (Block.Storage.TryGetValue(Comp.Session.LogicSettingsGuid, out rawData))
            {
                base64 = Convert.FromBase64String(rawData);
                loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<CompSettingsValues>(base64);
                
            }

            if (loadedSettings?.Weapons != null)
            {
                Value = loadedSettings;
                loadedSomething = true;
            }
            else
            {
                Value = new CompSettingsValues {Weapons = new WeaponSettingsValues[Comp.Platform.Weapons.Length]};
                for (int i = 0; i < Value.Weapons.Length; i++) Value.Weapons[i] = new WeaponSettingsValues();

            }
            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {
            Value.MId++;
            if (Comp.Session.IsServer || Comp.Session.DedicatedServer)
                Comp.Session.PacketizeToClientsInRange(Comp.FunctionalBlock, new SettingPacket { EntityId = Block.EntityId, SenderId = 0, PType = PacketType.CompStateUpdate, Data = Value });
            else // client, send settings to server
            {
                Comp.Session.SendPacketToServer(new SettingPacket { EntityId = Block.EntityId, PType = PacketType.CompSettingsUpdate, SenderId = MyAPIGateway.Multiplayer.MyId, Data = Value });
            }
        }
        #endregion
    }
}
