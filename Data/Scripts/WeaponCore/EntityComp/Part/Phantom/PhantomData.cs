using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    internal partial class Phantom : Part
    {
        internal class PhantomCompData : CompData
        {
            internal readonly PhantomComponent Comp;
            internal PhantomRepo Repo;

            internal PhantomCompData(PhantomComponent comp)
            {
                base.Init(comp);
                Comp = comp;
                Repo = (PhantomRepo)RepoBase;
            }

            internal void Load()
            {

            }
        }
    }
}
