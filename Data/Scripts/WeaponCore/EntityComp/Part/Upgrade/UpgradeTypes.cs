using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    internal partial class Upgrade : Part
    {
        internal class UpgradeComponent : CoreComponent
        {
            internal UpgradeCompData Data;

            internal UpgradeComponent(Session session, MyEntity coreEntity, MyDefinitionId id)
            {
                Data = new UpgradeCompData(this);
                base.Init(session, coreEntity, true, ((MyCubeBlock)coreEntity).CubeGrid, id);
            }
        }
    }
}
