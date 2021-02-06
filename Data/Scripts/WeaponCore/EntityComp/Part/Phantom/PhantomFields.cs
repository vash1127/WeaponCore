using System;
using System.Collections.Generic;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Phantoms : Part
    {
        internal readonly Phantom.PhantomComponent Comp;

        internal Phantoms(CoreSystem system, Phantom.PhantomComponent comp, int partId)
        {
            Comp = comp;
            Log.Line($"init Phantoms: {system.PartName}");

            base.Init(comp, system, partId);
        }
    }
}
