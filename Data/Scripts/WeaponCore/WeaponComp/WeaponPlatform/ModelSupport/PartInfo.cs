using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Entity;
using VRageMath;

namespace WeaponCore.Support
{
    class PartInfo
    {
        internal MyEntity Entity;
        internal Matrix ToTransformation;
        internal Matrix FromTransformation;
        internal Matrix FullRotationStep;
        internal Matrix RevFullRotationStep;
        internal Vector3 PartLocalLocation;
    }
}
