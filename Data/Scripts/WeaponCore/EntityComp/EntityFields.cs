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
        internal readonly List<int> ConsumableSelectionPartIds = new List<int>();
        internal readonly uint[] MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];
        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;

        internal List<Action<long, int, ulong, long, Vector3D, bool>>[] Monitors;

        internal bool InventoryInited;
        internal CompType Type;
        internal CompTypeSpecific TypeSpecific;
        internal MyEntity CoreEntity;
        internal IMySlimBlock Slim;
        internal IMyTerminalBlock TerminalBlock;
        internal IMyFunctionalBlock FunctionalBlock;

        internal MyCubeBlock Cube;
        internal Session Session;
        internal bool IsBlock;
        internal MyDefinitionId Id;
        internal MyStringHash SubTypeId;
        internal string SubtypeName;
        internal bool LazyUpdate;
        internal MyInventory CoreInventory;
        internal CompData Data;

        internal InputStateData InputState;
        internal Ai Ai;
        internal CorePlatform Platform;
        internal MyEntity TopEntity;
        internal uint IsWorkingChangedTick;
        internal uint NextLazyUpdateStart;
        internal int PartTracking;
        internal double MaxDetectDistance = double.MinValue;
        internal double MaxDetectDistanceSqr = double.MinValue;
        internal double MinDetectDistance = double.MaxValue;
        internal double MinDetectDistanceSqr = double.MaxValue;

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

        internal bool HasStrengthSlider;
        internal bool CanOverload;
        internal bool HasTurret;
        internal bool WasControlled;
        internal bool UpdatedState;
        internal bool UserControlled;
        internal bool Debug;
        internal bool UnlimitedPower;
        internal bool Registered;
        internal bool ResettingSubparts;
        internal bool UiEnabled;

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

        public void Init(Session session, MyEntity coreEntity, bool isBlock, MyEntity topEntity, MyDefinitionId id)
        {
            Session = session;
            CoreEntity = coreEntity;
            IsBlock = isBlock;
            Id = id;
            SubtypeName = id.SubtypeName;
            SubTypeId = id.SubtypeId;
            TopEntity = topEntity;

            if (IsBlock) {

                Cube = (MyCubeBlock)CoreEntity;
                Slim = Cube.SlimBlock;
                MaxIntegrity = Slim.MaxIntegrity;
                TerminalBlock = coreEntity as IMyTerminalBlock;
                FunctionalBlock = coreEntity as IMyFunctionalBlock;

                var turret = CoreEntity as IMyLargeTurretBase;
                if (turret != null)
                {
                    TypeSpecific = CompTypeSpecific.VanillaTurret;
                    Type = CompType.Weapon;
                }
                else if (CoreEntity is IMyConveyorSorter)
                {
                    if (Session.WeaponCoreArmorEnhancerDefs.Contains(Cube.BlockDefinition.Id))
                    {
                        TypeSpecific = CompTypeSpecific.Support;
                        Type = CompType.Support;
                    }
                    else if (Session.WeaponCoreUpgradeBlockDefs.Contains(Cube.BlockDefinition.Id))
                    {
                        TypeSpecific = CompTypeSpecific.Upgrade;
                        Type = CompType.Upgrade;
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
                
                MaxIntegrity = 1;
                TypeSpecific = CompTypeSpecific.Rifle;
                Type = CompType.Weapon;
            }
            else {
                TypeSpecific = CompTypeSpecific.Phantom;
                Type = CompType.Phantom;
            }

            LazyUpdate = Type == CompType.Support || Type == CompType.Upgrade;
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
