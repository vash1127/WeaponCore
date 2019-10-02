using System;
using System.Linq;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent : MyEntityComponentBase
    {
        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();
            if (Container.Entity.InScene)
            {
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {

            if (Container.Entity.InScene)
            {
            }

            base.OnBeforeRemovedFromContainer();
        }

        public override void OnAddedToScene()
        {
            try
            {
                base.OnAddedToScene();
                if (MainInit)
                {
                    MyGrid = MyCube.CubeGrid;
                    GridAi gridAi;
                    if (!Session.Instance.GridTargetingAIs.TryGetValue(MyGrid, out gridAi))
                    {
                        gridAi = new GridAi(MyGrid);
                        Session.Instance.GridTargetingAIs.TryAdd(MyGrid, gridAi);
                    }
                    Ai = gridAi;
                    MyGrid = MyCube.CubeGrid;
                    PowerInit();
                    RegisterEvents();
                    if (gridAi != null && gridAi.WeaponBase.TryAdd(MyCube, this))
                        OnAddedToSceneTasks();

                    return;
                }
                _isServer = Session.Instance.IsServer;
                _isDedicated = Session.Instance.DedicatedServer;
                _mpActive = Session.Instance.MpActive;
                InitPlatform();
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public void InitPlatform()
        {
            Platform = new MyWeaponPlatform(this);
            if (!Platform.Inited)
            {
                WeaponComponent removed;
                Ai.WeaponBase.TryRemove(MyCube, out removed);
                return;
            }

            PullingAmmoCnt = Platform.Structure.AmmoToWeaponIds.Count;

            FullInventory = BlockInventory.CargoPercentage >= 0.5;
            MultiInventory = PullingAmmoCnt > 1;
            if (MultiInventory)
            {
                MaxAmmoVolume = (float)MyFixedPoint.MultiplySafe(MaxInventoryVolume, 1 / (float)PullingAmmoCnt) * 0.5f;
                MaxAmmoMass = (float)MyFixedPoint.MultiplySafe(MaxInventoryMass, 1 / (float)PullingAmmoCnt) * 0.5f;
            }

            StorageSetup();

            MaxRequiredPower = 0;
            HeatPerSecond = 0;
            OptimalDPS = 0;
            foreach (var weapon in Platform.Weapons)
            {
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

                var mulitplier = (weapon.System.EnergyAmmo && weapon.System.BaseDamage > 0) ? weapon.BaseDamage / weapon.System.BaseDamage: 1;

                if (weapon.BaseDamage > weapon.System.BaseDamage)
                    mulitplier = mulitplier * mulitplier;

                weapon.HeatPShot = weapon.System.HeatPerShot * mulitplier;
                weapon.areaEffectDmg = weapon.System.AreaEffectDamage * mulitplier;
                weapon.detonateDmg = weapon.System.DetonationDamage * mulitplier; 


                MaxRequiredPower -= weapon.RequiredPower;
                weapon.RequiredPower = weapon.RequiredPower *mulitplier;
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

                HeatPerSecond += (60 / (float)weapon.TicksPerShot) *  weapon.HeatPShot * weapon.System.BarrelsPerShot;
                OptimalDPS += weapon.DPS;
                

                HeatSinkRate += weapon.HsRate;

                weapon.UpdateBarrelRotation();

                if (weapon.CurrentMags == 0)
                {
                    weapon.EventTriggerStateChanged(Weapon.EventTriggers.EmptyOnGameLoad, true);
                    weapon.FirstLoad = false;
                }

            }

            Ai.OptimalDPS += OptimalDPS;

            var gun = Gun.GunBase;
            var id = PullingAmmoCnt == 0 ? Platform.Weapons[0].System.MagazineDef.Id
                : Platform.Structure.AmmoToWeaponIds.First().Key;
            BlockInventory.Constraint.Clear();
            BlockInventory.Constraint.Add(id);
            gun.SwitchAmmoMagazine(id);
            foreach (var w in Platform.Weapons)
            {
                var otherId = w.System.MagazineDef.AmmoDefinitionId;
                if (otherId == id) continue;
                BlockInventory.Constraint.Add(otherId);
            }

            RegisterEvents();

            OnAddedToSceneTasks();

            if(!Turret.Enabled)
            {
                foreach (var w in Platform.Weapons)
                    w.EventTriggerStateChanged(Weapon.EventTriggers.TurnOff, true);
            }

            MainInit = true;
        }

        private void OnAddedToSceneTasks()
        {
            if (MainInit)
                Platform.ResetParts(this);

            Entity.NeedsWorldMatrix = true;
            Turret.EnableIdleRotation = false;
            Physics = ((IMyCubeGrid)MyCube.CubeGrid).Physics;

            Ai.TotalSinkPower += MaxRequiredPower;
            Ai.MinSinkPower += IdlePower;
            Ai.RecalcPowerPercent = true;
            Ai.UpdatePowerSources = true;
            if (!Ai.GridInit)
            {
                foreach (var cubeBlock in MyGrid.GetFatBlocks())
                {
                    Ai.FatBlockAdded(cubeBlock);
                }
                Ai.GridInit = true;
            }

            Status = Start.Starting;
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                base.OnRemovedFromScene();
                if(!Session.Instance.CompsToRemove.Contains(this))
                    Session.Instance.CompsToRemove.Enqueue(this);
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (Turret.Storage != null)
                {
                    State.SaveState();
                    Set.SaveSettings();
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
