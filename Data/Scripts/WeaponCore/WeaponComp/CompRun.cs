using System;
using System.Linq;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Components;
using VRage.Game.ModAPI;
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
                if (MainInit) return;
                base.OnAddedToScene();
                _isServer = Session.Instance.IsServer;
                _isDedicated = Session.Instance.DedicatedServer;
                _mpActive = Session.Instance.MpActive;
                InitPlatform();
                Log.Line("added to scene");
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public void Run()
        {
            try
            {
                if (!EntityAlive()) return;

                var state = WeaponState();
                if (state != Status.Online)
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

        public void InitPlatform()
        {
            Entity.NeedsWorldMatrix = true;
            Platform = new MyWeaponPlatform(this);

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
                //foreach (var field in typeof(weapon.System.Values.Ui).GetFields(BindingFlags.Instance | BindingFlags.NonPublic |BindingFlags.Public))
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
            State.Value.Online = true;
            Turret.EnableIdleRotation = false;
            Physics = ((IMyCubeGrid)MyCube.CubeGrid).Physics;

            RegisterEvents();

            CreateUi();

            MainInit = true;
            
            if (Turret.Enabled) Turret.Enabled = false; Turret.Enabled = true;
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                base.OnRemovedFromScene();
                Platform.Parts.Entity = null;
                RegisterEvents(false);
                IsWorking = false;
                IsFunctional = false;
                StopAllSounds();
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
    }
}
