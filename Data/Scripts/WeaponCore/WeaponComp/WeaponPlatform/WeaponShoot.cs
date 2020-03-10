using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.TrajectoryDef;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

using System;

namespace WeaponCore.Platform
{

    public partial class Weapon
    {
        internal void Shoot() // Inlined due to keens mod profiler
        {
            try
            {
                var session = Comp.Session;
                var tick = session.Tick;
                var bps = System.Values.HardPoint.Loading.BarrelsPerShot;
                var targetable = ActiveAmmoDef.Health > 0 && !ActiveAmmoDef.Const.IsBeamWeapon;

                if (_ticksUntilShoot++ < System.DelayToFire)
                {
                    if (AvCapable && System.PreFireSound && !PreFiringEmitter.IsPlaying)
                        StartPreFiringSound();

                    if (!PreFired)
                    {
                        var nxtMuzzle = NextMuzzle;
                        for (int i = 0; i < bps; i++)
                        {
                            _muzzlesToFire.Clear();
                            _muzzlesToFire.Add(MuzzleIdToName[NextMuzzle]);
                            if (i == bps) NextMuzzle++;
                            nxtMuzzle = (nxtMuzzle + (System.Values.HardPoint.Loading.SkipBarrels + 1)) % _numOfBarrels;
                        }

                        uint prefireLength;
                        if (System.WeaponAnimationLengths.TryGetValue(EventTriggers.PreFire, out prefireLength))
                        {
                            if (_prefiredTick + prefireLength <= tick)
                            {
                                EventTriggerStateChanged(EventTriggers.PreFire, true, _muzzlesToFire);
                                _prefiredTick = tick;
                            }
                        }
                        PreFired = true;
                    }
                    return;
                }

                if (PreFired)
                {
                    EventTriggerStateChanged(EventTriggers.PreFire, false);
                    _muzzlesToFire.Clear();
                    PreFired = false;
                }

                if (System.HasBarrelRotation)
                {
                    SpinBarrel();
                    if (BarrelRate < 9)
                    {
                        if (_spinUpTick <= tick)
                        {
                            BarrelRate++;
                            _spinUpTick = tick + _ticksBeforeSpinUp;
                        }
                        return;
                    }
                }

                if (ShootTick > tick)
                    return;

                ShootTick = tick + TicksPerShot;

                if (!IsShooting) StartShooting();

                var burstDelay = (uint)System.Values.HardPoint.Loading.DelayAfterBurst;

                if (ActiveAmmoDef.Const.BurstMode && ++State.ShotsFired > System.Values.HardPoint.Loading.ShotsInBurst)
                {
                    State.ShotsFired = 1;
                    EventTriggerStateChanged(EventTriggers.BurstReload, false);
                }
                else if (ActiveAmmoDef.Const.HasBurstDelay && System.Values.HardPoint.Loading.ShotsInBurst > 0 && ++State.ShotsFired == System.Values.HardPoint.Loading.ShotsInBurst)
                {
                    State.ShotsFired = 0;
                    ShootTick = burstDelay > TicksPerShot ? tick + burstDelay : tick + TicksPerShot;
                }

                if (Comp.Ai.VelocityUpdateTick != tick)
                {
                    Comp.Ai.GridVel = Comp.Ai.MyGrid.Physics?.LinearVelocity ?? Vector3D.Zero;
                    Comp.Ai.IsStatic = Comp.Ai.MyGrid.Physics?.IsStatic ?? false;
                    Comp.Ai.VelocityUpdateTick = tick;
                }

                if (!Comp.Session.IsClient && Comp.State.Value.CurrentPlayerControl.ControlType == ControlType.None && ActiveAmmoDef.Trajectory.Guidance == GuidanceType.None && (!Casting && tick - Comp.LastRayCastTick > 29 || System.Values.HardPoint.Other.MuzzleCheck && tick - LastMuzzleCheck > 29))
                    ShootRayCheck();

                var targetAiCnt = Comp.Ai.TargetAis.Count;

                Projectile vProjectile = null;
                if (ActiveAmmoDef.Const.VirtualBeams) vProjectile = CreateVirtualProjectile();

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

                    if (!ActiveAmmoDef.Const.EnergyAmmo || ActiveAmmoDef.Const.IsHybrid || ActiveAmmoDef.Const.MustCharge)
                    {
                        if (State.Sync.CurrentAmmo == 0) break;
                        State.Sync.CurrentAmmo--;
                    }

                    if (ActiveAmmoDef.Const.HasBackKickForce && !Comp.Ai.IsStatic)
                        Comp.Ai.MyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, -muzzle.Direction * ActiveAmmoDef.BackKickForce, muzzle.Position, Vector3D.Zero);

                    if (PlayTurretAv)
                    {
                        if (System.BarrelEffect1 && tick - muzzle.LastAv1Tick > System.Barrel1AvTicks && !muzzle.Av1Looping)
                        {
                            muzzle.LastAv1Tick = tick;
                            muzzle.Av1Looping = System.Values.HardPoint.Graphics.Barrel1.Extras.Loop;
                            session.Av.AvBarrels1.Add(new AvBarrel { Weapon = this, Muzzle = muzzle, StartTick = tick });
                        }

                        if (System.BarrelEffect2 && tick - muzzle.LastAv2Tick > System.Barrel2AvTicks && !muzzle.Av2Looping)
                        {
                            muzzle.LastAv2Tick = tick;
                            muzzle.Av2Looping = System.Values.HardPoint.Graphics.Barrel2.Extras.Loop;
                            session.Av.AvBarrels2.Add(new AvBarrel { Weapon = this, Muzzle = muzzle, StartTick = tick });
                        }
                    }

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

                        if (ActiveAmmoDef.Const.VirtualBeams && j == 0)
                        {
                            MyEntity primeE = null;
                            MyEntity triggerE = null;

                            if (ActiveAmmoDef.Const.PrimeModel)
                                primeE = ActiveAmmoDef.Const.PrimeEntityPool.Get();

                            if (ActiveAmmoDef.Const.TriggerModel)
                                triggerE = session.TriggerEntityPool.Get();

                            var info = session.Projectiles.VirtInfoPool.Get();
                            info.InitVirtual(System, Comp.Ai, ActiveAmmoDef, primeE, triggerE, Target, WeaponId, muzzle.MuzzleId, muzzle.Position, muzzle.DeviatedDir);
                            vProjectile.VrPros.Add(new VirtualProjectile { Info = info, VisualShot = session.Av.AvShotPool.Get() });

                            if (!ActiveAmmoDef.Const.RotateRealBeam) vProjectile.Info.WeaponCache.VirutalId = 0;
                            else if (i == _nextVirtual)
                            {

                                vProjectile.Info.Origin = muzzle.Position;
                                vProjectile.Direction = muzzle.DeviatedDir;
                                vProjectile.Info.WeaponCache.VirutalId = _nextVirtual;
                            }

                            Comp.Session.Projectiles.ActiveProjetiles.Add(vProjectile);
                        }
                        else
                        {
                            var p = Comp.Session.Projectiles.ProjectilePool.Count > 0 ? Comp.Session.Projectiles.ProjectilePool.Pop() : new Projectile();
                            p.Info.Id = Comp.Session.Projectiles.CurrentProjectileId++;
                            p.Info.System = System;
                            p.Info.Ai = Comp.Ai;
                            p.Info.AmmoDef = ActiveAmmoDef;
                            p.Info.Overrides = Comp.Set.Value.Overrides;
                            p.Info.Target.Entity = Target.Entity;
                            p.Info.Target.Projectile = Target.Projectile;
                            p.Info.Target.IsProjectile = Target.Projectile != null;
                            p.Info.Target.IsFakeTarget = Comp.TrackReticle;
                            p.Info.Target.FiringCube = Comp.MyCube;
                            p.Info.WeaponId = WeaponId;
                            p.Info.MuzzleId = muzzle.MuzzleId;
                            p.Info.BaseDamagePool = BaseDamage;
                            p.Info.EnableGuidance = Comp.Set.Value.Guidance;
                            p.Info.DetonationDamage = DetonateDmg;
                            p.Info.AreaEffectDamage = AreaEffectDmg;
                            p.Info.WeaponCache = WeaponCache;
                            p.Info.WeaponCache.VirutalId = -1;
                            p.Info.Seed = Comp.Seed;

                            p.TerminalControlled = Comp.State.Value.CurrentPlayerControl.ControlType == ControlType.None;
                            p.Info.ShooterVel = Comp.Ai.GridVel;
                            p.Info.Origin = muzzle.Position;
                            p.Info.OriginUp = MyPivotUp;
                            p.PredictedTargetPos = Target.TargetPos;
                            p.Direction = muzzle.DeviatedDir;
                            p.State = Projectile.ProjectileState.Start;
                            p.Info.PrimeEntity = ActiveAmmoDef.Const.PrimeModel ? ActiveAmmoDef.Const.PrimeEntityPool.Get() : null;
                            p.Info.TriggerEntity = ActiveAmmoDef.Const.TriggerModel ? session.TriggerEntityPool.Get() : null;
                            Comp.Session.Projectiles.ActiveProjetiles.Add(p);

                            if (targetable)
                            {
                                for (int t = 0; t < targetAiCnt; t++)
                                {
                                    var targetAi = Comp.Ai.TargetAis[t];
                                    var addProjectile = ActiveAmmoDef.Trajectory.Guidance != GuidanceType.None && targetAi.PointDefense;
                                    if (!addProjectile && targetAi.PointDefense)
                                    {
                                        if (Vector3.Dot(p.Direction, p.Info.Origin - targetAi.MyGrid.PositionComp.WorldMatrix.Translation) < 0)
                                        {
                                            var targetSphere = targetAi.MyGrid.PositionComp.WorldVolume;
                                            targetSphere.Radius *= 3;
                                            var testRay = new RayD(p.Info.Origin, p.Direction);
                                            var quickCheck = Vector3D.IsZero(targetAi.GridVel, 0.025) && targetSphere.Intersects(testRay) != null;
                                            if (!quickCheck)
                                            {
                                                var deltaPos = targetSphere.Center - MyPivotPos;
                                                var deltaVel = targetAi.GridVel - Comp.Ai.GridVel;
                                                var timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, ActiveAmmoDef.Const.DesiredProjectileSpeed);
                                                var predictedPos = targetSphere.Center + (float)timeToIntercept * deltaVel;
                                                targetSphere.Center = predictedPos;
                                            }

                                            if (quickCheck || targetSphere.Intersects(testRay) != null)
                                                addProjectile = true;
                                        }
                                    }
                                    if (addProjectile)
                                    {
                                        targetAi.LiveProjectile.Add(p);
                                        targetAi.LiveProjectileTick = tick;
                                        targetAi.NewProjectileTick = tick;
                                        p.Watchers.Add(targetAi);
                                    }
                                }
                            }
                        }
                    }

