using System;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using WeaponCore.Platform;
using static WeaponCore.Session;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore.Support
{
    public partial class WeaponComponent : MyEntityComponentBase
    {
        public override void OnAddedToContainer()
        {
            try {

                base.OnAddedToContainer();
                if (Container.Entity.InScene) {

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
                    ReInit();
                else
                   PlatformInit();
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }
        
        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
        }

        internal void PlatformInit()
        {
            switch (Platform.Init(this)) {

                case MyWeaponPlatform.PlatformState.Invalid:
                    Log.Line($"Platform PreInit is in an invalid state");
                    break;
                case MyWeaponPlatform.PlatformState.Valid:
                    Log.Line($"Something went wrong with Platform PreInit");
                    break;
                case MyWeaponPlatform.PlatformState.Delay:
                    Session.CompsDelayed.Add(this);
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
                    Entity.NeedsUpdate = ~MyEntityUpdateEnum.EACH_10TH_FRAME;
                    Ai.FirstRun = true;

                    StorageSetup();
                    InventoryInit();
                    PowerInit();
                    Ai.CompChange(true, this);
                    RegisterEvents();

                    if (Platform.State == MyWeaponPlatform.PlatformState.Inited)
                        Platform.ResetParts(this);

                    Entity.NeedsWorldMatrix = true;

                    for (int i = 0; i < Platform.Weapons.Length; i++)
                    {
                        var weapon = Platform.Weapons[i];
                        weapon.UpdatePivotPos();

                        if (Session.IsClient)
                        {
                            var target = WeaponValues.Targets[weapon.WeaponId];
                            if (target.Info != TransferTarget.TargetInfo.Expired)
                                target.SyncTarget(weapon.Target, false);
                            if (!weapon.Target.IsProjectile && !weapon.Target.IsFakeTarget && weapon.Target.Entity == null)
                            {
                                weapon.Target.StateChange(true, Target.States.Invalid);
                                weapon.Target.TargetChanged = false;
                            }
                            else if (weapon.Target.IsProjectile)
                            {
                                TargetType targetType;
                                AcquireProjectile(weapon, out targetType);

                                if (targetType == TargetType.None)
                                {
                                    if (weapon.NewTarget.CurrentState != Target.States.NoTargetsSeen)
                                        weapon.NewTarget.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen);
                                    if (weapon.Target.CurrentState != Target.States.NoTargetsSeen) weapon.Target.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen, !weapon.Comp.TrackReticle);
                                }
                            }

                        }
                    }

                    if (!Ai.GridInit) Session.CompReAdds.Add(new CompReAdd { Ai = Ai, Comp = this });
                    else OnAddedToSceneTasks();

                    Platform.State = MyWeaponPlatform.PlatformState.Ready;
                } 
                else Log.Line($"Comp Init() failed");
            }
        }

        internal void ReInit()
        {
            using (MyCube.Pin())  {

                if (!MyCube.MarkedForClose && Entity != null)  {

                    GridAi ai;
                    if (!Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out ai)) {

                        var newAi = Session.GridAiPool.Get();
                        newAi.Init(MyCube.CubeGrid, Session);
                        Session.GridTargetingAIs.TryAdd(MyCube.CubeGrid, newAi);
                        Ai = newAi;
                    }
                    else {
                        Ai = ai;
                    }

                    if (Ai != null && Ai.WeaponBase.TryAdd(MyCube, this)) {

                        Ai.FirstRun = true;
                        var blockDef = MyCube.BlockDefinition.Id.SubtypeId;

                        if (!Ai.WeaponCounter.ContainsKey(blockDef))
                            Ai.WeaponCounter.TryAdd(blockDef, Session.WeaponCountPool.Get());

                        Ai.WeaponCounter[blockDef].Current++;
                        Ai.CompChange(true, this);
                        RegisterEvents();


                        if (Platform.State == MyWeaponPlatform.PlatformState.Inited)
                            Platform.ResetParts(this);

                        Entity.NeedsWorldMatrix = true;

                        if (!Ai.GridInit || !Ai.Session.GridToFatMap.ContainsKey(Ai.MyGrid)) 
                            Session.CompReAdds.Add(new CompReAdd { Ai = Ai, Comp = this });
                        else 
                            OnAddedToSceneTasks();
                    }
                    else {
                        Log.Line($"Comp ReInit() failed stage2!");
                    }
                }
                else {
                    Log.Line($"Comp ReInit() failed stage1! - marked:{MyCube.MarkedForClose} - Entity:{Entity != null} - hasAi:{Session.GridTargetingAIs.ContainsKey(MyCube.CubeGrid)}");
                }
            }
        }

        internal void OnAddedToSceneTasks()
        {
            try {

                Ai.UpdatePowerSources = true;
                if (!Ai.GridInit) {

                    Ai.GridInit = true;
                    Ai.InitFakeShipController();
                    Ai.ScanBlockGroups = true;
                    var fatList = Session.GridToFatMap[MyCube.CubeGrid].MyCubeBocks;
                    
                    for (int i = 0; i < fatList.Count; i++) {

                        var cubeBlock = fatList[i];
                        if (cubeBlock is MyBatteryBlock || cubeBlock is IMyCargoContainer || cubeBlock is IMyAssembler || cubeBlock is IMyShipConnector)
                            Ai.FatBlockAdded(cubeBlock);
                    }
                }

                MaxRequiredPower = 0;
                HeatPerSecond = 0;
                OptimalDps = 0;
                MaxHeat = 0;

                var maxTrajectory = 0d;
                var ob = MyCube.BlockDefinition as MyLargeTurretBaseDefinition;

                for (int i = 0; i < Platform.Weapons.Length; i++) {
                    
                    var weapon = Platform.Weapons[i];
                    weapon.InitTracking();
                    
                    double weaponMaxRange;
                    DpsAndHeatInit(weapon, ob, out weaponMaxRange);

                    if(maxTrajectory < weaponMaxRange)
                        maxTrajectory = weaponMaxRange;
                }

                if (maxTrajectory + Ai.MyGrid.PositionComp.LocalVolume.Radius > Ai.MaxTargetingRange) {

                    Ai.MaxTargetingRange = maxTrajectory + Ai.MyGrid.PositionComp.LocalVolume.Radius;
                    Ai.MaxTargetingRangeSqr = Ai.MaxTargetingRange * Ai.MaxTargetingRange;
                }

                Ai.OptimalDps += OptimalDps;
                Ai.UpdateConstruct();
                
                if (!FunctionalBlock.Enabled)
                    for (int i = 0; i < Platform.Weapons.Length; i++)
                        Platform.Weapons[i].EventTriggerStateChanged(EventTriggers.TurnOff, true);

                Status = !IsWorking ? Start.Starting : Start.ReInit;
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToSceneTasks: {ex} AiNull:{Ai == null} - SessionNull:{Session == null} EntNull{Entity == null} MyCubeNull:{MyCube?.CubeGrid == null}"); }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                base.OnRemovedFromScene();
                RemoveComp();
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (Session.IsServer && Platform.State == MyWeaponPlatform.PlatformState.Ready) {

                Set.Value.Inventory = BlockInventory.GetObjectBuilder();

                if (MyCube?.Storage != null) {

                    State.SaveState();
                    Set.SaveSettings();
                    if (Session.MpActive)
                    {
                        WeaponValues.Save(this, Session.MpTargetSyncGuid);

                        if(Ai.LastSerializedTick != Session.Tick)
                        {
                            if(Ai.MyGrid.Storage != null)
                            {

                            }
                            else
                            {

                            }
                        }
                    }
                }
            }
            return false;
        }

        public override string ComponentTypeDebugString => "WeaponCore";
    }
}
