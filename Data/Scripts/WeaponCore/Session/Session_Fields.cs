using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.Entities;
using VRage.Collections;
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
        private readonly MyConcurrentPool<Shrinking> _shrinkPool = new MyConcurrentPool<Shrinking>();
        private readonly List<WeaponDefinition> _weaponDefinitions = new List<WeaponDefinition>();
        private DSUtils _dsUtil { get; set; } = new DSUtils();

        internal readonly Guid LogicSettingsGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811501");
        internal readonly Guid LogicStateGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811502");

        internal uint Tick;
        internal int PlayerEventId { get; set; }
        internal int ProCounter { get; set; }
        internal int ModelCount { get; set; }
        internal ulong AuthorSteamId { get; set; } = 76561197969691953;
        internal long AuthorPlayerId { get; set; }
        internal long LastTerminalId { get; set; }
        internal double SyncDistSqr { get; private set; }
        internal double SyncBufferedDistSqr { get; private set; }
        internal double SyncDist { get; private set; }
        internal float Zoom { get; set; }

        internal bool MpActive { get; set; }
        internal bool IsServer { get; set; }
        internal bool DedicatedServer { get; set; }
        internal bool WepAction { get; set; }
        internal bool WepControl { get; set; }
        internal bool FirstLoop { get; set; }
        internal bool GameLoaded { get; set; }
        internal bool MiscLoaded { get; set; }
        internal bool Tick20 { get; set; }
        internal bool Tick60 { get; set; }
        internal bool Tick180 { get; set; }
        internal bool Tick300 { get; set; }
        internal bool Tick600 { get; set; }
        internal bool Tick1800 { get; set; }
        internal bool ShieldMod { get; set; }
        internal bool ShieldApiLoaded { get; set; }
        internal bool MouseButtonPressed { get; set; }
        internal bool MouseButtonLeft { get; set; }
        internal bool MouseButtonMiddle { get; set; }
        internal bool MouseButtonRight { get; set; }
        internal bool InTurret { get; set; }
        internal MyEntity ControlledEntity { get; set; }

        internal readonly MyStringId LaserMaterial = MyStringId.GetOrCompute("WeaponLaser");
        internal readonly MyStringId WarpMaterial = MyStringId.GetOrCompute("WarpBubble");

        internal readonly Guid LogictateGuid = new Guid("85BED4F5-4FB9-4230-FEED-BE79D9811500");
        internal readonly Guid LogicettingsGuid = new Guid("85BED4F5-4FB9-4230-FEED-BE79D9811501");

        internal List<WeaponHit> WeaponHits = new List<WeaponHit>();
        internal readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();
        internal readonly ConcurrentQueue<DrawProjectile> DrawBeams = new ConcurrentQueue<DrawProjectile>();
        private readonly ConcurrentQueue<WeaponComponent> _compsToStart = new ConcurrentQueue<WeaponComponent>();
        internal readonly MyConcurrentDictionary<MyCubeGrid, GridTargetingAi> GridTargetingAIs = new MyConcurrentDictionary<MyCubeGrid, GridTargetingAi>();
        internal readonly Dictionary<MyStringHash, WeaponStructure> WeaponPlatforms = new Dictionary<MyStringHash, WeaponStructure>(MyStringHash.Comparer);
        internal readonly Dictionary<string, MyStringHash> SubTypeIdHashMap = new Dictionary<string, MyStringHash>();
        internal readonly Dictionary<int, string> ModelIdToName = new Dictionary<int, string>();
        internal readonly Projectiles.Projectiles Projectiles = new Projectiles.Projectiles();

        internal ShieldApi SApi = new ShieldApi();
        internal FutureEvents FutureEvents { get; set; } = new FutureEvents();
        internal MatrixD EndMatrix = MatrixD.CreateTranslation(Vector3D.MaxValue);
        internal readonly HashSet<string> WepActions = new HashSet<string>()
        {
            "WC-L_PowerLevel",
            "WC-L_Guidance"
        };

    }
}