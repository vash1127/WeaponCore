using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Jakaria;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.ModAPI.Weapons;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;
using WeaponCore.Api;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using WeaponCore.Settings;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Settings.CoreSettings.ServerSettings;
using Task = ParallelTasks.Task;

namespace WeaponCore
{
    public partial class Session
    {
        internal const ushort StringPacketId = 62517;
        internal const ushort ServerPacketId = 62518;
        internal const ushort ClientPacketId = 62519;
        internal const double TickTimeDiv = 0.0625;
        internal const double VisDirToleranceAngle = 2; //in degrees
        internal const double AimDirToleranceAngle = 5; //in degrees
        internal const int VersionControl = 32;
        internal const int AwakeBuckets = 60;
        internal const int AsleepBuckets = 180;
        internal const int ServerCfgVersion = 3;
        internal const int ClientCfgVersion = 2;
        internal const string ServerCfgName = "WeaponCoreServer.cfg";
        internal const string ClientCfgName = "WeaponCoreClient.cfg";
        internal volatile bool Inited;
        internal volatile bool TurretControls;
        internal volatile bool FixedMissileControls;
        internal volatile bool FixedMissileReloadControls;
        internal volatile bool FixedGunControls;
        internal volatile bool SorterControls;
        internal volatile bool BaseControlsActions;
        internal volatile uint LastDeform;
        internal volatile uint Tick;

        internal readonly TargetCompare TargetCompare = new TargetCompare();
        internal readonly WaterModAPI WApi = new WaterModAPI();

        internal static readonly HashSet<ulong> AuthorIds = new HashSet<ulong> { 76561197969691953, 76561198061737246, 76561198116813162 };
        internal readonly MyStringHash ShieldBypassDamageType = MyStringHash.GetOrCompute("bypass");
        internal readonly MyConcurrentPool<ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>> BlockTypePool = new MyConcurrentPool<ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>>(64);
        internal readonly MyConcurrentPool<TargetInfo> TargetInfoPool = new MyConcurrentPool<TargetInfo>(256, info => info.Clean());
        internal readonly MyConcurrentPool<WeaponAmmoMoveRequest> InventoryMoveRequestPool = new MyConcurrentPool<WeaponAmmoMoveRequest>(128, invMove => invMove.Clean());
        internal readonly MyConcurrentPool<ConcurrentCachingList<MyCubeBlock>> ConcurrentListPool = new MyConcurrentPool<ConcurrentCachingList<MyCubeBlock>>(100, cList => cList.ClearImmediate());

        internal readonly MyConcurrentPool<GridMap> GridMapPool = new MyConcurrentPool<GridMap>(128, fatMap => fatMap.Clean());
        internal readonly MyConcurrentPool<WeaponCount> WeaponCountPool = new MyConcurrentPool<WeaponCount>(64, count => count.Current = 0);
        internal readonly MyConcurrentPool<GridAi> GridAiPool = new MyConcurrentPool<GridAi>(128, ai => ai.CleanUp());
        internal readonly MyConcurrentPool<List<IMySlimBlock>> SlimPool = new MyConcurrentPool<List<IMySlimBlock>>(128, slim => slim.Clear());
        internal readonly MyConcurrentPool<CorePlatform> PlatFormPool = new MyConcurrentPool<CorePlatform>(256, platform => platform.Clean());
        internal readonly MyConcurrentPool<PacketObj> PacketObjPool = new MyConcurrentPool<PacketObj>(128, packet => packet.Clean());
        internal readonly MyConcurrentPool<ConstructPacket> PacketConstructPool = new MyConcurrentPool<ConstructPacket>(64, packet => packet.CleanUp());
        internal readonly MyConcurrentPool<ConstructFociPacket> PacketConstructFociPool = new MyConcurrentPool<ConstructFociPacket>(64, packet => packet.CleanUp());
        internal readonly MyConcurrentPool<AiDataPacket> PacketAiPool = new MyConcurrentPool<AiDataPacket>(64, packet => packet.CleanUp());
        internal readonly MyConcurrentPool<CompBasePacket> PacketCompBasePool = new MyConcurrentPool<CompBasePacket>(64, packet => packet.CleanUp());
        internal readonly MyConcurrentPool<CompStatePacket> PacketStatePool = new MyConcurrentPool<CompStatePacket>(64, packet => packet.CleanUp());
        internal readonly MyConcurrentPool<WeaponReloadPacket> PacketReloadPool = new MyConcurrentPool<WeaponReloadPacket>(64, packet => packet.CleanUp());
        internal readonly MyConcurrentPool<WeaponAmmoPacket> PacketAmmoPool = new MyConcurrentPool<WeaponAmmoPacket>(64, packet => packet.CleanUp());
        internal readonly MyConcurrentPool<TargetPacket> PacketTargetPool = new MyConcurrentPool<TargetPacket>(64, packet => packet.CleanUp());
        internal readonly MyConcurrentPool<BetterInventoryItem> BetterInventoryItems = new MyConcurrentPool<BetterInventoryItem>(256);
        internal readonly MyConcurrentPool<MyConcurrentList<MyPhysicalInventoryItem>> PhysicalItemListPool = new MyConcurrentPool<MyConcurrentList<MyPhysicalInventoryItem>>(256, list => list.Clear());
        internal readonly MyConcurrentPool<MyConcurrentList<BetterInventoryItem>> BetterItemsListPool = new MyConcurrentPool<MyConcurrentList<BetterInventoryItem>>(256, list => list.Clear());

