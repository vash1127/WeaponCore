using ProtoBuf;
using Sandbox.Game.Entities;
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

            [ProtoContract]
            public class ShipSize
            {
                [ProtoMember(1)] public string Name;
                [ProtoMember(2)] public int BlockCount;
                [ProtoMember(3)] public bool LargeGrid;
            }

            [ProtoContract]
            public class AmmoModifer
            {
                [ProtoMember(1)] public string Name;
                [ProtoMember(2)] public float DirectDamageModifer;
                [ProtoMember(3)] public float AreaDamageModifer;
                [ProtoMember(4)] public float DetonationDamageModifer;
            }

            [ProtoMember(1)] public int Version = -1;
            [ProtoMember(2)] public int Debug = -1;
            [ProtoMember(3)] public bool DisableWeaponGridLimits;
            [ProtoMember(4)] public float DirectDamageModifer = 1;
            [ProtoMember(5)] public float AreaDamageModifer = 1;
            [ProtoMember(6)] public bool ServerOptimizations = true;
            [ProtoMember(7)] public bool ServerSleepSupport = false;
            [ProtoMember(8)] public BlockModifer[] BlockModifers =
            {
                new BlockModifer {SubTypeId = "TestSubId1", DirectDamageModifer = 0.5f, AreaDamageModifer = 0.1f}, 
                new BlockModifer { SubTypeId = "TestSubId2", DirectDamageModifer = -1f, AreaDamageModifer = 0f }
            };
            [ProtoMember(9)]
            public ShipSize[] ShipSizes =
            {
                new ShipSize {Name = "Scout", BlockCount = 0, LargeGrid = false },
                new ShipSize {Name = "Fighter", BlockCount = 2000, LargeGrid = false },
                new ShipSize {Name = "Frigate", BlockCount = 0, LargeGrid = true },
                new ShipSize {Name = "Destroyer", BlockCount = 3000, LargeGrid = true },
                new ShipSize {Name = "Cruiser", BlockCount = 6000, LargeGrid = true },
                new ShipSize {Name = "Battleship", BlockCount = 12000, LargeGrid = true },
                new ShipSize {Name = "Capital", BlockCount = 24000, LargeGrid = true },
            };
            [ProtoMember(10)]
            public AmmoModifer[] AmmoModifers =
            {
                new AmmoModifer {Name = "TestAmmo1", DirectDamageModifer = 1f, AreaDamageModifer = 0.5f, DetonationDamageModifer = 3.5f},
                new AmmoModifer {Name = "TestAmmo2", DirectDamageModifer = 2f, AreaDamageModifer = 0f, DetonationDamageModifer = 0f },
            };
            [ProtoMember(11)] public double MinHudFocusDistance;
        }

        [ProtoContract]
        public class ClientSettings
        {
            [ProtoMember(1)] public int Version = -1;
            [ProtoMember(2)] public bool ClientOptimizations;
            [ProtoMember(3)] public int MaxProjectiles = 3000;
            [ProtoMember(4)] public string MenuButton = MyMouseButtonsEnum.Middle.ToString();
            [ProtoMember(5)] public string ActionKey = MyKeys.R.ToString();
            [ProtoMember(6)] public bool ShowHudTargetSizes;
        }
    }
}
