using System.Collections.Generic;
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
        internal bool Overheated;
        internal bool Gunner;
        internal bool Starting;
        internal int Shooting;
        internal bool Charging;
        internal bool ReturnHome;
        internal bool Debug;
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
        internal MyWeaponPlatform Platform;
        internal IMyLargeMissileTurret MissileBase;
        internal Sandbox.ModAPI.IMyConveyorSorter SorterBase;
        internal Weapon TrackingWeapon;
        internal MyInventory BlockInventory;
        internal bool MainInit;
        internal bool SettingsUpdated;
        internal bool ClientUiUpdate;
        internal bool IsFunctional;
        internal bool IsWorking;
        internal bool AiMoving;
        internal bool HasEnergyWeapon;
        internal bool IsSorterTurret;
        internal bool HasInventory;
        internal bool IgnoreInvChange;
        internal LogicSettings Set;
        internal LogicState State;
        internal MyResourceSinkComponent Sink;
        internal MyResourceSinkInfo SinkInfo;
        internal MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;

        public WeaponComponent(GridAi ai, MyCubeBlock myCube)
        {
            Ai = ai;
            MyCube = myCube;
            BlockInventory = (MyInventory)MyCube.GetInventoryBase();

            if (myCube is IMyLargeMissileTurret)
            {
                MissileBase = (IMyLargeMissileTurret) myCube;
                IsSorterTurret = false;
            }

            else if (myCube is IMyConveyorSorter)
            {
                SorterBase = (Sandbox.ModAPI.IMyConveyorSorter) myCube;
                IsSorterTurret = true;
                BlockInventory.Constraint = new MyInventoryConstraint("ammo");
            }

            //TODO add to config
            
            BlockInventory.Constraint.m_useDefaultIcon = false;
            BlockInventory.ResetVolume();
            BlockInventory.Refresh();

            SinkPower = IdlePower;
            
        }        
    }
}
