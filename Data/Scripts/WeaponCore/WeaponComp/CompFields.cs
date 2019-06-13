using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        //private const int SyncCount = 60;
        //private readonly MyDefinitionId _gId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        private uint _tick;

        private int _count = -1;
        //private int _bCount;
        //private int _bTime;


        //private bool _aInit;
        private bool _allInited;
        //private bool _containerInited;
        private bool _clientOn;
        private bool _isServer;
        private bool _isDedicated;
        private bool _mpActive;
        private bool _clientNotReady;
        private bool _firstRun = true;
        private bool _firstLoop = true;
        private bool _readyToSync;
        private bool _firstSync;
        //private bool _bInit;
        //private bool _wasOnline;

        //private MyStringHash _subTypeIdHash;
        private DSUtils Dsutil1 { get; set; } = new DSUtils();
        internal GridTargetingAi MyAi { get; set; }

        internal MyResourceSinkInfo ResourceInfo;
        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
        internal bool InThisTerminal => Session.Instance.LastTerminalId == Turret.EntityId;
        //internal uint ResetEntityTick;
        internal float SinkCurrentPower;
        internal bool TurretTargetLock;
        internal float SinkPower = 0.01f;
        internal bool NotFailed;
        internal bool WarmedUp;
        internal bool Starting;
        internal bool Sync = true;
        internal enum Status
        {
            Online,
            Offline,
            OverHeating,
            WarmingUp,
        }

        public MyCubeBlock MyCube;
        public MyCubeGrid MyGrid;
        public MyPhysicsComponentBase Physics;
        public MyWeaponPlatform Platform;
        public IMyLargeMissileTurret Turret;
        internal Weapon TrackingWeapon;
        internal Vector3D MyPivotPos;
        internal Vector3D MyPivotDir;

        internal IMyGunObject<MyGunBase> Gun;
        internal bool MainInit;
        internal bool SettingsUpdated;
        internal bool ClientUiUpdate;
        internal bool IsFunctional;
        internal bool IsWorking;

        internal LogicSettings Set;
        internal LogicState State;
        internal MyResourceSinkComponent Sink;

        public WeaponComponent(GridTargetingAi ai, MyCubeBlock myCube, IMyLargeMissileTurret turret)
        {
            MyAi = ai;
            MyCube = myCube;
            MyGrid = MyCube.CubeGrid;
            Turret = turret;
            Gun = (IMyGunObject<MyGunBase>)MyCube;
        }
    }
}
