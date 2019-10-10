using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Library.Threading;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        internal const ushort PACKET_ID = 62518;
        internal const double TickTimeDiv = 0.0625;
        internal const double VisDirToleranceAngle = 2; //in degrees
        internal const double AimDirToleranceAngle = 5; //in degrees

        internal static readonly double VisDirToleranceCosine = Math.Cos(MathHelper.ToRadians(VisDirToleranceAngle));
        internal static readonly double AimDirToleranceCosine = Math.Cos(MathHelper.ToRadians(AimDirToleranceAngle));

        internal static Session Instance { get; private set; }

        internal volatile bool Inited;
        internal volatile bool TurretControls;
        internal volatile bool UpgradeControls;
        internal object InitObj = new object();
        internal volatile bool Dispatched;
        internal bool DbsUpdating;
        internal bool HighLoad;

        private int _count = -1;
        private int _lCount;
        private int _eCount;
        private double _syncDistSqr;

        internal readonly SpinLockRef _configLock = new SpinLockRef();

        internal readonly Dictionary<double, List<Vector3I>> LargeBlockSphereDb = new Dictionary<double, List<Vector3I>>();
        internal readonly Dictionary<double, List<Vector3I>> SmallBlockSphereDb = new Dictionary<double, List<Vector3I>>();

        internal readonly List<GridAi> DbsToUpdate = new List<GridAi>();
        internal readonly Projectiles.Projectiles Projectiles = new Projectiles.Projectiles();
        internal readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridAi> GridTargetingAIs = new ConcurrentDictionary<MyCubeGrid, GridAi>();
        internal readonly MyConcurrentDictionary<MyEntity, Dictionary<Vector3I, IMySlimBlock>> SlimSpace = new MyConcurrentDictionary<MyEntity, Dictionary<Vector3I, IMySlimBlock>>();
        internal readonly ConcurrentQueue<InventoryChange> InventoryEvent = new ConcurrentQueue<InventoryChange>();
        internal readonly Dictionary<MyStringHash, WeaponStructure> WeaponPlatforms = new Dictionary<MyStringHash, WeaponStructure>(MyStringHash.Comparer);
        internal readonly Dictionary<string, MyStringHash> SubTypeIdHashMap = new Dictionary<string, MyStringHash>();
        internal readonly Dictionary<int, string> ModelIdToName = new Dictionary<int, string>();
        internal readonly CachingDictionary<LineD, uint> RayCheckLines = new CachingDictionary<LineD, uint>();
        internal readonly ConcurrentQueue<Projectile> Hits = new ConcurrentQueue<Projectile>();
        internal Queue<PartAnimation> animationsToProcess = new Queue<PartAnimation>();
        internal Queue<PartAnimation> animationsToQueue = new Queue<PartAnimation>();

        internal IMyPhysics Physics;
        internal IMyCamera Camera;
        internal IMyGps TargetGps;
        internal MyEntity Target;
        internal GridAi TrackingAi;
        internal IMyBlockPlacerBase Placer;
        internal MyEntity LcdEntity1;
        internal IMyTextPanel LcdPanel1;
        internal readonly HashSet<string> WepActions = new HashSet<string>()
        {
            "WC-L_PowerLevel",
            "WC-L_Guidance"
        };

        internal List<WeaponHit> WeaponHits = new List<WeaponHit>();
        internal DictionaryValuesReader<MyDefinitionId, MyDefinitionBase> AllDefinitions;
        internal DictionaryValuesReader<MyDefinitionId, MyAudioDefinition> SoundDefinitions;
        internal HashSet<MyDefinitionBase> AllArmorBaseDefinitions = new HashSet<MyDefinitionBase>();
        internal HashSet<MyDefinitionBase> HeavyArmorBaseDefinitions = new HashSet<MyDefinitionBase>();

        private readonly MyConcurrentPool<List<Vector3I>> _blockSpherePool = new MyConcurrentPool<List<Vector3I>>(50);
        private readonly CachingList<Shrinking> _shrinking = new CachingList<Shrinking>();
        private readonly CachingList<AfterGlow> _afterGlow = new CachingList<AfterGlow>();
        private readonly Dictionary<string, Dictionary<string, MyTuple<string, string, string>>> _turretDefinitions = new Dictionary<string, Dictionary<string, MyTuple<string, string, string>>>();
        private readonly Dictionary<string, List<WeaponDefinition>> _subTypeIdToWeaponDefs = new Dictionary<string, List<WeaponDefinition>>();
        private readonly MyConcurrentPool<Shrinking> _shrinkPool = new MyConcurrentPool<Shrinking>();

        private readonly List<WeaponDefinition> _weaponDefinitions = new List<WeaponDefinition>();
        private readonly List<Vector3D> _offsetList = new List<Vector3D>();
        internal readonly MyDynamicAABBTreeD ProjectileTree = new MyDynamicAABBTreeD(Vector3D.One * 10.0, 10.0);
        private readonly HashSet<IMySlimBlock> _slimsSet = new HashSet<IMySlimBlock>();
        private readonly List<RadiatedBlock> _slimsSortedList = new List<RadiatedBlock>();
        private readonly HashSet<IMySlimBlock> _destroyedSlims = new HashSet<IMySlimBlock>();

        internal readonly ConcurrentCachingList<WeaponComponent> CompsToStart = new ConcurrentCachingList<WeaponComponent>();
        internal readonly double ApproachDegrees = Math.Cos(MathHelper.ToRadians(25));
        internal DSUtils DsUtil { get; set; } = new DSUtils();

        internal readonly Guid LogicSettingsGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811501");
        internal readonly Guid LogicStateGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811502");
        internal readonly Wheel Ui = new Wheel();
        internal readonly Pointer Pointer = new Pointer();
        internal Color[] HeatEmissives;

        internal double Load;
        internal uint Tick;
        internal int PlayerEventId;
        internal int ProCounter;
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
        internal bool WepControl;
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
        internal bool ControlChanged;

        internal Vector3D CameraPos;
        internal MyCockpit ActiveCockPit;
        internal MyEntity ControlledEntity;


        internal readonly MyStringId LaserMaterial = MyStringId.GetOrCompute("WeaponLaser");
        internal readonly MyStringId WarpMaterial = MyStringId.GetOrCompute("WarpBubble");

        internal readonly Guid LogictateGuid = new Guid("85BED4F5-4FB9-4230-FEED-BE79D9811500");
        internal readonly Guid LogicettingsGuid = new Guid("85BED4F5-4FB9-4230-FEED-BE79D9811501");

        internal ShieldApi SApi = new ShieldApi();
        private readonly FutureEvents _futureEvents = new FutureEvents();
        internal MatrixD EndMatrix = MatrixD.CreateTranslation(Vector3D.MaxValue);

    }
}