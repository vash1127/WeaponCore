using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    internal partial class Upgrade : Part
    {
        internal class UpgradeCompData : CompData
        {
            internal UpgradeComponent Comp;
            internal UpgradeCompData(UpgradeComponent comp)
            {
                Comp = comp;
                base.Init(comp);
            }

            internal void Load()
            {

            }
        }
    }
}
