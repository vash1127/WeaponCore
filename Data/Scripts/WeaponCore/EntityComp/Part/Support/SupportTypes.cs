using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    internal partial class SupportSys : Part
    {
        internal class SupportComponent : CoreComponent
        {

            internal SupportComponent(Session session, MyEntity coreEntity, MyDefinitionId id)
            {
                base.Init(session, coreEntity, true, ((MyCubeBlock)coreEntity).CubeGrid, id);
            }
        }
    }
}
