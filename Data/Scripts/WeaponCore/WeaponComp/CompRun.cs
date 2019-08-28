using System;
using System.Linq;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
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
                    if (Ai.MyGrid != MyCube.CubeGrid) Log.Line("grid mismatch");
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
                Log.Line("removing");
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
            foreach (var weapon in Platform.Weapons)
            {
                weapon.InitTracking();
                Session.ComputeStorage(weapon);
            }

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

            StorageSetup();

            MaxRequiredPower = 0;
            HeatPerSecond = 0;
            OptimalDPS = 0;
            foreach (var weapon in Platform.Weapons)
            {
                weapon.RateOfFire = State.Value.Weapons[weapon.WeaponId].ROF != 0 ? State.Value.Weapons[weapon.WeaponId].ROF : weapon.System.Values.HardPoint.Loading.RateOfFire;
                weapon.BaseDamage = State.Value.Weapons[weapon.WeaponId].BaseDamage != 0 ? State.Value.Weapons[weapon.WeaponId].BaseDamage : weapon.System.Values.Ammo.BaseDamage;

                weapon.UpdateShotEnergy();
                weapon.UpdateRequiredPower();

                var mulitplier = weapon.BaseDamage / weapon.System.Values.Ammo.BaseDamage;

                if (weapon.BaseDamage != weapon.System.Values.Ammo.BaseDamage)
                {

                    weapon.HeatPShot = weapon.System.Values.HardPoint.Loading.HeatPerShot * (int)(mulitplier * mulitplier);

                    MaxRequiredPower -= weapon.RequiredPower;
                    weapon.RequiredPower = weapon.RequiredPower * (mulitplier * mulitplier);
                    MaxRequiredPower += weapon.RequiredPower;
                }
                else
                    weapon.HeatPShot = weapon.System.Values.HardPoint.Loading.HeatPerShot;


                weapon.TicksPerShot =  (uint)(3600 / weapon.RateOfFire);
                weapon.TimePerShot = (3600d / weapon.RateOfFire);

                HeatPerSecond += (60 / weapon.TicksPerShot) * weapon.HeatPShot;
                OptimalDPS += (int)((60 / weapon.TicksPerShot) * weapon.BaseDamage);

                HeatSinkRate += weapon.HsRate;
            }

            RegisterEvents(true);

            OnAddedToSceneTasks();

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
                if (Platform.Inited)
                {
                    RegisterEvents(false);
                    StopAllSounds();
                    Platform.RemoveParts(this);
                    Ai.WeaponCounter[MyCube.BlockDefinition.Id.SubtypeId].Current--;

                    WeaponComponent comp;
                    Ai.WeaponBase.TryRemove(MyCube, out comp);
                }

                if (Ai.WeaponBase.Count == 0)
                {
                    GridAi gridAi;
                    Session.Instance.GridTargetingAIs.TryRemove(MyGrid, out gridAi);
                }
                Ai = null;
                MyGrid = null;
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
