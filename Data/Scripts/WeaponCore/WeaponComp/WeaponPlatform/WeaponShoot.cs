using System;
using Sandbox.Game.Entities;
using VRage;
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
            if (Comp.Ai.MyGrid.MarkedForClose) Log.Line("grid marked");

            var session = Comp.Ai.Session;
            var tick = session.Tick;
            var bps = System.Values.HardPoint.Loading.BarrelsPerShot;
            if (System.BurstMode)
            {
                if (_shots > System.Values.HardPoint.Loading.ShotsInBurst)
                {
                    if (tick - _lastShotTick > System.Values.HardPoint.Loading.DelayAfterBurst)
                    {
                        _shots = 1;
                        EventTriggerStateChanged(EventTriggers.BurstReload,false);
                    }

                    else
                    {
                        EventTriggerStateChanged(EventTriggers.BurstReload, true);
                        if (AvCapable && RotateEmitter != null && RotateEmitter.IsPlaying) StopRotateSound();
                        return;
                    }
                }
                _lastShotTick = tick;
            }

            if (AvCapable && (!PlayTurretAv || session.Tick60))
                PlayTurretAv = Vector3D.DistanceSquared(session.CameraPos, MyPivotPos) < System.HardPointAvMaxDistSqr;

            if (System.BarrelAxisRotation)
            {
                MuzzlePart.Item1.PositionComp.LocalMatrix *= BarrelRotationPerShot;

                if (PlayTurretAv && RotateEmitter != null && !RotateEmitter.IsPlaying)
                    StartRotateSound();
            }

            if (ShotCounter == 0 && _newCycle)
                _newCycle = false;

            if (ShotCounter++ >= TicksPerShot - 1) ShotCounter = 0;

            _ticksUntilShoot++;
            if (ShotCounter != 0) return;

            if (!IsShooting) StartShooting();

            if(_ticksUntilShoot < FirstFireDelay) return;

            FirstFireDelay = 0;

            if (_ticksUntilShoot < System.DelayToFire)
            {
                EventTriggerStateChanged(EventTriggers.PreFire, true);
                return;
            }
            if (System.DelayToFire > 0)
                EventTriggerStateChanged(EventTriggers.PreFire, false);

            _shots++;

            if (_shotsInCycle++ == _numOfBarrels - 1)
            {
                _shotsInCycle = 0;
                _newCycle = true;
            }
            var userControlled = Comp.Gunner || ManualShoot != TerminalActionState.ShootOff;
            if (!userControlled && !Casting && tick - Comp.LastRayCastTick > 89 && Target != null && !DelayCeaseFire) ShootRayCheck();

            if (Comp.Ai.VelocityUpdateTick != tick)
            {
                Comp.Ai.GridVel = Comp.Ai.MyGrid.Physics.LinearVelocity;
                Comp.Ai.VelocityUpdateTick = tick;
            }

            lock (session.Projectiles.Wait[session.ProCounter])
            {
                Projectile vProjectile = null;
                var targetAiCnt = Comp.Ai.TargetAis.Count;
                var targetable = System.Values.Ammo.Health > 0 && !System.IsBeamWeapon;
                if (System.VirtualBeams) vProjectile = CreateVirtualProjectile();
                var isStatic = Comp.Ai.MyGrid.Physics.IsStatic;
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
                        Comp.Ai.MyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, -muzzle.Direction * System.Values.Ammo.BackKickForce, muzzle.Position, Vector3D.Zero);

                    muzzle.LastShot = tick;
                    if (PlayTurretAv) BarrelAvUpdater.Add(muzzle, tick, true);
                    for (int j = 0; j < System.Values.HardPoint.Loading.TrajectilesPerBarrel; j++)
                    {
                        if (System.Values.HardPoint.DeviateShotAngle > 0)
                        {
                            var dirMatrix = Matrix.CreateFromDir(muzzle.Direction);
                            var randomFloat1 = MyUtils.GetRandomFloat(-System.Values.HardPoint.DeviateShotAngle, System.Values.HardPoint.DeviateShotAngle);
                            var randomFloat2 = MyUtils.GetRandomFloat(0.0f, MathHelper.TwoPi);

                            muzzle.DeviatedDir = Vector3.TransformNormal(-new Vector3(
                                    MyMath.FastSin(randomFloat1) * MyMath.FastCos(randomFloat2),
                                    MyMath.FastSin(randomFloat1) * MyMath.FastSin(randomFloat2),
                                    MyMath.FastCos(randomFloat1)), dirMatrix);
                        }
                        else muzzle.DeviatedDir = muzzle.Direction;

                        if (System.VirtualBeams && j == 0)
                        {
                            MyEntity primeE = null;
                            MyEntity triggerE = null;

                            Trajectile t;
                            session.Projectiles.TrajectilePool[session.ProCounter].AllocateOrCreate(out t);
                            if (System.PrimeModelId != -1)
                            {
                                MyEntity ent;
                                session.Projectiles.EntityPool[session.ProCounter][System.PrimeModelId].AllocateOrCreate(out ent);
                                if (!ent.InScene)
                                {
                                    ent.InScene = true;
                                    ent.Render.AddRenderObjects();
                                }
                                primeE = ent;
                            }

                            if (System.TriggerModelId != -1)
                            {
                                MyEntity ent;
                                session.Projectiles.EntityPool[session.ProCounter][System.TriggerModelId].AllocateOrCreate(out ent);
                                if (!ent.InScene)
                                {
                                    //ent.InScene = false;
                                    //ent.Render.AddRenderObjects();
                                }
                                triggerE = ent;
                            }

                            t.InitVirtual(System, Comp.Ai, primeE, triggerE, Target, WeaponId, muzzle.MuzzleId, muzzle.Position, muzzle.DeviatedDir);
                            vProjectile.VrTrajectiles.Add(t);

                            if (System.RotateRealBeam && i == _nextVirtual)
                            {
                                vProjectile.T.Origin = muzzle.Position;
                                vProjectile.Direction = muzzle.DeviatedDir;
                            }
                        }
                        else
                        {
                            Projectile p;
                            session.Projectiles.ProjectilePool[session.ProCounter].AllocateOrCreate(out p);
                            p.T.System = System;
                            p.T.Ai = Comp.Ai;
                            p.T.Target.Entity = Target.Entity;
                            p.T.Target.Projectile = Target.Projectile;
                            p.T.Target.IsProjectile = Target.Projectile != null;
                            p.T.Target.FiringCube = Comp.MyCube;
                            p.T.WeaponId = WeaponId;
                            p.T.MuzzleId = muzzle.MuzzleId;
                            p.T.BaseDamagePool = BaseDamage;
                            p.T.EnableGuidance = Comp.Set.Value.Guidance;
                            p.T.DetonationDamage = DetonateDmg;
                            p.T.AreaEffectDamage = AreaEffectDmg;

                            p.SelfDamage = System.SelfDamage || Comp.Gunner;
                            p.GridVel = Comp.Ai.GridVel;
                            p.T.Origin = muzzle.Position;
                            p.T.OriginUp = MyPivotUp;
                            p.PredictedTargetPos = TargetPos;
                            p.Direction = muzzle.DeviatedDir;
                            p.State = Projectile.ProjectileState.Start;

                            if (System.PrimeModelId != -1)
                            {
                                MyEntity ent;
                                session.Projectiles.EntityPool[session.ProCounter][System.PrimeModelId].AllocateOrCreate(out ent);
                                if (!ent.InScene)
                                {
                                    ent.InScene = true;
                                    ent.Render.AddRenderObjects();
                                }
                                p.T.PrimeEntity = ent;
                            }
                            if (System.TriggerModelId != -1)
                            {
                                MyEntity ent;
                                session.Projectiles.EntityPool[session.ProCounter][System.TriggerModelId].AllocateOrCreate(out ent);
                                ent.InScene = false;
                                ent.Render.RemoveRenderObjects();
                                p.T.TriggerEntity = ent;
                            }
                            if (targetable)
                            {
                                for (int t = 0; t < targetAiCnt; t++)
                                {
                                    var targetAi = Comp.Ai.TargetAis[t];
                                    if (System.Values.Ammo.Trajectory.Guidance == AmmoTrajectory.GuidanceType.None || Comp.Set.Value.Guidance)
                                    {
                                        var threatLin = targetAi.GridVel;

                                        bool intercept;
                                        if (Vector3D.IsZero(threatLin, 0.025)) intercept = Vector3.Dot(p.Direction, p.Position - targetAi.MyGrid.PositionComp.WorldMatrix.Translation) < 0;
                                        else intercept = Vector3.Dot(threatLin, targetAi.MyGrid.PositionComp.WorldMatrix.Translation - TargetPos) < 0;

                                        if (!intercept) continue;
                                    }
                                    targetAi.LiveProjectile.Add(p);
                                    p.Watchers.Add(targetAi);
                                }
                            }
                        }
                    }

                    _muzzlesToFire.Add(MuzzleIdToName[current]);

                    if (Comp.State.Value.Weapons[WeaponId].Heat <= 0 && Comp.State.Value.Weapons[WeaponId].Heat + HeatPShot > 0)
                        Comp.Ai.Session.UpdateWeaponHeat(MyTuple.Create(this, 0, true));

                    Comp.State.Value.Weapons[WeaponId].Heat += HeatPShot;
                    Comp.CurrentHeat += HeatPShot;
                    if (Comp.State.Value.Weapons[WeaponId].Heat > System.MaxHeat)
                    {
                        EventTriggerStateChanged(EventTriggers.Overheated, true);
                        Comp.Overheated = true;
                        StopShooting();
                    }

                    if (i == bps) NextMuzzle++;

                    NextMuzzle = (NextMuzzle + (System.Values.HardPoint.Loading.SkipBarrels + 1)) % _numOfBarrels;
                }

                EventTriggerStateChanged(state: EventTriggers.Firing, active: true, muzzles: _muzzlesToFire);
                _muzzlesToFire.Clear();

                _nextVirtual = _nextVirtual + 1 < bps ? _nextVirtual + 1 : 0;
                if (session.ProCounter++ >= session.Projectiles.Wait.Length - 1)
                {
                    session.ProCounter = 0;
                    session.Load += System.IsBeamWeapon ? 0.0625 : 1;
                }
            }
        }

        private Projectile CreateVirtualProjectile()
        {
            Projectile p;
            Comp.Ai.Session.Projectiles.ProjectilePool[Comp.Ai.Session.ProCounter].AllocateOrCreate(out p);
            p.T.System = System;
            p.T.Ai = Comp.Ai;
            p.T.Target.Entity = Target.Entity;
            p.T.Target.Projectile = Target.Projectile;
            p.T.Target.IsProjectile = Target.Projectile != null;
            p.T.Target.FiringCube = Comp.MyCube;
            p.T.BaseDamagePool = BaseDamage;
            p.T.EnableGuidance = Comp.Set.Value.Guidance;
            p.T.DetonationDamage = DetonateDmg;
            p.T.AreaEffectDamage = AreaEffectDmg;

            p.T.WeaponCache = WeaponCache;

            WeaponCache.VirtualHit = false;
            WeaponCache.Hits = 0;
            WeaponCache.HitEntity.Entity = null;
            p.T.WeaponId = WeaponId;
            p.T.MuzzleId = -1;

            p.SelfDamage = System.SelfDamage || Comp.Gunner;
            p.GridVel = Comp.Ai.GridVel;
            p.T.Origin = MyPivotPos;
            p.T.OriginUp = MyPivotUp;
            p.PredictedTargetPos = TargetPos;
            p.Direction = MyPivotDir;
            p.State = Projectile.ProjectileState.Start;
            return p;
        }

        private void ShootRayCheck()
        {
            Comp.LastRayCastTick = Comp.Ai.Session.Tick;
            var masterWeapon = TrackTarget || Comp.TrackingWeapon == null ? this : Comp.TrackingWeapon;
            if (true)
            {
                masterWeapon.Target.Expired = true;
                if (masterWeapon != this) Target.Expired = true;
                return;
            }
            /*
            if (Target.Projectile != null)
            {
                if (!Comp.Ai.LiveProjectile.Contains(Target.Projectile))
                {
                    Log.Line($"{System.WeaponName} - ShootRayCheckFail - projectile not alive");
                    masterWeapon.Target.Expired = true;
                    if (masterWeapon != this) Target.Expired = true;
                    return;
                }
            }
            */
            if (Target.Projectile == null)
            {
                if ((Target.Entity == null || Target.Entity.MarkedForClose))
                {
                    Log.Line($"{System.WeaponName} - ShootRayCheckFail - target null/marked/misMatch - weaponId:{Comp.MyCube.EntityId} - Null:{Target.Entity == null} - Marked:{Target.Entity?.MarkedForClose} - IdMisMatch:{Target.TopEntityId != Target.Entity?.GetTopMostParent()?.EntityId} - OldId:{Target.TopEntityId} - Id:{Target.Entity?.GetTopMostParent()?.EntityId}");
                    masterWeapon.Target.Expired = true;
                    if (masterWeapon != this) Target.Expired = true;
                    return;
                }
                var topMostEnt = Target.Entity.GetTopMostParent();
                if (Target.TopEntityId != topMostEnt.EntityId || !Comp.Ai.Targets.ContainsKey(topMostEnt))
                {
                    Log.Line("topmostEnt checks");
                    masterWeapon.Target.Expired = true;
                    if (masterWeapon != this) Target.Expired = true;
                    return;
                }
            }

            var targetPos = Target.Projectile?.Position ?? Target.Entity.PositionComp.WorldMatrix.Translation;
            if (Vector3D.DistanceSquared(targetPos, MyPivotPos) > System.MaxTrajectorySqr)
            {
                Log.Line($"{System.WeaponName} - ShootRayCheck Fail - out of range");
                masterWeapon.Target.Expired = true;
                if (masterWeapon !=  this) Target.Expired = true;
                return;
            }
            Casting = true;
            Comp.Ai.Session.Physics.CastRayParallel(ref MyPivotPos, ref targetPos, CollisionLayers.DefaultCollisionLayer, ShootRayCheckCallBack);
        }

        public void ShootRayCheckCallBack(IHitInfo hitInfo)
        {
            Casting = false;
            var masterWeapon = TrackTarget ? this : Comp.TrackingWeapon;
            if (hitInfo?.HitEntity == null)
            {
                if (Target.Projectile != null)
                    return;

                masterWeapon.Target.Expired = true;
                if (masterWeapon != this) Target.Expired = true;
                Log.Line($"{System.WeaponName} - ShootRayCheck failure - unexpected nullHit - target:{Target?.Entity?.DebugName} - {Target?.Entity?.MarkedForClose}");
                return;
            }

            var projectile = Target.Projectile != null;
            var unexpectedHit = projectile || (hitInfo.HitEntity != Target.Entity && hitInfo.HitEntity != Target.Entity.Parent);

            if (unexpectedHit)
            {
                var rootAsGrid = hitInfo.HitEntity as MyCubeGrid;
                var parentAsGrid = hitInfo.HitEntity?.Parent as MyCubeGrid;

                if (rootAsGrid == null && parentAsGrid == null)
                {
                    Log.Line($"{System.WeaponName} - ShootRayCheck Success - junk: {((MyEntity)hitInfo.HitEntity).DebugName}");
                    return;
                }

                var grid = parentAsGrid ?? rootAsGrid;
                if (grid == Comp.Ai.MyGrid)
                {
                    masterWeapon.Target.Expired = true;
                    if (masterWeapon != this) Target.Expired = true;
                    Log.Line($"{System.WeaponName} - ShootRayCheck failure - own grid: {grid?.DebugName}");
                    return;
                }

                if (!GridAi.GridEnemy(Comp.MyCube, grid))
                {
                    if (!grid.IsSameConstructAs(Comp.Ai.MyGrid))
                    {
                        Log.Line($"{System.WeaponName} - ShootRayCheck fail - friendly grid: {grid?.DebugName} - {grid?.DebugName}");
                        masterWeapon.Target.Expired = true;
                        if (masterWeapon != this) Target.Expired = true;
                    }
                    Log.Line($"{System.WeaponName} - ShootRayCheck Success - sameLogicGroup: {((MyEntity)hitInfo.HitEntity).DebugName}");
                    return;
                }
                Log.Line($"{System.WeaponName} - ShootRayCheck Success - non-friendly target in the way of primary target, shoot through: {((MyEntity)hitInfo.HitEntity).DebugName}");
                return;
            }
            if (System.ClosestFirst)
            {
                if (Target.Projectile != null)
                {
                    Log.Line($"projectile not null other branch2: {((MyEntity)hitInfo.HitEntity).DebugName} - {Comp.Ai.MyGrid.IsSameConstructAs(hitInfo.HitEntity as MyCubeGrid)}");
                }
                var grid = hitInfo.HitEntity as MyCubeGrid;
                if (grid != null && Target.Entity.GetTopMostParent() == grid)
                {
                    var maxChange = hitInfo.HitEntity.PositionComp.LocalAABB.HalfExtents.Min();
                    var targetPos = Target.Entity.PositionComp.WorldMatrix.Translation;
                    var weaponPos = MyPivotPos;

                    double rayDist;
                    Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var newHitShortDist = rayDist * (1 - hitInfo.Fraction);
                    var distanceToTarget = rayDist * hitInfo.Fraction;

                    var shortDistExceed = newHitShortDist - Target.HitShortDist > maxChange;
                    var escapeDistExceed = distanceToTarget - Target.OrigDistance > Target.OrigDistance;
                    if (shortDistExceed || escapeDistExceed)
                    {
                        masterWeapon.Target.Expired = true;
                        if (masterWeapon != this) Target.Expired = true;
                        //if (shortDistExceed) Log.Line($"{System.WeaponName} - ShootRayCheck fail - Distance to sorted block exceeded");
                        //else Log.Line($"{System.WeaponName} - ShootRayCheck fail - Target distance to escape has been met - {distanceToTarget} - {Target.OrigDistance} -{distanceToTarget - Target.OrigDistance} > {Target.OrigDistance}");
                    }
                }
            }
        }
    }
}
