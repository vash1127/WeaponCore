using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        private int _count = -1;
        private bool _allInited;
        private bool _isServer;
        private bool _isDedicated;
        private bool _mpActive;
        private bool _clientNotReady;
        private bool _firstSync;

        internal volatile bool InventoryInited;
        //internal volatile bool IsSorterTurret;
        internal volatile BlockType BaseType;
        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
        internal bool InThisTerminal => Session.LastTerminalId == MyCube.EntityId;
        internal int OnAddedAttempts;

        internal MatrixD CubeMatrix;
        internal uint LastRayCastTick;
        internal uint LastUpdateTick;
        internal uint LastInventoryChangedTick;
        internal uint ChargeUntilTick;
        internal uint DelayTicks;
        internal uint IsWorkingChangedTick;
        internal uint PositionUpdateTick;
        internal uint MatrixUpdateTick;
        internal float MaxInventoryVolume;
        internal float OptimalDps;
        internal float CurrentDps;
        internal float CurrentHeat;
        internal float MaxHeat;
        internal float HeatPerSecond;
        internal float HeatSinkRate;
        internal float SinkPower;
        internal float MaxRequiredPower;
        internal float CurrentCharge;
        internal float IdlePower = 0.001f;
        internal float MaxIntegrity;
        internal bool Overheated;
        internal Control LastGunner;
        internal Control Gunner;
        internal bool Starting;
        internal bool Debug;
        internal bool MouseShoot;
        internal bool UnlimitedPower;
        internal bool Registered;
        internal Start Status;
        internal enum Start
        {
            Started,
            Starting,
            Stopped,
            ReInit,
            WarmingUp,
        }

        internal enum Control
        {
            None,
            Direct,
            Manual,
        }

        internal enum BlockType
        {
            Turret,
            Fixed,
            Sorter
        }

        internal readonly MyCubeBlock MyCube;
        internal readonly IMySlimBlock Slim;
        internal readonly MyStringHash SubtypeHash;

        internal readonly Session Session;
        internal readonly MyInventory BlockInventory;
        internal readonly IMyTerminalBlock TerminalBlock;
        internal readonly IMyFunctionalBlock FunctionalBlock;
        internal readonly IMyLargeTurretBase TurretBase;
        internal readonly CompSettings Set;
        internal readonly CompState State;
        internal GridAi Ai;
        internal Weapon TrackingWeapon;
        internal MyWeaponPlatform Platform;
        internal bool SettingsUpdated;
        internal bool ClientUiUpdate;
        internal bool IsFunctional;
        internal bool IsWorking;
        internal bool HasEnergyWeapon;
        internal bool IgnoreInvChange;

        //ui fields
        internal bool HasGuidanceToggle;
        internal bool HasDamageSlider;
        internal bool HasRofSlider;
        internal bool CanOverload;
        internal bool HasTurret;
        internal bool HasChargeWeapon;


        internal MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;

        public WeaponComponent(Session session, GridAi ai, MyCubeBlock myCube, MyStringHash subtype)
        {
            Ai = ai;
            Session = session;
            MyCube = myCube;
            Slim = myCube.SlimBlock;
            SubtypeHash = subtype;

            MaxIntegrity = Slim.MaxIntegrity;

            if (MyCube is IMyLargeTurretBase)
            {
                TurretBase = myCube as IMyLargeTurretBase;
                TurretBase.EnableIdleRotation = false;
                BaseType = BlockType.Turret;
            }
            else if (MyCube is IMyConveyorSorter)
                BaseType = BlockType.Sorter;
            else
                BaseType = BlockType.Fixed;

            TerminalBlock = myCube as IMyTerminalBlock;
            FunctionalBlock = myCube as IMyFunctionalBlock;
            
            BlockInventory = (MyInventory)MyCube.GetInventoryBase();
            SinkPower = IdlePower;
            Platform = session.PlatFormPool.Get();
            Platform.Setup(this);

            State = new CompState(this);
            Set = new CompSettings(this);

            MyCube.OnClose += Session.CloseComps;
        }        
    }
}
