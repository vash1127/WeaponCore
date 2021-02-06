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
        internal readonly List<PartAnimation> AllAnimations = new List<PartAnimation>();
        internal readonly List<int> AmmoSelectionPartIds = new List<int>();
        internal readonly uint[] MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];
        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;

        internal List<Action<long, int, ulong, long, Vector3D, bool>>[] Monitors;

        internal bool InventoryInited;
        internal CompTypeSpecific TypeSpecific;
        internal CompType Type;
        internal MyEntity CoreEntity;
        internal IMySlimBlock Slim;
        internal IMyTerminalBlock TerminalBlock;
        internal IMyFunctionalBlock FunctionalBlock;
        internal IMyLargeTurretBase VanillaTurretBase;
        internal IMyAutomaticRifleGun Rifle;
        internal IMyHandheldGunObject<MyGunBase> GunBase;
        internal MyCubeBlock Cube;
        internal Session Session;
        internal bool IsBlock;

        internal MyStringHash SubTypeId;
        internal string SubtypeName;
        internal bool LazyUpdate;
        internal MyInventory CoreInventory;
        internal CompData Data;

        internal InputStateData InputState;
        internal Ai Ai;
        internal Weapon TrackingWeapon;
        internal CorePlatform Platform;
        internal MyEntity TopEntity;
        internal uint LastRayCastTick;
        internal uint IsWorkingChangedTick;
        internal uint NextLazyUpdateStart;
        internal int PartTracking;
        internal double MaxDetectDistance = double.MinValue;
        internal double MaxDetectDistanceSqr = double.MinValue;
        internal double MinDetectDistance = double.MaxValue;
        internal double MinDetectDistanceSqr = double.MaxValue;

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
        internal bool DetectOtherSignals;
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
        internal bool UiEnabled;
        internal bool ShootSubmerged;
        internal bool HasTracking;
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

        internal enum CompTypeSpecific
        {
            VanillaTurret,
            VanillaFixed,
            SorterWeapon,
            Support,
            Upgrade,
            Phantom,
            Rifle,
        }

        internal enum CompType
        {
            Phantom,
            Weapon,
            Support,
            Upgrade
        }

        public enum TriggerActions
        {
            TriggerOff,
            TriggerOn,
            TriggerOnce,
            TriggerClick,
        }

        internal bool FakeIsWorking => !IsBlock || IsWorking;

        public void Init(Session session, MyEntity coreEntity)
        {
            Session = session;
            CoreEntity = coreEntity;
            IsBlock = coreEntity is MyCubeBlock;

            if (IsBlock) {

                Cube = (MyCubeBlock)CoreEntity;
                Slim = Cube.SlimBlock;
                MaxIntegrity = Slim.MaxIntegrity;
                TopEntity = Cube.CubeGrid;
                SubtypeName = Cube.BlockDefinition.Id.SubtypeName;
                SubTypeId = Cube.BlockDefinition.Id.SubtypeId;

                TerminalBlock = coreEntity as IMyTerminalBlock;
                FunctionalBlock = coreEntity as IMyFunctionalBlock;

                var turret = CoreEntity as IMyLargeTurretBase;
                if (turret != null)
                {

                    VanillaTurretBase = turret;
                    VanillaTurretBase.EnableIdleRotation = false;
                    TypeSpecific = CompTypeSpecific.VanillaTurret;
                    Type = CompType.Weapon;
                }
                else if (CoreEntity is IMyConveyorSorter)
                {
                    if (Session.WeaponCoreArmorEnhancerDefs.Contains(Cube.BlockDefinition.Id))
                    {
                        TypeSpecific = CompTypeSpecific.Support;
                        Type = CompType.Support;
                        LazyUpdate = true;
                    }
                    else if (Session.WeaponCoreUpgradeBlockDefs.Contains(Cube.BlockDefinition.Id))
                    {
                        TypeSpecific = CompTypeSpecific.Upgrade;
                        Type = CompType.Upgrade;
                        LazyUpdate = true;
                    }
                    else {

                        TypeSpecific = CompTypeSpecific.SorterWeapon;
                        Type = CompType.Weapon;
                    }
                }
                else {
                    TypeSpecific = CompTypeSpecific.VanillaFixed;
                    Type = CompType.Weapon;
                }

            }
            else if (CoreEntity is IMyAutomaticRifleGun) {
                
                Rifle = (IMyAutomaticRifleGun)CoreEntity;
                GunBase = (IMyHandheldGunObject<MyGunBase>)CoreEntity;
                TopEntity = Rifle.Owner;
                SubtypeName = Rifle.DefinitionId.SubtypeName;
                SubTypeId = Rifle.DefinitionId.SubtypeId;
                MaxIntegrity = 1;
                TypeSpecific = CompTypeSpecific.Rifle;
                Type = CompType.Weapon;
            }
            else {
                TypeSpecific = CompTypeSpecific.Phantom;
                Type = CompType.Phantom;
            }

            CoreInventory = (MyInventory)CoreEntity.GetInventoryBase();
            SinkPower = IdlePower;
            Platform = session.PlatFormPool.Get();
            Platform.Setup(this);

            Monitors = new List<Action<long, int, ulong, long, Vector3D, bool>>[Platform.Structure.PartHashes.Length];
            for (int i = 0; i < Monitors.Length; i++)
                Monitors[i] = new List<Action<long, int, ulong, long, Vector3D, bool>>();

            Data = new CompData(this);

            CoreEntity.OnClose += Session.CloseComps;
        }        
    }
}
