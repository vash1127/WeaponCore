using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Upgrade : Part
    {
        internal class UpgradeComponent : CoreComponent
        {

            internal UpgradeComponent(Session session, MyEntity coreEntity)
            {
                base.Init(session, coreEntity);
            }
        }
    }
}
