using System;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using WeaponCore.Platform;

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
                    lock (this)
                    {
                        using (MyCube.Pin())
                        {
                            if (MyCube.MarkedForClose)
                            {
                                Log.Line($"OnAddedToContainer cube marked for close,");
                                return;
                            }
                        }
                        using (MyCube.CubeGrid.Pin())
                        {
                            if (MyCube.CubeGrid.MarkedForClose)
                            {
                                Log.Line($"OnAddedToContainer CubeGrid marked for close");
                                return;
                            }
                        }
                        if (!Ai.Session.GridToFatMap.ContainsKey(MyCube.CubeGrid))
                        {
                            Log.Line($"OnAddedToContainer didn't exist in GridToFatMap - Marked:{MyCube.CubeGrid.MarkedForClose} - Closed:{MyCube.CubeGrid.Closed} - InScene:{MyCube.CubeGrid.InScene} - Preview:{MyCube.CubeGrid.IsPreview} - Physics:{MyCube.CubeGrid.Physics != null}");
                            return;
                        }

                        if (Platform.State == MyWeaponPlatform.PlatformState.Fresh)
                            PlatformInit(null);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToContainer: {ex}"); }
        }

        public override void OnAddedToScene()
        {
            try
            {
                base.OnAddedToScene();
                lock (this)
                {

                    if (Platform.State == MyWeaponPlatform.PlatformState.Inited || Platform.State == MyWeaponPlatform.PlatformState.Ready)
                        //MyAPIGateway.Utilities.InvokeOnGameThread(ReInit);
                        Ai.Session.CompChanges.Enqueue(new CompChange {Ai = Ai, Comp = this, Change = CompChange.ChangeType.Reinit});
                    else
                        //MyAPIGateway.Utilities.InvokeOnGameThread(PlatformInit);
                        Ai.Session.CompChanges.Enqueue(new CompChange { Ai = Ai, Comp = this, Change = CompChange.ChangeType.PlatformInit });
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }
        
        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
            if (!Container.Entity.InScene)
                Ai.Session.FutureEvents.Schedule(RemoveSinkDelegate, null, 100);
        }

        internal void PlatformInit(object o)
        {
            lock (this)
            {
                using (MyCube.Pin())
                {
                    if (MyCube.MarkedForClose)
                    {
                        Log.Line($"PreInit cube marked for close,");
                        return;
                    }
                }
                using (MyCube.CubeGrid.Pin())
                {
                    if (MyCube.CubeGrid.MarkedForClose)
                    {
                        Log.Line($"PreInit CubeGrid marked for close");
                        return;
                    }
                }
                if (!Ai.Session.GridToFatMap.ContainsKey(MyCube.CubeGrid))
                {
                    Log.Line($"PlatformInit didn't exist in GridToFatMap - Marked:{MyCube.CubeGrid.MarkedForClose} - Closed:{MyCube.CubeGrid.Closed} - InScene:{MyCube.CubeGrid.InScene} - Preview:{MyCube.CubeGrid.IsPreview} - Physics:{MyCube.CubeGrid.Physics != null}");
                    //return;
                }
                switch (Platform.Init(this))
                {
                    case MyWeaponPlatform.PlatformState.Invalid:
                        Log.Line($"Platform PreInit is in an invalid state");
                        break;
                    case MyWeaponPlatform.PlatformState.Valid:
                        Log.Line($"Something went wrong with Platform PreInit");
                        break;
                    case MyWeaponPlatform.PlatformState.Delay:
                        //Log.Line($"Platform RePreInit in 120");
                        if (Ai == null)
                        {
                            Log.Line($"Ai null in PreInit");
                            break;
                        }
                        Ai.Session.FutureEvents.Schedule(DelayedPlatformInit, null, 120);
                        break;
                    case MyWeaponPlatform.PlatformState.Inited:
                        //Log.Line($"Platform Inited");
                        Init();
                        break;
                }
            }
        }

        internal void DelayedPlatformInit(object o)
        {
            Ai.Session.CompChanges.Enqueue(new CompChange { Ai = Ai, Comp = this, Change = CompChange.ChangeType.PlatformInit });
        }

        internal void Init()
        {
            if (!Ai.Session.GridToFatMap.ContainsKey(MyCube.CubeGrid))
            {
                Log.Line($"Init didn't exist in GridToFatMap - Marked:{MyCube.CubeGrid.MarkedForClose} - Closed:{MyCube.CubeGrid.Closed} - InScene:{MyCube.CubeGrid.InScene} - Preview:{MyCube.CubeGrid.IsPreview} - Physics:{MyCube.CubeGrid.Physics != null}");
                //return;
            }
            lock (this)
            {
                _isServer = Ai.Session.IsServer;
                _isDedicated = Ai.Session.DedicatedServer;
                _mpActive = Ai.Session.MpActive;

                Entity.NeedsUpdate = ~MyEntityUpdateEnum.EACH_10TH_FRAME;
                Ai.FirstRun = true;

                StorageSetup();

                MaxRequiredPower = 0;
                HeatPerSecond = 0;
                OptimalDps = 0;
                MaxHeat = 0;

                InventoryInit();

                //range slider fix
                var maxTrajectory = 0d;
                var ob = MyCube.BlockDefinition as MyLargeTurretBaseDefinition;
                for (int i = 0; i < Platform.Weapons.Length; i++)
                {
                    var weapon = Platform.Weapons[i];
                    var state = State.Value.Weapons[weapon.WeaponId];

                    weapon.InitTracking();
                    DpsAndHeatInit(weapon);
                    weapon.UpdateBarrelRotation();

                    //range slider fix
                    if (ob != null && ob.MaxRangeMeters > maxTrajectory)
                        maxTrajectory = ob.MaxRangeMeters;
                    else if (weapon.System.MaxTrajectory > maxTrajectory)
                        maxTrajectory = weapon.System.MaxTrajectory;

                    if (weapon.TrackProjectiles)
                        Ai.PointDefense = true;

                    if (!weapon.System.EnergyAmmo && !weapon.System.MustCharge)
                        Session.ComputeStorage(weapon);

                    if (state.CurrentAmmo == 0 && !weapon.Reloading)
                        weapon.EventTriggerStateChanged(Weapon.EventTriggers.EmptyOnGameLoad, true);
                    else if (weapon.System.MustCharge && ((weapon.System.IsHybrid && state.CurrentAmmo == weapon.System.MagazineDef.Capacity) || state.CurrentAmmo == weapon.System.EnergyMagSize))
                    {
                        weapon.CurrentCharge = weapon.System.EnergyMagSize;
                        CurrentCharge += weapon.System.EnergyMagSize;
                    }
                    else if (weapon.System.MustCharge)
                    {
                        if (weapon.CurrentCharge > 0)
                            CurrentCharge -= weapon.CurrentCharge;

                        weapon.CurrentCharge = 0;
                        state.CurrentAmmo = 0;
                        weapon.Reloading = false;
                        Session.ComputeStorage(weapon);
                    }

                    if (state.ManualShoot != Weapon.TerminalActionState.ShootOff)
                    {
                        Ai.ManualComps++;
                        Shooting++;
                    }

                }

                //range slider fix - removed from weaponFields.cs
                if (maxTrajectory + Ai.GridRadius > Ai.MaxTargetingRange)
                {
                    Ai.MaxTargetingRange = maxTrajectory + Ai.GridRadius;
                    Ai.MaxTargetingRangeSqr = Ai.MaxTargetingRange * Ai.MaxTargetingRange;
                }

                Ai.OptimalDps += OptimalDps;

                
                PowerInit();
                RegisterEvents();
                OnAddedToSceneTasks();
                if (IsSorterTurret)
                {
                    if (!SorterBase.Enabled)
                    {
                        foreach (var w in Platform.Weapons)
                            w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);
                    }
                }
                else
                {
                    if (!MissileBase.Enabled)
                    {
                        foreach (var w in Platform.Weapons)
                            w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);
                    }
                }
                Platform.State = MyWeaponPlatform.PlatformState.Ready;
            }
        }

        internal void ReInit()
        {
            using (MyCube.Pin())
            {
                if (MyCube.MarkedForClose)
                {
                    Log.Line($"ReInitPlatform cube marked for close");
                    return;
                }
            }
            using (MyCube.CubeGrid.Pin())
            {
                if (MyCube.CubeGrid.MarkedForClose)
                {
                    Log.Line($"ReInitPlatform cubeGrid marked for close");
                    return;
                }
            }
            if (!Ai.Session.GridToFatMap.ContainsKey(MyCube.CubeGrid))
            {
                Log.Line($"ReInit didn't exist in GridToFatMap - Marked:{MyCube.CubeGrid.MarkedForClose} - Closed:{MyCube.CubeGrid.Closed} - InScene:{MyCube.CubeGrid.InScene} - Preview:{MyCube.CubeGrid.IsPreview} - Physics:{MyCube.CubeGrid.Physics != null}");
                //return;
            }
            if (Ai.Session.GridTargetingAIs.ContainsKey(MyCube.CubeGrid) && Ai.Session.GridTargetingAIs[MyCube.CubeGrid] != Ai) Log.Line($"ReInit Ai malfunction");

            var gridAiAdded = false;

            GridAi ai;
            if (!Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out ai))
            {
                gridAiAdded = true;
                Ai.Session.DsUtil2.Start("ReInit");
                var newAi = new GridAi(MyCube.CubeGrid, Ai.Session, Ai.Session.Tick);
                Ai.Session.GridTargetingAIs.TryAdd(MyCube.CubeGrid, newAi);
                Ai = newAi;
            }
            else Ai = ai;

            if (Ai != null && Ai.WeaponBase.TryAdd(MyCube, this))
            {
                if (!gridAiAdded) Ai.Session.DsUtil2.Start("ReInit");
                RegisterEvents();
                AddCompList();

                var blockDef = MyCube.BlockDefinition.Id.SubtypeId;
                if (!Ai.WeaponCounter.ContainsKey(blockDef))
                    Ai.WeaponCounter.TryAdd(blockDef, Ai.Session.WeaponCountPool.Get());

                Ai.WeaponCounter[blockDef].Current++;

                OnAddedToSceneTasks();
                Ai.Session.DsUtil2.Complete("ReInit", false, true);
            }
            else Log.Line($"ReInit failed!");

        }

        internal void OnAddedToSceneTasks()
        {
            try
            {
                using (MyCube.Pin())
                {
                    if (MyCube.MarkedForClose)
                    {
                        Log.Line($"cubeMarked for close in ONAddedToSceneTasks");
                        return;
                    }
                }
                using (MyCube.CubeGrid.Pin())
                {
                    if (MyCube.CubeGrid.MarkedForClose)
                    {
                        Log.Line($"cubeMarked for close in ONAddedToSceneTasks");
                        return;
                    }
                }
                if (MyCube.CubeGrid != Ai.MyGrid)
                {
                    Log.Line($"OnAddedToSceneTasks cubeGrid not Match AI Grid? {MyCube.CubeGrid.DebugName} - GridMarked:{MyCube.CubeGrid.MarkedForClose} - CubeMarked:{MyCube.MarkedForClose} - GridMatch:{MyCube.CubeGrid == Ai.MyGrid} ");
                    return;
                }
                if (Entity == null)
                {
                    Log.Line($"OnAddedToSceneTasks had a null Entity how? {MyCube.CubeGrid.DebugName} - GridMarked:{MyCube.CubeGrid.MarkedForClose} - CubeMarked:{MyCube.MarkedForClose} - InitState:{Platform.State}");
                    return;
                }
                if (!Ai.Session.GridToFatMap.ContainsKey(MyCube.CubeGrid))
                {
                    Log.Line($"OnAddedToSceneTasks didn't exist in GridToFatMap - Marked:{MyCube.CubeGrid.MarkedForClose} - Closed:{MyCube.CubeGrid.Closed} - InScene:{MyCube.CubeGrid.InScene} - Preview:{MyCube.CubeGrid.IsPreview} - Physics:{MyCube.CubeGrid.Physics != null}");
                    //return;
                }
                if (Platform.State == MyWeaponPlatform.PlatformState.Inited)
                    Platform.ResetParts(this);

                Entity.NeedsWorldMatrix = true;

                Ai.UpdatePowerSources = true;
                if (!Ai.GridInit)
                {
                    Ai.GridInit = true;
                    Ai.InitFakeShipController();
                    foreach (var cubeBlock in Ai.Session.GridToFatMap[MyCube.CubeGrid].MyCubeBocks)
                    {
                        Ai.FatBlockAdded(cubeBlock);
                    }
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
                //MyAPIGateway.Utilities.InvokeOnGameThread(OnRemovedToSceneTasks);
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
