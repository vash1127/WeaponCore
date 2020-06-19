using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;
using WeaponCore.Api;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
using Task = ParallelTasks.Task;

namespace WeaponCore
{
    public partial class Session
    {
        internal const ushort ServerPacketId = 62518;
        internal const ushort ClientPacketId = 62519;
        internal const ushort StringPacketId = 62520;
        internal const double TickTimeDiv = 0.0625;
        internal const double VisDirToleranceAngle = 2; //in degrees
        internal const double AimDirToleranceAngle = 5; //in degrees
        internal const int VersionControl = 3;
        internal const uint ResyncMinDelayTicks = 120;
        internal const uint ServerTickOffset = 4;
        internal const int AwakeBuckets = 60;
        internal const int AsleepBuckets = 180;

        internal volatile bool Inited;
        internal volatile bool TurretControls;
        internal volatile bool FixedMissileControls;
        internal volatile bool FixedGunControls;
        internal volatile bool SorterControls;
        internal volatile bool BaseControlsActions;
        internal volatile bool Pause;
        internal volatile int AmmoPulls;
        internal volatile uint LastDeform;
        internal volatile uint Tick;

        internal readonly TargetCompare TargetCompare = new TargetCompare();

        internal static readonly HashSet<ulong> AuthorIds = new HashSet<ulong> { 76561197969691953, 76561198061737246 };

        internal readonly MyConcurrentPool<ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>> BlockTypePool = new MyConcurrentPool<ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>>(64);
        internal readonly MyConcurrentPool<TargetInfo> TargetInfoPool = new MyConcurrentPool<TargetInfo>(256, info => info.Clean());
        internal readonly MyConcurrentPool<GroupInfo> GroupInfoPool = new MyConcurrentPool<GroupInfo>(128, info => info.Comps.Clear());
        internal readonly MyConcurrentPool<WeaponAmmoMoveRequest> InventoryMoveRequestPool = new MyConcurrentPool<WeaponAmmoMoveRequest>(128, invMove => invMove.Clean());
        internal readonly MyConcurrentPool<ConcurrentCachingList<MyCubeBlock>> ConcurrentListPool = new MyConcurrentPool<ConcurrentCachingList<MyCubeBlock>>(100, cList => cList.ClearImmediate());
        internal readonly MyConcurrentPool<FatMap> FatMapPool = new MyConcurrentPool<FatMap>(128, fatMap => fatMap.Clean());
        internal readonly MyConcurrentPool<WeaponCount> WeaponCountPool = new MyConcurrentPool<WeaponCount>(64, count => count.Current = 0);
        internal readonly MyConcurrentPool<GridAi> GridAiPool = new MyConcurrentPool<GridAi>(128, ai => ai.CleanUp());
        internal readonly MyConcurrentPool<List<IMySlimBlock>> SlimPool = new MyConcurrentPool<List<IMySlimBlock>>(128, slim => slim.Clear());
        internal readonly MyConcurrentPool<MyWeaponPlatform> PlatFormPool = new MyConcurrentPool<MyWeaponPlatform>(256, platform => platform.Clean());
        internal readonly MyConcurrentPool<PacketObj> PacketObjPool = new MyConcurrentPool<PacketObj>(128, packet => packet.Clean());

        internal readonly Stack<MyEntity3DSoundEmitter> Emitters = new Stack<MyEntity3DSoundEmitter>(256);
        internal readonly Stack<MySoundPair> SoundPairs = new Stack<MySoundPair>(256);
        internal readonly Stack<VoxelCache> VoxelCachePool = new Stack<VoxelCache>(256);

