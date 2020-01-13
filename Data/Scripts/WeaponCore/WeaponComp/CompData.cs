using SpaceEngineers.Game.ModAPI;
using WeaponCore.Support;

namespace WeaponCore
{
    using System;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;

    public class CompState
    {
        public CompStateValues Value = new CompStateValues();
        public readonly WeaponComponent Comp;
        public readonly IMyConveyorSorter SorterBase;
        public readonly IMyLargeMissileTurret ControllableTurret;
        public readonly bool IsSorterTurret;
        public CompState(WeaponComponent comp)
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
            if (Value.Weapons.Length != Comp.Platform.Weapons.Length)
            {
                Array.Resize(ref Value.Weapons, Comp.Platform.Structure.MuzzlePartNames.Length);

                for (int i = 0; i < Comp.Platform.Weapons.Length; i++)
                    if (Value.Weapons[i] == null) Value.Weapons[i] = new WeaponStateValues();
            }

            if (IsSorterTurret)
            {
                if (SorterBase.Storage == null)
                {
                    SorterBase.Storage = new MyModStorageComponent { [Comp.Session.LogicSettingsGuid] = "" };
                }
            }
            else
            {
                if (ControllableTurret.Storage == null)
                {
                    ControllableTurret.Storage = new MyModStorageComponent { [Comp.Session.LogicSettingsGuid] = "" };
                }
            }
        }

        public void SaveState(bool createStorage = false)
        {
            if (IsSorterTurret) { 
                if (SorterBase.Storage == null) return;

                var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
                SorterBase.Storage[Comp.Session.LogicStateGuid] = Convert.ToBase64String(binary);
            }
            else
            {
                if (ControllableTurret.Storage == null) return;

                var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
                ControllableTurret.Storage[Comp.Session.LogicStateGuid] = Convert.ToBase64String(binary);
            }
        }

        public bool LoadState()
        {
            if ((IsSorterTurret && SorterBase.Storage == null) || (!IsSorterTurret && ControllableTurret.Storage == null)) return false;

            byte[] base64;
            CompStateValues loadedState = null;
            string rawData;
            bool loadedSomething = false;

            if (IsSorterTurret)
            {
                if (SorterBase.Storage.TryGetValue(Comp.Session.LogicStateGuid, out rawData))
                {
                    base64 = Convert.FromBase64String(rawData);
                    loadedState = MyAPIGateway.Utilities.SerializeFromBinary<CompStateValues>(base64);
                }
            }
            else
            {
                if (ControllableTurret.Storage.TryGetValue(Comp.Session.LogicStateGuid, out rawData))
                {
                    base64 = Convert.FromBase64String(rawData);
                    loadedState = MyAPIGateway.Utilities.SerializeFromBinary<CompStateValues>(base64);
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

            if (Comp.Session.IsServer)
            {
                Value.MId++;
                if(IsSorterTurret)
                    Comp.Session.PacketizeToClientsInRange(SorterBase, new DataCompState(SorterBase.EntityId, Value)); // update clients with server's state
                else
                    Comp.Session.PacketizeToClientsInRange(ControllableTurret, new DataCompState(ControllableTurret.EntityId, Value));
            }
        }
        #endregion
    }

    public class CompSettings
    {
        public CompSettingsValues Value = new CompSettingsValues();
        public readonly WeaponComponent Comp;
        public readonly IMyConveyorSorter SorterBase;
        public readonly IMyLargeMissileTurret MissileBase;
        public readonly bool IsSorterTurret;
        public CompSettings(WeaponComponent comp)
        {
            Comp = comp;
            SorterBase = comp.SorterBase;
            MissileBase = comp.MissileBase;
            IsSorterTurret = comp.IsSorterTurret;
            Value.Weapons = new WeaponSettingsValues[Comp.Platform.Weapons.Length];
            for (int i = 0; i < Comp.Platform.Weapons.Length; i++)
                if (Value.Weapons[i] == null) Value.Weapons[i] = new WeaponSettingsValues();
        }

        public void SettingsInit()
        {
            if (Value.Weapons.Length != Comp.Platform.Weapons.Length)
            {
                Array.Resize(ref Value.Weapons, Comp.Platform.Structure.MuzzlePartNames.Length);

                for (int i = 0; i < Comp.Platform.Weapons.Length; i++)
                    if (Value.Weapons[i] == null) Value.Weapons[i] = new WeaponSettingsValues();
            }

            var maxTrajectory = 0f;
            for (int i = 0; i < Comp.Platform.Weapons.Length; i++)
                if (maxTrajectory < Comp.Platform.Weapons[i].System.MaxTrajectory) maxTrajectory = (float)Comp.Platform.Weapons[i].System.MaxTrajectory;

            Value.Range = IsSorterTurret ? maxTrajectory : MissileBase.Range;
        }

        public void SaveSettings(bool createStorage = false)
        {
            if ((IsSorterTurret && SorterBase.Storage == null) || (!IsSorterTurret && MissileBase.Storage == null)) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);

            if(IsSorterTurret)
                SorterBase.Storage[Comp.Session.LogicSettingsGuid] = Convert.ToBase64String(binary);
            else
                MissileBase.Storage[Comp.Session.LogicSettingsGuid] = Convert.ToBase64String(binary);
        }

        public bool LoadSettings()
        {
            if ((IsSorterTurret && SorterBase.Storage == null) || (!IsSorterTurret && MissileBase.Storage == null)) return false;
            string rawData;
            bool loadedSomething = false;
            byte[] base64;
            CompSettingsValues loadedSettings = null;


            if (IsSorterTurret && SorterBase.Storage.TryGetValue(Comp.Session.LogicSettingsGuid, out rawData))
            {
                base64 = Convert.FromBase64String(rawData);
                loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<CompSettingsValues>(base64);
                
            }
            else if (MissileBase.Storage.TryGetValue(Comp.Session.LogicSettingsGuid, out rawData))
            {
                base64 = Convert.FromBase64String(rawData);
                loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<CompSettingsValues>(base64);
            }

            if (loadedSettings?.Weapons != null)
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
            if (Comp.Session.IsServer)
            {
                if(IsSorterTurret)
                    Comp.Session.PacketizeToClientsInRange(SorterBase, new DataCompSettings(SorterBase.EntityId, Value)); // update clients with server's settings
                else
                    Comp.Session.PacketizeToClientsInRange(MissileBase, new DataCompSettings(MissileBase.EntityId, Value));
            }
            else // client, send settings to server
            {
                byte[] bytes = null;
                if(IsSorterTurret)
                    MyAPIGateway.Utilities.SerializeToBinary(new DataCompSettings(SorterBase.EntityId, Value));
                else
                    MyAPIGateway.Utilities.SerializeToBinary(new DataCompSettings(MissileBase.EntityId, Value));

                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PacketId, bytes);
            }
        }
        #endregion
    }
}