                    _muzzlesToFire.Add(MuzzleIdToName[current]);

                    if (HeatPShot > 0)
                    {
                        if (!HeatLoopRunning) { 
                            Comp.Session.FutureEvents.Schedule(UpdateWeaponHeat, null, 20);
                           HeatLoopRunning = true;
                        }

                        State.Sync.Heat += HeatPShot;
                        Comp.CurrentHeat += HeatPShot;
                        if (State.Sync.Heat >= System.MaxHeat)
                        {
                            if (!Comp.Session.IsClient && Comp.Set.Value.Overload > 1)
                            {
                                var dmg = .02f * Comp.MaxIntegrity;
                                Comp.Slim.DoDamage(dmg, MyDamageType.Environment, true, null, Comp.Ai.MyGrid.EntityId);
                            }
                            EventTriggerStateChanged(EventTriggers.Overheated, true);
                            State.Sync.Overheated = true;
                            StopShooting();
                            break;
                        }
                    }                    

                    if (i == bps) NextMuzzle++;

                    NextMuzzle = (NextMuzzle + (System.Values.HardPoint.Loading.SkipBarrels + 1)) % _numOfBarrels;
                }

                if(IsShooting)
                    EventTriggerStateChanged(state: EventTriggers.Firing, active: true, muzzles: _muzzlesToFire);

                if (ActiveAmmoDef.Const.BurstMode && (State.Sync.CurrentAmmo > 0 || (ActiveAmmoDef.Const.EnergyAmmo && !ActiveAmmoDef.Const.MustCharge)) && State.ShotsFired == System.Values.HardPoint.Loading.ShotsInBurst)
                {
                    uint delay = 0;
                    FinishBurst = false;
                    if (System.WeaponAnimationLengths.TryGetValue(EventTriggers.Firing, out delay))
                        session.FutureEvents.Schedule(o => { EventTriggerStateChanged(EventTriggers.BurstReload, true); }, null, delay);
                    else
                        EventTriggerStateChanged(EventTriggers.BurstReload, true);

                    if (AvCapable && RotateEmitter != null && RotateEmitter.IsPlaying) StopRotateSound();
                    if (IsShooting) StopShooting();

                    ShootTick = burstDelay > TicksPerShot ? tick + burstDelay + delay : tick + TicksPerShot + delay;
                }
                else if (ActiveAmmoDef.Const.BurstMode && System.AlwaysFireFullBurst && State.Sync.CurrentAmmo > 0)
                    FinishBurst = (State.Sync.CurrentAmmo > 0 || ActiveAmmoDef.Const.EnergyAmmo) && State.ShotsFired < System.Values.HardPoint.Loading.ShotsInBurst;

                else if ((!ActiveAmmoDef.Const.EnergyAmmo || ActiveAmmoDef.Const.MustCharge) && State.Sync.CurrentAmmo == 0 && !State.Sync.Reloading)
                    StartReload();
                

                if (State.ManualShoot == TerminalActionState.ShootOnce && --Comp.State.Value.Weapons[WeaponId].SingleShotCounter <= 0)
                    State.ManualShoot = TerminalActionState.ShootOff;

                _muzzlesToFire.Clear();

                _nextVirtual = _nextVirtual + 1 < bps ? _nextVirtual + 1 : 0;
            }
            catch (Exception e)
            {
                Log.Line($"Error in shoot: {e}");
            }
        }

        private Projectile CreateVirtualProjectile()
        {
            var p = Comp.Session.Projectiles.ProjectilePool.Count > 0 ? Comp.Session.Projectiles.ProjectilePool.Pop() : new Projectile();
            p.Info.Id = Comp.Session.Projectiles.CurrentProjectileId++;
            p.Info.System = System;
            p.Info.Ai = Comp.Ai;
            p.Info.AmmoDef = ActiveAmmoDef;
            p.Info.Overrides = Comp.Set.Value.Overrides;
            p.Info.Target.Entity = Target.Entity;
            p.Info.Target.Projectile = Target.Projectile;
            p.Info.Target.IsProjectile = Target.Projectile != null;
            p.Info.Target.IsFakeTarget = Comp.TrackReticle;
            p.Info.Target.FiringCube = Comp.MyCube;
            p.Info.BaseDamagePool = BaseDamage;
            p.Info.EnableGuidance = Comp.Set.Value.Guidance;
            p.Info.DetonationDamage = DetonateDmg;
            p.Info.AreaEffectDamage = AreaEffectDmg;
            p.Info.Seed = Comp.Seed;

            p.Info.WeaponCache = WeaponCache;

            WeaponCache.VirtualHit = false;
            WeaponCache.Hits = 0;
            WeaponCache.HitEntity.Entity = null;
            p.Info.WeaponId = WeaponId;
            p.Info.MuzzleId = -1;

            p.TerminalControlled = Comp.State.Value.CurrentPlayerControl.ControlType == ControlType.Camera;
            p.Info.ShooterVel = Comp.Ai.GridVel;
            p.Info.Origin = MyPivotPos;
            p.Info.OriginUp = MyPivotUp;
            p.PredictedTargetPos = Target.TargetPos;
            p.Direction = MyPivotDir;
            p.State = Projectile.ProjectileState.Start;
            return p;
        }

        private void ShootRayCheck()
        {
            var tick = Comp.Session.Tick;
            var masterWeapon = TrackTarget || Comp.TrackingWeapon == null ? this : Comp.TrackingWeapon;
            if (System.Values.HardPoint.Other.MuzzleCheck)
            {
                LastMuzzleCheck = tick;
                if (MuzzleHitSelf())
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed,!Comp.TrackReticle);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed, !Comp.TrackReticle);
                    return;
                }
                if (tick - Comp.LastRayCastTick <= 29) return;
            }
            Comp.LastRayCastTick = tick;
            
            if (Target.IsFakeTarget)
            {
                Casting = true;
                Comp.Session.Physics.CastRayParallel(ref MyPivotPos, ref Target.TargetPos, CollisionLayers.DefaultCollisionLayer, ManualShootRayCallBack);
                return;
            }
            if (Comp.TrackReticle) return;


            if (Target.Projectile != null)
            {
                if (!Comp.Ai.LiveProjectile.Contains(Target.Projectile))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return;
                }
            }
            if (Target.Projectile == null)
            {
                if ((Target.Entity == null || Target.Entity.MarkedForClose))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return;
                }
                var cube = Target.Entity as MyCubeBlock;
                if (cube != null && !cube.IsWorking)
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return;
                }  
                var topMostEnt = Target.Entity.GetTopMostParent();
                if (Target.TopEntityId != topMostEnt.EntityId || !Comp.Ai.Targets.ContainsKey(topMostEnt))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return;
                }
            }

            var targetPos = Target.Projectile?.Position ?? Target.Entity.PositionComp.WorldMatrix.Translation;
            if (Vector3D.DistanceSquared(targetPos, MyPivotPos) > (Comp.Set.Value.Range * Comp.Set.Value.Range))
            {
                masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                if (masterWeapon !=  this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                return;
            }
            Casting = true;
            Comp.Session.Physics.CastRayParallel(ref MyPivotPos, ref targetPos, CollisionLayers.DefaultCollisionLayer, NormalShootRayCallBack);
        }

        public void NormalShootRayCallBack(IHitInfo hitInfo)
        {
            Casting = false;
            var masterWeapon = TrackTarget ? this : Comp.TrackingWeapon;
            if (hitInfo?.HitEntity == null)
            {
                if (Target.Projectile != null)
                    return;

                masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                return;
            }

            var projectile = Target.Projectile != null;
            var unexpectedHit = projectile || (hitInfo.HitEntity != Target.Entity && hitInfo.HitEntity != Target.Entity.Parent);

            if (unexpectedHit)
            {
                var rootAsGrid = hitInfo.HitEntity as MyCubeGrid;
                var parentAsGrid = hitInfo.HitEntity?.Parent as MyCubeGrid;

                if (rootAsGrid == null && parentAsGrid == null)
                    return;

                var grid = parentAsGrid ?? rootAsGrid;
                if (grid == Comp.Ai.MyGrid)
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return;
                }

                if (!GridAi.GridEnemy(Comp.Ai.MyOwner, grid))
                {
                    if (!grid.IsSameConstructAs(Comp.Ai.MyGrid))
                    {
                        masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                        if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    }
                    return;
                }
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
                        masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                        if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    }
                }
            }
        }

        public void ManualShootRayCallBack(IHitInfo hitInfo)
        {
            Casting = false;
            var masterWeapon = TrackTarget ? this : Comp.TrackingWeapon;

            var grid = hitInfo.HitEntity as MyCubeGrid;
            if (grid != null)
            {
                if (grid.IsSameConstructAs(Comp.MyCube.CubeGrid))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed, false);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed, false);
                }
            }
        }

        public bool MuzzleHitSelf()
        {
            for (int i = 0; i < Muzzles.Length; i++)
            {
                var m = Muzzles[i];
                var grid = Comp.Ai.MyGrid;
                var dummy = Dummies[i];
                var newInfo = dummy.Info;
                m.Direction = newInfo.Direction;
                m.Position = newInfo.Position;
                m.LastUpdateTick = Comp.Session.Tick;

                var start = m.Position;
                var end = m.Position + (m.Direction * grid.PositionComp.LocalVolume.Radius);

                Vector3D? hit;
                if (GridIntersection.BresenhamGridIntersection(grid, ref start, ref end, out hit, Comp.MyCube))
                    return true;
            }
            return false;
        }
    }
}
