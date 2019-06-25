using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        internal readonly MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;

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

        private DSUtils Dsutil1 { get; set; } = new DSUtils();
        internal GridTargetingAi MyAi { get; set; }

        internal MyResourceSinkInfo ResourceInfo;
        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
        internal bool InThisTerminal => Session.Instance.LastTerminalId == Turret.EntityId;

        internal MyFixedPoint MaxInventoryVolume;
        internal MyFixedPoint MaxInventoryMass;
        internal uint LastAmmoUnSuspendTick;
        internal int PullingAmmoCnt;
        internal float MaxAmmoVolume;
        internal float MaxAmmoMass;
        internal float SinkCurrentPower;
        internal float SinkPower = 0.01f;
        internal bool TurretTargetLock;
        internal bool Gunner;
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

        internal MyCubeBlock MyCube;
        internal MyCubeGrid MyGrid;
        internal MyPhysicsComponentBase Physics;
        internal MyWeaponPlatform Platform;
        internal IMyLargeMissileTurret Turret;
        internal Weapon TrackingWeapon;
        internal MyInventory BlockInventory;
        internal Vector3D MyPivotPos;
        internal Vector3D MyPivotDir;
        internal LineD MyPivotTestLine;
        internal double MyPivotOffset;
        internal IMyGunObject<MyGunBase> Gun;
        internal bool MainInit;
        internal bool SettingsUpdated;
        internal bool ClientUiUpdate;
        internal bool IsFunctional;
        internal bool IsWorking;
        internal bool FullInventory;
        internal bool MultiInventory;
        internal bool PivotLengthSet;
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
            BlockInventory = (MyInventory)MyCube.GetInventoryBase();
            BlockInventory.Constraint.m_useDefaultIcon = false;
            MaxInventoryVolume = BlockInventory.MaxVolume;
            MaxInventoryMass = BlockInventory.MaxMass;
        }
    }
}
