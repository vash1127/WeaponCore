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
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using Task = ParallelTasks.Task;

namespace WeaponCore
{
    public partial class Session
    {
        internal const ushort PACKET_ID = 62518;
        internal const double TickTimeDiv = 0.0625;
        internal const double VisDirToleranceAngle = 2; //in degrees
        internal const double AimDirToleranceAngle = 5; //in degrees

        internal volatile bool Inited;
        internal volatile bool TurretControls;
        internal volatile bool SorterControls;

        private readonly MyConcurrentPool<List<Vector3I>> _blockSpherePool = new MyConcurrentPool<List<Vector3I>>(25);
        private readonly CachingList<Shrinking> _shrinking = new CachingList<Shrinking>();
        private readonly Dictionary<string, Dictionary<string, MyTuple<string, string, string>>> _turretDefinitions = new Dictionary<string, Dictionary<string, MyTuple<string, string, string>>>();
        private readonly Dictionary<string, List<WeaponDefinition>> _subTypeIdToWeaponDefs = new Dictionary<string, List<WeaponDefinition>>();
        private readonly MyConcurrentPool<Shrinking> _shrinkPool = new MyConcurrentPool<Shrinking>();
        private readonly List<MyTuple<MyInventory, int>> _inventoriesToPull = new List<MyTuple<MyInventory, int>>();
        private readonly List<UpgradeDefinition> _upgradeDefinitions = new List<UpgradeDefinition>();
        private readonly List<Vector3D> _offsetList = new List<Vector3D>();
        private readonly HashSet<IMySlimBlock> _slimsSet = new HashSet<IMySlimBlock>();
        private readonly List<RadiatedBlock> _slimsSortedList = new List<RadiatedBlock>();
        private readonly HashSet<IMySlimBlock> _destroyedSlims = new HashSet<IMySlimBlock>();
        private readonly CachingList<AfterGlow> _afterGlow = new CachingList<AfterGlow>();

        private List<WeaponDefinition> _weaponDefinitions = new List<WeaponDefinition>();

        private long _prevTargetId;
        private int _count = -1;
        private int _lCount;
        private int _eCount;
        private double _syncDistSqr;

        internal readonly Dictionary<double, List<Vector3I>> LargeBlockSphereDb = new Dictionary<double, List<Vector3I>>();
        internal readonly Dictionary<double, List<Vector3I>> SmallBlockSphereDb = new Dictionary<double, List<Vector3I>>();
        internal readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridAi> GridTargetingAIs = new ConcurrentDictionary<MyCubeGrid, GridAi>();
        internal readonly Dictionary<MyStringHash, WeaponStructure> WeaponPlatforms = new Dictionary<MyStringHash, WeaponStructure>(MyStringHash.Comparer);
        internal readonly Dictionary<string, MyStringHash> SubTypeIdHashMap = new Dictionary<string, MyStringHash>();
        internal readonly Dictionary<int, string> ModelIdToName = new Dictionary<int, string>();
        internal readonly ConcurrentQueue<Projectile> Hits = new ConcurrentQueue<Projectile>();
        internal readonly ConcurrentQueue<Weapon> WeaponAmmoPullQueue = new ConcurrentQueue<Weapon>();
        internal readonly ConcurrentQueue<MyTuple<Weapon, MyTuple<MyInventory, int>[]>> AmmoToPullQueue = new ConcurrentQueue<MyTuple<Weapon, MyTuple<MyInventory, int>[]>>();
        internal readonly MyConcurrentPool<MyConcurrentList<MyCubeBlock>> ConcurrentListPool = new MyConcurrentPool<MyConcurrentList<MyCubeBlock>>();
        internal readonly MyConcurrentPool<FatMap> FatMapPool = new MyConcurrentPool<FatMap>();
        internal readonly MyConcurrentDictionary<MyCubeGrid, FatMap> GridToFatMap = new MyConcurrentDictionary<MyCubeGrid, FatMap>();
        internal readonly ConcurrentQueue<MyCubeGrid> NewGrids = new ConcurrentQueue<MyCubeGrid>();
        internal readonly MyConcurrentHashSet<MyCubeGrid> DirtyGrids = new MyConcurrentHashSet<MyCubeGrid>();
        internal readonly List<MyCubeGrid> DirtyGridsTmp = new List<MyCubeGrid>();
        internal readonly MyConcurrentPool<ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>>> BlockTypePool = new MyConcurrentPool<ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>>>();
        internal readonly ConcurrentDictionary<MyCubeGrid, ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>>> GridToBlockTypeMap = new ConcurrentDictionary<MyCubeGrid, ConcurrentDictionary<TargetingDefinition.BlockTypes, MyConcurrentList<MyCubeBlock>>>();
        internal readonly ConcurrentDictionary<MyDefinitionId, Dictionary<MyInventory, MyFixedPoint>> AmmoInventoriesMaster = new ConcurrentDictionary<MyDefinitionId, Dictionary<MyInventory, MyFixedPoint>>(MyDefinitionId.Comparer);
        internal readonly ConcurrentCachingList<WeaponComponent> CompsToStart = new ConcurrentCachingList<WeaponComponent>();
        internal readonly double ApproachDegrees = Math.Cos(MathHelper.ToRadians(50));
        internal readonly FutureEvents FutureEvents = new FutureEvents();
        internal ApiBackend Api = new ApiBackend();
        internal readonly Guid LogicSettingsGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811501");
        internal readonly Guid LogicStateGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811502");

