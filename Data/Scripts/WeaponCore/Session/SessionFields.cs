using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Weapons;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Data.Scripts.WeaponCore.Support;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
using Task = ParallelTasks.Task;

namespace WeaponCore
{
    public partial class Session
    {
        internal const ushort PacketId = 62518;
        internal const double TickTimeDiv = 0.0625;
        internal const double VisDirToleranceAngle = 2; //in degrees
        internal const double AimDirToleranceAngle = 5; //in degrees

        internal volatile bool Inited;
        internal volatile bool TurretControls;
        internal volatile bool SorterControls;
        internal volatile bool DbCallBackComplete = true;
        internal volatile bool Pause;
        internal volatile uint TypeCleanTick;

        internal readonly TargetCompare TargetCompare = new TargetCompare();
        internal readonly MyConcurrentPool<ConcurrentDictionary<TargetingDefinition.BlockTypes, ConcurrentCachingList<MyCubeBlock>>> BlockTypePool = new MyConcurrentPool<ConcurrentDictionary<TargetingDefinition.BlockTypes, ConcurrentCachingList<MyCubeBlock>>>(64);
        internal readonly MyConcurrentPool<TargetInfo> TargetInfoPool = new MyConcurrentPool<TargetInfo>(256);
        internal readonly MyConcurrentPool<GroupInfo> GroupInfoPool = new MyConcurrentPool<GroupInfo>(128);
        internal readonly MyConcurrentPool<ConcurrentCachingList<MyCubeBlock>> ConcurrentListPool = new MyConcurrentPool<ConcurrentCachingList<MyCubeBlock>>(100);
        internal readonly MyConcurrentPool<FatMap> FatMapPool = new MyConcurrentPool<FatMap>(128);
        internal readonly MyConcurrentPool<WeaponCount> WeaponCountPool = new MyConcurrentPool<WeaponCount>(64, count => count.Current = 0);
        internal readonly MyConcurrentPool<GridAi> GridAiPool = new MyConcurrentPool<GridAi>(128, ai => ai.CleanUp());
        
        internal readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridAi> GridTargetingAIs = new ConcurrentDictionary<MyCubeGrid, GridAi>();
        internal readonly ConcurrentDictionary<MyCubeGrid, ConcurrentDictionary<TargetingDefinition.BlockTypes, ConcurrentCachingList<MyCubeBlock>>> GridToBlockTypeMap = new ConcurrentDictionary<MyCubeGrid, ConcurrentDictionary<TargetingDefinition.BlockTypes, ConcurrentCachingList<MyCubeBlock>>>();
        internal readonly ConcurrentDictionary<MyDefinitionId, ConcurrentDictionary<MyInventory, MyFixedPoint>> AmmoInventoriesMaster = new ConcurrentDictionary<MyDefinitionId, ConcurrentDictionary<MyInventory, MyFixedPoint>>(MyDefinitionId.Comparer);
        internal readonly ConcurrentDictionary<MyCubeGrid, FatMap> GridToFatMap = new ConcurrentDictionary<MyCubeGrid, FatMap>();
        internal readonly MyConcurrentHashSet<MyCubeGrid> DirtyGrids = new MyConcurrentHashSet<MyCubeGrid>();

        internal readonly ConcurrentCachingList<WeaponComponent> CompsToStart = new ConcurrentCachingList<WeaponComponent>();

        internal readonly ConcurrentQueue<Weapon> WeaponAmmoPullQueue = new ConcurrentQueue<Weapon>();
        internal readonly ConcurrentQueue<MyTuple<Weapon, MyTuple<MyInventory, int>[]>> AmmoToPullQueue = new ConcurrentQueue<MyTuple<Weapon, MyTuple<MyInventory, int>[]>>();
        internal readonly ConcurrentQueue<MyCubeGrid> NewGrids = new ConcurrentQueue<MyCubeGrid>();
        internal readonly ConcurrentQueue<PartAnimation> ThreadedAnimations = new ConcurrentQueue<PartAnimation>();
        internal readonly ConcurrentQueue<DeferedTypeCleaning> BlockTypeCleanUp = new ConcurrentQueue<DeferedTypeCleaning>();

