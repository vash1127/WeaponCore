using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.Utils;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Logic
    {
        private const int SyncCount = 60;

        private readonly MyDefinitionId _gId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private uint _lastTargetTick;
        private uint _lastShootTick;
        private uint _tick;

        private int _count = -1;
        private int _bCount;
        private int _bTime;
        private long _currentShootTime;
        private long _lastShootTime;

        private float _step = 0.01f;
        private double _azimuth;
        private double _elevation;

        private bool _trackingUpdate;
        private bool _aInit;
        private bool _allInited;
        private bool _containerInited;
        private bool _clientOn;
        private bool _isServer;
        private bool _isDedicated;
        private bool _mpActive;
        private bool _clientNotReady;
        private bool _firstRun = true;
        private bool _firstLoop = true;
        private bool _readyToSync;
        private bool _firstSync;
        private bool _bInit;
        private bool _wasOnline;
        //private List<SerializableDefinitionId> _chargeDefinitionIds;
        //private List<MyObjectBuilder_AmmoMagazine> _chargeObjectBuilders;

        private MyStringHash _subTypeIdHash;
        private DSUtils Dsutil1 { get; set; } = new DSUtils();

        internal MyResourceSinkInfo ResourceInfo;
        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
        internal bool InThisTerminal => Session.Instance.LastTerminalId == Turret.EntityId;
        internal uint ResetEntityTick;
        internal float SinkCurrentPower { get; set; }
        internal float SinkPower { get; set; } = 0.01f;
        internal bool NotFailed { get; set; }
        internal bool WarmedUp { get; set; }
        internal bool Starting { get; set; }
        internal bool Sync { get; set; } = true;
        internal enum Status
        {
            Online,
            Offline,
            OverHeating,
            WarmingUp,
        }

        internal bool IsAfterInited
        {
            get { return _aInit; }
            set
            {
                if (_aInit != value)
                {
                    _aInit = value;
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                }
            }
        }

        internal bool StorageInit { get; set; }
        internal bool ShotsFired { get; set; }
        internal bool MainInit { get; set; }
        internal bool SettingsUpdated { get; set; }
        internal bool ClientUiUpdate { get; set; }
        internal bool IsFunctional { get; set; }
        internal bool IsWorking { get; set; }
        internal LogicSettings Set { get; set; }
        internal LogicState State { get; set; }
        internal MyResourceSinkComponent Sink { get; set; }
        internal MyCubeGrid MyGrid { get; set; }
        internal MyCubeBlock MyCube { get; set; }
        internal IMyLargeTurretBase Turret { get; set; }
        internal IMyGunObject<MyGunBase> Gun { get; set; }
        internal MyWeaponPlatform Platform;
        internal MyGridTargeting Targeting { get; set; }
    }
}
