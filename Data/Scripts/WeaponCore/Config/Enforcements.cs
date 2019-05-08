using System;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using WeaponCore.Support;

namespace WeaponCore
{
    internal class Enforcements
    {
        internal static void SaveEnforcement(IMyFunctionalBlock shield, WeaponEnforcement enforce, bool createStorage = false)
        {
            if (createStorage && shield.Storage == null) shield.Storage = new MyModStorageComponent();
            else if (shield.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(enforce);
            shield.Storage[Session.Instance.LogicEnforceGuid] = Convert.ToBase64String(binary);
            if (Session.Enforced.Debug == 3) Log.Line($"Enforcement Saved - Version:{enforce.Version} - ControllerId [{shield.EntityId}]");
        }

        internal static WeaponEnforcement LoadEnforcement(IMyFunctionalBlock shield)
        {
            if (shield.Storage == null) return null;

            string rawData;

            if (shield.Storage.TryGetValue(Session.Instance.LogicEnforceGuid, out rawData))
            {
                WeaponEnforcement loadedEnforce = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedEnforce = MyAPIGateway.Utilities.SerializeFromBinary<WeaponEnforcement>(base64);
                if (Session.Enforced.Debug == 3) Log.Line($"Enforcement Loaded {loadedEnforce != null} - Version:{loadedEnforce?.Version} - ControllerId [{shield.EntityId}]");
                if (loadedEnforce != null) return loadedEnforce;
            }
            return null;
        }
    }
}
