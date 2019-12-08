﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using static WeaponCore.Support.TargetingDefinition;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal volatile bool Scanning;
        internal volatile bool Ready;
        internal volatile bool SubGridsChanged;
        internal readonly MyConcurrentPool<Dictionary<BlockTypes, List<MyCubeBlock>>> BlockTypePool = new MyConcurrentPool<Dictionary<BlockTypes, List<MyCubeBlock>>>(8);

        internal readonly MyConcurrentPool<List<MyCubeBlock>> CubePool = new MyConcurrentPool<List<MyCubeBlock>>(10);
        internal readonly MyConcurrentPool<TargetInfo> TargetInfoPool = new MyConcurrentPool<TargetInfo>();
        internal readonly MyConcurrentPool<GroupInfo> GroupInfoPool = new MyConcurrentPool<GroupInfo>();

        internal readonly ConcurrentDictionary<MyCubeBlock, WeaponComponent> WeaponBase = new ConcurrentDictionary<MyCubeBlock, WeaponComponent>();
        internal readonly ConcurrentDictionary<MyStringHash, WeaponCount> WeaponCounter = new ConcurrentDictionary<MyStringHash, WeaponCount>(MyStringHash.Comparer);
        internal readonly CachingDictionary<string, GroupInfo> BlockGroups = new CachingDictionary<string, GroupInfo>();

        internal readonly ConcurrentDictionary<MyDefinitionId, Dictionary<MyInventory, MyFixedPoint>> AmmoInventories;
        internal readonly ConcurrentQueue<Projectile> DeadProjectiles = new ConcurrentQueue<Projectile>();
        internal readonly HashSet<MyEntity> ValidGrids = new HashSet<MyEntity>();
        internal readonly HashSet<MyBatteryBlock> Batteries = new HashSet<MyBatteryBlock>();
        internal readonly HashSet<MyCubeGrid> PrevSubGrids = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> SubGrids = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> RemSubGrids = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> AddSubGrids = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> TmpSubGrids = new HashSet<MyCubeGrid>();

        internal readonly HashSet<Projectile> LiveProjectile = new HashSet<Projectile>();

        internal readonly List<GridAi> TargetAisTmp = new List<GridAi>();
        internal readonly List<GridAi> ThreatsTmp = new List<GridAi>();
        internal readonly List<MyEntity> EntitiesInRange = new List<MyEntity>();
        internal readonly List<MyEntity> ObstructionsTmp = new List<MyEntity>();
        internal readonly List<MyEntity> StaticsInRangeTmp = new List<MyEntity>();
        internal readonly List<Projectile> ProjetileCache = new List<Projectile>();
        internal List<MyEntity> StaticsInRange = new List<MyEntity>();
        internal List<MyEntity> Obstructions = new List<MyEntity>();
        internal List<GridAi> Threats = new List<GridAi>();
        internal List<GridAi> TargetAis = new List<GridAi>();
        internal readonly List<TargetInfo> SortedTargets = new List<TargetInfo>();
        internal readonly Dictionary<MyEntity, TargetInfo> Targets = new Dictionary<MyEntity, TargetInfo>();
        internal readonly List<DetectInfo> NewEntities = new List<DetectInfo>();
        internal readonly TargetCompare TargetCompare1 = new TargetCompare();
        internal readonly Session Session;
        internal readonly MyCubeGrid MyGrid;
        internal readonly MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;
        internal readonly uint CreatedTick;
        internal IMyGridTerminalSystem TerminalSystem;

        internal IMyTerminalBlock LastWeaponTerminal;
        internal IMyTerminalBlock LastTerminal;

        internal Focus Focus;
        internal MyEntity MyShieldTmp;
        internal MyEntity MyShield;
        internal MyPlanet MyPlanetTmp;
        internal MyPlanet MyPlanet;
        internal MyShipController FakeShipController = new MyShipController();
        internal Vector3D PlanetClosestPoint;
        internal MyDefinitionId NewAmmoType;
        internal bool PlanetSurfaceInRange;
        internal bool FirstRun = true;
        internal uint TargetsUpdatedTick;
        internal uint RecalcLowPowerTick;
        internal uint ResetPowerTick;
        internal uint VelocityUpdateTick;
        internal uint TargetResetTick;
        internal uint LiveProjectileTick;
        internal int SourceCount;
        internal int ManualComps;
        internal int BlockCount;
        internal long MyOwner;
        internal bool GridInit;
        internal bool DbReady;
        internal bool ScanBlockGroups = true;
        internal bool ResetPower = true;
        internal bool RecalcPowerPercent;
        internal bool UpdatePowerSources;
        internal bool AvailablePowerIncrease;
        internal bool RecalcDone;
        internal bool StaticEntitiesInRange;
        internal bool Reloading;
        internal bool ReturnHome;
        internal bool ShieldNearTmp;
        internal bool ShieldNear;
        internal bool CheckReload;
        internal bool HasPower;
        internal bool HadPower;
        internal bool HasGunner;
        internal double MaxTargetingRange;
        internal double MaxTargetingRangeSqr;
        internal double GridRadius;
        internal float GridMaxPower;
        internal float WeaponCleanPower;
        internal float GridCurrentPower;
        internal float GridAvailablePower;
        internal float BatteryMaxPower;
        internal float BatteryCurrentOutput;
        internal float BatteryCurrentInput;
        internal float TotalSinkPower;
        internal float MinSinkPower;
        internal float CurrentWeaponsDraw;
        internal float LastAvailablePower;
        internal float OptimalDps;
        internal Vector3D GridCenter;
        internal Vector3 GridVel;
        private readonly List<MyEntity> _possibleTargets = new List<MyEntity>();
        private readonly FastResourceLock _scanLock = new FastResourceLock();

        internal bool WeaponTerminalAccess;
        private uint _lastScan;
        private uint _pCacheTick;

        internal enum TargetType
        {
            Projectile,
            Other,
            None,
        }

        internal GridAi(MyCubeGrid grid, Session session, uint createdTick)
        {
            MyGrid = grid;
            Session = session;
            CreatedTick = createdTick;
            RegisterMyGridEvents(true, grid);
            Focus = new Focus(2, this);
            AmmoInventories = new ConcurrentDictionary<MyDefinitionId, Dictionary<MyInventory, MyFixedPoint>>(Session.AmmoInventoriesMaster, MyDefinitionId.Comparer);
        }
    }
}
