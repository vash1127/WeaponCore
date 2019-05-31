using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
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

        public MyCubeBlock MyCube;
        public MyCubeGrid MyGrid;
        public MyWeaponPlatform Platform;
        public IMyLargeMissileTurret Turret;

        internal IMyGunObject<MyGunBase> Gun { get; set; }
        internal bool MainInit { get; set; }
        internal bool SettingsUpdated { get; set; }
        internal bool ClientUiUpdate { get; set; }
        internal bool IsFunctional { get; set; }
        internal bool IsWorking { get; set; }
        internal int FireRequest { get; set; }

        internal LogicSettings Set { get; set; }
        internal LogicState State { get; set; }
        internal MyResourceSinkComponent Sink { get; set; }

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