        internal readonly Dictionary<MyStringHash, WeaponStructure> WeaponPlatforms = new Dictionary<MyStringHash, WeaponStructure>(MyStringHash.Comparer);
        internal readonly Dictionary<string, MyDefinitionId> WeaponCoreBlockDefs = new Dictionary<string, MyDefinitionId>();
        internal readonly Dictionary<string, MyStringHash> SubTypeIdHashMap = new Dictionary<string, MyStringHash>();
        internal readonly Dictionary<int, string> ModelIdToName = new Dictionary<int, string>();
        internal readonly Dictionary<double, List<Vector3I>> LargeBlockSphereDb = new Dictionary<double, List<Vector3I>>();
        internal readonly Dictionary<double, List<Vector3I>> SmallBlockSphereDb = new Dictionary<double, List<Vector3I>>();

        internal readonly HashSet<MyDefinitionBase> AllArmorBaseDefinitions = new HashSet<MyDefinitionBase>();
        internal readonly HashSet<MyDefinitionBase> HeavyArmorBaseDefinitions = new HashSet<MyDefinitionBase>();

        internal readonly List<WeaponComponent> CompsDelayed = new List<WeaponComponent>();
        internal readonly List<CompReAdd> CompReAdds = new List<CompReAdd>();
        internal readonly List<Projectile> Hits = new List<Projectile>(16);
        internal readonly List<Weapon> ChargingWeapons = new List<Weapon>(64);
        internal readonly List<Weapon> AcquireTargets = new List<Weapon>(128);
        internal readonly List<MyDefinitionId> WeaponCoreFixedBlockDefs = new List<MyDefinitionId>();
        internal readonly List<MyDefinitionId> WeaponCoreTurretBlockDefs = new List<MyDefinitionId>();
        internal readonly List<MyCubeGrid> DirtyGridsTmp = new List<MyCubeGrid>(10);
        internal readonly List<GridAi> DbsToUpdate = new List<GridAi>(16);
        internal readonly List<Weapon> ShootingWeapons = new List<Weapon>(128);


        internal readonly double ApproachDegrees = Math.Cos(MathHelper.ToRadians(50));
        internal readonly FutureEvents FutureEvents = new FutureEvents();
        internal readonly Guid LogicSettingsGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811501");
        internal readonly Guid LogicStateGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811502");

        internal readonly double VisDirToleranceCosine;
        internal readonly double AimDirToleranceCosine;


        //internal readonly MyConcurrentPool<Shrinking> ShrinkPool = new MyConcurrentPool<Shrinking>(100);
        private readonly MyConcurrentPool<List<Vector3I>> _blockSpherePool = new MyConcurrentPool<List<Vector3I>>(25);


        private readonly HashSet<IMySlimBlock> _slimsSet = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _destroyedSlims = new HashSet<IMySlimBlock>();
        
        private readonly Dictionary<string, Dictionary<string, MyTuple<string, string, string>>> _turretDefinitions = new Dictionary<string, Dictionary<string, MyTuple<string, string, string>>>();
        private readonly Dictionary<string, List<WeaponDefinition>> _subTypeIdToWeaponDefs = new Dictionary<string, List<WeaponDefinition>>();

        private readonly List<AfterGlow> _afterGlow = new List<AfterGlow>(128);
        private readonly List<MyTuple<MyInventory, int>> _inventoriesToPull = new List<MyTuple<MyInventory, int>>();
        private readonly List<UpgradeDefinition> _upgradeDefinitions = new List<UpgradeDefinition>();
        private readonly List<RadiatedBlock> _slimsSortedList = new List<RadiatedBlock>(1024);
        //private readonly CachingList<Shrinking> _shrinking = new CachingList<Shrinking>(100);

