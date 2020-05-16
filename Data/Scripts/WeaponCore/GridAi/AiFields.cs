using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using static WeaponCore.Session;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal volatile bool Scanning;
        internal volatile bool GridInit;
        internal volatile bool SubGridsChanged;
        internal volatile bool PowerDirty = true;
        internal readonly Focus Focus = new Focus(2);
        internal readonly FakeTarget DummyTarget = new FakeTarget();
        internal readonly AiTargetingInfo TargetingInfo = new AiTargetingInfo();
        internal readonly MyShipController FakeShipController = new MyShipController();

        internal readonly ConcurrentDictionary<MyCubeBlock, WeaponComponent> WeaponBase = new ConcurrentDictionary<MyCubeBlock, WeaponComponent>();
        internal readonly Dictionary<MyStringHash, WeaponCount> WeaponCounter = new Dictionary<MyStringHash, WeaponCount>(MyStringHash.Comparer);
        internal readonly MyConcurrentHashSet<MyInventory> Inventories = new MyConcurrentHashSet<MyInventory>();

        internal readonly CachingDictionary<string, GroupInfo> BlockGroups = new CachingDictionary<string, GroupInfo>();

        internal readonly HashSet<MyEntity> ValidGrids = new HashSet<MyEntity>();
        internal readonly HashSet<MyBatteryBlock> Batteries = new HashSet<MyBatteryBlock>();
        internal readonly HashSet<MyCubeGrid> PrevSubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<MyCubeGrid> SubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<MyCubeGrid> RemSubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<MyCubeGrid> AddSubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<MyCubeGrid> TmpSubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<Projectile> LiveProjectile = new HashSet<Projectile>();
        internal readonly HashSet<GroupInfo> GroupsToCheck = new HashSet<GroupInfo>();

        internal readonly List<WeaponComponent> Weapons = new List<WeaponComponent>(32);
        internal readonly List<Projectile> DeadProjectiles = new List<Projectile>();
        internal readonly List<GridAi> TargetAisTmp = new List<GridAi>();
        internal readonly List<Shields> NearByShieldsTmp = new List<Shields>();
        internal readonly List<MyEntity> NearByFriendlyShields = new List<MyEntity>();
        internal readonly List<MyEntity> TestShields = new List<MyEntity>();
        internal readonly List<MyEntity> EntitiesInRange = new List<MyEntity>();
        internal readonly List<MyEntity> ObstructionsTmp = new List<MyEntity>();
        internal readonly List<MyEntity> StaticsInRangeTmp = new List<MyEntity>();
        internal readonly List<Projectile> ProjetileCache = new List<Projectile>();
        internal readonly List<MyEntity> StaticsInRange = new List<MyEntity>();
        internal readonly List<MyEntity> Obstructions = new List<MyEntity>();
        internal readonly List<GridAi> TargetAis = new List<GridAi>(32);
        internal readonly List<TargetInfo> SortedTargets = new List<TargetInfo>();
        internal readonly List<DetectInfo> NewEntities = new List<DetectInfo>();

        internal readonly Dictionary<MyEntity, TargetInfo> Targets = new Dictionary<MyEntity, TargetInfo>(32);
        internal readonly Dictionary<WeaponComponent, int> WeaponsIdx = new Dictionary<WeaponComponent, int>(32);
        internal readonly CachingDictionary<long, MyCubeBlock> ControllingPlayers = new CachingDictionary<long, MyCubeBlock>();


        internal Session Session;
        internal MyCubeGrid MyGrid;
        internal Constructs Construct = new Constructs();
        internal MyCubeBlock PowerBlock;
        //internal GridAIValues AIValues = new GridAIValues();
        internal MyResourceDistributorComponent PowerDistributor;
        internal readonly MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;
        internal uint CreatedTick;
        
        internal Vector3 GridVel;

        internal IMyGridTerminalSystem TerminalSystem;
        internal IMyTerminalBlock LastWeaponTerminal;
        internal IMyTerminalBlock LastTerminal;
        internal MyEntity MyShield;
        internal MyPlanet MyPlanetTmp;
        internal MyPlanet MyPlanet;
        internal Vector3D PlanetClosestPoint;
        internal Vector3D NaturalGravity;
        internal BoundingSphereD GridVolume;
        internal bool PlanetSurfaceInRange;
        internal bool InPlanetGravity;
        internal bool FirstRun = true;
        internal bool ScanBlockGroups = true;
        internal bool ScanBlockGroupSettings;
        internal bool Registered;
        internal uint TargetsUpdatedTick;
        internal uint VelocityUpdateTick;
        internal uint TargetResetTick;
        internal uint NewProjectileTick;
        internal uint LiveProjectileTick;
        internal uint LastPowerUpdateTick;
        internal uint LastSerializedTick;
        internal uint TurnOffManualTick;
        internal uint UiMId;
        internal uint ProjectileTicker;
        internal int SourceCount;
        internal int BlockCount;
        internal int NumSyncWeapons;
        internal int Version;
        internal long MyOwner;
        internal bool PointDefense;
        internal bool SupressMouseShoot;
        internal bool OverPowered;
        internal bool IsStatic;
        internal bool DbReady;
        internal bool UpdatePowerSources;
        internal bool AvailablePowerChanged;
        internal bool PowerIncrease;
        internal bool RequestedPowerChanged;
        internal bool RequestIncrease;
        internal bool StaticEntitiesInRange;
        internal bool StaticGridInRange;
        internal bool FriendlyShieldNear;
        internal bool ShieldNear;
        internal bool HasPower;
        internal bool HadPower;
        internal bool CheckProjectiles;
        internal bool WeaponTerminalAccess;
        internal bool FadeOut;
        internal bool Concealed; 
        internal double MaxTargetingRange;
        internal double MaxTargetingRangeSqr;
        internal double ClosestStaticSqr = double.MaxValue;
        internal double ClosestPlanetSqr = double.MaxValue;
        internal float GridMaxPower;
        internal float GridCurrentPower;
        internal float GridAvailablePower;
        internal float BatteryMaxPower;
        internal float BatteryCurrentOutput;
        internal float BatteryCurrentInput;
        internal float CurrentWeaponsDraw;
        internal float RequestedWeaponsDraw;
        internal float LastRequestedPower;
        internal float LastAvailablePower;
        internal float OptimalDps;
        internal float EffectiveDps;
        internal enum TargetType
        {
            Projectile,
            Other,
            None,
        }

        private readonly List<MyEntity> _possibleTargets = new List<MyEntity>();
        private readonly FastResourceLock _scanLock = new FastResourceLock();
        private uint _lastScan;
        private uint _pCacheTick;

        internal void Init(MyCubeGrid grid, Session session)
        {
            MyGrid = grid;
            MyGrid.Flags |= (EntityFlags)(1 << 31);

            Session = session;
            CreatedTick = session.Tick;
            RegisterMyGridEvents(true, grid);

            if (Session.IsClient)
                session.SendUpdateRequest(grid.EntityId, PacketType.GridSyncRequestUpdate);
        }
    }
}
