using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Phantom : Part
    {
        internal class PhantomComponent : CoreComponent
        {

            internal PhantomComponent(Session session, MyEntity coreEntity, MyDefinitionId id)
            {
                base.Init(session, coreEntity, false, coreEntity, id);
            }
        }
    }
}
