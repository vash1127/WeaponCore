using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectiles;

namespace WeaponCore
{
    public partial class Session
    {
        internal const ushort PACKET_ID = 62518;
        internal const double TickTimeDiv = 0.0625;

        internal static Session Instance { get; private set; }

        internal volatile bool Inited;
        internal volatile bool Dispatched;

        private int _count = -1;
        private int _lCount;
        private int _eCount;
        private double _syncDistSqr;

        private readonly object _configLock = new object();
        private readonly CachingList<Shrinking> _shrinking = new CachingList<Shrinking>();
        private readonly Dictionary<string, Dictionary<string, string>> _turretDefinitions = new Dictionary<string, Dictionary<string, string>>();
        private readonly Dictionary<string, List<WeaponDefinition>> _subTypeIdToWeaponDefs = new Dictionary<string, List<WeaponDefinition>>();
        private readonly MyConcurrentPool<Shrinking> _shrinkPool = new MyConcurrentPool<Shrinking>();
        private readonly List<WeaponDefinition> _weaponDefinitions = new List<WeaponDefinition>();
        private readonly ConcurrentQueue<WeaponComponent> _compsToStart = new ConcurrentQueue<WeaponComponent>();

        internal DSUtils DsUtil { get; set; } = new DSUtils();

        internal readonly Guid LogicSettingsGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811501");
        internal readonly Guid LogicStateGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811502");

        internal uint Tick;
        internal int PlayerEventId;
        internal int ProCounter;
        internal int ModelCount;
        internal int ExplosionCounter;

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

        internal ulong AuthorSteamId = 76561197969691953;
        internal long AuthorPlayerId;
        internal long LastTerminalId;
        internal double SyncDistSqr;
        internal double SyncBufferedDistSqr;
        internal double SyncDist;
        internal double MaxEntitySpeed;
        internal float Zoom;

        internal bool ControlInit;
        internal bool MpActive;
        internal bool IsServer;
        internal bool DedicatedServer;
        internal bool WepAction;
        internal bool WepControl;
        internal bool FirstLoop;
        internal bool GameLoaded;
        internal bool MiscLoaded;
        internal bool Tick20;
        internal bool Tick60;
        internal bool Tick180;
        internal bool Tick300;
        internal bool Tick600;
        internal bool Tick1800;
        internal bool ShieldMod;
        internal bool ShieldApiLoaded;
        internal bool MouseButtonPressed;
        internal bool MouseButtonLeft;
        internal bool MouseButtonMiddle;
        internal bool MouseButtonRight;
        internal bool InTurret;
        internal Vector3D CameraPos;
        internal MyEntity ControlledEntity;

        internal readonly MyStringId LaserMaterial = MyStringId.GetOrCompute("WeaponLaser");
        internal readonly MyStringId WarpMaterial = MyStringId.GetOrCompute("WarpBubble");

        internal readonly Guid LogictateGuid = new Guid("85BED4F5-4FB9-4230-FEED-BE79D9811500");
        internal readonly Guid LogicettingsGuid = new Guid("85BED4F5-4FB9-4230-FEED-BE79D9811501");

        internal readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();
        internal readonly ConcurrentQueue<InventoryChange> InventoryEvent = new ConcurrentQueue<InventoryChange>();
        internal readonly ConcurrentDictionary<MyCubeGrid, GridTargetingAi> GridTargetingAIs = new ConcurrentDictionary<MyCubeGrid, GridTargetingAi>();
        internal readonly Dictionary<MyStringHash, WeaponStructure> WeaponPlatforms = new Dictionary<MyStringHash, WeaponStructure>(MyStringHash.Comparer);
        internal readonly Dictionary<string, MyStringHash> SubTypeIdHashMap = new Dictionary<string, MyStringHash>();
        internal readonly Dictionary<int, string> ModelIdToName = new Dictionary<int, string>();
        internal readonly Projectiles.Projectiles Projectiles = new Projectiles.Projectiles();
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
        internal ShieldApi SApi = new ShieldApi();
        internal FutureEvents FutureEvents = new FutureEvents();
        internal MatrixD EndMatrix = MatrixD.CreateTranslation(Vector3D.MaxValue);
    }
}