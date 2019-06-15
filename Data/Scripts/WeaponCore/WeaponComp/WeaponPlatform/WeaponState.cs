using VRage.Game.Components;
using VRageMath;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        public void PositionChanged(MyPositionComponentBase pComp)
        {
            _posChangedTick = Session.Instance.Tick;
            if (TrackingAi) Comp.UpdatePivotPos(this, _upPivotOffsetLen);
        }

        public class Muzzle
        {
            public Vector3D Position;
            public Vector3D Direction;
            public Vector3D DeviatedDir;
            public uint LastShot;
            public uint LastUpdateTick;
        }
    }
}
