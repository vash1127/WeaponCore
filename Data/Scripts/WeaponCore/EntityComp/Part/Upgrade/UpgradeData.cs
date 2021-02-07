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
            internal ProtoUpgradeRepo ProtoRepo;

            internal UpgradeCompData(UpgradeComponent comp)
            {
                base.Init(comp);
                Comp = comp;
                ProtoRepo = (ProtoUpgradeRepo)ProtoRepoBase;
            }

            internal void Load()
            {

            }
        }
    }
}