        internal readonly Stack<MyEntity3DSoundEmitter> Emitters = new Stack<MyEntity3DSoundEmitter>(256);
        internal readonly Stack<VoxelCache> VoxelCachePool = new Stack<VoxelCache>(256);

        internal readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();
        internal readonly ConcurrentDictionary<long, IMyCharacter> Admins = new ConcurrentDictionary<long, IMyCharacter>();
        internal readonly ConcurrentDictionary<IMyCharacter, IMyPlayer> AdminMap = new ConcurrentDictionary<IMyCharacter, IMyPlayer>();
        internal readonly ConcurrentDictionary<ulong, long> SteamToPlayer = new ConcurrentDictionary<ulong, long>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridAi> GridTargetingAIs = new ConcurrentDictionary<MyCubeGrid, GridAi>();
        internal readonly ConcurrentDictionary<MyCubeGrid, ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>> GridToBlockTypeMap = new ConcurrentDictionary<MyCubeGrid, ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>>();
        internal readonly ConcurrentDictionary<MyInventory, MyConcurrentList<MyPhysicalInventoryItem>> InventoryItems = new ConcurrentDictionary<MyInventory, MyConcurrentList<MyPhysicalInventoryItem>>();
        internal readonly ConcurrentDictionary<MyInventory, ConcurrentDictionary<uint, BetterInventoryItem>> BlockInventoryItems = new ConcurrentDictionary<MyInventory, ConcurrentDictionary<uint, BetterInventoryItem>>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridMap> GridToInfoMap = new ConcurrentDictionary<MyCubeGrid, GridMap>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridAi> GridToMasterAi = new ConcurrentDictionary<MyCubeGrid, GridAi>();
        internal readonly ConcurrentDictionary<MyInventory, MyConcurrentList<BetterInventoryItem>> AmmoThreadItemList = new ConcurrentDictionary<MyInventory, MyConcurrentList<BetterInventoryItem>>();
        internal readonly ConcurrentDictionary<Weapon, int> WeaponsToRemoveAmmoIndexer = new ConcurrentDictionary<Weapon, int>();

        internal readonly MyConcurrentHashSet<MyCubeGrid> DirtyGridInfos = new MyConcurrentHashSet<MyCubeGrid>();

        internal readonly MyConcurrentHashSet<Weapon> WeaponToPullAmmo = new MyConcurrentHashSet<Weapon>();

        internal readonly ConcurrentCachingList<CoreComponent> CompsToStart = new ConcurrentCachingList<CoreComponent>();
        internal readonly ConcurrentCachingList<GridAi> DelayedAiClean = new ConcurrentCachingList<GridAi>();

        internal readonly CachingHashSet<PacketObj> ClientSideErrorPkt = new CachingHashSet<PacketObj>();

        internal readonly ConcurrentQueue<MyCubeGrid> NewGrids = new ConcurrentQueue<MyCubeGrid>();
        internal readonly ConcurrentQueue<DeferedTypeCleaning> BlockTypeCleanUp = new ConcurrentQueue<DeferedTypeCleaning>();

        internal readonly Queue<PartAnimation> ThreadedAnimations = new Queue<PartAnimation>();