        internal readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();
        internal readonly ConcurrentDictionary<long, IMyCharacter> Admins = new ConcurrentDictionary<long, IMyCharacter>();
        internal readonly ConcurrentDictionary<IMyCharacter, IMyPlayer> AdminMap = new ConcurrentDictionary<IMyCharacter, IMyPlayer>();
        internal readonly ConcurrentDictionary<ulong, long> SteamToPlayer = new ConcurrentDictionary<ulong, long>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridAi> GridTargetingAIs = new ConcurrentDictionary<MyCubeGrid, GridAi>();
        internal readonly ConcurrentDictionary<MyCubeGrid, ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>> GridToBlockTypeMap = new ConcurrentDictionary<MyCubeGrid, ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>>();
        internal readonly ConcurrentDictionary<MyInventory, ConcurrentDictionary<MyDefinitionId, MyFixedPoint>> InventoryItems = new ConcurrentDictionary<MyInventory, ConcurrentDictionary<MyDefinitionId, MyFixedPoint>>();
        internal readonly ConcurrentDictionary<MyCubeGrid, FatMap> GridToFatMap = new ConcurrentDictionary<MyCubeGrid, FatMap>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridAi> GridToMasterAi = new ConcurrentDictionary<MyCubeGrid, GridAi>();
        internal readonly MyConcurrentHashSet<MyCubeGrid> DirtyGrids = new MyConcurrentHashSet<MyCubeGrid>();
        internal readonly ConcurrentCachingList<WeaponComponent> CompsToStart = new ConcurrentCachingList<WeaponComponent>();
        internal readonly ConcurrentCachingList<WeaponAmmoMoveRequest> AmmoToRemoveQueue = new ConcurrentCachingList<WeaponAmmoMoveRequest>();
        internal readonly ConcurrentCachingList<WeaponAmmoMoveRequest> AmmoToPullQueue = new ConcurrentCachingList<WeaponAmmoMoveRequest>();
        internal readonly ConcurrentCachingList<GridAi> DelayedGridAiClean = new ConcurrentCachingList<GridAi>();

        internal readonly ConcurrentQueue<MyCubeGrid> NewGrids = new ConcurrentQueue<MyCubeGrid>();

        internal readonly ConcurrentQueue<PartAnimation> ThreadedAnimations = new ConcurrentQueue<PartAnimation>();
        internal readonly ConcurrentQueue<DeferedTypeCleaning> BlockTypeCleanUp = new ConcurrentQueue<DeferedTypeCleaning>();
        
        internal readonly Dictionary<PacketType, MyConcurrentPool<Packet>> PacketPools = new Dictionary<PacketType, MyConcurrentPool<Packet>>();
        internal readonly Dictionary<MyStringHash, WeaponStructure> WeaponPlatforms = new Dictionary<MyStringHash, WeaponStructure>(MyStringHash.Comparer);
        internal readonly Dictionary<string, MyDefinitionId> WeaponCoreBlockDefs = new Dictionary<string, MyDefinitionId>();
        internal readonly Dictionary<string, MyStringHash> SubTypeIdHashMap = new Dictionary<string, MyStringHash>();
        internal readonly Dictionary<double, List<Vector3I>> LargeBlockSphereDb = new Dictionary<double, List<Vector3I>>();
        internal readonly Dictionary<double, List<Vector3I>> SmallBlockSphereDb = new Dictionary<double, List<Vector3I>>();
        internal readonly Dictionary<MyDefinitionId, MyStringHash> VanillaIds = new Dictionary<MyDefinitionId, MyStringHash>(MyDefinitionId.Comparer);
        internal readonly Dictionary<MyStringHash, MyDefinitionId> VanillaCoreIds = new Dictionary<MyStringHash, MyDefinitionId>(MyStringHash.Comparer);
        internal readonly Dictionary<long, InputStateData> PlayerMouseStates = new Dictionary<long, InputStateData>() {[-1] = new InputStateData()};
        internal readonly Dictionary<long, FakeTarget> PlayerDummyTargets = new Dictionary<long, FakeTarget>() { [-1] = new FakeTarget() };
        internal readonly Dictionary<ulong, HashSet<long>> PlayerEntityIdInRange = new Dictionary<ulong, HashSet<long>>();
        internal readonly Dictionary<long, ulong> ConnectedAuthors = new Dictionary<long, ulong>();
        internal readonly Dictionary<ulong, AvInfoCache> AvShotCache = new Dictionary<ulong, AvInfoCache>();
        internal readonly Dictionary<ulong, VoxelCache> VoxelCaches = new Dictionary<ulong, VoxelCache>();
        internal readonly Dictionary<MyCubeBlock, WeaponComponent> ArmorCubes = new Dictionary<MyCubeBlock, WeaponComponent>();

