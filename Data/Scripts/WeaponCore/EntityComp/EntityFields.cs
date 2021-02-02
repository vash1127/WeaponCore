using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class CoreComponent
    {
        internal bool InventoryInited;
        internal BlockType BaseType;

        internal readonly MyEntity CoreEntity;

        internal readonly IMySlimBlock Slim;
        internal readonly IMyTerminalBlock TerminalBlock;
        internal readonly IMyFunctionalBlock FunctionalBlock;
        internal readonly IMyLargeTurretBase TurretBase;
        internal readonly IMyAutomaticRifleGun Rifle;
        internal readonly IMyHandheldGunObject<MyGunBase> GunBase;
        internal readonly MyCubeBlock Cube;

        internal readonly List<PartAnimation> AllAnimations = new List<PartAnimation>();
        internal readonly List<int> AmmoSelectionWeaponIds = new List<int>();
        internal readonly List<Action<long, int, ulong, long, Vector3D, bool>>[] Monitors;
        internal readonly Session Session;
        internal readonly MyInventory CoreInventory;
        internal readonly bool IsBlock;
        internal readonly CompData Data;
        internal readonly uint[] MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];
        internal readonly MyStringHash SubTypeId;
        internal readonly string SubtypeName;
        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;

        internal InputStateData InputState;
        internal Ai Ai;
        internal Part TrackingPart;
        internal CorePlatform Platform;
        internal MyEntity TopEntity;
        internal uint LastRayCastTick;
        internal uint IsWorkingChangedTick;

        internal int WeaponsTracking;
        internal double MaxTargetDistance = double.MinValue;
        internal double MaxTargetDistanceSqr = double.MinValue;
        internal double MinTargetDistance = double.MaxValue;
        internal double MinTargetDistanceSqr = double.MaxValue;
        internal long PreviousOwner = long.MaxValue;

        internal float EffectiveDps;
        internal float PeakDps;
        internal float ShotsPerSec;
        internal float BaseDps;
        internal float AreaDps;
        internal float DetDps;
        internal float CurrentDps;
        internal float CurrentHeat;
        internal float MaxHeat;
        internal float HeatPerSecond;
        internal float HeatSinkRate;
        internal float SinkPower;
        internal float MaxRequiredPower;
        internal float IdlePower = 0.001f;
        internal float MaxIntegrity;
        internal float CurrentCharge;
        internal float CurrentInventoryVolume;
        internal bool TargetNonThreats;
        internal bool IsAsleep;
        internal bool IsFunctional;
        internal bool IsWorking;
        internal bool IsDisabled;
        internal bool HasEnergyWeapon;
        internal bool HasGuidanceToggle;
        internal bool HasDamageSlider;
        internal bool HasRofSlider;
        internal bool CanOverload;
        internal bool HasTurret;
        internal bool HasChargeWeapon;
        internal bool WasControlled;
        internal bool UpdatedState;
        internal bool UserControlled;
        internal bool Debug;
        internal bool UnlimitedPower;
        internal bool Registered;
        internal bool ResettingSubparts;
        internal bool HasArmor;
        internal bool UiEnabled;
        internal bool ShootSubmerged;
        internal bool HasTracking;
        internal bool UnexpectedMag;
        internal string CustomIcon;

        internal MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;

        internal Start Status;

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
            Sorter,
            Armor,
            Upgrade,
            Rifle,
        }

        public enum TriggerActions
        {
            TriggerOff,
            TriggerOn,
            TriggerOnce,
            TriggerClick,
        }

        internal bool FakeIsWorking => !IsBlock || IsWorking;

        public CoreComponent(Session session, MyEntity coreEntity)
        {
            Session = session;
            CoreEntity = coreEntity;

            var turret = CoreEntity as IMyLargeTurretBase;
            if (turret != null)
            {
                TurretBase = turret;
                TurretBase.EnableIdleRotation = false;
                BaseType = BlockType.Turret;
            }
            else if (CoreEntity is IMyConveyorSorter)
                BaseType = BlockType.Sorter;
            else if (CoreEntity is IMyAutomaticRifleGun)
                BaseType = BlockType.Rifle;
            else
                BaseType = BlockType.Fixed;

            IsBlock = BaseType != BlockType.Rifle;

            if (IsBlock) {

                Cube = (MyCubeBlock)CoreEntity;
                Slim = Cube.SlimBlock;
                MaxIntegrity = Slim.MaxIntegrity;
                TopEntity = Cube.CubeGrid;
                SubtypeName = Cube.BlockDefinition.Id.SubtypeName;
                SubTypeId = Cube.BlockDefinition.Id.SubtypeId;

                TerminalBlock = coreEntity as IMyTerminalBlock;
                FunctionalBlock = coreEntity as IMyFunctionalBlock;
            }
            else {

                Rifle = (IMyAutomaticRifleGun)CoreEntity;
                GunBase = (IMyHandheldGunObject<MyGunBase>)CoreEntity;
                TopEntity = Rifle.Owner;
                SubtypeName = Rifle.DefinitionId.SubtypeName;
                SubTypeId = Rifle.DefinitionId.SubtypeId;
                MaxIntegrity = 1;
            }

            CoreInventory = (MyInventory)CoreEntity.GetInventoryBase();
            SinkPower = IdlePower;
            Platform = session.PlatFormPool.Get();
            Platform.Setup(this);

            Monitors = new List<Action<long, int, ulong, long, Vector3D, bool>>[Platform.Weapons.Length];
            for (int i = 0; i < Monitors.Length; i++)
                Monitors[i] = new List<Action<long, int, ulong, long, Vector3D, bool>>();

            Data = new CompData(this);

            CoreEntity.OnClose += Session.CloseComps;

        }        
    }
}