        internal readonly ConcurrentDictionary<MyInventory, int> InventoryMonitors = new ConcurrentDictionary<MyInventory, int>();
        internal readonly Dictionary<MyDefinitionBase, BlockDamage> BlockDamageMap = new Dictionary<MyDefinitionBase, BlockDamage>();
        internal readonly Dictionary<MyDefinitionId, CoreStructure> WeaponPlatforms = new Dictionary<MyDefinitionId, CoreStructure>(MyDefinitionId.Comparer);
        internal readonly Dictionary<string, MyDefinitionId> WeaponCoreBlockDefs = new Dictionary<string, MyDefinitionId>();
        internal readonly Dictionary<string, MyStringHash> SubTypeIdHashMap = new Dictionary<string, MyStringHash>();
        internal readonly Dictionary<double, List<Vector3I>> LargeBlockSphereDb = new Dictionary<double, List<Vector3I>>();
        internal readonly Dictionary<double, List<Vector3I>> SmallBlockSphereDb = new Dictionary<double, List<Vector3I>>();
        internal readonly Dictionary<MyDefinitionId, MyStringHash> VanillaIds = new Dictionary<MyDefinitionId, MyStringHash>(MyDefinitionId.Comparer);
        internal readonly Dictionary<MyStringHash, MyDefinitionId> VanillaCoreIds = new Dictionary<MyStringHash, MyDefinitionId>(MyStringHash.Comparer);
        internal readonly Dictionary<MyStringHash, WeaponAreaRestriction> WeaponAreaRestrictions = new Dictionary<MyStringHash, WeaponAreaRestriction>(MyStringHash.Comparer);
        internal readonly Dictionary<long, InputStateData> PlayerMouseStates = new Dictionary<long, InputStateData>() {[-1] = new InputStateData()};
        internal readonly Dictionary<long, FakeTarget> PlayerDummyTargets = new Dictionary<long, FakeTarget>() { [-1] = new FakeTarget() };
        internal readonly Dictionary<ulong, HashSet<long>> PlayerEntityIdInRange = new Dictionary<ulong, HashSet<long>>();
        internal readonly Dictionary<long, ulong> ConnectedAuthors = new Dictionary<long, ulong>();
        internal readonly Dictionary<ulong, AvInfoCache> AvShotCache = new Dictionary<ulong, AvInfoCache>();
        internal readonly Dictionary<ulong, VoxelCache> VoxelCaches = new Dictionary<ulong, VoxelCache>();
        internal readonly Dictionary<MyCubeBlock, CoreComponent> ArmorCubes = new Dictionary<MyCubeBlock, CoreComponent>();
        internal readonly Dictionary<MyInventory, MyFixedPoint> InventoryVolume = new Dictionary<MyInventory, MyFixedPoint>();
        internal readonly Dictionary<ulong, uint[]> PlayerMIds = new Dictionary<ulong, uint[]>();
        internal readonly Dictionary<object, PacketInfo> PrunedPacketsToClient = new Dictionary<object, PacketInfo>();
        internal readonly Dictionary<long, CoreComponent> IdToCompMap = new Dictionary<long, CoreComponent>();
        internal readonly Dictionary<uint, MyPhysicalInventoryItem> AmmoItems = new Dictionary<uint, MyPhysicalInventoryItem>();
        internal readonly Dictionary<string, MyKeys> KeyMap = new Dictionary<string, MyKeys>();
        internal readonly Dictionary<string, MyMouseButtonsEnum> MouseMap = new Dictionary<string, MyMouseButtonsEnum>();
        internal readonly Dictionary<Weapon, int> ChargingWeaponsIndexer = new Dictionary<Weapon, int>();
        internal readonly Dictionary<MyPlanet, Water> WaterMap = new Dictionary<MyPlanet, Water>();
        internal readonly Dictionary<MyPlanet, double> MaxWaterHeightSqr = new Dictionary<MyPlanet, double>();
        internal readonly Dictionary<WeaponDefinition.ConsumableDef, AmmoModifer> AmmoDamageMap = new Dictionary<WeaponDefinition.ConsumableDef, AmmoModifer>();
        internal readonly Dictionary<ulong, Projectile> MonitoredProjectiles = new Dictionary<ulong, Projectile>();
        internal readonly HashSet<MyDefinitionId> DefIdsComparer = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        internal readonly HashSet<string> VanillaSubpartNames = new HashSet<string>();
        internal readonly HashSet<MyDefinitionBase> AllArmorBaseDefinitions = new HashSet<MyDefinitionBase>();
        internal readonly HashSet<MyDefinitionBase> HeavyArmorBaseDefinitions = new HashSet<MyDefinitionBase>();
        internal readonly HashSet<MyStringHash> CustomArmorSubtypes = new HashSet<MyStringHash>();
        internal readonly HashSet<MyStringHash> CustomHeavyArmorSubtypes = new HashSet<MyStringHash>();
        internal readonly HashSet<MyDefinitionId> AmmoDefIds = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        internal readonly HashSet<MyCubeGrid> DeformProtection = new HashSet<MyCubeGrid>();
        internal readonly HashSet<IMyTerminalAction> CustomActions = new HashSet<IMyTerminalAction>();
        internal readonly HashSet<IMyTerminalAction> AlteredActions = new HashSet<IMyTerminalAction>();
        internal readonly HashSet<IMyTerminalControl> CustomControls = new HashSet<IMyTerminalControl>();
        internal readonly HashSet<IMyTerminalControl> AlteredControls = new HashSet<IMyTerminalControl>();
        internal readonly HashSet<Weapon> WeaponLosDebugActive = new HashSet<Weapon>();

