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

        internal GridAi Ai { get; set; }
        internal MySoundPair RotationSound;
        internal MyEntity3DSoundEmitter RotationEmitter; 

        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
        internal bool InThisTerminal => Ai.Session.LastTerminalId == MyCube.EntityId;

        internal MatrixD CubeMatrix;
        internal uint LastRayCastTick;
        internal uint LastUpdateTick;
        internal uint LastInventoryChangedTick;
        internal uint ShootTick = 0;
        internal uint DelayTicks = 0;
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
        internal float CurrentSinkPowerRequested;
        internal float CompPowerPerc;
        internal float IdlePower = 0.001f;
        internal float MaxIntegrity;
        internal bool Overheated;
        internal bool Gunner;
        internal bool Starting;
        internal int Shooting;
        internal bool Charging;
        internal bool ReturnHome;
        internal bool Debug;
        internal bool MouseShoot;
        internal bool UnlimitedPower;
        internal Start Status;
        internal enum Start
        {
            Started,
            Starting,
            Stopped,
            ReInit,
            WarmingUp,
        }
        
        internal MyCubeBlock MyCube;
        internal IMySlimBlock Slim;
        internal readonly MyWeaponPlatform Platform = new MyWeaponPlatform();
        internal IMyLargeMissileTurret MissileBase;
        internal IMyConveyorSorter SorterBase;
        internal Weapon TrackingWeapon;
        internal MyInventory BlockInventory;
        internal bool SettingsUpdated;
        internal bool ClientUiUpdate;
        internal bool IsFunctional;
        internal bool IsWorking;
        internal bool AiMoving;
        internal bool HasEnergyWeapon;
        internal bool IgnoreInvChange;
        internal CompSettings Set;
        internal CompState State;
        internal MyResourceSinkInfo SinkInfo;
        internal MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;

        public WeaponComponent(GridAi ai, MyCubeBlock myCube)
        {
            Ai = ai;
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

            SinkPower = IdlePower;
        }        
    }
}
