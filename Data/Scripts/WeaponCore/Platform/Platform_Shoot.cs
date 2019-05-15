using System;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {

        internal void Shoot()
        {
            var tick = Session.Instance.Tick;
            var rotateAxis = WeaponType.RotateBarrelAxis;
            var radiansPerShot = (2 * Math.PI / _numOfBarrels);
            var radiansPerTick = radiansPerShot / _timePerShot;
            if (ShotCounter == 0 && _newCycle) _rotationTime = 0;
            _newCycle = false;

            if (ShotCounter++ >= _ticksPerShot - 1) ShotCounter = 0;

            var bps = WeaponType.BarrelsPerShot;
            var skipAhead = WeaponType.SkipBarrels;

            if (rotateAxis != 0) MovePart(radiansPerTick, -1 * bps, rotateAxis == 1, rotateAxis == 2, rotateAxis == 3);

            if (ShotCounter != 0) return;

            var endBarrel = _numOfBarrels - 1;
            var updatePos = _posChangedTick > _posUpdatedTick;

            if (_shotsInCycle++ == (_numOfBarrels - 1))
            {
                _shotsInCycle = 0;
                _newCycle = true;
            }

            if (updatePos)
            {
                for (int j = 0; j < _numOfBarrels; j++)
                {
                    var muzzle = Muzzles[j];
                    var dummy = Dummies[j];
                    var newInfo = dummy.Info;
                    muzzle.Direction = newInfo.Direction;
                    muzzle.Position = newInfo.Position;
                    muzzle.LastPosUpdate = tick;
                }
            }

            for (int i = 0; i < bps; i++)
            {
                var current = _nextMuzzle;
                Muzzles[current].LastShot = tick;

                if (i == bps - 1) _nextMuzzle++;
                _nextMuzzle = (_nextMuzzle + (skipAhead + 1)) % (endBarrel + 1);
            }

            if (tick - _posChangedTick > 10) _posUpdatedTick = tick;
        }

        public void MovePart(double radians, int time, bool xAxis, bool yAxis, bool zAxis)
        {
            MatrixD rotationMatrix;
            if (xAxis) rotationMatrix = MatrixD.CreateRotationX(radians * _rotationTime);
            else if (yAxis) rotationMatrix = MatrixD.CreateRotationY(radians * _rotationTime);
            else if (zAxis) rotationMatrix = MatrixD.CreateRotationZ(radians * _rotationTime);
            else return;

            _rotationTime += time;
            rotationMatrix.Translation = _localTranslation;
            EntityPart.PositionComp.LocalMatrix = rotationMatrix;
        }
    }
}
