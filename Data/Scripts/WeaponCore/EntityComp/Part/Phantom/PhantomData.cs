using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Phantom : Part
    {
        internal class PhantomCompData : CompData
        {
            internal readonly PhantomComponent Comp;
            internal ProtoPhantomRepo Repo;

            internal PhantomCompData(PhantomComponent comp)
            {
                base.Init(comp);
                Comp = comp;
                Repo = (ProtoPhantomRepo)ProtoRepoBase;
            }

            internal void Load()
            {

            }
        }
    }
}
