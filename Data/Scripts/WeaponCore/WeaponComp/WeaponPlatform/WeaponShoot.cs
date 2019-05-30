using System;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal void Shoot()
        {
            var session = Session.Instance;
            var tick = session.Tick;
            var rotateAxis = WeaponType.RotateBarrelAxis;
            var radiansPerShot = (2 * Math.PI / _numOfBarrels);
            var radiansPerTick = radiansPerShot / _timePerShot;
            if (ShotCounter == 0 && _newCycle) _rotationTime = 0;
            _newCycle = false;

            if (ShotCounter++ >= _ticksPerShot - 1) ShotCounter = 0;

            var bps = WeaponType.BarrelsPerShot;
            var skipAhead = WeaponType.SkipBarrels;

            if (rotateAxis != 0) MovePart(radiansPerTick, -1 * bps, rotateAxis == 1, rotateAxis == 2, rotateAxis == 3);

            _targetTick++;
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
            if (_targetTick > 59)
            {
                _targetTick = 0;
                IHitInfo hitInfo;
                MyAPIGateway.Physics.CastRay(EntityPart.PositionComp.WorldAABB.Center, Target.PositionComp.GetPosition(), out hitInfo, 15);
                if (hitInfo?.HitEntity == null || (hitInfo.HitEntity != Target && hitInfo.HitEntity != Target?.Parent))
                {
                    Target = null;
                    return;
                }
            }
            for (int i = 0; i < bps; i++)
            {
                var current = _nextMuzzle;
                var muzzle = Muzzles[current];
                muzzle.LastShot = tick;

                var deviatedAngle = WeaponType.DeviateShotAngle;
                if (deviatedAngle > 0)
                {
                    var dirMatrix = Matrix.CreateFromDir(muzzle.Direction);
                    var randomFloat1 = MyUtils.GetRandomFloat(-deviatedAngle, deviatedAngle);
                    var randomFloat2 = MyUtils.GetRandomFloat(0.0f, 6.283185f);

                    muzzle.DeviatedDir = Vector3.TransformNormal(
                        -new Vector3(MyMath.FastSin(randomFloat1) * MyMath.FastCos(randomFloat2),
                            MyMath.FastSin(randomFloat1) * MyMath.FastSin(randomFloat2),
                            MyMath.FastCos(randomFloat1)), dirMatrix);
                }
                else muzzle.DeviatedDir = muzzle.Direction;

                if (i == bps - 1) _nextMuzzle++;
                _nextMuzzle = (_nextMuzzle + (skipAhead + 1)) % (endBarrel + 1);

                lock (session.Projectiles.Wait[session.ProCounter])
                {
                    Projectile pro;
                    session.Projectiles.ProjectilePool[session.ProCounter].AllocateOrCreate(out pro);
                    pro.Weapon = this;
                    pro.Origin = muzzle.Position;
                    pro.Direction = muzzle.DeviatedDir;
                    pro.State = Projectile.ProjectileState.Start;
                }
                if (session.ProCounter++ >= session.Projectiles.Wait.Length - 1) session.ProCounter = 0;
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
