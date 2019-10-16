using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
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
                        if (Platform == null)
                            InitPlatform();
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
                    //if (MainInit) MyAPIGateway.Utilities.InvokeOnGameThread(ReInitPlatform);
                    if (MainInit) ReInitPlatform();
                    else MyAPIGateway.Utilities.InvokeOnGameThread(InitPlatform);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }
        
        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
            if (Container.Entity.InScene)
            {
            }
        }

        public void InitPlatform()
        {
            _isServer = Ai.Session.IsServer;
            _isDedicated = Ai.Session.DedicatedServer;
            _mpActive = Ai.Session.MpActive;

            Ai.FirstRun = true;
            Platform = new MyWeaponPlatform(this);
            if (!Platform.Inited)
            {
                WeaponComponent removed;
                Ai.WeaponBase.TryRemove(MyCube, out removed);
                Log.Line("init platform returned");
                return;
            }




            StorageSetup();

            MaxRequiredPower = 0;
            HeatPerSecond = 0;
            OptimalDPS = 0;

            for (int i  = 0; i< Platform.Weapons.Length; i++)
            {
                var weapon = Platform.Weapons[i];
                weapon.InitTracking();
                Session.ComputeStorage(weapon);

                MaxHeat += weapon.System.MaxHeat;
                weapon.RateOfFire = (int)(weapon.System.RateOfFire * Set.Value.ROFModifier);

                if (weapon.System.EnergyAmmo)
                    weapon.BaseDamage = weapon.System.BaseDamage * Set.Value.DPSModifier;
                else
                    weapon.BaseDamage = weapon.System.BaseDamage;

                if (weapon.System.IsBeamWeapon)
                    weapon.BaseDamage *= Set.Value.Overload;

                if (weapon.BaseDamage < 0)
                    weapon.BaseDamage = 0;

                if (weapon.RateOfFire < 1)
                    weapon.RateOfFire = 1;

                weapon.UpdateShotEnergy();
                weapon.UpdateRequiredPower();

                var mulitplier = (weapon.System.EnergyAmmo && weapon.System.BaseDamage > 0) ? weapon.BaseDamage / weapon.System.BaseDamage : 1;

                if (weapon.BaseDamage > weapon.System.BaseDamage)
                    mulitplier = mulitplier * mulitplier;

                weapon.HeatPShot = weapon.System.HeatPerShot * mulitplier;
                weapon.areaEffectDmg = weapon.System.AreaEffectDamage * mulitplier;
                weapon.detonateDmg = weapon.System.DetonationDamage * mulitplier;


                MaxRequiredPower -= weapon.RequiredPower;
                weapon.RequiredPower *= mulitplier;
                MaxRequiredPower += weapon.RequiredPower;


                weapon.TicksPerShot = (uint)(3600f / weapon.RateOfFire);
                weapon.TimePerShot = (3600d / weapon.RateOfFire);

                weapon.DPS = (60 / (float)weapon.TicksPerShot) * weapon.BaseDamage * weapon.System.BarrelsPerShot;

                if (weapon.System.Values.Ammo.AreaEffect.AreaEffect != AreaDamage.AreaEffectType.Disabled)
                {
                    if (weapon.System.Values.Ammo.AreaEffect.Detonation.DetonateOnEnd)
                        weapon.DPS += (weapon.detonateDmg / 2) * (weapon.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                            ? weapon.System.Values.Ammo.Trajectory.AccelPerSec /
                                            weapon.System.Values.Ammo.Trajectory.DesiredSpeed
                                            : 1);
                    else
                        weapon.DPS += (weapon.areaEffectDmg / 2) *
                                        (weapon.System.Values.Ammo.Trajectory.DesiredSpeed > 0
                                            ? weapon.System.Values.Ammo.Trajectory.AccelPerSec /
                                            weapon.System.Values.Ammo.Trajectory.DesiredSpeed
                                            : 1);
                }

                HeatPerSecond += (60 / (float)weapon.TicksPerShot) * weapon.HeatPShot * weapon.System.BarrelsPerShot;
                OptimalDPS += weapon.DPS;


                HeatSinkRate += weapon.HsRate;

                weapon.UpdateBarrelRotation();

                if (weapon.CurrentMags == 0)
                {
                    weapon.EventTriggerStateChanged(Weapon.EventTriggers.EmptyOnGameLoad, true);
                    weapon.FirstLoad = false;
                }

                MaxInventoryVolume += weapon.System.MaxAmmoVolume;
            }

            Ai.OptimalDPS += OptimalDPS;

            if (MyCube.HasInventory)
            {
                if (!IsAIOnlyTurret)
                {
                    foreach (var w in Platform.Weapons)
                    {
                        var otherId = w.System.MagazineDef.AmmoDefinitionId;
                        BlockInventory.Constraint.Add(otherId);
                    }
                }
                else
                {
                    BlockInventory.FixInventoryVolume(MaxInventoryVolume);
                }
                BlockInventory.Refresh();
            }

            PowerInit();
            RegisterEvents();
            OnAddedToSceneTasks();

            if (IsAIOnlyTurret)
            {
                if (!AIOnlyTurret.Enabled)
                {
                    foreach (var w in Platform.Weapons)
                        w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);
                }
            }
            else {
                if (!ControllableTurret.Enabled)
                {
                    foreach (var w in Platform.Weapons)
                        w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);
                }
            }

            MainInit = true;
        }

        public void ReInitPlatform()
        {
            GridAi gridAi;
            if (!Ai.Session.GridTargetingAIs.TryGetValue(MyCube.CubeGrid, out gridAi))
            {
                gridAi = new GridAi(MyCube.CubeGrid, Ai.Session);
                Ai.Session.GridTargetingAIs.TryAdd(MyCube.CubeGrid, gridAi);
            }
            Ai = gridAi;
            RegisterEvents();
            //Log.Line($"reinit comp: grid:{MyCube.CubeGrid.DebugName} - Weapon:{MyCube.DebugName}");
            if (gridAi != null && gridAi.WeaponBase.TryAdd(MyCube, this))
                OnAddedToSceneTasks();
        }

        private void OnAddedToSceneTasks()
        {
            if (MainInit)
                Platform.ResetParts(this);

            Entity.NeedsWorldMatrix = true;
            //Turret.EnableIdleRotation = false;

            Ai.TotalSinkPower += MaxRequiredPower;
            Ai.MinSinkPower += IdlePower;
            Ai.RecalcPowerPercent = true;
            Ai.UpdatePowerSources = true;
            if (!Ai.GridInit)
            {
                Ai.GridInit = true;
                if (Ai.MyGrid != MyCube.CubeGrid || Ai.MyGrid.MarkedForClose) Log.Line($"AiGrid Mismatch during OnAddedToScene");
                Ai.TerminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Ai.MyGrid);

                foreach (var cubeBlock in MyCube.CubeGrid.GetFatBlocks())
                    Ai.FatBlockAdded(cubeBlock);
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
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (IsAIOnlyTurret)
                {
                    if (AIOnlyTurret.Storage != null)
                    {
                        State.SaveState();
                        Set.SaveSettings();
                    }
                }
                else
                {
                    if (ControllableTurret.Storage != null)
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

        /*
        public void Run()
        {
            try
            {
                if (!EntityAlive()) return;

                var state = WeaponState();
                if (state != Start.Online)
                {
                    if (NotFailed) FailWeapon(state);
                    else if (State.Value.Message) UpdateNetworkState();
                    return;
                }

                if (!_isServer || !State.Value.Online) return;
                if (Starting) ComingOnline();
                if (_mpActive && (Sync || _count == 29))
                {
                    if (Sync)
                    {
                        UpdateNetworkState();
                        Sync = false;
                    }
                    else if (Session.Instance.Tick1800) UpdateNetworkState();
                }
                _firstRun = false;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }
        */
    }
}