        internal readonly HashSet<string> VanillaSubpartNames = new HashSet<string>();
        internal readonly HashSet<MyDefinitionBase> AllArmorBaseDefinitions = new HashSet<MyDefinitionBase>();
        internal readonly HashSet<MyDefinitionBase> HeavyArmorBaseDefinitions = new HashSet<MyDefinitionBase>();
        internal readonly HashSet<Weapon> WeaponsSyncCheck = new HashSet<Weapon>();
        internal readonly HashSet<MyDefinitionId> AmmoDefIds = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        internal readonly HashSet<MyCubeGrid> DeformProtection = new HashSet<MyCubeGrid>();

        internal readonly List<WeaponComponent> CompsDelayed = new List<WeaponComponent>();
        internal readonly List<CompReAdd> CompReAdds = new List<CompReAdd>();
        internal readonly List<Projectile> Hits = new List<Projectile>(16);
        internal readonly List<Weapon> AcquireTargets = new List<Weapon>(128);
        internal readonly List<MyDefinitionId> WeaponCoreFixedBlockDefs = new List<MyDefinitionId>();
        internal readonly List<MyDefinitionId> WeaponCoreTurretBlockDefs = new List<MyDefinitionId>();
        internal readonly List<MyCubeGrid> DirtyGridsTmp = new List<MyCubeGrid>(10);
        internal readonly List<DbScan> DbsToUpdate = new List<DbScan>(32);
        internal readonly List<Weapon> ShootingWeapons = new List<Weapon>(128);
        internal readonly List<PacketInfo> PacketsToClient = new List<PacketInfo>(128);
        internal readonly List<Packet> PacketsToServer = new List<Packet>(128);
        internal readonly List<Weapon> WeaponsToSync = new List<Weapon>(128);
        internal readonly List<Fragment> FragmentsNeedingEntities = new List<Fragment>(128);
        internal readonly List<WeaponComponent> ClientGridResyncRequests = new List<WeaponComponent>(128);
        internal readonly List<Weapon> CheckStorage = new List<Weapon>();
        internal readonly List<DebugLine> DebugLines = new List<DebugLine>();

        internal readonly CachingHashSet<ErrorPacket> ClientSideErrorPkt = new CachingHashSet<ErrorPacket>();

        /// <summary>
        /// DsUniqueListFastRemove without the class for less method calls
        /// </summary>
        internal readonly Dictionary<Weapon, int> ChargingWeaponsIndexer = new Dictionary<Weapon, int>();
        internal readonly Dictionary<GridAi, int> GridsToUpdateInvetoriesIndexer = new Dictionary<GridAi, int>();
        internal readonly ConcurrentDictionary<Weapon, int> WeaponToPullAmmoIndexer = new ConcurrentDictionary<Weapon, int>();
        internal readonly ConcurrentDictionary<Weapon, int> WeaponsToRemoveAmmoIndexer = new ConcurrentDictionary<Weapon, int>();

        internal readonly List<Weapon> ChargingWeapons = new List<Weapon>(64);
        internal readonly List<GridAi> GridsToUpdateInvetories = new List<GridAi>(64);
        internal readonly MyConcurrentList<Weapon> WeaponToPullAmmo = new MyConcurrentList<Weapon>(64);
        internal readonly MyConcurrentList<Weapon> WeaponsToRemoveAmmo = new MyConcurrentList<Weapon>(64);        
        ///
        ///
        ///

