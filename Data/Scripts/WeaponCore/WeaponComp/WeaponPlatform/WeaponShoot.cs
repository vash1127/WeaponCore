using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
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
            var session = Comp.Ai.MySession;
            var tick = session.Tick;
            var bps = System.Values.HardPoint.Loading.BarrelsPerShot;

            if (ChargeAmtLeft > session.GridAvailablePower) return;

            if (System.BurstMode)
            {
                if (_shots > System.Values.HardPoint.Loading.ShotsInBurst)
                {
                    if (tick - _lastShotTick > System.Values.HardPoint.Loading.DelayAfterBurst) _shots = 1;
                    else
                    {
                        if (AvCapable && RotateEmitter != null && RotateEmitter.IsPlaying) StopRotateSound();
                        return;
                    }
                }
                _lastShotTick = tick;
            }

            if (AvCapable && (!PlayTurretAv || Comp.Ai.MySession.Tick60))
                PlayTurretAv = Vector3D.DistanceSquared(session.CameraPos, Comp.MyPivotPos) < System.HardPointSoundMaxDistSqr;

            if (System.BarrelAxisRotation) MovePart(-1 * bps);

            if (ShotCounter == 0 && _newCycle) _rotationTime = 0;
            if (ShotCounter++ >= _ticksPerShot - 1) ShotCounter = 0;

            _newCycle = false;

            _ticksUntilShoot++;
            if (ShotCounter != 0) return;
            _shots++;

            if (!IsShooting) StartShooting();

            if (_ticksUntilShoot < System.DelayToFire) return;

            if (_shotsInCycle++ == _numOfBarrels - 1)
            {
                _shotsInCycle = 0;
                _newCycle = true;
            }

            if (!Comp.Gunner  && !Casting && tick - Comp.LastRayCastTick > 59) ShootRayCheck();

            lock (session.Projectiles.Wait[session.ProCounter])
            {
                Projectile vProjectile = null;
                if (System.VirtualBeams) vProjectile = CreateVirtualProjectile();

                var isStatic = Comp.Physics.IsStatic;
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

                    if (!System.EnergyAmmo)
                    {
                        if (CurrentAmmo == 0) continue;
                        CurrentAmmo--;
                    }

                    if (System.HasBackKickForce && !isStatic)
                        Comp.MyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, -muzzle.Direction * System.Values.Ammo.BackKickForce, muzzle.Position, Vector3D.Zero);

                    muzzle.LastShot = tick;
                    if (PlayTurretAv) BarrelAvUpdater.Add(muzzle, tick, true);
                    for (int j = 0; j < System.Values.HardPoint.Loading.TrajectilesPerBarrel; j++)
                    {
                        if (System.Values.HardPoint.DeviateShotAngle > 0)
                        {
                            var dirMatrix = Matrix.CreateFromDir(muzzle.Direction);
                            var randomFloat1 = MyUtils.GetRandomFloat(-System.Values.HardPoint.DeviateShotAngle, System.Values.HardPoint.DeviateShotAngle);
                            var randomFloat2 = MyUtils.GetRandomFloat(0.0f, 6.283185f);

                            muzzle.DeviatedDir = Vector3.TransformNormal(-new Vector3(
                                    MyMath.FastSin(randomFloat1) * MyMath.FastCos(randomFloat2),
                                    MyMath.FastSin(randomFloat1) * MyMath.FastSin(randomFloat2),
                                    MyMath.FastCos(randomFloat1)), dirMatrix);
                        }
                        else muzzle.DeviatedDir = muzzle.Direction;

                        if (System.VirtualBeams && j == 0)
                        {
                            Trajectile t;
                            MyEntity e = null;
                            Comp.Ai.MySession.Projectiles.TrajectilePool[Comp.Ai.MySession.ProCounter].AllocateOrCreate(out t);
                            if (System.ModelId != -1)
                            {
                                MyEntity ent;
                                session.Projectiles.EntityPool[session.ProCounter][System.ModelId].AllocateOrCreate(out ent);
                                if (!ent.InScene)
                                {
                                    ent.InScene = true;
                                    ent.Render.AddRenderObjects();
                                }
                                e = ent;
                            }
                            t.InitVirtual(System, Comp.MyCube, e, WeaponId, muzzle.MuzzleId, muzzle.Position, muzzle.DeviatedDir);
                            vProjectile.VrTrajectiles.Add(t);
                            if (System.RotateRealBeam && i == _nextVirtual)
                            {
                                vProjectile.Origin = muzzle.Position;
                                vProjectile.Direction = muzzle.DeviatedDir;
                            }
                        }
                        else
                        {
                            Projectile pro;
                            session.Projectiles.ProjectilePool[session.ProCounter].AllocateOrCreate(out pro);
                            pro.Trajectile.System = System;
                            pro.Trajectile.FiringCube = Comp.MyCube;
                            pro.Origin = muzzle.Position;
                            pro.OriginUp = Comp.MyPivotUp;
                            pro.PredictedTargetPos = TargetPos;
                            pro.Direction = muzzle.DeviatedDir;
                            pro.State = Projectile.ProjectileState.Start;
                            pro.Ai = Comp.Ai;
                            pro.Trajectile.WeaponId = WeaponId;
                            pro.Trajectile.MuzzleId = muzzle.MuzzleId;
                            pro.Target.Entity = Target.Entity;
                            pro.DamageFrame = DamageFrame;
                            if (System.ModelId != -1)
                            {
                                MyEntity ent;
                                session.Projectiles.EntityPool[session.ProCounter][System.ModelId].AllocateOrCreate(out ent);
                                if (!ent.InScene)
                                {
                                    ent.InScene = true;
                                    ent.Render.AddRenderObjects();
                                }
                                pro.Trajectile.Entity = ent;
                            }
                        }
                    }

                    CurrentHeat += System.HeatPShot;
                    ChargeAmtLeft = RequiredPower;

                    if (CurrentHeat > System.MaxHeat) Overheated = true;

                    if (i == bps) NextMuzzle++;

                    NextMuzzle = (NextMuzzle + (System.Values.HardPoint.Loading.SkipBarrels + 1)) % _numOfBarrels;
                }
                _nextVirtual = _nextVirtual + 1 < bps ? _nextVirtual + 1 : 0;
                if (session.ProCounter++ >= session.Projectiles.Wait.Length - 1) session.ProCounter = 0;
            }
        }

        private Projectile CreateVirtualProjectile()
        {
            DamageFrame.VirtualHit = false;
            DamageFrame.Hits = 0;
            DamageFrame.HitEntity.Entity = null;
            Projectile pro;
            Comp.Ai.MySession.Projectiles.ProjectilePool[Comp.Ai.MySession.ProCounter].AllocateOrCreate(out pro);
            pro.Trajectile.System = System;
            pro.Trajectile.FiringCube = Comp.MyCube;
            pro.Origin = Comp.MyPivotPos;
            pro.OriginUp = Comp.MyPivotUp;
            pro.PredictedTargetPos = TargetPos;
            pro.Direction = Comp.MyPivotDir;
            pro.State = Projectile.ProjectileState.Start;
            pro.Ai = Comp.Ai;
            pro.Trajectile.WeaponId = WeaponId;
            pro.Trajectile.MuzzleId = -1;
            pro.Target.Entity = Target.Entity;
            pro.DamageFrame = DamageFrame;
            return pro;
        }

        private void ShootRayCheck()
        {
            Comp.LastRayCastTick = Comp.Ai.MySession.Tick;
            var masterWeapon = TrackTarget ? this : Comp.TrackingWeapon;
            if (Target.Entity == null || Target.Entity.MarkedForClose || Target.TopEntityId != Target.Entity.GetTopMostParent().EntityId)
            {
                Log.Line($"{System.WeaponName} - ShootRayCheckFail - target null or marked - Null:{Target.Entity == null} - Marked:{Target.Entity?.MarkedForClose} - IdMisMatch:{Target.TopEntityId != Target.Entity?.GetTopMostParent()?.EntityId} - OldId:{Target.TopEntityId} - Id:{Target.Entity?.GetTopMostParent()?.EntityId}");
                masterWeapon.TargetExpired = true;
                if (masterWeapon != this) TargetExpired = true;
                return;
            }

            var targetPos = Target.Entity.PositionComp.WorldMatrix.Translation;
            if (Vector3D.DistanceSquared(targetPos, Comp.MyPivotPos) > System.MaxTrajectorySqr)
            {
                Log.Line($"{System.WeaponName} - ShootRayCheck Fail - out of range");
                masterWeapon.TargetExpired = true;
                if (masterWeapon !=  this) TargetExpired = true;
                return;
            }
            if (!TrackingAi && !ValidTarget(this, Target.Entity))
            {
                Log.Line($"{System.WeaponName} - ShootRayCheck Fail - not trackingAi and notValid target");
                masterWeapon.TargetExpired = true;
                if (masterWeapon != this) TargetExpired = true;
                return;
            }
            Casting = true;
            MyAPIGateway.Physics.CastRayParallel(ref Comp.MyPivotPos, ref targetPos, CollisionLayers.DefaultCollisionLayer, ShootRayCheckCallBack);
        }

        public void ShootRayCheckCallBack(IHitInfo hitInfo)
        {
            Casting = false;
            var masterWeapon = TrackTarget ? this : Comp.TrackingWeapon;
            if (hitInfo?.HitEntity == null || (hitInfo.HitEntity != Target.Entity && hitInfo.HitEntity != Target.Entity.Parent))
            {
                if (hitInfo?.HitEntity == null && DelayCeaseFire)
                {
                    Log.Line($"{System.WeaponName} - ShootRayCheck Sucess - due to null DelayCeaseFire");
                    return;
                }
                if ((hitInfo?.HitEntity != null))
                {
                    var rootAsGrid = hitInfo.HitEntity as MyCubeGrid;
                    var parentAsGrid = hitInfo.HitEntity?.Parent as MyCubeGrid;
                    if (rootAsGrid == null && parentAsGrid == null)
                    {
                        Log.Line($"{System.WeaponName} - ShootRayCheck Success - junk: {((MyEntity)hitInfo.HitEntity).DebugName}");
                        return;
                    }

                    var grid = parentAsGrid ?? rootAsGrid;
                    if (grid == Comp.MyGrid)
                    {
                        //Session.Instance.RayCheckLines.Add(new LineD(Comp.MyPivotPos, hitInfo.Position), Comp.Ai.MySession.Tick, true);
                        Log.Line($"{System.WeaponName} - ShootRayCheck failure - own grid: {grid?.DebugName}");
                        masterWeapon.TargetExpired = true;
                        if (masterWeapon != this) TargetExpired = true;
                        return;
                    }

                    if (!GridAi.GridEnemy(Comp.MyCube, grid))
                    {
                        if (!grid.IsInSameLogicalGroupAs(Comp.MyGrid))
                        {
                            Log.Line($"{System.WeaponName} - ShootRayCheck fail - friendly grid: {grid?.DebugName} - {grid?.DebugName}");
                            masterWeapon.TargetExpired = true;
                            if (masterWeapon != this) TargetExpired = true;
                        }
                        return;
                    }
                    Log.Line($"{System.WeaponName} - ShootRayCheck Success - non-friendly target in the way of primary target, shoot through: {((MyEntity)hitInfo.HitEntity).DebugName}");
                    return;
                }
                if (hitInfo?.HitEntity == null) Log.Line($"{System.WeaponName} - ShootRayCheck Fail - null");
                else if (hitInfo.HitEntity != null) Log.Line($"{System.WeaponName} - ShootRayCheck Fail - General: {((MyEntity)hitInfo.HitEntity).DebugName}");
                else Log.Line($"{System.WeaponName} - ShootRayCheck fail - Unknown");
                masterWeapon.TargetExpired = true;
                if (masterWeapon != this) TargetExpired = true;
            }
            else if (System.SortBlocks)
            {
                var grid = hitInfo.HitEntity as MyCubeGrid;
                if (grid != null && Target.Entity.GetTopMostParent() == grid)
                {
                    var maxChange = hitInfo.HitEntity.PositionComp.LocalAABB.HalfExtents.Min();
                    var targetPos = Target.Entity.PositionComp.WorldMatrix.Translation;
                    var weaponPos = Comp.MyPivotPos;

                    double rayDist;
                    Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var newHitShortDist = rayDist * (1 - hitInfo.Fraction);
                    var distanceToTarget = rayDist * hitInfo.Fraction;

                    var shortDistExceed = newHitShortDist - Target.HitShortDist > maxChange;
                    var escapeDistExceed = distanceToTarget - Target.OrigDistance > Target.OrigDistance;
                    if (shortDistExceed || escapeDistExceed)
                    {
                        masterWeapon.TargetExpired = true;
                        if (masterWeapon != this) TargetExpired = true;
                        if (shortDistExceed) Log.Line($"{System.WeaponName} - ShootRayCheck fail - Distance to sorted block exceeded");
                        else Log.Line($"{System.WeaponName} - ShootRayCheck fail - Target distance to escape has been met - {distanceToTarget} - {Target.OrigDistance} -{distanceToTarget - Target.OrigDistance} > {Target.OrigDistance}");
                    }
                }
            }
        }

        public void MovePart(int time)
        {
            BarrelMove = true;
            double radiansPerShot;
            if (System.DegROF && CurrentHeat > (System.MaxHeat * .8)) _timePerShot = (3600d / System.Values.HardPoint.Loading.RateOfFire) / (CurrentHeat / System.MaxHeat);
            if (_timePerShot > 0.999999 && _timePerShot < 1.000001) radiansPerShot = 0.06666666666;
            else radiansPerShot = 2 * Math.PI / _numOfBarrels;
            var radians = radiansPerShot / _timePerShot;
            var axis = System.Values.HardPoint.RotateBarrelAxis;
            MatrixD rotationMatrix;
            if (axis == 1) rotationMatrix = MatrixD.CreateRotationX(radians * _rotationTime);
            else if (axis == 2) rotationMatrix = MatrixD.CreateRotationY(radians * _rotationTime);
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
