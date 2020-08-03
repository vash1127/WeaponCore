using System;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using static WeaponCore.Session;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.PartAnimation;
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
                    Ai.FirstRun = true;

                    StorageSetup();
                    InventoryInit();
                    PowerInit();

                    if (Platform.State == MyWeaponPlatform.PlatformState.Inited)
                        Platform.ResetParts(this);

                    Entity.NeedsWorldMatrix = true;

                    if (!Ai.GridInit) Session.CompReAdds.Add(new CompReAdd { Ai = Ai, AiVersion = Ai.Version, AddTick = Ai.Session.Tick, Comp = this });
                    else OnAddedToSceneTasks();

                    Platform.State = MyWeaponPlatform.PlatformState.Ready;

                    for (int i = 0; i < Platform.Weapons.Length; i++)
                    {
                        var weapon = Platform.Weapons[i];
                        weapon.UpdatePivotPos();

                        if (Session.IsClient)
                            weapon.Target.ClientDirty = true;

                        if (weapon.State.CurrentAmmo == 0 && !weapon.Reloading)
                            weapon.EventTriggerStateChanged(EventTriggers.EmptyOnGameLoad, true);
                    }
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
                        Session.GridTargetingAIs[MyCube.CubeGrid] = newAi;
                        Ai = newAi;
                    }
                    else {
                        Ai = ai;
                    }

                    if (Ai != null) {

                        Ai.FirstRun = true;

                        if (Platform.State == MyWeaponPlatform.PlatformState.Inited)
                            Platform.ResetParts(this);

                        Entity.NeedsWorldMatrix = true;

                        if (!Ai.GridInit || !Ai.Session.GridToFatMap.ContainsKey(Ai.MyGrid)) 
                            Session.CompReAdds.Add(new CompReAdd { Ai = Ai, AiVersion = Ai.Version, AddTick = Ai.Session.Tick, Comp = this });
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
                if (Ai.MarkedForClose)
                    Log.Line($"OnAddedToSceneTasks and AI MarkedForClose - CubeMarked:{MyCube.MarkedForClose} - GridMarked:{MyCube.CubeGrid.MarkedForClose} - GridMatch:{MyCube.CubeGrid == Ai.MyGrid} - AiContainsMe:{Ai.WeaponBase.ContainsKey(MyCube)} - MyGridInAi:{Ai.Session.GridToMasterAi.ContainsKey(MyCube.CubeGrid)}[{Ai.Session.GridTargetingAIs.ContainsKey(MyCube.CubeGrid)}]");
                Ai.UpdatePowerSources = true;
                RegisterEvents();
                if (!Ai.GridInit) {

                    Ai.GridInit = true;
                    var fatList = Session.GridToFatMap[MyCube.CubeGrid].MyCubeBocks;

                    for (int i = 0; i < fatList.Count; i++) {

                        var cubeBlock = fatList[i];
                        if (cubeBlock is MyBatteryBlock || cubeBlock.HasInventory)
                            Ai.FatBlockAdded(cubeBlock);
                    }

                    SubGridInit();
                    if (Ai.Session.IsServer) Ai.Construct.RootAi.ScanBlockGroups = true;
                }

                var maxTrajectory = 0d;

                for (int i = 0; i < Platform.Weapons.Length; i++) {
                    
                    var weapon = Platform.Weapons[i];
                    weapon.InitTracking();
                    
                    double weaponMaxRange;
                    DpsAndHeatInit(weapon, out weaponMaxRange);

                    if (maxTrajectory < weaponMaxRange)
                        maxTrajectory = weaponMaxRange;

                    //if (weapon.State.CurrentAmmo > weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize)
                        //weapon.State.CurrentAmmo = weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize;

                    var notValid = !IsWorking || !Data.Repo.Set.Overrides.Activate || !weapon.TrackTarget || Session.IsClient;
                    if (!notValid)
                        Session.AcqManager.AddAwake(weapon.Acquire);
                }

                if (maxTrajectory + Ai.MyGrid.PositionComp.LocalVolume.Radius > Ai.MaxTargetingRange) {

                    Ai.MaxTargetingRange = maxTrajectory + Ai.MyGrid.PositionComp.LocalVolume.Radius;
                    Ai.MaxTargetingRangeSqr = Ai.MaxTargetingRange * Ai.MaxTargetingRange;
                }

                Ai.OptimalDps += PeakDps;
                Ai.EffectiveDps += EffectiveDps;


                if (!Ai.WeaponBase.TryAdd(MyCube, this))
                    Log.Line($"failed to add cube to gridAi");

                Ai.CompChange(true, this);

                Ai.IsStatic = Ai.MyGrid.Physics?.IsStatic ?? false;
                Ai.Construct.Refresh(Ai, Constructs.RefreshCaller.Init);

                if (!FunctionalBlock.Enabled)
                    for (int i = 0; i < Platform.Weapons.Length; i++)
                        Session.FutureEvents.Schedule(Platform.Weapons[i].DelayedStart, null, 1);

                TurretBase?.SetTarget(Vector3D.MaxValue);

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

                if (MyCube?.Storage != null) {
                    Data.Save();
                }
            }
            return false;
        }

        public override string ComponentTypeDebugString => "WeaponCore";
    }
}
