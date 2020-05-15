using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ParallelTasks;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Weapons;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Profiler;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;
using WeaponCore.Api;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Hud;
using static WeaponCore.Support.GridAi;
using Task = ParallelTasks.Task;

namespace WeaponCore
{
    public partial class Session
    {
        internal const ushort ServerPacketId = 62518;
        internal const ushort ClientPacketId = 62519;
        internal const double TickTimeDiv = 0.0625;
        internal const double VisDirToleranceAngle = 2; //in degrees
        internal const double AimDirToleranceAngle = 5; //in degrees
        internal const int VersionControl = 2;
        internal const uint ResyncMinDelayTicks = 720;
        internal const uint ServerTickOffset = 4;

        internal volatile bool Inited;
        internal volatile bool TurretControls;
        internal volatile bool FixedMissileControls;
        internal volatile bool FixedGunControls;
        internal volatile bool SorterControls;
        internal volatile bool BaseControlsActions;
        internal volatile bool DbCallBackComplete = true;
        internal volatile bool Pause;

        internal readonly TargetCompare TargetCompare = new TargetCompare();

        internal readonly
            MyConcurrentPool<ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>> BlockTypePool = new MyConcurrentPool<ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>>(64);

        internal readonly MyConcurrentPool<TargetInfo> TargetInfoPool = new MyConcurrentPool<TargetInfo>(256);
        internal readonly MyConcurrentPool<GroupInfo> GroupInfoPool = new MyConcurrentPool<GroupInfo>(128);
        internal readonly MyConcurrentPool<WeaponAmmoMoveRequest> InventoryMoveRequestPool = new MyConcurrentPool<WeaponAmmoMoveRequest>(128);
        internal readonly MyConcurrentPool<Dictionary<MyInventory, MyFixedPoint>> CachedInvDefDictPool = new MyConcurrentPool<Dictionary<MyInventory, MyFixedPoint>>(128);
        internal readonly MyConcurrentPool<Dictionary<MyDefinitionId, Dictionary<MyInventory, MyFixedPoint>>> CachedInvPullDictPool = new MyConcurrentPool<Dictionary<MyDefinitionId, Dictionary<MyInventory, MyFixedPoint>>>(128);
        internal readonly MyConcurrentPool<Dictionary<MyInventory, float>> CachedInvRemoveDictPool = new MyConcurrentPool<Dictionary<MyInventory, float>>(128);
        internal readonly MyConcurrentPool<List<MyInventory>> TmpInventoryListPool = new MyConcurrentPool<List<MyInventory>>(128);
        internal readonly MyConcurrentPool<ConcurrentCachingList<MyCubeBlock>> ConcurrentListPool = new MyConcurrentPool<ConcurrentCachingList<MyCubeBlock>>(100);
        internal readonly MyConcurrentPool<FatMap> FatMapPool = new MyConcurrentPool<FatMap>(128);
        internal readonly MyConcurrentPool<WeaponCount> WeaponCountPool = new MyConcurrentPool<WeaponCount>(64, count => count.Current = 0);
        internal readonly MyConcurrentPool<GridAi> GridAiPool = new MyConcurrentPool<GridAi>(128, ai => ai.CleanUp());
        internal readonly MyConcurrentPool<List<IMySlimBlock>> SlimPool = new MyConcurrentPool<List<IMySlimBlock>>(128, slim => slim.Clear());
        internal readonly MyConcurrentPool<MyWeaponPlatform> PlatFormPool = new MyConcurrentPool<MyWeaponPlatform>(256, platform => platform.Clean());
        internal readonly MyConcurrentPool<Weapon.AmmoInfo> AmmoInfoPool = new MyConcurrentPool<Weapon.AmmoInfo>(128, info => info.Clean());
        internal readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();
        internal readonly ConcurrentDictionary<long, IMyCharacter> Admins = new ConcurrentDictionary<long, IMyCharacter>();
        internal readonly ConcurrentDictionary<IMyCharacter, IMyPlayer> AdminMap = new ConcurrentDictionary<IMyCharacter, IMyPlayer>();
        internal readonly ConcurrentDictionary<ulong, long> SteamToPlayer = new ConcurrentDictionary<ulong, long>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridAi> GridTargetingAIs = new ConcurrentDictionary<MyCubeGrid, GridAi>();
        internal readonly ConcurrentDictionary<MyCubeGrid, ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>> GridToBlockTypeMap = new ConcurrentDictionary<MyCubeGrid, ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>>>();
        internal readonly ConcurrentDictionary<MyCubeGrid, FatMap> GridToFatMap = new ConcurrentDictionary<MyCubeGrid, FatMap>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridAi> GridToMasterAi = new ConcurrentDictionary<MyCubeGrid, GridAi>();
        internal readonly MyConcurrentHashSet<MyCubeGrid> DirtyGrids = new MyConcurrentHashSet<MyCubeGrid>();
        internal readonly ConcurrentCachingList<WeaponComponent> CompsToStart = new ConcurrentCachingList<WeaponComponent>();
        internal readonly ConcurrentCachingList<Weapon> WeaponToPullAmmo = new ConcurrentCachingList<Weapon>();
        internal readonly ConcurrentCachingList<Weapon> WeaponsToRemoveAmmo = new ConcurrentCachingList<Weapon>();
        internal readonly ConcurrentCachingList<WeaponAmmoMoveRequest> AmmoToRemoveQueue = new ConcurrentCachingList<WeaponAmmoMoveRequest>();
        internal readonly ConcurrentCachingList<WeaponAmmoMoveRequest> AmmoToPullQueue = new ConcurrentCachingList<WeaponAmmoMoveRequest>();
        internal readonly ConcurrentCachingList<Weapon> ClientAmmoCheck = new ConcurrentCachingList<Weapon>();
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

