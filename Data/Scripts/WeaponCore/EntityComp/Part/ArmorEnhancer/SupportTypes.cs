using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class SupportSys : Part
    {
        internal class SupportComponent : CoreComponent
        {

            internal SupportComponent(Session session, MyEntity coreEntity)
            {
                base.Init(session, coreEntity);
            }
        }
    }
}
