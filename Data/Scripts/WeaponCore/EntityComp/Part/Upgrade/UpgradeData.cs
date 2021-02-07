using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    internal partial class Upgrade : Part
    {
        internal class UpgradeCompData : CompData
        {
            internal readonly UpgradeComponent Comp;
            internal UpgradeRepo Repo;

            internal UpgradeCompData(UpgradeComponent comp)
            {
                base.Init(comp);
                Comp = comp;
                Repo = (UpgradeRepo)RepoBase;
            }

            internal void Load()
            {

            }
        }
    }
}
