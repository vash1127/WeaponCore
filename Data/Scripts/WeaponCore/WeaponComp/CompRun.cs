using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
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
                        if (Platform.State == MyWeaponPlatform.PlatformState.Refresh)
                            PreInit();
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
                    if (Platform.State == MyWeaponPlatform.PlatformState.Inited) ReInitPlatform();
                    else MyAPIGateway.Utilities.InvokeOnGameThread(PreInit);
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

        public void RePreInit(object o)
        {
            PreInit();
        }

        public void PreInit()
        {
            lock (this)
            {
                switch (Platform.PreInit(this))
                {
                    case MyWeaponPlatform.PlatformState.Invalid:
                        Log.Line($"Platform PreInit is in an invalid state");
                        break;
                    case MyWeaponPlatform.PlatformState.Valid:
                        Log.Line($"Something went wrong with Platform PreInit");
                        break;
                    case MyWeaponPlatform.PlatformState.Delay:
                        Ai.Session.FutureEvents.Schedule(RePreInit, null, 120);
                        break;
                    case MyWeaponPlatform.PlatformState.Inited:
                        InitPlatform();
                        break;
                }
            }
        }

        public void InitPlatform()
        {
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

                InventoryInit();

                //range slider fix
                var maxTrajectory = 0d;
                for (int i = 0; i < Platform.Weapons.Length; i++)
                {
                    var weapon = Platform.Weapons[i];
                    var state = State.Value.Weapons[weapon.WeaponId];

                    weapon.InitTracking();
                    DpsAndHeatInit(weapon);
                    weapon.UpdateBarrelRotation();

                    //range slider fix
                    if (weapon.System.MaxTrajectory > maxTrajectory)
                        maxTrajectory = weapon.System.MaxTrajectory;

                    Session.ComputeStorage(weapon);

                    if (state.CurrentAmmo == 0 && !weapon.Reloading)
                        weapon.EventTriggerStateChanged(Weapon.EventTriggers.EmptyOnGameLoad, true);

                    if(state.ManualShoot != Weapon.TerminalActionState.ShootOff)
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

        public void ReInitPlatform()
        {
            GridAi gridAi;
            if (!Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out gridAi))
            {
                gridAi = new GridAi(MyCube.CubeGrid, Ai.Session, Ai.Session.Tick);
                Ai.Session.GridTargetingAIs.TryAdd(MyCube.CubeGrid, gridAi);
            }
            Ai = gridAi;
            RegisterEvents();
            if (gridAi != null && gridAi.WeaponBase.TryAdd(MyCube, this))
                MyAPIGateway.Utilities.InvokeOnGameThread(OnAddedToSceneTasks);
        }

        private void OnAddedToSceneTasks()
        {
            if (!Ai.Session.GridToFatMap.ContainsKey(MyCube.CubeGrid))
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(OnAddedToSceneTasks);
                return;
            }

            if (Platform.State == MyWeaponPlatform.PlatformState.Inited)
                Platform.ResetParts(this);

            Entity.NeedsWorldMatrix = true;

            Ai.TotalSinkPower += MaxRequiredPower;
            Ai.MinSinkPower += IdlePower;
            Ai.RecalcPowerPercent = true;
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
            if (_isServer && Platform.State == MyWeaponPlatform.PlatformState.Ready)
            {
                if(BlockInventory != null) Set.Value.Inventory = BlockInventory.GetObjectBuilder();
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
