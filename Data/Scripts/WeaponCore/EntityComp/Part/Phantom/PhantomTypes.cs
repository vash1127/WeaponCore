using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    internal partial class Phantom : Part
    {
        internal class PhantomComponent : CoreComponent
        {
            internal PhantomCompData Data;
            internal PhantomComponent(Session session, MyEntity coreEntity, MyDefinitionId id)
            {
                Data = new PhantomCompData(this);
                base.Init(session, coreEntity, false, coreEntity, id);
            }
        }
    }
}