        internal readonly double VisDirToleranceCosine;
        internal readonly double AimDirToleranceCosine;


        internal DsUniqueListFastRemove<PartAnimation> AnimationsToProcess = new DsUniqueListFastRemove<PartAnimation>();
        internal List<GridAi> DbsToUpdate = new List<GridAi>();
        internal MyDynamicAABBTreeD ProjectileTree = new MyDynamicAABBTreeD(Vector3D.One * 10.0, 10.0);
        internal Projectiles.Projectiles Projectiles;

        internal IMyPhysics Physics;
        internal IMyCamera Camera;
        internal IMyGps TargetGps;
        internal GridAi TrackingAi;
        internal DSUtils DsUtil { get; set; } = new DSUtils();
        internal DSUtils DsUtil2 { get; set; } = new DSUtils();
        internal Wheel WheelUi;
        internal TargetUi TargetUi;
        internal UiInput UiInput;
        internal IMyBlockPlacerBase Placer;
        internal MatrixD CameraMatrix;
        internal DictionaryValuesReader<MyDefinitionId, MyDefinitionBase> AllDefinitions;
        internal DictionaryValuesReader<MyDefinitionId, MyAudioDefinition> SoundDefinitions;
        internal HashSet<MyDefinitionBase> AllArmorBaseDefinitions = new HashSet<MyDefinitionBase>();
        internal HashSet<MyDefinitionBase> HeavyArmorBaseDefinitions = new HashSet<MyDefinitionBase>();
        internal Color[] HeatEmissives;
        internal ApiServer ApiServer;

        internal object InitObj = new object();
        internal bool DbsUpdating;
        internal bool HighLoad;
        internal double Load;
        internal uint Tick;
        internal int PlayerEventId;
        internal int ModelCount;
        internal int ExplosionCounter;

        internal int TargetRequests;
        internal int TargetChecks;
        internal int BlockChecks;
        internal int ClosestRayCasts;
        internal int RandomRayCasts;
        internal int TopRayCasts;
        internal int CanShoot;
        internal int ProjectileChecks;
        internal int TargetTransfers;
        internal int TargetSets;
        internal int TargetResets;
        internal int AmmoMoveTriggered;
        internal int AmmoPulls;
        internal int GridsUpdated;

        internal bool ExplosionReady
        {
            get
            {
                if (++ExplosionCounter <= 5)
                {
                    return true;
                }
                return false;
            }
        }


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

        internal bool MpActive;
        internal bool IsServer;
        internal bool DedicatedServer;
        internal bool WepAction;
        internal bool FirstLoop;
        internal bool GameLoaded;
        internal bool MiscLoaded;
        internal bool Tick10;
        internal bool Tick20;
        internal bool Tick60;
        internal bool Tick180;
        internal bool Tick300;
        internal bool Tick600;
        internal bool Tick1800;
        internal bool ShieldMod;
        internal bool ShieldApiLoaded;
        internal bool TargetArmed;
        internal bool InGridAiCockPit;
        internal bool IsCreative;
        //internal bool ControlChanged;

        internal Vector3D CameraPos;
        internal MyCockpit ActiveCockPit;
        internal MyEntity ControlledEntity;
        internal Task PTask = new Task();
        internal Task GridTask = new Task();
        internal Task DbTask = new Task();
        internal Task ITask = new Task();

        internal ShieldApi SApi = new ShieldApi();

        public Session()
        {
            UiInput = new UiInput(this);
            TargetUi = new TargetUi(this);
            WheelUi = new Wheel(this);
            Projectiles = new Projectiles.Projectiles(this);
            VisDirToleranceCosine = Math.Cos(MathHelper.ToRadians(VisDirToleranceAngle));
            AimDirToleranceCosine = Math.Cos(MathHelper.ToRadians(AimDirToleranceAngle));
        }
    }
}