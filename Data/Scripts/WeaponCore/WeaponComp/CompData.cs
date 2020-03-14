using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.BlockType;

namespace WeaponCore
{
    using System;
    using Sandbox.Game.Entities;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;
    using static Session;

    public class CompState
    {
        public CompStateValues Value;
        public readonly WeaponComponent Comp;
        public readonly MyCubeBlock Block;

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

            CompStateValues loadedState = null;
            string rawData;
            bool loadedSomething = false;

            if (Block.Storage.TryGetValue(Comp.Session.LogicStateGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    loadedState = MyAPIGateway.Utilities.SerializeFromBinary<CompStateValues>(base64);
                }
                catch(Exception e)
                {
                    Log.Line("Invalid State Loaded, Re-init");
                }
            }

            if (loadedState != null && loadedState.Version == VersionControl)
            {
                Value = loadedState;
                loadedSomething = true;
            }
            else
            {
                Value = new CompStateValues { Weapons = new WeaponStateValues[Comp.Platform.Weapons.Length] };
                for (int i = 0; i < Value.Weapons.Length; i++)
                {
                    Value.Weapons[i] = new WeaponStateValues {Sync = new WeaponSyncValues()};
                }
                Value.CurrentPlayerControl = new PlayerControl();
            }

            for(int i = 0; i < Comp.Platform.Weapons.Length; i++)
                Comp.Platform.Weapons[i].State = Value.Weapons[i];

            return loadedSomething;
        }

        #region Network
        public void NetworkUpdate()
        {
            Value.MId++;
            if (Comp.Session.MpActive && Comp.Session.IsServer)
            {
                Comp.Session.PacketsToClient.Add(new PacketInfo {
                    Entity = Comp.MyCube,
                    Packet = new StatePacket {
                        EntityId = Block.EntityId,
                        SenderId = 0,
                        PType = PacketType.CompStateUpdate,
                        Data = Value
                    }
                });
            }
            else if (Comp.Session.IsClient)
            { // client, send settings to server
                Comp.Session.PacketsToServer.Add(new StatePacket {
                    EntityId = Block.EntityId,
                    PType = PacketType.CompStateUpdate,
                    SenderId = Comp.Session.MultiplayerId,
                    Data = Value
                });
            }
        }
        #endregion
    }

    public class CompSettings
    {
        public CompSettingsValues Value;
        public readonly WeaponComponent Comp;
        public readonly MyCubeBlock Block;

        public CompSettings(WeaponComponent comp)
        {
            Comp = comp;
            Block = comp.MyCube;
        }

        public void SettingsInit()
        {
            var maxTrajectory = 0f;
            for (int i = 0; i < Comp.Platform.Weapons.Length; i++)
            {
                var weapon = Comp.Platform.Weapons[i];
                if (weapon.ActiveAmmoDef.AmmoDef == null) {
                    var ammoDef = weapon.System.WeaponAmmoTypes[0];
                    if (!ammoDef.AmmoDef.Const.IsTurretSelectable) {
                        Log.Line($"Your first ammoType cannot be a shrapnel with HardPointUsable set to false, I am crashing now Dave.");
                        return;
                    }
                    weapon.ActiveAmmoDef = ammoDef;
                }

                if (maxTrajectory < weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory) maxTrajectory = (float)weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;
            }

            //TODO change this
            Value.Range =  Comp.BaseType != Turret ? maxTrajectory : Comp.TurretBase.Range;
        }

        public void SaveSettings(bool createStorage = false)
        {
            if (Block?.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
            Comp.MyCube.Storage[Comp.Session.LogicSettingsGuid] = Convert.ToBase64String(binary);
            
        }

        public bool LoadSettings()
        {
            if (Block?.Storage == null) return false;
            string rawData;
            bool loadedSomething = false;
            CompSettingsValues loadedSettings = null;


            if (Block.Storage.TryGetValue(Comp.Session.LogicSettingsGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<CompSettingsValues>(base64);
                }
                catch (Exception e)
                {
                    Log.Line("Invalid Stettings Loaded, Re-init");
                }
            }

            if (loadedSettings?.Weapons != null && loadedSettings.Version == VersionControl)
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
        /*public void NetworkUpdate()
        {
            Value.MId++;
            if (Comp.Session.MpActive && Comp.Session.IsServer)
            {
                Comp.Session.PacketsToClient.Add(new PacketInfo {
                    Entity = Comp.MyCube,
                    Packet = new SettingPacket {
                        EntityId = Block.EntityId,
                        SenderId = 0,
                        PType = PacketType.CompStateUpdate,
                        Data = Value
                    }
                });
            }
            else if (Comp.Session.IsClient)// client, send settings to server
            {
                Comp.Session.PacketsToServer.Add(new SettingPacket {
                    EntityId = Block.EntityId,
                    PType = PacketType.CompSettingsUpdate,
                    SenderId = Comp.Session.MultiplayerId,
                    Data = Value
                });
            }
        }*/
        #endregion
    }
}
