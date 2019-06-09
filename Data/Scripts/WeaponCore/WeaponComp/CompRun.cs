using System;
using Sandbox.ModAPI;
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
            RegisterEvents(true);
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
            StorageSetup();
            State.Value.Online = true;
            Turret.EnableIdleRotation = false;
            Physics = ((IMyCubeGrid)MyCube.CubeGrid).Physics;
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
