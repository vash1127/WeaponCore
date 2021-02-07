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
    }
}
