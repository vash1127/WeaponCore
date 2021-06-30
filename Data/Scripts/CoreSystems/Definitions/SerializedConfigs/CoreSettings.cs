using ProtoBuf;
using VRage.Input;
using VRageMath;
using WeaponCore.Data.Scripts.CoreSystems.Support;

namespace CoreSystems.Settings
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
                [ProtoMember(4)] public float ShieldDamageModifer;
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
                [ProtoMember(2)] public float BaseDamage;
                [ProtoMember(3)] public float AreaEffectDamage;
                [ProtoMember(4)] public float DetonationDamage;
                [ProtoMember(5)] public float Health;
                [ProtoMember(6)] public float MaxTrajectory;
                [ProtoMember(7)] public float ShieldModifer;
            }

            [ProtoMember(1)] public int Version = -1;
            [ProtoMember(2)] public int Debug = -1;
            [ProtoMember(3)] public bool DisableWeaponGridLimits;
            [ProtoMember(4)] public float DirectDamageModifer = 1;
            [ProtoMember(5)] public float AreaDamageModifer = 1;
            [ProtoMember(6)] public float ShieldDamageModifer = 1;
            [ProtoMember(7)] public bool ServerOptimizations = true;
            [ProtoMember(8)] public bool ServerSleepSupport = false;
            [ProtoMember(9)]
            public BlockModifer[] BlockModifers =
            {
                new BlockModifer {SubTypeId = "TestSubId1", DirectDamageModifer = 0.5f, AreaDamageModifer = 0.1f},
                new BlockModifer { SubTypeId = "TestSubId2", DirectDamageModifer = -1f, AreaDamageModifer = 0f }
            };
            [ProtoMember(10)]
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
            [ProtoMember(11)]
            public AmmoModifer[] AmmoModifers =
            {
                new AmmoModifer {Name = "TestAmmo1", BaseDamage = 1f, AreaEffectDamage = 2500f, DetonationDamage = 0f, Health = 5f, MaxTrajectory = 3500f, ShieldModifer = 2.2f},
                new AmmoModifer {Name = "TestAmmo2", BaseDamage = 2100f, AreaEffectDamage = 0f, DetonationDamage = 1000f, Health = 0f, MaxTrajectory = 1000f, ShieldModifer = 1f},
            };
            [ProtoMember(12)] public double MinHudFocusDistance;
            [ProtoMember(13)] public bool DisableAi;
            [ProtoMember(14)] public bool DisableLeads;
        }

        [ProtoContract]
        public class ClientSettings
        {
            [ProtoMember(1)] public int Version = -1;
            [ProtoMember(2)] public bool ClientOptimizations;
            [ProtoMember(3)] public int MaxProjectiles = 3000;
            [ProtoMember(4)] public string MenuButton = MyMouseButtonsEnum.Middle.ToString();
            [ProtoMember(5)] public string ControlKey = MyKeys.R.ToString();
            [ProtoMember(6)] public bool ShowHudTargetSizes;
            [ProtoMember(7)] public string ActionKey = MyKeys.NumPad0.ToString();
            [ProtoMember(8)] public Vector2 HudPos = new Vector2(0, 0);
            [ProtoMember(9)] public float HudScale = 1f;
            [ProtoMember(10)] public string InfoKey = MyKeys.Decimal.ToString();
            [ProtoMember(11)] public bool MinimalHud = false;
        }
    }
}
