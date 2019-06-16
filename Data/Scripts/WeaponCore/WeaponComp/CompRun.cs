using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game;
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
            if (MainInit) return;
            base.OnAddedToScene();
            _isServer = Session.Instance.IsServer;
            _isDedicated = Session.Instance.DedicatedServer;
            _mpActive = Session.Instance.MpActive;
            InitPlatform();
            Log.Line("added to scene");
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
            Platform = new MyWeaponPlatform(this);
            foreach (var t in Platform.Weapons) t.InitTracking();
            var ammoTypeCnt = Platform.Structure.AmmoToWeaponIds.Count;
            var gun = Gun.GunBase;
            var id = ammoTypeCnt == 0 ? Platform.Weapons[0].WeaponSystem.MagazineDef.Id 
                : Platform.Structure.AmmoToWeaponIds.First().Key;
            BlockInventory.Constraint.Clear();
            BlockInventory.Constraint.Add(id);
            gun.SwitchAmmoMagazine(id);

            StorageSetup();
            State.Value.Online = true;
            Turret.EnableIdleRotation = false;
            MultiInventory = ammoTypeCnt > 1;
            Physics = ((IMyCubeGrid)MyCube.CubeGrid).Physics;
            FullInventory = BlockInventory.CargoPercentage >= 0.5;

            RegisterEvents();
            MainInit = true;
        }

        public override void OnRemovedFromScene()
        {
            base.OnRemovedFromScene();
            Platform.SubParts.Entity = null;
            RegisterEvents(false);
            IsWorking = false;
            IsFunctional = false;
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
