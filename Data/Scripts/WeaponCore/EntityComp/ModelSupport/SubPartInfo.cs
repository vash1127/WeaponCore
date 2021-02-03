using VRage.Game.Entity;
using VRageMath;

namespace WeaponCore.Support
{
    class PartInfo
    {
        internal MyEntity Entity;
        internal MyEntity Parent;
        internal Matrix ToTransformation;
        internal Matrix FromTransformation;
        internal Matrix FullRotationStep;
        internal Matrix RevFullRotationStep;
        internal Matrix OriginalPosition;
        internal Vector3 PartLocalLocation;
        internal Vector3 RotationAxis;
    }
}
