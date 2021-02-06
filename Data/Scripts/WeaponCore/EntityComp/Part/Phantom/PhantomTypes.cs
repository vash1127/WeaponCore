using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Phantom : Part
    {
        internal class PhantomComponent : CoreComponent
        {

            internal PhantomComponent(Session session, MyEntity coreEntity)
            {
                base.Init(session, coreEntity);
            }
        }
    }
}
