using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using static WeaponCore.Support.TargetingDefinition;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal volatile bool Ready;
        internal static volatile bool SubGridUpdate;
        internal readonly MyCubeGrid MyGrid;
        internal readonly MyConcurrentPool<Dictionary<BlockTypes, List<MyCubeBlock>>> BlockTypePool = new MyConcurrentPool<Dictionary<BlockTypes, List<MyCubeBlock>>>(50);
        internal readonly MyConcurrentPool<List<MyCubeBlock>> CubePool = new MyConcurrentPool<List<MyCubeBlock>>(50);
        internal readonly ConcurrentDictionary<MyCubeBlock, WeaponComponent> WeaponBase = new ConcurrentDictionary<MyCubeBlock, WeaponComponent>();
        internal readonly ConcurrentDictionary<MyStringHash, WeaponCount> WeaponCounter = new ConcurrentDictionary<MyStringHash, WeaponCount>(MyStringHash.Comparer);
        internal readonly ConcurrentQueue<Projectile> DeadProjectiles = new ConcurrentQueue<Projectile>();
        internal readonly Dictionary<MyEntity, Dictionary<BlockTypes, List<MyCubeBlock>>> ValidGrids = new Dictionary<MyEntity, Dictionary<BlockTypes, List<MyCubeBlock>>>();
        internal readonly HashSet<MyResourceSourceComponent> Sources = new HashSet<MyResourceSourceComponent>();
        internal readonly List<MyCubeGrid> SubGridsTmp = new List<MyCubeGrid>();
        internal readonly HashSet<MyCubeGrid> SubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<Projectile> LiveProjectile = new HashSet<Projectile>();

        internal readonly List<GridAi> TargetAisTmp = new List<GridAi>();
        internal readonly List<GridAi> TargetAis = new List<GridAi>();
        internal readonly List<GridAi> ThreatsTmp = new List<GridAi>();
        internal readonly List<GridAi> Threats = new List<GridAi>();
        internal readonly List<MyEntity> EntitiesInRange = new List<MyEntity>();
        internal readonly List<MyEntity> ObstructionsTmp = new List<MyEntity>();
        internal readonly List<MyEntity> Obstructions = new List<MyEntity>();
        internal readonly List<MyEntity> StaticsInRangeTmp = new List<MyEntity>();
        internal readonly List<MyEntity> StaticsInRange = new List<MyEntity>();

        internal readonly List<DetectInfo> NewEntities = new List<DetectInfo>();

        internal readonly MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;
        internal List<TargetInfo> SortedTargets = new List<TargetInfo>();

        internal MyResourceDistributorComponent MyResourceDist;
        internal MyGridTargeting Targeting { get; set; }
        internal DSUtils DsWatch = new DSUtils();
        internal MyEntity MyShieldTmp;
        internal MyEntity MyShield;
        internal MyPlanet MyPlanetTmp;
        internal MyPlanet MyPlanet;
        internal Vector3D PlanetClosestPoint;
        internal bool PlanetSurfaceInRange;
        internal bool FirstRun = true;
        internal uint SubTick;
        internal uint TargetsUpdatedTick;
        internal uint RecalcLowPowerTick;
        internal uint ResetPowerTick;
        internal uint VelocityUpdateTick;
        internal int DbUpdating;
        internal int SourceCount;
        internal int ManualComps;
        internal long MyOwner;
        internal bool TargetNeutrals;
        internal bool TargetNoOwners;
        internal bool SubUpdate;
        internal bool DbReady;
        internal bool ResetPower = true;
        internal bool RecalcPowerPercent;
        internal bool GridInit;
        internal bool UpdatePowerSources;
        internal bool AvailablePowerIncrease;
        internal bool RecalcDone;
        internal bool StaticEntitiesInRange;
        internal bool turnWeaponShootOff;
        internal bool Reloading;
        internal bool ReturnHome;
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
        internal Vector3D GridCenter;
        internal Vector3 GridVel;
        internal enum TargetType
        {
            Projectile,
            Other,
            None,
        }

        internal BoundingBoxD GroupAABB;
        internal readonly TargetCompare TargetCompare1 = new TargetCompare();

        internal GridAi(MyCubeGrid grid)
        {
            MyGrid = grid;
            RegisterMyGridEvents(true, grid);
            Targeting = MyGrid.Components.Get<MyGridTargeting>();

        }
    }
}
