using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    using System;
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI;

    public class CompData
    {
        public CoreComponent BaseComp;
        public ProtoRepo ProtoRepoBase;

        public void Init (CoreComponent comp)
        {
            BaseComp = comp;
        }

        public void StorageInit()
        {
            if (BaseComp.CoreEntity.Storage == null) 
            {
                BaseComp.CoreEntity.Storage = new MyModStorageComponent { [BaseComp.Session.CompDataGuid] = "" };
            }
        }

        public void Save()
        {
            if (BaseComp.CoreEntity.Storage == null) return;

            if (ProtoRepoBase != null)
            {
                var binary = MyAPIGateway.Utilities.SerializeToBinary(ProtoRepoBase);
                BaseComp.CoreEntity.Storage[BaseComp.Session.CompDataGuid] = Convert.ToBase64String(binary);
            }

        }
        public enum DataState
        {
            Load,
            Reset,
        }

        public void DataManager (DataState change)
        {
            switch (BaseComp.Type)
            {
                case CoreComponent.CompType.Upgrade:
                    ((Upgrade.UpgradeComponent)BaseComp).Data.Change(change);
                    break;
                case CoreComponent.CompType.Support:
                    ((SupportSys.SupportComponent)BaseComp).Data.Change(change);
                    break;
                case CoreComponent.CompType.Phantom:
                    ((Phantom.PhantomComponent)BaseComp).Data.Change(change);
                    break;
                case CoreComponent.CompType.Weapon:
                    ((Weapon.WeaponComponent)BaseComp).Data.Change(change);
                    break;
            }
        }
    }
}
