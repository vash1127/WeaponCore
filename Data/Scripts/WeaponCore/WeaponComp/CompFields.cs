using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
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

        private HashSet<string> UIControls = new HashSet<string>(); 
        internal GridAi Ai { get; set; }
        internal MySoundPair RotationSound;
        internal MyEntity3DSoundEmitter RotationEmitter; 

        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
        internal bool InThisTerminal => Session.Instance.LastTerminalId == MyCube.EntityId;

        internal MyFixedPoint MaxInventoryMass;
        internal uint LastRayCastTick;
        internal uint LastUpdateTick;
        internal uint lastInventoryChangedTick;
        internal uint ShootTick = 0;
        internal uint DelayTicks = 0;
        internal uint IsWorkingChangedTick;
        internal uint PositionUpdateTick;
        internal float MaxInventoryVolume;
        internal float OptimalDPS;
        internal float CurrentDPS;
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
        internal float IdlePower;
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
        internal IMyUpgradeModule AIOnlyTurret;
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
        internal bool IsAIOnlyTurret;
        internal bool HasInventory;
        internal bool IgnoreInvChange;
        internal LogicSettings Set;
        internal LogicState State;
        internal MyResourceSinkComponent Sink;
        public WeaponComponent(GridAi ai, MyCubeBlock myCube)
        {
            if (myCube == null)
                Log.Line("Cube null");

            if (ai == null)
                Log.Line("ai null");

            Ai = ai;
            MyCube = myCube;

            if (myCube is IMyLargeMissileTurret)
            {
                ControllableTurret = myCube as IMyLargeMissileTurret;
                IsAIOnlyTurret = false;
            }

            else if (myCube is IMyUpgradeModule)
            {
                AIOnlyTurret = myCube as IMyUpgradeModule;
                IsAIOnlyTurret = true;
            }

            //TODO add to config

            if (IsAIOnlyTurret)
            {
                BlockInventory = new MyInventory(0.384f, Vector3.Zero, MyInventoryFlags.CanReceive | MyInventoryFlags.CanSend);

                if (BlockInventory == null)
                    Log.Line("Inventory null");

                MyCube.Components.Add(BlockInventory);

                //MaxInventoryVolume = BlockInventory.MaxVolume;
                MaxInventoryMass = BlockInventory.MaxMass;
                BlockInventory.Refresh();

                var invOB = BlockInventory.GetObjectBuilder();
                var cubeOB = MyCube.GetObjectBuilderCubeBlock();
                cubeOB.ConstructionInventory = invOB;
                MyCube.Init(cubeOB, myCube.CubeGrid);
                
            }

            BlockInventory = myCube.GetInventory(0);

            //BlockInventory = MyCube.GetInventory();

            MaxInventoryVolume = (float)BlockInventory.MaxVolume;
            MaxInventoryMass = BlockInventory.MaxMass;

            PowerInit();

            //IdlePower = Turret.ResourceSink.RequiredInputByType(GId);
            SinkPower = IdlePower;
            
        }        
    }
}
