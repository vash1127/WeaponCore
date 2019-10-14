using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRage;
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
        internal readonly MyCubeGrid MyGrid;
        internal readonly MyConcurrentPool<Dictionary<BlockTypes, List<MyCubeBlock>>> BlockTypePool = new MyConcurrentPool<Dictionary<BlockTypes, List<MyCubeBlock>>>(50);
        internal readonly MyConcurrentPool<List<MyCubeBlock>> CubePool = new MyConcurrentPool<List<MyCubeBlock>>(50);
        internal readonly MyConcurrentPool<TargetInfo> TargetInfoPool = new MyConcurrentPool<TargetInfo>();

        internal readonly ConcurrentDictionary<MyCubeBlock, WeaponComponent> WeaponBase = new ConcurrentDictionary<MyCubeBlock, WeaponComponent>();
        internal readonly ConcurrentDictionary<MyStringHash, WeaponCount> WeaponCounter = new ConcurrentDictionary<MyStringHash, WeaponCount>(MyStringHash.Comparer);
        internal readonly ConcurrentDictionary<MyDefinitionId, Dictionary<MyInventory, MyFixedPoint>> AmmoInventories;
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
        internal readonly Session Session;
        internal List<TargetInfo> SortedTargets = new List<TargetInfo>();
        internal Dictionary<MyEntity, TargetInfo> Targets = new Dictionary<MyEntity, TargetInfo>();

        internal MyResourceDistributorComponent MyResourceDist;
        internal CoreTargeting Targeting { get; set; }
        internal DSUtils DsWatch = new DSUtils();
        internal MyEntity MyShieldTmp;
        internal MyEntity MyShield;
        internal MyEntity PrimeTarget;
        internal MyPlanet MyPlanetTmp;
        internal MyPlanet MyPlanet;
        internal Vector3D PlanetClosestPoint;
        internal MyDefinitionId NewAmmoType;
        internal bool PlanetSurfaceInRange;
        internal bool FirstRun = true;
        internal uint TargetsUpdatedTick;
        internal uint RecalcLowPowerTick;
        internal uint ResetPowerTick;
        internal uint VelocityUpdateTick;
        internal uint TargetResetTick;
        internal int DbUpdating;
        internal int SourceCount;
        internal int ManualComps;
        internal int BlockCount;
        internal long MyOwner;
        internal bool GridInit;
        internal bool DbReady;
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
        internal float OptimalDPS;
        internal Vector3D GridCenter;
        internal Vector3 GridVel;
        internal MatrixD GridMatrix;
        internal enum TargetType
        {
            Projectile,
            Other,
            None,
        }

        internal readonly TargetCompare TargetCompare1 = new TargetCompare();

        internal GridAi(MyCubeGrid grid, Session session)
        {
            MyGrid = grid;
            Session = session;
            RegisterMyGridEvents(true, grid);

            Targeting = MyGrid.Components.Get<MyGridTargeting>() as CoreTargeting;

            if (Targeting == null)
            {
                Targeting = new CoreTargeting(Session);
                MyGrid.Components.Remove<MyGridTargeting>();
                MyGrid.Components.Add<MyGridTargeting>(Targeting);
            }

            AmmoInventories = new ConcurrentDictionary<MyDefinitionId, Dictionary<MyInventory, MyFixedPoint>>(Session.AmmoInventoriesMaster, MyDefinitionId.Comparer);
        }
    }
}