        internal readonly Dictionary<ulong, HashSet<long>> PlayerEntityIdInRange = new Dictionary<ulong, HashSet<long>>();

        internal readonly Dictionary<Weapon, int> ChargingWeaponsCheck = new Dictionary<Weapon, int>();
        internal readonly HashSet<string> VanillaSubpartNames = new HashSet<string>();
        internal readonly HashSet<MyDefinitionBase> AllArmorBaseDefinitions = new HashSet<MyDefinitionBase>();
        internal readonly HashSet<MyDefinitionBase> HeavyArmorBaseDefinitions = new HashSet<MyDefinitionBase>();
        internal readonly HashSet<Weapon> WeaponsSyncCheck = new HashSet<Weapon>();
        internal readonly HashSet<MyDefinitionId> AmmoDefIds = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);


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
        internal readonly List<PacketInfo> PacketsToClient = new List<PacketInfo>(128);
        internal readonly List<Packet> PacketsToServer = new List<Packet>(128);
        internal readonly List<Weapon> WeaponsToSync = new List<Weapon>(128);
        internal readonly List<Fragment> FragmentsNeedingEntities = new List<Fragment>(128);
        internal readonly List<WeaponComponent> ClientGridResyncRequests = new List<WeaponComponent>(128);


        internal readonly DsUniqueListFastRemove<ErrorPacket> ClientSideErrorPktList = new DsUniqueListFastRemove<ErrorPacket>(128);

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

        internal List<RadiatedBlock> SlimsSortedList = new List<RadiatedBlock>(1024);
        internal MyConcurrentPool<MyEntity> TriggerEntityPool;

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
        internal MyCubeBlock ActiveControlBlock;
        internal MyEntity ControlledEntity;
        internal Projectiles.Projectiles Projectiles;
        internal ApiBackend Api;
        internal Action<Vector3, float> ProjectileAddedCallback = (location, health) => { };
        internal ShieldApi SApi = new ShieldApi();
        internal NetworkReporter Reporter = new NetworkReporter();
        internal MyStorageData TmpStorage = new MyStorageData();

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
        internal int AmmoPulls;
        internal int Count = -1;
        internal int LCount;
        internal int SCount;

        internal uint Tick;

        internal ulong AuthorSteamId = 76561197969691953;
        internal ulong MultiplayerId;
        internal long PlayerId;
        internal long AuthorPlayerId;
        internal long LastTerminalId;

        internal double SyncDistSqr;
        internal double SyncBufferedDistSqr;
        internal double SyncDist;
        internal double MaxEntitySpeed;
        internal double Load;

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

            VisDirToleranceCosine = Math.Cos(MathHelper.ToRadians(VisDirToleranceAngle));
            AimDirToleranceCosine = Math.Cos(MathHelper.ToRadians(AimDirToleranceAngle));
            HeatEmissives = CreateHeatEmissive();

            LoadVanillaData();
            var arrayOfPacketTypes = Enum.GetValues(typeof(PacketType));
            foreach (var suit in (PacketType[]) arrayOfPacketTypes)
            {
                PacketPools.Add(suit, new MyConcurrentPool<Packet>(128, packet => packet.CleanUp()));
                PacketQueues = new List<PacketObj>[arrayOfPacketTypes.Length];
                for (int i = 0; i < PacketQueues.Length; i++)
                    PacketQueues[i] = new List<PacketObj>();
            }
        }
    }
}