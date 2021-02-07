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
            internal SupportProtoRepo ProtoRepo;

            internal SupportCompData(SupportSys.SupportComponent comp)
            {
                base.Init(comp);
                Comp = comp;
                ProtoRepo = (SupportProtoRepo)ProtoRepoBase;
            }

            internal void Load()
            {

            }
        }
    }
}
