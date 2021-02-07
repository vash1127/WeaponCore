using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    internal partial class Support : Part
    {
        internal class SupportCompData : CompData
        {
            internal readonly SupportSys.SupportComponent Comp;
            internal SupportRepo Repo;

            internal SupportCompData(SupportSys.SupportComponent comp)
            {
                base.Init(comp);
                Comp = comp;
                Repo = (SupportRepo)RepoBase;
            }

            internal void Load()
            {

            }
        }
    }
}
