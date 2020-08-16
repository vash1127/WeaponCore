using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Input;

namespace WeaponCore.Settings
{
    public class CoreSettings
    {
        internal readonly VersionControl VersionControl;
        internal ServerSettings Enforcement;
        internal ClientSettings ClientConfig;
        internal Session Session;
        internal bool ClientWaiting;
        internal CoreSettings(Session session)
        {
            Session = session;
            VersionControl = new VersionControl(this);
            VersionControl.InitSettings();
            if (Session.IsClient)
                ClientWaiting = true;
        }

        [ProtoContract]
        public class ServerSettings
        {
            [ProtoContract]
            public class BlockModifer
            {
                [ProtoMember(1)] public string SubTypeId;
                [ProtoMember(2)] public float DirectDamageModifer;
                [ProtoMember(3)] public float AreaDamageModifer;
            }

            [ProtoMember(1)] public int Version = -1;
            [ProtoMember(2)] public int Debug = -1;
            [ProtoMember(3)] public bool DisableWeaponGridLimits;
            [ProtoMember(4)] public float DirectDamageModifer = 1;
            [ProtoMember(5)] public float AreaDamageModifer = 1;
            [ProtoMember(6)] public bool ServerOptimizations;
            [ProtoMember(7)] public bool ServerSleepSupport;
            [ProtoMember(8)] public BlockModifer[] BlockModifers = {new BlockModifer {SubTypeId = "TestSubId1", DirectDamageModifer = 0.5f, AreaDamageModifer = 0.1f}, new BlockModifer { SubTypeId = "TestSubId2", DirectDamageModifer = -1f, AreaDamageModifer = 0f } };
        }

        [ProtoContract]
        public class ClientSettings
        {
            [ProtoMember(1)] public int Version = -1;
            [ProtoMember(2)] public bool ClientOptimizations;
            [ProtoMember(3)] public int MaxProjectiles = 3000;
            [ProtoMember(4)] public string MenuButton = MyMouseButtonsEnum.Middle.ToString();
            [ProtoMember(5)] public string ActionButton = MyKeys.R.ToString();
        }
    }
}
