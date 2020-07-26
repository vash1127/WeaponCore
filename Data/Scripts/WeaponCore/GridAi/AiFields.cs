using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal volatile bool ScanInProgress;
        internal volatile bool GridInit;
        internal volatile bool SubGridsChanged;
        internal volatile bool PowerDirty = true;
        internal volatile uint AiSpawnTick;
        internal volatile uint AiCloseTick;
        internal volatile uint AiMarkedTick;
        internal volatile uint LastAiDataSave;
        internal readonly AiTargetingInfo TargetingInfo = new AiTargetingInfo();
        internal readonly MyShipController FakeShipController = new MyShipController();
        internal readonly Constructs Construct = new Constructs();

        internal readonly ConcurrentDictionary<MyCubeBlock, WeaponComponent> WeaponBase = new ConcurrentDictionary<MyCubeBlock, WeaponComponent>();
        internal readonly Dictionary<MyStringHash, WeaponCount> WeaponCounter = new Dictionary<MyStringHash, WeaponCount>(MyStringHash.Comparer);
        internal readonly ConcurrentDictionary<MyInventory, int> InventoryIndexer = new ConcurrentDictionary<MyInventory, int>();
        internal readonly MyConcurrentList<MyInventory> Inventories = new MyConcurrentList<MyInventory>();

        internal readonly HashSet<MyEntity> ValidGrids = new HashSet<MyEntity>();
        internal readonly HashSet<MyBatteryBlock> Batteries = new HashSet<MyBatteryBlock>();
        internal readonly HashSet<MyCubeGrid> PrevSubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<MyCubeGrid> SubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<MyCubeGrid> RemSubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<MyCubeGrid> AddSubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<MyCubeGrid> TmpSubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<Projectile> LiveProjectile = new HashSet<Projectile>();
        internal readonly HashSet<Weapon> OutOfAmmoWeapons = new HashSet<Weapon>();

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
        internal readonly List<MyEntity> NearByEntityCache = new List<MyEntity>();
        internal readonly Dictionary<MyEntity, TargetInfo> Targets = new Dictionary<MyEntity, TargetInfo>(32);
        internal readonly Dictionary<WeaponComponent, int> WeaponsIdx = new Dictionary<WeaponComponent, int>(32);
        internal readonly Dictionary<MyCubeBlock, Weapon> Armor = new Dictionary<MyCubeBlock, Weapon>(32);
        internal readonly MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;
        internal readonly object AiLock = new object();
        internal readonly uint[] MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];
        internal readonly AiData Data = new AiData();
        internal TargetStatus[] TargetState = new TargetStatus[2];

        internal Session Session;
        internal MyCubeGrid MyGrid;
        internal MyCubeBlock PowerBlock;
        internal MyResourceDistributorComponent PowerDistributor;

        internal uint CreatedTick;
        internal Vector3 GridVel;
        internal IMyGridTerminalSystem TerminalSystem;
        internal IMyTerminalBlock LastTerminal;
        internal MyEntity MyShield;
        internal MyPlanet MyPlanetTmp;
        internal MyPlanet MyPlanet;
        internal Vector3D PlanetClosestPoint;
        internal Vector3D ClosestPlanetCenter;
        internal Vector3D NaturalGravity;
        internal BoundingSphereD NearByEntitySphere;
        internal BoundingSphereD GridVolume;
        internal BoundingSphereD ScanVolume;
        internal long MyOwner;
        internal bool AiSleep;
        internal bool DbUpdated;
        internal bool TargetNonThreats;
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
        internal bool FadeOut;
        internal bool Concealed;
        internal bool RamProtection = true;
        internal bool RamProximity;
        internal bool AccelChecked;
        internal bool PlanetSurfaceInRange;
        internal bool InPlanetGravity;
        internal bool FirstRun = true;
        internal bool ScanBlockGroups;
        internal bool ScanBlockGroupSettings;
        internal bool Registered;
        internal bool MarkedForClose;
        internal bool Closed;
        internal uint TargetsUpdatedTick;
        internal uint VelocityUpdateTick;
        internal uint TargetResetTick;
        internal uint NewProjectileTick;
        internal uint LiveProjectileTick;
        internal uint LastPowerUpdateTick;
        internal uint TurnOffManualTick;
        internal uint ProjectileTicker;
        internal uint LastDetectEvent;
        internal uint LastGroupScanTick;

        internal int SleepingComps;
        internal int AwakeComps;
        internal int SourceCount;
        internal int BlockCount;
        internal int Version;
        internal int MyProjectiles;
        internal int NearByEntities;
        internal int NearByEntitiesTmp;
        internal int ProInMinCacheRange;
        internal int WeaponsTracking;

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
        private uint _pCacheTick;

        public GridAi()
        {
            for (int i = 0; i < TargetState.Length; i++)
                TargetState[i] = new TargetStatus();
        }

        internal void Init(MyCubeGrid grid, Session session)
        {
            MyGrid = grid;
            MyGrid.Flags |= (EntityFlags)(1 << 31);
            Closed = false;
            Session = session;
            CreatedTick = session.Tick;
            RegisterMyGridEvents(true, grid);
            AiSpawnTick = Session.Tick;
            Data.Init(this);
            Construct.Init(this);

            if (Session.IsClient)
                Session.SendUpdateRequest(MyGrid.EntityId, PacketType.ClientAiAdd);
        }
    }
}
