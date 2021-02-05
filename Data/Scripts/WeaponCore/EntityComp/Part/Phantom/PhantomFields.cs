using System;
using System.Collections.Generic;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Phantoms : Part
    {
        internal Phantoms(CoreSystem system, CoreComponent comp, int partId)
        {
            base.Init(comp, system, partId);
        }
    }
}