        internal readonly List<Weapon> InvPullClean = new List<Weapon>();
        internal readonly List<Weapon> InvRemoveClean = new List<Weapon>();
        internal readonly List<CoreComponent> CompsDelayed = new List<CoreComponent>();
        internal readonly List<CompReAdd> CompReAdds = new List<CompReAdd>();
        internal readonly List<Projectile> Hits = new List<Projectile>(16);
        internal readonly List<Weapon> AcquireTargets = new List<Weapon>(128);
        internal readonly List<Weapon> HomingWeapons = new List<Weapon>(128);
        internal readonly List<MyDefinitionId> WeaponCoreFixedBlockDefs = new List<MyDefinitionId>();
        internal readonly List<MyDefinitionId> WeaponCoreTurretBlockDefs = new List<MyDefinitionId>();
        internal readonly List<MyCubeGrid> DirtyGridsTmp = new List<MyCubeGrid>(10);
        internal readonly List<DbScan> DbsToUpdate = new List<DbScan>(32);
        internal readonly List<Weapon> ShootingWeapons = new List<Weapon>(128);
        internal readonly List<PacketInfo> PacketsToClient = new List<PacketInfo>(128);
        internal readonly List<Packet> PacketsToServer = new List<Packet>(128);
        internal readonly List<Fragment> FragmentsNeedingEntities = new List<Fragment>(128);
        internal readonly List<WeaponAmmoMoveRequest> AmmoToPullQueue = new List<WeaponAmmoMoveRequest>(128);
        internal readonly List<PacketObj> ClientPacketsToClean = new List<PacketObj>(64);
        internal readonly List<Weapon> ChargingWeapons = new List<Weapon>(64);
        internal readonly HashSet<GridAi> GridsToUpdateInventories = new HashSet<GridAi>();
        internal readonly List<CleanSound> SoundsToClean = new List<CleanSound>(128);
        internal readonly List<LosDebug> LosDebugList = new List<LosDebug>(128);

        internal readonly int[] AuthorSettings = new int[6];

        ///
        ///
        ///

        internal readonly double ApproachDegrees = Math.Cos(MathHelper.ToRadians(50));
        internal readonly FutureEvents FutureEvents = new FutureEvents();
        internal readonly BoundingFrustumD CameraFrustrum = new BoundingFrustumD();
        internal readonly Guid CompDataGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811501");
        internal readonly Guid AiDataGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811502");
        internal readonly Guid ConstructDataGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811503");

        internal readonly double VisDirToleranceCosine;
        internal readonly double AimDirToleranceCosine;

        private readonly MyConcurrentPool<List<Vector3I>> _blockSpherePool = new MyConcurrentPool<List<Vector3I>>(25);
        private readonly HashSet<IMySlimBlock> _slimsSet = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _destroyedSlims = new HashSet<IMySlimBlock>();
        private readonly HashSet<IMySlimBlock> _destroyedSlimsClient = new HashSet<IMySlimBlock>();
        private readonly Dictionary<IMySlimBlock, float> _slimHealthClient = new Dictionary<IMySlimBlock, float>();
        private readonly Dictionary<string, Dictionary<string, MyTuple<string, string, string>>> _turretDefinitions = new Dictionary<string, Dictionary<string, MyTuple<string, string, string>>>();
        private readonly Dictionary<string, List<WeaponDefinition>> _subTypeIdToWeaponDefs = new Dictionary<string, List<WeaponDefinition>>();
        private readonly List<MyKeys> _pressedKeys = new List<MyKeys>();
        private readonly List<MyMouseButtonsEnum> _pressedButtons = new List<MyMouseButtonsEnum>();
        private readonly List<MyEntity> _tmpNearByBlocks = new List<MyEntity>();

        internal List<RadiatedBlock> SlimsSortedList = new List<RadiatedBlock>(1024);
        internal MyConcurrentPool<MyEntity> TriggerEntityPool;

