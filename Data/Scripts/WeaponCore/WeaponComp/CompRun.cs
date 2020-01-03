using System;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using WeaponCore.Platform;
using static WeaponCore.Session;
namespace WeaponCore.Support
{
    public partial class WeaponComponent : MyEntityComponentBase
    {
        public override void OnAddedToContainer()
        {
            try
            {
                base.OnAddedToContainer();
                if (Container.Entity.InScene)
                {
                    if (Platform.State == MyWeaponPlatform.PlatformState.Fresh)
                        PlatformInit();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToContainer: {ex}"); }
        }

        public override void OnAddedToScene()
        {
            try
            {
                base.OnAddedToScene();

                if (Platform.State == MyWeaponPlatform.PlatformState.Inited || Platform.State == MyWeaponPlatform.PlatformState.Ready)
                    Ai.Session.CompChanges.Enqueue(new CompChange {Ai = Ai, Comp = this, Change = CompChange.ChangeType.Reinit});
                else
                    Ai.Session.CompChanges.Enqueue(new CompChange { Ai = Ai, Comp = this, Change = CompChange.ChangeType.PlatformInit });
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }
        
        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
            if (!Container.Entity.InScene)
                Ai.Session.FutureEvents.Schedule(RemoveSinkDelegate, null, 100);
        }

        internal void PlatformInit()
        {
            switch (Platform.Init(this))
            {
                case MyWeaponPlatform.PlatformState.Invalid:
                    Log.Line($"Platform PreInit is in an invalid state");
                    break;
                case MyWeaponPlatform.PlatformState.Valid:
                    Log.Line($"Something went wrong with Platform PreInit");
                    break;
                case MyWeaponPlatform.PlatformState.Delay:
                    Ai.Session.CompsDelayed.Enqueue(this);
                    break;
                case MyWeaponPlatform.PlatformState.Inited:
                    Init();
                    break;
            }
        }

        internal void Init()
        {
            using (MyCube.Pin())
            {
                if (!MyCube.MarkedForClose && Entity != null)
                {
                    _isServer = Ai.Session.IsServer;
                    _isDedicated = Ai.Session.DedicatedServer;
                    _mpActive = Ai.Session.MpActive;

                    Entity.NeedsUpdate = ~MyEntityUpdateEnum.EACH_10TH_FRAME;
                    Ai.FirstRun = true;

                    StorageSetup();

                    InventoryInit();
                    PowerInit();
                    OnAddedToSceneTasks();

                    Platform.State = MyWeaponPlatform.PlatformState.Ready;
                }
                else Log.Line($"Comp Init() failed");
            }
        }

        internal void ReInit()
        {
            using (MyCube.Pin())
            {
                if (!MyCube.MarkedForClose && Entity != null)
                {
                    GridAi ai;
                    if (!Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out ai))
                    {
                        var newAi = Ai.Session.GridAiPool.Get();
                        newAi.Init(MyCube.CubeGrid, Ai.Session);
                        Ai.Session.GridTargetingAIs.TryAdd(MyCube.CubeGrid, newAi);
                        Ai = newAi;
                    }
                    else Ai = ai;

                    if (Ai != null && Ai.WeaponBase.TryAdd(MyCube, this))
                    {
                        Ai.FirstRun = true;

                        AddCompList();

                        var blockDef = MyCube.BlockDefinition.Id.SubtypeId;
                        if (!Ai.WeaponCounter.ContainsKey(blockDef))
                            Ai.WeaponCounter.TryAdd(blockDef, Ai.Session.WeaponCountPool.Get());

                        Ai.WeaponCounter[blockDef].Current++;

                        OnAddedToSceneTasks();
                    }
                    else Log.Line($"Comp ReInit() failed stage2!");
                }
                else
                {
                    Log.Line($"Comp ReInit() failed stage1! - marked:{MyCube.MarkedForClose} - Entity:{Entity != null} - hasAi:{Ai.Session.GridTargetingAIs.ContainsKey(MyCube.CubeGrid)} - hasMe:{Ai.WeaponBase.ContainsKey(MyCube)}");
                }
            }
        }

        internal void OnAddedToSceneTasks()
        {
            try
            {
                RegisterEvents();

                if (Platform.State == MyWeaponPlatform.PlatformState.Inited)
                    Platform.ResetParts(this);

                Entity.NeedsWorldMatrix = true;

                Ai.UpdatePowerSources = true;
                if (!Ai.GridInit)
                {
                    Ai.GridInit = true;
                    Ai.InitFakeShipController();
                    Ai.ScanBlockGroups = true;
                    var fatList = Ai.Session.GridToFatMap[MyCube.CubeGrid].MyCubeBocks;
                    for (int i = 0; i < fatList.Count; i++)
                    {
                        var cubeBlock = fatList[i];
                        if (cubeBlock is MyBatteryBlock || cubeBlock is IMyCargoContainer || cubeBlock is IMyAssembler || cubeBlock is IMyShipConnector)
                            Ai.FatBlockAdded(cubeBlock);
                    }
                }

                MaxRequiredPower = 0;
                HeatPerSecond = 0;
                OptimalDps = 0;
                MaxHeat = 0;

                //range slider fix - removed from weaponFields.cs
                var maxTrajectory = 0d;
                var ob = MyCube.BlockDefinition as MyLargeTurretBaseDefinition;
                for (int i = 0; i < Platform.Weapons.Length; i++)
                {
                    var weapon = Platform.Weapons[i];

                    weapon.InitTracking();
                    
                    double weaponMaxRange;
                    DpsAndHeatInit(weapon, ob, out weaponMaxRange);
                    maxTrajectory += weaponMaxRange;

                    weapon.UpdateBarrelRotation();
                }

                if (maxTrajectory + Ai.GridRadius > Ai.MaxTargetingRange)
                {
                    Ai.MaxTargetingRange = maxTrajectory + Ai.GridRadius;
                    Ai.MaxTargetingRangeSqr = Ai.MaxTargetingRange * Ai.MaxTargetingRange;
                }
                Ai.OptimalDps += OptimalDps;

                if (IsSorterTurret)
                {
                    if (!SorterBase.Enabled)
                        for (int i = 0; i < Platform.Weapons.Length; i++)
                            Platform.Weapons[i].EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);
                }
                else
                {
                    if (!MissileBase.Enabled)
                        for (int i = 0; i < Platform.Weapons.Length; i++)
                            Platform.Weapons[i].EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);
                }

                Status = !IsWorking ? Start.Starting : Start.ReInit;
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToSceneTasks: {ex} AiNull:{Ai == null} - SessionNull:{Ai?.Session == null} EntNull{Entity == null} MyCubeNull:{MyCube?.CubeGrid == null}"); }
        }

        internal void OnRemovedFromSceneQueue()
        {
            RemoveComp();
            RegisterEvents(false);
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                base.OnRemovedFromScene();
                Ai.Session.CompChanges.Enqueue(new CompChange { Ai = Ai, Comp = this, Change = CompChange.ChangeType.OnRemovedFromSceneQueue });
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (_isServer && Platform.State == MyWeaponPlatform.PlatformState.Ready)
            {
                Set.Value.Inventory = BlockInventory.GetObjectBuilder();
                if (IsSorterTurret)
                {
                    if (SorterBase?.Storage != null)
                    {
                        State.SaveState();
                        Set.SaveSettings();
                    }
                }
                else
                {
                    if (MissileBase?.Storage != null)
                    {
                        State.SaveState();
                        Set.SaveSettings();
                    }
                }
            }
            return false;
        }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}
