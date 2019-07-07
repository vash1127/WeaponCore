using System;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Audio;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using  WeaponCore.Support;
namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal void Shoot()
        {
            var session = Comp.MyAi.MySession;
            var tick = session.Tick;
            if (ShotCounter == 0 && _newCycle) _rotationTime = 0;
            _newCycle = false;
            var targetLock = Target != null;
            if (ShotCounter++ >= _ticksPerShot - 1) ShotCounter = 0;
            var bps = Kind.HardPoint.BarrelsPerShot;
            var skipAhead = Kind.HardPoint.SkipBarrels;
            if (AvCapable && (!PlayTurretAv || Comp.MyAi.MySession.Tick60))
                PlayTurretAv = Vector3D.DistanceSquared(session.CameraPos, Comp.MyPivotPos) < System.HardPointMaxSoundDistSqr;

            if (Kind.HardPoint.RotateBarrelAxis != 0) MovePart(-1 * bps);
            if (targetLock) _targetTick++;
            TicksUntilShoot++;

            if (ShotCounter != 0) return;
            if (!IsShooting) StartShooting();
            if (TicksUntilShoot >= Kind.HardPoint.DelayUntilFire) ShootGraphics();
            if (TicksUntilShoot < Kind.HardPoint.DelayUntilFire) return;

            if (!System.EnergyAmmo) CurrentAmmo--;

            var endBarrel = _numOfBarrels - 1;
            if (_shotsInCycle++ == (_numOfBarrels - 1))
            {
                _shotsInCycle = 0;
                _newCycle = true;
            }

            if (targetLock && _targetTick > 59)
            {
                _targetTick = 0;
                var targetPos = Target.PositionComp.GetPosition();
                if (Vector3D.DistanceSquared(targetPos, Comp.MyPivotPos) > System.MaxTrajectorySqr)
                {
                    Target = null;
                    return;
                }
                if (!TrackingAi && !ValidTarget(this, Target))
                {
                    Log.Line("shootStep2: setting target null");
                    Target = null;
                    return;
                }
                IHitInfo hitInfo;
                MyAPIGateway.Physics.CastRay(Comp.MyPivotPos, Target.PositionComp.GetPosition(), out hitInfo, 15);
                if (hitInfo?.HitEntity == null || (hitInfo.HitEntity != Target && hitInfo.HitEntity != Target.Parent))
                {
                    Log.Line($"rayFail, setting target to null");
                    Target = null;
                    return;
                }
            }

            for (int i = 0; i < bps; i++)
            {
                var current = NextMuzzle;
                var muzzle = Muzzles[current];
                var lastTick = muzzle.LastUpdateTick;
                var recentMovement = lastTick >= _posChangedTick && lastTick - _posChangedTick < 10;
                if (recentMovement || _posChangedTick > lastTick)
                {
                    var dummy = Dummies[current];
                    var newInfo = dummy.Info;
                    muzzle.Direction = newInfo.Direction;
                    muzzle.Position = newInfo.Position;
                    muzzle.LastUpdateTick = tick;
                }
                muzzle.LastShot = tick;
                var deviatedAngle = Kind.HardPoint.DeviateShotAngle;
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

                if (i == bps) NextMuzzle++;
                NextMuzzle = (NextMuzzle + (skipAhead + 1)) % (endBarrel + 1);
                lock (session.Projectiles.Wait[session.ProCounter])
                {
                    Projectile pro;
                    session.Projectiles.ProjectilePool[session.ProCounter].AllocateOrCreate(out pro);
                    pro.System = System;
                    pro.FiringCube = Comp.MyCube;
                    pro.Origin = muzzle.Position;
                    pro.PredictedTargetPos = TargetPos;
                    pro.Direction = muzzle.DeviatedDir;
                    pro.State = Projectile.ProjectileState.Start;
                    pro.Target = Target;

                    if (System.ModelId != -1)
                    {
                        MyEntity ent;
                        session.Projectiles.EntityPool[session.ProCounter][System.ModelId].AllocateOrCreate(out ent);
                        if (!ent.InScene)
                        {
                            ent.InScene = true;
                            ent.Render.AddRenderObjects();
                        }
                        pro.Entity = ent;
                    }
                }
                if (session.ProCounter++ >= session.Projectiles.Wait.Length - 1) session.ProCounter = 0;
            }
        }

        public void MovePart(int time)
        {
            BarrelMove = true;
            var radiansPerShot = (2 * Math.PI / _numOfBarrels);
            var radians = radiansPerShot / _timePerShot;
            var axis = Kind.HardPoint.RotateBarrelAxis;
            MatrixD rotationMatrix;
            if (axis == 1) rotationMatrix = MatrixD.CreateRotationX(radians * _rotationTime);
            else if (axis == 2 ) rotationMatrix = MatrixD.CreateRotationY(radians * _rotationTime);
            else if (axis == 3) rotationMatrix = MatrixD.CreateRotationZ(radians * _rotationTime);
            else return;

            _rotationTime += time;
            rotationMatrix.Translation = _localTranslation;
            EntityPart.PositionComp.LocalMatrix = rotationMatrix;
            BarrelMove = false;
            if (PlayTurretAv && RotateEmitter != null && !RotateEmitter.IsPlaying)
                StartRotateSound();
        }
    }
}