        internal readonly double ApproachDegrees = Math.Cos(MathHelper.ToRadians(50));
        internal readonly FutureEvents FutureEvents = new FutureEvents();
        internal readonly BoundingFrustumD CameraFrustrum = new BoundingFrustumD();
        internal readonly Guid LogicSettingsGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811501");
        internal readonly Guid LogicStateGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811502");
        internal readonly Guid MpWeaponSyncGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811503");

        internal readonly double VisDirToleranceCosine;
        internal readonly double AimDirToleranceCosine;

        private readonly MyConcurrentPool<List<Vector3I>> _blockSpherePool = new MyConcurrentPool<List<Vector3I>>(25);
        private readonly HashSet<IMySlimBlock> _slimsSet = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _destroyedSlims = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _destroyedSlimsClient = new HashSet<IMySlimBlock>();
        private readonly Dictionary<IMySlimBlock, float> _slimHealthClient = new Dictionary<IMySlimBlock, float>();
        private readonly Dictionary<string, Dictionary<string, MyTuple<string, string, string>>> _turretDefinitions = new Dictionary<string, Dictionary<string, MyTuple<string, string, string>>>();
        private readonly Dictionary<string, List<WeaponDefinition>> _subTypeIdToWeaponDefs = new Dictionary<string, List<WeaponDefinition>>();

        internal readonly int[] AuthorSettings = new int[6];

        internal List<RadiatedBlock> SlimsSortedList = new List<RadiatedBlock>(1024);
        internal MyConcurrentPool<MyEntity> TriggerEntityPool;

        internal MyDynamicAABBTreeD ProjectileTree = new MyDynamicAABBTreeD(Vector3D.One * 10.0, 10.0);
        internal List<PartAnimation> AnimationsToProcess = new List<PartAnimation>(128);
        internal List<WeaponDefinition> WeaponDefinitions = new List<WeaponDefinition>();

        internal IMyPhysics Physics;
        internal IMyCamera Camera;
        internal IMyGps TargetGps;
        internal IMyBlockPlacerBase Placer;
        internal IMyTerminalBlock LastTerminal;
        internal GridAi TrackingAi;
        internal ApiServer ApiServer;
        internal MyCockpit ActiveCockPit;
        internal MyCubeBlock ActiveControlBlock;
        internal MyEntity ControlledEntity;
        internal Projectiles.Projectiles Projectiles;
        internal ApiBackend Api;
        internal Action<Vector3, float> ProjectileAddedCallback = (location, health) => { };
        internal ShieldApi SApi = new ShieldApi();
        internal NetworkReporter Reporter = new NetworkReporter();
        internal MyStorageData TmpStorage = new MyStorageData();
        internal InputStateData DefaultInputStateData = new InputStateData();
        internal AcquireManager AcqManager;

        internal RunAv Av;
        internal DSUtils DsUtil;
        internal DSUtils DsUtil2;
        internal StallReporter StallReporter;
        internal UiInput UiInput;
        internal Wheel WheelUi;
        internal TargetUi TargetUi;
        internal Hud HudUi;
        internal Enforcements Enforced;
        internal NetworkProccessor Proccessor;
        internal TerminalMonitor TerminalMon;
        internal ProblemReport ProblemRep;

        internal MatrixD CameraMatrix;
        internal DictionaryValuesReader<MyDefinitionId, MyDefinitionBase> AllDefinitions;
        internal DictionaryValuesReader<MyDefinitionId, MyAudioDefinition> SoundDefinitions;
        internal Color[] HeatEmissives;

        internal Vector3D CameraPos;
        internal Vector3D PlayerPos;
        internal Task PTask = new Task();
        internal Task GridTask = new Task();
        internal Task DbTask = new Task();
        internal Task ITask = new Task();
        internal Task CTask = new Task();
        internal string TriggerEntityModel;
        internal object InitObj = new object();

