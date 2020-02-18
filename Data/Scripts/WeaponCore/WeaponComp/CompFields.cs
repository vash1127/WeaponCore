using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Entity;
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
        internal volatile BlockType BaseType;

        internal readonly MyCubeBlock MyCube;
        internal readonly IMySlimBlock Slim;
        internal readonly MyStringHash SubtypeHash;
        internal readonly List<MyEntity> SubpartStatesQuickList = new List<MyEntity>();
        internal readonly Dictionary<MyEntity, MatrixD> SubpartStates = new Dictionary<MyEntity, MatrixD>();
        internal readonly List<PartAnimation> AllAnimations = new List<PartAnimation>();
        internal readonly Dictionary<string, int> SubpartNameToIndex = new Dictionary<string, int>();
        internal readonly Dictionary<int, string> SubpartIndexToName = new Dictionary<int, string>();

        internal readonly Session Session;
        internal readonly MyInventory BlockInventory;
        internal readonly IMyTerminalBlock TerminalBlock;
        internal readonly IMyFunctionalBlock FunctionalBlock;
        internal readonly IMyLargeTurretBase TurretBase;
        internal readonly CompSettings Set;
        internal readonly CompState State;

        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
        internal bool InThisTerminal => Session.LastTerminalId == MyCube.EntityId;

        internal IMyTerminalAction shootAction;
        internal IMyTerminalAction ClickShootAction;

        internal MatrixD CubeMatrix;
        internal uint LastRayCastTick;
        internal uint LastInventoryChangedTick;
        internal uint IsWorkingChangedTick;
        internal uint MatrixUpdateTick;
        internal int Seed;
        internal float MaxInventoryVolume;
        internal float OptimalDps;
        internal float CurrentDps;
        internal float CurrentHeat;
        internal float MaxHeat;
        internal float HeatPerSecond;
        internal float HeatSinkRate;
        internal float SinkPower;
        internal float MaxRequiredPower;
        //internal float CurrentCharge;
        internal float IdlePower = 0.001f;
        internal float MaxIntegrity;
        //internal double Azimuth;
        //internal double Elevation;

        internal GridAi Ai;
        internal Weapon TrackingWeapon;
        internal MyWeaponPlatform Platform;
        internal TransferTarget[] TargetsToUpdate = new TransferTarget[1];
        //internal TransferTargets[] TargetsToUpdate = new TransferTargets[1];
        internal bool SettingsUpdated;
        internal bool ClientUiUpdate;
        internal bool IsFunctional;
        internal bool IsWorking;
        internal bool HasEnergyWeapon;
        internal bool IgnoreInvChange;
        internal bool HasGuidanceToggle;
        internal bool HasDamageSlider;
        internal bool HasRofSlider;
        internal bool CanOverload;
        internal bool HasTurret;
        internal bool HasChargeWeapon;
        //internal bool TargetPainter;
        //internal bool ManualControl;
        internal bool WasControlled;
        internal bool TrackReticle;
        internal bool UserControlled;
        internal bool Overheated;
        internal bool Debug;
        internal bool UnlimitedPower;
        internal bool Registered;
        //internal bool ShootOn;
        //internal bool ClickShoot;
        internal bool ResettingSubparts;

        internal MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;

        internal Start Status;
        internal TerminalControl TerminalControlled = TerminalControl.None;

        internal enum TerminalControl
        {
            ToolBarControl,
            ApiControl,
            CameraControl,
            None,
        }

        internal enum Start
        {
            Started,
            Starting,
            Stopped,
            ReInit,
        }

        internal enum BlockType
        {
            Turret,
            Fixed,
            Sorter
        }

        public WeaponComponent(Session session, GridAi ai, MyCubeBlock myCube, MyStringHash subtype)
        {
            Ai = ai;
            Session = session;
            MyCube = myCube;
            Seed = MyCube.EntityId.GetHashCode();
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

            Array.Resize(ref TargetsToUpdate, Platform.Weapons.Length);
            for (int i = 0; i < TargetsToUpdate.Length; i++)
                TargetsToUpdate[i] = new TransferTarget();

            State = new CompState(this);
            Set = new CompSettings(this);

            MyCube.OnClose += Session.CloseComps;
        }        
    }
}
