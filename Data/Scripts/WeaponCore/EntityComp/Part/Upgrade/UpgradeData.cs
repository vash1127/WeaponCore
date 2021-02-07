using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Upgrade : Part
    {
        internal class UpgradeCompData : CompData
        {
            internal readonly UpgradeComponent Comp;
            internal ProtoUpgradeRepo Repo;

            internal UpgradeCompData(UpgradeComponent comp)
            {
                base.Init(comp);
                Comp = comp;
                Repo = (ProtoUpgradeRepo)ProtoRepoBase;
            }

            internal void Load()
            {

            }
        }
    }
}
