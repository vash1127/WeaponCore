using System;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Session;
using static WeaponCore.Support.Ai;
using static WeaponCore.Support.PartDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore.Support
{
    public partial class CoreComponent : MyEntityComponentBase
    {
        public override void OnAddedToContainer()
        {
            try {

                base.OnAddedToContainer();
                TopEntity = CoreEntity.GetTopMostParent();
                if (Container.Entity.InScene) {

                    if (Platform.State == CorePlatform.PlatformState.Fresh)
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
                TopEntity = CoreEntity.GetTopMostParent();

                if (Platform.State == CorePlatform.PlatformState.Inited || Platform.State == CorePlatform.PlatformState.Ready)
                    ReInit();
                else {

                    if (Platform.State == CorePlatform.PlatformState.Delay)
                        return;
                    
                    if (Platform.State != CorePlatform.PlatformState.Fresh)
                        Log.Line($"OnAddedToScene != Fresh, Inited or Ready: {Platform.State}");

                    PlatformInit();
                }
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

                case CorePlatform.PlatformState.Invalid:
                    Platform.PlatformCrash(this, false, false, $"Platform PreInit is in an invalid state: {SubtypeName}");
                    break;
                case CorePlatform.PlatformState.Valid:
                    Platform.PlatformCrash(this, false, true, $"Something went wrong with Platform PreInit: {SubtypeName}");
                    break;
                case CorePlatform.PlatformState.Delay:
                    Session.CompsDelayed.Add(this);
                    break;
                case CorePlatform.PlatformState.Inited:
                    Init();
                    break;
            }
        }

        internal void Init()
        {
            using (CoreEntity.Pin()) 
            {
                if (!CoreEntity.MarkedForClose && Entity != null) 
                {
                    Ai.FirstRun = true;

                    StorageSetup();
                    InventoryInit();
                    if (IsBlock)
                        PowerInit();
                    else Ai.AiOwner = GunBase.OwnerId;

                    if (IsWeapon && Platform.State == CorePlatform.PlatformState.Inited)
                        Platform.ResetParts(this);

                    Entity.NeedsWorldMatrix = true;

                    if (!Ai.GridInit) Session.CompReAdds.Add(new CompReAdd { Ai = Ai, AiVersion = Ai.Version, AddTick = Ai.Session.Tick, Comp = this });
                    else OnAddedToSceneTasks();

                    Platform.State = CorePlatform.PlatformState.Ready;

                    for (int i = 0; i < Platform.Weapons.Count; i++)
                    {
                        var weapon = Platform.Weapons[i];
                        weapon.UpdatePivotPos();

                        if (Session.IsClient)
                            weapon.Target.ClientDirty = true;

                        if (weapon.Ammo.CurrentAmmo == 0 && !weapon.Reloading)
                            weapon.EventTriggerStateChanged(EventTriggers.EmptyOnGameLoad, true);
                    }
                } 
                else Log.Line($"Comp Init() failed");
            }
        }

        internal void ReInit()
        {
            using (CoreEntity.Pin())  {

                if (!CoreEntity.MarkedForClose && Entity != null)  {

                    Ai ai;
                    if (!Session.GridTargetingAIs.TryGetValue(TopEntity, out ai)) {

                        var newAi = Session.GridAiPool.Get();
                        newAi.Init(TopEntity, Session);
                        Session.GridTargetingAIs[TopEntity] = newAi;
                        Ai = newAi;
                    }
                    else {
                        Ai = ai;
                    }

                    if (Ai != null) {

                        Ai.FirstRun = true;

                        if (IsWeapon && Platform.State == CorePlatform.PlatformState.Inited)
                            Platform.ResetParts(this);

                        Entity.NeedsWorldMatrix = true;

                        // ReInit Counters
                        if (!Ai.PartCounting.ContainsKey(SubTypeId)) // Need to account for reinit case
                            Ai.PartCounting[SubTypeId] = Session.WeaponCountPool.Get();

                        var wCounter = Ai.PartCounting[SubTypeId];
                        wCounter.Max = Platform.Structure.GridWeaponCap;

                        wCounter.Current++;
                        Constructs.UpdateWeaponCounters(Ai);
                        // end ReInit

                        if (!Ai.GridInit || !Ai.Session.GridToInfoMap.ContainsKey(Ai.TopEntity)) 
                            Session.CompReAdds.Add(new CompReAdd { Ai = Ai, AiVersion = Ai.Version, AddTick = Ai.Session.Tick, Comp = this });
                        else 
                            OnAddedToSceneTasks();
                    }
                    else {
                        Log.Line($"Comp ReInit() failed stage2!");
                    }
                }
                else {
                    Log.Line($"Comp ReInit() failed stage1! - marked:{CoreEntity.MarkedForClose} - Entity:{Entity != null} - hasAi:{Session.GridTargetingAIs.ContainsKey(TopEntity)}");
                }
            }
        }

        internal void OnAddedToSceneTasks()
        {
            try {

                if (Ai.MarkedForClose)
                    Log.Line($"OnAddedToSceneTasks and AI MarkedForClose - Subtype:{SubtypeName} - grid:{TopEntity.DebugName} - CubeMarked:{CoreEntity.MarkedForClose} - GridMarked:{TopEntity.MarkedForClose} - GridMatch:{TopEntity == Ai.TopEntity} - AiContainsMe:{Ai.PartBase.ContainsKey(CoreEntity)} - MyGridInAi:{Ai.Session.GridToMasterAi.ContainsKey(TopEntity)}[{Ai.Session.GridTargetingAIs.ContainsKey(TopEntity)}]");
                Ai.UpdatePowerSources = true;
                RegisterEvents();
                if (IsBlock && !Ai.GridInit) {

                    Ai.GridInit = true;
                    var fatList = Session.GridToInfoMap[TopEntity].MyCubeBocks;

                    for (int i = 0; i < fatList.Count; i++) {

                        var cubeBlock = fatList[i];
                        if (cubeBlock is MyBatteryBlock || cubeBlock.HasInventory)
                            Ai.FatBlockAdded(cubeBlock);
                    }

                    SubGridInit();
                }

                var maxTrajectory = 0d;

                for (int i = 0; i < Platform.Weapons.Count; i++) {
                    
                    var weapon = Platform.Weapons[i];
                    weapon.InitTracking();
                    
                    double weaponMaxRange;
                    DpsAndHeatInit(weapon, out weaponMaxRange);

                    if (maxTrajectory < weaponMaxRange)
                        maxTrajectory = weaponMaxRange;

                    if (weapon.Ammo.CurrentAmmo > weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize)
                        weapon.Ammo.CurrentAmmo = weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize;

                    if (Session.IsServer && weapon.TrackTarget)
                        Session.AcqManager.Monitor(weapon.Acquire);
                }

                if (maxTrajectory + Ai.TopEntity.PositionComp.LocalVolume.Radius > Ai.MaxTargetingRange) {

                    Ai.MaxTargetingRange = maxTrajectory + Ai.TopEntity.PositionComp.LocalVolume.Radius;
                    Ai.MaxTargetingRangeSqr = Ai.MaxTargetingRange * Ai.MaxTargetingRange;
                }

                Ai.OptimalDps += PeakDps;
                Ai.EffectiveDps += EffectiveDps;


                if (!Ai.PartBase.TryAdd(CoreEntity, this))
                    Log.Line($"failed to add cube to gridAi");

                Ai.CompChange(true, this);

                Ai.IsStatic = Ai.TopEntity.Physics?.IsStatic ?? false;
                Ai.Construct.Refresh(Ai, Constructs.RefreshCaller.Init);

                if (!FunctionalBlock.Enabled)
                    for (int i = 0; i < Platform.Weapons.Count; i++)
                        Session.FutureEvents.Schedule(Platform.Weapons[i].DelayedStart, null, 1);

                VanillaTurretBase?.SetTarget(Vector3D.MaxValue);

                Status = !IsWorking ? Start.Starting : Start.ReInit;
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToSceneTasks: {ex} AiNull:{Ai == null} - SessionNull:{Session == null} EntNull{Entity == null} MyCubeNull:{TopEntity == null}"); }
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
            if (Session.IsServer && Platform.State == CorePlatform.PlatformState.Ready) {

                if (CoreEntity?.Storage != null) {
                    Data.Save();
                }
            }
            return false;
        }

        public override string ComponentTypeDebugString => "WeaponCore";
    }
}