        internal MyDynamicAABBTreeD ProjectileTree = new MyDynamicAABBTreeD(Vector3D.One * 10.0, 10.0);

        internal List<PartAnimation> AnimationsToProcess = new List<PartAnimation>(128);
        internal List<WeaponDefinition> WeaponDefinitions = new List<WeaponDefinition>();
        internal DictionaryValuesReader<MyDefinitionId, MyDefinitionBase> AllDefinitions;
        internal DictionaryValuesReader<MyDefinitionId, MyAudioDefinition> SoundDefinitions;
        internal Color[] HeatEmissives;

        internal ControlQuery ControlRequest;
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
        internal StallReporter InnerStallReporter;
        internal UiInput UiInput;
        internal TargetUi TargetUi;
        internal Hud HudUi;
        internal CoreSettings Settings;
        internal TerminalMonitor TerminalMon;
        internal ProblemReport ProblemRep;

        internal MatrixD CameraMatrix;
        internal Vector3D CameraPos;
        internal Vector3D PlayerPos;
        internal Task PTask = new Task();
        internal Task GridTask = new Task();
        internal Task DbTask = new Task();
        internal Task ITask = new Task();
        internal Task CTask = new Task();
        internal MyStringHash ShieldHash;
        internal MyStringHash WaterHash;
        internal string TriggerEntityModel;
        internal string ServerVersion;
        internal string PlayerMessage;
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
        internal int Rays;
        internal ulong MultiplayerId;
        internal ulong MuzzleIdCounter;
        internal long PlayerId;
        internal double SyncDistSqr;
        internal double SyncBufferedDistSqr;
        internal double SyncDist;
        internal double MaxEntitySpeed;
        internal double Load;
        internal double ScaleFov;
        internal double RayMissAmounts;
        internal float AspectRatio;
        internal float AspectRatioInv;
        internal float UiBkOpacity;
        internal float UiOpacity;
        internal float CurrentFovWithZoom;
        internal bool PurgedAll;
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
        internal bool WaterApiLoaded;
        internal bool TargetArmed;
        internal bool InGridAiBlock;
        internal bool IsCreative;
        internal bool IsClient;
        internal bool HandlesInput;
        internal bool AuthLogging;
        internal bool DamageHandler;
        internal bool LocalVersion;
        internal bool SuppressWc;
        internal bool PbApiInited;
        internal bool PbActivate;
        internal bool ManualShot;
        internal bool ClientCheck;
        internal bool DbUpdating;
        internal bool InventoryUpdate;
        internal bool GlobalDamageModifed;
        internal bool WaterMod;
        internal bool DebugLos = false;
        internal bool DebugTargetAcquire = true;
        internal bool QuickDisableGunsCheck;
        [Flags]
        internal enum SafeZoneAction
        {
            Damage = 1,
            Shooting = 2,
            Drilling = 4,
            Welding = 8,
            Grinding = 16, // 0x00000010
            VoxelHand = 32, // 0x00000020
            Building = 64, // 0x00000040
            LandingGearLock = 128, // 0x00000080
            ConvertToStation = 256, // 0x00000100
            All = ConvertToStation | LandingGearLock | Building | VoxelHand | Grinding | Welding | Drilling | Shooting | Damage, // 0x000001FF
            AdminIgnore = ConvertToStation | Building | VoxelHand | Grinding | Welding | Drilling | Shooting, // 0x0000017E
        }

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

        public static T CastProhibit<T>(T ptr, object val) => (T) val;

        public Session()
        {
            UiInput = new UiInput(this);
            HudUi = new Hud(this);
            TargetUi = new TargetUi(this);
            DsUtil = new DSUtils(this);
            DsUtil2 = new DSUtils(this);
            StallReporter = new StallReporter();
            InnerStallReporter = new StallReporter();
            Av = new RunAv(this);
            Api = new ApiBackend(this);
            ApiServer = new ApiServer(this);
            Projectiles = new Projectiles.Projectiles(this);
            AcqManager = new AcquireManager(this);
            TerminalMon = new TerminalMonitor(this);

            ProblemRep = new ProblemReport(this);
            VisDirToleranceCosine = Math.Cos(MathHelper.ToRadians(VisDirToleranceAngle));
            AimDirToleranceCosine = Math.Cos(MathHelper.ToRadians(AimDirToleranceAngle));

            VoxelCaches[ulong.MaxValue] = new VoxelCache();

            HeatEmissives = CreateHeatEmissive();
            LoadVanillaData();

            for (int i = 0; i < AuthorSettings.Length; i++)
                AuthorSettings[i] = -1;
        }
    }
}