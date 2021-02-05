using System;
using System.Collections.Generic;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Armor : Part
    {
        internal Armor(CoreSystem system, CoreComponent comp, int partId)
        {
            base.Init(comp, system, partId);
        }
    }
}