        internal int WeaponIdCounter;
        internal int PlayerEventId;
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
        internal int Count = -1;
        internal int LCount;
        internal int SCount;
        internal int LogLevel;
        internal int AwakeCount = -1;
        internal int AsleepCount = -1;
        internal ulong MultiplayerId;
        internal ulong MuzzleIdCounter;
        internal long PlayerId;
        internal double SyncDistSqr;
        internal double SyncBufferedDistSqr;
        internal double SyncDist;
        internal double MaxEntitySpeed;
        internal double Load;
        internal double ScaleFov;
        internal float UiBkOpacity;
        internal float UiOpacity;
        internal bool InMenu;
        internal bool GunnerBlackList;
        internal bool MpActive;
        internal bool IsServer;
        internal bool DedicatedServer;
        internal bool FirstLoop;
        internal bool GameLoaded;
        internal bool PlayersLoaded;
        internal bool MiscLoaded;
        internal bool Tick10;
        internal bool Tick20;
        internal bool Tick60;
        internal bool Tick120;
        internal bool Tick180;
        internal bool Tick300;
        internal bool Tick600;
        internal bool Tick1800;
        internal bool Tick3600;
        internal bool ShieldMod;
        internal bool ReplaceVanilla;
        internal bool ShieldApiLoaded;
        internal bool TargetArmed;
        internal bool InGridAiBlock;
        internal bool IsCreative;
        internal bool IsClient;
        internal bool HandlesInput;
        internal bool AuthLogging;
        internal bool DamageHandler;
        internal bool LocalVersion;
        internal bool SupressLoad;

        internal enum AnimationType
        {
            Movement,
            ShowInstant,
            HideInstant,
            ShowFade,
            HideFade,
            Delay,
            EmissiveOnly
        }

        private int _loadCounter = 1;
        private int _shortLoadCounter = 1;
        private uint _lastDrawTick;
        private bool _paused;

        internal class HackEqualityComparer : System.Collections.IEqualityComparer
        {
            internal MyObjectBuilder_Definitions Def;
            public bool Equals(object a, object b) => false;

            public int GetHashCode(object o)
            {
                var definitions = o as MyObjectBuilder_Definitions;
                if (definitions != null)
                    Def = definitions;
                return 0;
            }
        }

        internal VoxelCache NewVoxelCache
        {
            get {
                if (VoxelCachePool.Count > 0)
                    return VoxelCachePool.Pop();

                var cache = new VoxelCache { Id = MuzzleIdCounter++ };
                VoxelCaches.Add(cache.Id, cache);
                return cache;
            }   
            set { VoxelCachePool.Push(value); } 
        }

        internal int UniqueWeaponId => WeaponIdCounter++;

        public T CastProhibit<T>(T ptr, object val) => (T) val;

        public Session()
        {
            UiInput = new UiInput(this);
            TargetUi = new TargetUi(this);
            WheelUi = new Wheel(this);
            HudUi = new Hud(this);
            DsUtil = new DSUtils(this);
            DsUtil2 = new DSUtils(this);
            StallReporter = new StallReporter(this);
            Av = new RunAv(this);
            Api = new ApiBackend(this);
            ApiServer = new ApiServer(this);
            Projectiles = new Projectiles.Projectiles(this);
            Proccessor = new NetworkProccessor(this);
            AcqManager = new AcquireManager(this);
            TerminalMon = new TerminalMonitor(this);
            ProblemRep = new ProblemReport(this);
            VisDirToleranceCosine = Math.Cos(MathHelper.ToRadians(VisDirToleranceAngle));
            AimDirToleranceCosine = Math.Cos(MathHelper.ToRadians(AimDirToleranceAngle));

            VoxelCaches[ulong.MaxValue] = new VoxelCache();

            HeatEmissives = CreateHeatEmissive();
            LoadVanillaData();
            var arrayOfPacketTypes = Enum.GetValues(typeof(PacketType));
            foreach (var suit in (PacketType[]) arrayOfPacketTypes)
            {
                PacketPools.Add(suit, new MyConcurrentPool<Packet>(128, packet => packet.CleanUp()));
            }

            for (int i = 0; i < AuthorSettings.Length; i++)
                AuthorSettings[i] = -1;
        }
    }
}