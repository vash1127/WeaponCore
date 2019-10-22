using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        internal readonly MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;
        private int _count = -1;

        private bool _allInited;
        private bool _clientOn;
        private bool _isServer;
        private bool _isDedicated;
        private bool _mpActive;
        private bool _clientNotReady;
        private bool _firstRun = true;
        private bool _firstLoop = true;
        private bool _readyToSync;
        private bool _firstSync;

        internal GridAi Ai { get; set; }
        internal MySoundPair RotationSound;
        internal MyEntity3DSoundEmitter RotationEmitter; 

        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
        internal bool InThisTerminal => Ai.Session.LastTerminalId == MyCube.EntityId;

        internal HashSet<string> GroupNames = new HashSet<string>();
        internal MyFixedPoint MaxInventoryMass;
        internal uint LastRayCastTick;
        internal uint LastUpdateTick;
        internal uint LastInventoryChangedTick;
        internal uint ShootTick = 0;
        internal uint DelayTicks = 0;
        internal uint IsWorkingChangedTick;
        internal uint PositionUpdateTick;
        internal float MaxInventoryVolume;
        internal float OptimalDps;
        internal float CurrentDps;
        internal float CurrentHeat;
        internal float MaxHeat;
        internal float HeatPerSecond;
        internal float HeatSinkRate;
        internal float MaxAmmoVolume;
        internal float MaxAmmoMass;
        internal float SinkPower;
        internal float MaxRequiredPower;
        internal float CurrentSinkPowerRequested;
        internal float CompPowerPerc;
        internal float IdlePower = 0.001f;
        internal bool Overheated;
        internal bool Gunner;
        internal bool NotFailed;
        internal bool WarmedUp;
        internal bool Starting;
        internal bool Sync = true;
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
        internal IMyLargeMissileTurret ControllableTurret;
        internal Sandbox.ModAPI.IMyConveyorSorter AiOnlyTurret;
        internal Weapon TrackingWeapon;
        internal MyInventory BlockInventory;
        internal bool MainInit;
        internal bool SettingsUpdated;
        internal bool ClientUiUpdate;
        internal bool IsFunctional;
        internal bool IsWorking;
        internal bool FullInventory;
        internal bool AiMoving;
        internal bool HasEnergyWeapon;
        internal bool IsAiOnlyTurret;
        internal bool HasInventory;
        internal bool IgnoreInvChange;
        internal LogicSettings Set;
        internal LogicState State;
        internal MyResourceSinkComponent Sink;
        public WeaponComponent(GridAi ai, MyCubeBlock myCube)
        {
            Ai = ai;
            MyCube = myCube;

            if (myCube is IMyLargeMissileTurret)
            {
                ControllableTurret = (IMyLargeMissileTurret) myCube;
                IsAiOnlyTurret = false;
            }

            else if (myCube is Sandbox.ModAPI.IMyConveyorSorter)
            {
                AiOnlyTurret = (Sandbox.ModAPI.IMyConveyorSorter) myCube;
                IsAiOnlyTurret = true;
            }

            //TODO add to config
            BlockInventory = myCube.GetInventory(0);

            BlockInventory.SetFlags(MyInventoryFlags.CanReceive);
            BlockInventory.ResetVolume();
            BlockInventory.Refresh();
            MaxInventoryMass = BlockInventory.MaxMass;

            SinkPower = IdlePower;
            
        }        
    }
}