        internal MyDynamicAABBTreeD ProjectileTree = new MyDynamicAABBTreeD(Vector3D.One * 10.0, 10.0);
        internal List<PartAnimation> AnimationsToProcess = new List<PartAnimation>(128);
        internal List<WeaponDefinition> WeaponDefinitions = new List<WeaponDefinition>();

        internal IMyPhysics Physics;
        internal IMyCamera Camera;
        internal IMyGps TargetGps;
        internal IMyBlockPlacerBase Placer;
        internal GridAi TrackingAi;
        internal ApiServer ApiServer;
        internal MyCockpit ActiveCockPit;
        internal MyEntity ControlledEntity;
        internal Projectiles.Projectiles Projectiles;
        internal ApiBackend Api;

        internal ShieldApi SApi = new ShieldApi();
        internal RunAv Av;
        internal DSUtils DsUtil;
        internal DSUtils DsUtil2;
        internal UiInput UiInput;
        internal Wheel WheelUi;
        internal TargetUi TargetUi;

        internal MatrixD CameraMatrix;
        internal DictionaryValuesReader<MyDefinitionId, MyDefinitionBase> AllDefinitions;
        internal DictionaryValuesReader<MyDefinitionId, MyAudioDefinition> SoundDefinitions;
        internal Color[] HeatEmissives;

        internal Vector3D CameraPos;
        internal Task PTask = new Task();
        internal Task GridTask = new Task();
        internal Task DbTask = new Task();
        internal Task ITask = new Task();

        internal object InitObj = new object();
        internal bool HighLoad;
        internal bool InMenu;
        internal double Load;
        internal bool GunnerBlackList;
        internal uint Tick;
        internal int PlayerEventId;
        internal int ModelCount;

        internal int TargetRequests;
        internal int TargetChecks;
        internal int BlockChecks;
        internal int ClosestRayCasts;
        internal int RandomRayCasts;
        internal int TopRayCasts;
        internal int CanShoot;
        internal int TargetTransfers;
        internal int TargetSets;
        internal int TargetResets;
        internal int AmmoMoveTriggered;
        internal int AmmoPulls;
        internal int Count = -1;
        internal int LCount;
        internal int ECount;

        internal enum AnimationType
        {
            Movement,
            ShowInstant,
            HideInstant,
            ShowFade,
            HideFade,
            Delay
        }

        internal ulong AuthorSteamId = 76561197969691953;
        internal long AuthorPlayerId;
        internal long LastTerminalId;
        internal double SyncDistSqr;
        internal double SyncBufferedDistSqr;
        internal double SyncDist;
        internal double MaxEntitySpeed;
        internal float UiBkOpacity;
        internal float UiOpacity;
        internal bool MpActive;
        internal bool IsServer;
        internal bool DedicatedServer;
        internal bool FirstLoop;
        internal bool GameLoaded;
        internal bool MiscLoaded;
        internal bool Tick10;
        internal bool Tick20;
        internal bool Tick60;
        internal bool Tick120;
        internal bool Tick180;
        internal bool Tick300;
        internal bool Tick600;
        internal bool Tick1800;
        internal bool ShieldMod;
        internal bool ShieldApiLoaded;
        internal bool TargetArmed;
        internal bool InGridAiCockPit;
        internal bool IsCreative;

        public Session()
        {
            UiInput = new UiInput(this);
            TargetUi = new TargetUi(this);
            WheelUi = new Wheel(this);
            DsUtil = new DSUtils(this);
            DsUtil2 = new DSUtils(this);
            Av = new RunAv(this);

            Projectiles = new Projectiles.Projectiles(this);
            VisDirToleranceCosine = Math.Cos(MathHelper.ToRadians(VisDirToleranceAngle));
            AimDirToleranceCosine = Math.Cos(MathHelper.ToRadians(AimDirToleranceAngle));
        }



        private int _loadCounter = 1;
        private uint _lastDrawTick;
        private bool _paused;
    }
}