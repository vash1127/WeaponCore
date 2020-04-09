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
                var targetable = ActiveAmmoDef.AmmoDef.Health > 0 && !ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon;

                if (_ticksUntilShoot++ < System.DelayToFire)
                {
                    if (AvCapable && System.PreFireSound && !PreFiringEmitter.IsPlaying)
                        StartPreFiringSound();

                    if (ActiveAmmoDef.AmmoDef.Const.MustCharge || System.AlwaysFireFullBurst)
                        FinishBurst = true;

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

                if (LockOnFireState && (Target.Entity != Comp.Ai.Focus.Target[0] || Target.Entity != Comp.Ai.Focus.Target[1])) 
                    Comp.Ai.Focus.GetPriorityTarget(out Target.Entity);

                ShootTick = tick + TicksPerShot;

                if (!IsShooting) StartShooting();

                var burstDelay = (uint)System.Values.HardPoint.Loading.DelayAfterBurst;

                if (ActiveAmmoDef.AmmoDef.Const.BurstMode && ++State.ShotsFired > System.ShotsPerBurst)
                {
                    State.ShotsFired = 1;
                    EventTriggerStateChanged(EventTriggers.BurstReload, false);
                }
                else if (ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay && System.ShotsPerBurst > 0 && ++State.ShotsFired == System.ShotsPerBurst)
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

                Projectile vProjectile = null;
                if (ActiveAmmoDef.AmmoDef.Const.VirtualBeams) vProjectile = CreateVirtualProjectile();

                var firingPlayer = Comp.State.Value.CurrentPlayerControl.PlayerId == Comp.Session.PlayerId;

                for (int i = 0; i < bps; i++)
                {
                    var current = NextMuzzle;
                    var muzzle = Muzzles[current];
                    if (muzzle.LastUpdateTick != tick)
                    {
                        var dummy = Dummies[current];
                        var newInfo = dummy.Info;
                        muzzle.Direction = newInfo.Direction;
                        muzzle.Position = newInfo.Position;
                        muzzle.LastUpdateTick = tick;
                    }

                    if (ActiveAmmoDef.AmmoDef.Const.Reloadable)
                    {
                        if (State.Sync.CurrentAmmo == 0) break;
                        State.Sync.CurrentAmmo--;
                    }

                    if (ActiveAmmoDef.AmmoDef.Const.HasBackKickForce && !Comp.Ai.IsStatic)
                        Comp.Ai.MyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, -muzzle.Direction * ActiveAmmoDef.AmmoDef.BackKickForce, muzzle.Position, Vector3D.Zero);

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

                        if (ActiveAmmoDef.AmmoDef.Const.VirtualBeams && j == 0)
                        {
                            MyEntity primeE = null;
                            MyEntity triggerE = null;

                            if (ActiveAmmoDef.AmmoDef.Const.PrimeModel)
                                primeE = ActiveAmmoDef.AmmoDef.Const.PrimeEntityPool.Get();

                            if (ActiveAmmoDef.AmmoDef.Const.TriggerModel)
                                triggerE = session.TriggerEntityPool.Get();

                            var info = session.Projectiles.VirtInfoPool.Get();
                            info.InitVirtual(System, Comp.Ai, ActiveAmmoDef.AmmoDef, primeE, triggerE, Target, WeaponId, muzzle.MuzzleId, muzzle.Position, muzzle.DeviatedDir);
                            vProjectile.VrPros.Add(new VirtualProjectile { Info = info, VisualShot = session.Av.AvShotPool.Get() });

                            if (!ActiveAmmoDef.AmmoDef.Const.RotateRealBeam) vProjectile.Info.WeaponCache.VirutalId = 0;
                            else if (i == _nextVirtual)
                            {

                                vProjectile.Info.Origin = muzzle.Position;
                                vProjectile.Info.Direction = muzzle.DeviatedDir;
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
                            p.Info.IsFiringPlayer = firingPlayer;
                            p.Info.AmmoDef = ActiveAmmoDef.AmmoDef;
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
                            p.Info.DetonationDamage = ActiveAmmoDef.AmmoDef.Const.DetonationDamage;
                            p.Info.AreaEffectDamage = ActiveAmmoDef.AmmoDef.Const.AreaEffectDamage;
                            p.Info.WeaponCache = WeaponCache;
                            p.Info.WeaponCache.VirutalId = -1;
                            p.Info.Seed = Comp.Seed;
                            p.Info.LockOnFireState = LockOnFireState;
                            p.Info.ShooterVel = Comp.Ai.GridVel;
                            p.Info.Origin = muzzle.Position;
                            p.Info.OriginUp = MyPivotUp;
                            p.PredictedTargetPos = Target.TargetPos;
                            p.Info.Direction = muzzle.DeviatedDir;
                            p.DeadSphere.Center = MyPivotPos;
                            p.DeadSphere.Radius = Comp.Ai.MyGrid.GridSizeHalf + 0.1;
                            p.State = Projectile.ProjectileState.Start;
                            p.Info.PrimeEntity = ActiveAmmoDef.AmmoDef.Const.PrimeModel ? ActiveAmmoDef.AmmoDef.Const.PrimeEntityPool.Get() : null;
                            p.Info.TriggerEntity = ActiveAmmoDef.AmmoDef.Const.TriggerModel ? session.TriggerEntityPool.Get() : null;
                            Comp.Session.Projectiles.ActiveProjetiles.Add(p);

                            if (targetable)
                                Comp.Ai.Session.Projectiles.AddTargets.Add(p);
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

                if (!ActiveAmmoDef.AmmoDef.Const.MustCharge && ActiveAmmoDef.AmmoDef.Const.BurstMode && (State.Sync.CurrentAmmo > 0 || ActiveAmmoDef.AmmoDef.Const.EnergyAmmo) && State.ShotsFired == System.ShotsPerBurst)
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
                else if (((ActiveAmmoDef.AmmoDef.Const.BurstMode && System.AlwaysFireFullBurst) || ActiveAmmoDef.AmmoDef.Const.MustCharge) && State.Sync.CurrentAmmo > 0 && State.ShotsFired < System.ShotsPerBurst)
                    FinishBurst = true;

                else if (CanReload || ActiveAmmoDef.AmmoDef.Const.MustCharge && State.ShotsFired >= System.ShotsPerBurst)
                    StartReload();
                

                if (State.ManualShoot == ManualShootActionState.ShootOnce && --State.SingleShotCounter <= 0)
                    State.ManualShoot = ManualShootActionState.ShootOff;

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
            p.Info.AmmoDef = ActiveAmmoDef.AmmoDef;
            p.Info.Overrides = Comp.Set.Value.Overrides;
            p.Info.Target.Entity = Target.Entity;
            p.Info.Target.Projectile = Target.Projectile;
            p.Info.Target.IsProjectile = Target.Projectile != null;
            p.Info.Target.IsFakeTarget = Comp.TrackReticle;
            p.Info.Target.FiringCube = Comp.MyCube;
            p.Info.BaseDamagePool = BaseDamage;
            p.Info.EnableGuidance = Comp.Set.Value.Guidance;
            p.Info.DetonationDamage = ActiveAmmoDef.AmmoDef.Const.DetonationDamage;
            p.Info.AreaEffectDamage = ActiveAmmoDef.AmmoDef.Const.AreaEffectDamage;
            p.Info.Seed = Comp.Seed;
            p.Info.LockOnFireState = LockOnFireState;
            p.Info.WeaponCache = WeaponCache;

            WeaponCache.VirtualHit = false;
            WeaponCache.Hits = 0;
            WeaponCache.HitEntity.Entity = null;
            p.Info.WeaponId = WeaponId;
            p.Info.MuzzleId = -1;

            p.Info.ShooterVel = Comp.Ai.GridVel;
            p.Info.Origin = MyPivotPos;
            p.Info.OriginUp = MyPivotUp;
            p.PredictedTargetPos = Target.TargetPos;
            p.Info.Direction = MyPivotDir;
            p.DeadSphere.Center = MyPivotPos;
            p.DeadSphere.Radius = Comp.Ai.MyGrid.GridSizeHalf + 0.1;
            p.State = Projectile.ProjectileState.Start;

            return p;
        }

        private bool RayCheckTest()
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
                    return false;
                }
                if (tick - Comp.LastRayCastTick <= 29) return true;
            }
            Comp.LastRayCastTick = tick;
            
            if (Target.IsFakeTarget)
            {
                Casting = true;
                Comp.Session.Physics.CastRayParallel(ref MyPivotPos, ref Target.TargetPos, CollisionLayers.DefaultCollisionLayer, ManualShootRayCallBack);
                return true;
            }
            if (Comp.TrackReticle) return true;


            if (Target.IsProjectile)
            {
                if (!Comp.Ai.LiveProjectile.Contains(Target.Projectile))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }
            }
            if (!Target.IsProjectile)
            {
                var character = Target.Entity as IMyCharacter;
                if ((Target.Entity == null || Target.Entity.MarkedForClose) || character != null && (Comp.Session.AdminMap.ContainsKey(character) || character.IsDead))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }

                var cube = Target.Entity as MyCubeBlock;
                if (cube != null && !cube.IsWorking)
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }  
                var topMostEnt = Target.Entity.GetTopMostParent();
                if (Target.TopEntityId != topMostEnt.EntityId || !Comp.Ai.Targets.ContainsKey(topMostEnt))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }
            }

            var targetPos = Target.Projectile?.Position ?? Target.Entity.PositionComp.WorldMatrixRef.Translation;
            if (Vector3D.DistanceSquared(targetPos, MyPivotPos) > MaxTargetDistanceSqr)
            {
                masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                if (masterWeapon !=  this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                return false;
            }
            Casting = true;
            Comp.Session.Physics.CastRayParallel(ref MyPivotPos, ref targetPos, CollisionLayers.DefaultCollisionLayer, NormalShootRayCallBack);
            return true;
        }

        public void NormalShootRayCallBack(IHitInfo hitInfo)
        {
            Casting = false;
            var masterWeapon = TrackTarget ? this : Comp.TrackingWeapon;
            var ignoreTargets = Target.IsProjectile || Target.Entity is IMyCharacter;
            var hitTopEnt = (MyEntity)hitInfo.HitEntity?.GetTopMostParent();
            if (hitTopEnt == null)
            {
                if (ignoreTargets)
                    return;

                masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                return;
            }

            var targetTopEnt = Target.Entity?.GetTopMostParent();
            if (targetTopEnt == null)
                return;

            var unexpectedHit = ignoreTargets || targetTopEnt != hitTopEnt;
            var topAsGrid = hitTopEnt as MyCubeGrid;

            if (unexpectedHit)
            {
                if (topAsGrid == null)
                    return;

                if (topAsGrid.IsSameConstructAs(Comp.Ai.MyGrid))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return;
                }

                if (!GridAi.GridEnemy(Comp.Ai.MyOwner, topAsGrid))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return;
                }
                return;
            }
            if (System.ClosestFirst && topAsGrid != null)
            {
                var maxChange = hitInfo.HitEntity.PositionComp.LocalAABB.HalfExtents.Min();
                var targetPos = Target.Entity.PositionComp.WorldMatrixRef.Translation;
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
                if (GridIntersection.BresenhamGridIntersection(grid, ref start, ref end, out hit, Comp.MyCube, Comp.Ai))
                    return true;
            }
            return false;
        }
    }
}
