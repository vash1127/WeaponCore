using System;
using System.ComponentModel;
using ProtoBuf;
namespace WeaponCore.Data.Scripts.WeaponCore.Session.Config
{
    internal class Enforcements
    {
        internal CoresEnforcement Enforcement;

        internal Enforcements()
        {

        }

        [ProtoContract]
        public class CoresEnforcement
        {
            [ProtoMember(1), DefaultValue(-1)] public int Debug = -1;
            [ProtoMember(2), DefaultValue(-1)] public int Version = -1;
            [ProtoMember(3)] public bool DisableWeaponGridLimits;
        }

        /*
        public static void SaveEnforcement(IMyFunctionalBlock shield, CoreEnforcement enforce, bool createStorage = false)
        {
            if (createStorage && shield.Storage == null) shield.Storage = new MyModStorageComponent();
            else if (shield.Storage == null) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(enforce);
            shield.Storage[Session.Instance.ControllerEnforceGuid] = Convert.ToBase64String(binary);
            if (Session.Enforced.Debug == 3) Log.Line($"Enforcement Saved - Version:{enforce.Version} - ShieldId [{shield.EntityId}]");
        }

        public static CoreEnforcement LoadEnforcement(IMyFunctionalBlock shield)
        {
            if (shield.Storage == null) return null;

            string rawData;

            if (shield.Storage.TryGetValue(Session.Instance.ControllerEnforceGuid, out rawData))
            {
                CoreEnforcement loadedEnforce = null;
                var base64 = Convert.FromBase64String(rawData);
                loadedEnforce = MyAPIGateway.Utilities.SerializeFromBinary<CoreEnforcement>(base64);
                if (Session.Enforced.Debug == 3) Log.Line($"Enforcement Loaded {loadedEnforce != null} - Version:{loadedEnforce?.Version} - ShieldId [{shield.EntityId}]");
                if (loadedEnforce != null) return loadedEnforce;
            }
            return null;
        }
        */
    }
}
