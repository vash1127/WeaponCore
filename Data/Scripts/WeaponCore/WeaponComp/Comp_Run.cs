using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
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
            base.OnAddedToScene();
            _isServer = Session.Instance.IsServer;
            _isDedicated = Session.Instance.DedicatedServer;
            _mpActive = Session.Instance.MpActive;
            RegisterEvents(true);
            Targeting = MyCube.CubeGrid.Components.Get<MyGridTargeting>();
            _subTypeIdHash = MyStringHash.GetOrCompute(Turret.BlockDefinition.SubtypeId);
            Log.Line("added to scene");
        }

        public void InitPlatform()
        {
            Platform = new MyWeaponPlatform(_subTypeIdHash, this);
            StorageSetup();
            State.Value.Online = true;
            Log.Line("Platform initted");
        }

        public override void OnRemovedFromScene()
        {
            base.OnRemovedFromScene();
            Log.Line("removed from scene");
        }

        public override bool IsSerialized()
        {
            return true;
        }

        public override string ComponentTypeDebugString
        {
            get { return "Shield"; }
        }
    }
}
