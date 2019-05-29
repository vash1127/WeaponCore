using WeaponCore.Support;

namespace WeaponCore
{
    using System;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;

    public class LogicState
    {
        internal LogicStateValues Value = new LogicStateValues();
        internal readonly IMyFunctionalBlock Logic;
        internal LogicState(IMyFunctionalBlock logic)
        {
            Logic = logic;
        }

        internal void StorageInit()
        {
            if (Logic.Storage == null)
            {
                Logic.Storage = new MyModStorageComponent {[Session.Instance.LogicSettingsGuid] = ""};
            }
        }

        internal void SaveState(bool createStorage = false)
        {
            if (createStorage && Logic.Storage == null) Logic.Storage = new MyModStorageComponent();
            else if (Logic.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
            Logic.Storage[Session.Instance.LogicStateGuid] = Convert.ToBase64String(binary);
        }

        internal bool LoadState()
        {
            if (Logic.Storage == null) return false;

            string rawData;
            bool loadedSomething = false;

            if (Logic.Storage.TryGetValue(Session.Instance.LogicStateGuid, out rawData))
            {
                LogicStateValues loadedState = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedState = MyAPIGateway.Utilities.SerializeFromBinary<LogicStateValues>(base64);

                if (loadedState != null)
                {
                    Value = loadedState;
                    loadedSomething = true;
                }
            }
            return loadedSomething;
        }

        #region Network
        internal void NetworkUpdate()
        {

            if (Session.Instance.IsServer)
            {
                Value.MId++;
                Session.Instance.PacketizeToClientsInRange(Logic, new DataLogicState(Logic.EntityId, Value)); // update clients with server's state
            }
        }
        #endregion
    }

    internal class LogicSettings
    {
        internal LogicSettingsValues Value = new LogicSettingsValues();
        internal readonly IMyFunctionalBlock Logic;
        internal LogicSettings(IMyFunctionalBlock logic)
        {
            Logic = logic;
        }

        internal void SaveSettings(bool createStorage = false)
        {
            if (createStorage && Logic.Storage == null) Logic.Storage = new MyModStorageComponent();
            else if (Logic.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(Value);
            Logic.Storage[Session.Instance.LogicSettingsGuid] = Convert.ToBase64String(binary);
        }

        internal bool LoadSettings()
        {
            if (Logic.Storage == null) return false;
            string rawData;
            bool loadedSomething = false;

            if (Logic.Storage.TryGetValue(Session.Instance.LogicSettingsGuid, out rawData))
            {
                LogicSettingsValues loadedSettings = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<LogicSettingsValues>(base64);

                if (loadedSettings != null)
                {
                    Value = loadedSettings;
                    loadedSomething = true;
                }
                //if (Session.Enforced.Debug == 3) Log.Line($"Loaded -LogicId [{Logic.EntityId}]:\n{Value.ToString()}");
            }
            return loadedSomething;
        }

        #region Network
        internal void NetworkUpdate()
        {
            Value.MId++;
            if (Session.Instance.IsServer)
            {
                Session.Instance.PacketizeToClientsInRange(Logic, new DataLogicSettings(Logic.EntityId, Value)); // update clients with server's settings
            }
            else // client, send settings to server
            {
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(new DataLogicSettings(Logic.EntityId, Value));
                MyAPIGateway.Multiplayer.SendMessageToServer(Session.PACKET_ID, bytes);
            }
        }
        #endregion
    }
}
