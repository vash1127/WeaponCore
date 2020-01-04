using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
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
        internal volatile bool IsSorterTurret;

        internal GridAi Ai;
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
        internal bool Gunner;
        internal bool Starting;
        internal int Shooting;
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
        
        internal readonly MyCubeBlock MyCube;
        internal readonly IMySlimBlock Slim;
        internal MyWeaponPlatform Platform;
        internal readonly MyInventory BlockInventory;
        internal readonly IMyLargeMissileTurret MissileBase;
        internal readonly IMyConveyorSorter SorterBase;
        internal Weapon TrackingWeapon;
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

        internal CompSettings Set;
        internal CompState State;
        internal Session Session;
        internal MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;

        public WeaponComponent(Session session, GridAi ai, MyCubeBlock myCube)
        {
            Ai = ai;
            Session = session;
            MyCube = myCube;
            Slim = myCube.SlimBlock;

            MaxIntegrity = Slim.MaxIntegrity;

            var cube = MyCube as IMyLargeMissileTurret;
            if (cube != null)
            {
                MissileBase = cube;
                IsSorterTurret = false;
                MissileBase.EnableIdleRotation = false;
            }
            else if (MyCube is IMyConveyorSorter)
            {
                SorterBase = (IMyConveyorSorter)MyCube;
                IsSorterTurret = true;
            }
            
            BlockInventory = (MyInventory)MyCube.GetInventoryBase();
            SinkPower = IdlePower;
            Platform = session.PlatFormPool.Get();
            Platform.Setup(this);

            MyCube.OnClose += Session.CloseComps;
        }        
    }
}
