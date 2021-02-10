using CoreSystems.Support;

namespace CoreSystems.Platform
{
    public partial class Phantom : Part
    {
        internal readonly PhantomComponent Comp;
        internal ProtoPhantomPartState PartState;

        internal Phantom(PhantomSystem system, PhantomComponent comp, int partId)
        {
            Comp = comp;
            Log.Line($"init Phantom: {system.PartName}");

            Init(comp, system, partId);
        }
    }
}
