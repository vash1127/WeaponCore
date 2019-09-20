using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectile;
using static WeaponCore.Support.Trajectile;
namespace WeaponCore.Projectiles
{
    public partial class Projectiles
    {
        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal const int PoolCount = 16;

        internal readonly MyConcurrentPool<Fragments>[] ShrapnelPool = new MyConcurrentPool<Fragments>[PoolCount];
        internal readonly MyConcurrentPool<Fragment>[] FragmentPool = new MyConcurrentPool<Fragment>[PoolCount];
        internal readonly List<Fragments>[] ShrapnelToSpawn = new List<Fragments>[PoolCount];

        internal readonly MyConcurrentPool<List<MyEntity>>[] CheckPool = new MyConcurrentPool<List<MyEntity>>[PoolCount];
        internal readonly ObjectsPool<Projectile>[] ProjectilePool = new ObjectsPool<Projectile>[PoolCount];
        internal readonly EntityPool<MyEntity>[][] EntityPool = new EntityPool<MyEntity>[PoolCount][];
        internal readonly MyConcurrentPool<HitEntity>[] HitEntityPool = new MyConcurrentPool<HitEntity>[PoolCount];
        internal readonly ObjectsPool<Trajectile>[] TrajectilePool = new ObjectsPool<Trajectile>[PoolCount];
        internal readonly List<Trajectile>[] DrawProjectiles = new List<Trajectile>[PoolCount];
        internal readonly List<Projectile>[] CleanUp = new List<Projectile>[PoolCount];

        internal readonly MyConcurrentPool<object>[] GenericListPool = new MyConcurrentPool<object>[PoolCount];
        internal readonly MyConcurrentPool<object>[] GenericHashSetPool = new MyConcurrentPool<object>[PoolCount];

        object CreateListInstance<T>(HashSet<T> lst)
        {
            return new List<T>();
        }

        object CreateHashSetInstance<T>(HashSet<T> lst)
        {
            return new HashSet<T>();
        }

        internal readonly MyConcurrentPool<List<Vector3I>> V3Pool = new MyConcurrentPool<List<Vector3I>>();
        internal readonly object[] Wait = new object[PoolCount];

        internal Projectiles()
        {
            for (int i = 0; i < Wait.Length; i++)
            {
                Wait[i] = new object();
                ShrapnelToSpawn[i] = new List<Fragments>(25);
                GenericListPool[i] = new MyConcurrentPool<object>(25, null, 10000, () => CreateListInstance(((MyCubeGrid)null)?.CubeBlocks));
                GenericHashSetPool[i] = new MyConcurrentPool<object>(25, null, 10000, () => CreateHashSetInstance(((MyCubeGrid)null)?.CubeBlocks));
                ShrapnelPool[i] = new MyConcurrentPool<Fragments>(25);
                FragmentPool[i] = new MyConcurrentPool<Fragment>(100);
                CheckPool[i] = new MyConcurrentPool<List<MyEntity>>(50);
                ProjectilePool[i] = new ObjectsPool<Projectile>(100);
                HitEntityPool[i] = new MyConcurrentPool<HitEntity>(50);
                DrawProjectiles[i] = new List<Trajectile>(100);
                CleanUp[i] = new List<Projectile>(100);
                TrajectilePool[i] = new ObjectsPool<Trajectile>(100);
            }
        }

        internal static MyEntity EntityActivator(string model)
        {
            var ent = new MyEntity();
            ent.Init(null, model, null, null, null);
            ent.Render.CastShadows = false;
            ent.IsPreview = true;
            ent.Save = false;
            ent.SyncFlag = false;
            ent.NeedsWorldMatrix = false;
            ent.Flags |= EntityFlags.IsNotGamePrunningStructureObject;
            MyEntities.Add(ent);
            return ent;
        }

        internal void Update()
        {
            //Session.Instance.DsUtil.Start("");
            //MyAPIGateway.Parallel.For(0, Wait.Length, x => Process(x), 1);
            for (int i = 0; i < Wait.Length; i++) Process(i);
            //Session.Instance.DsUtil.Complete();
        }

        private void Process(int i)
        {
            var noAv = Session.Instance.DedicatedServer;
            var camera = Session.Instance.Camera;
            var cameraPos = Session.Instance.CameraPos;
            lock (Wait[i])
            {
                var modelClose = false;
                var pool = ProjectilePool[i];
                var entPool = EntityPool[i];
                var drawList = DrawProjectiles[i];
                var cleanUp = CleanUp[i];
                var vtPool = TrajectilePool[i];
                var spawnShrapnel = ShrapnelToSpawn[i];

                if (spawnShrapnel.Count > 0) {
                    for (int j = 0; j < spawnShrapnel.Count; j++)
                        spawnShrapnel[j].Spawn(i);
                    spawnShrapnel.Clear();
                }

                foreach (var p in pool.Active)
                {
                    p.Age++;
                    p.T.OnScreen = false;

                    switch (p.State)
                    {
                        case ProjectileState.Dead:
                            continue;
                        case ProjectileState.Start:
                            p.Start(noAv, i);
                            if (p.ModelState == EntityState.NoDraw)
                                modelClose = p.CloseModel(this, i);
                            break;
                        case ProjectileState.Ending:
                        case ProjectileState.OneAndDone:
                        case ProjectileState.Depleted:
                            if (p.State == ProjectileState.Depleted)
                                p.ProjectileClose(this, i);
                            if (p.ModelState != EntityState.Exists) p.Stop(this, i);
                            else
                            {
                                modelClose = p.CloseModel(this, i);
                                p.Stop(this, i);
                            }
                            continue;
                    }

                    if (p.AccelLength > 0)
                    {
                        if (p.SmartsOn)
                        {
                            Vector3D newVel;
                            if ((p.AccelLength <= 0 || Vector3D.DistanceSquared(p.Origin, p.Position) >= p.SmartsDelayDistSqr))
                            {
                                var giveUpChase = p.Age - p.ChaseAge > p.MaxChaseAge;
                                var newChase = (giveUpChase || p.PickTarget);

                                var validTarget = p.T.Target.IsProjectile || p.T.Target.Entity != null && !p.T.Target.Entity.MarkedForClose;
                                if (newChase && p.EndChase() || validTarget || !p.IsMine && p.ZombieLifeTime % 30 == 0 && GridAi.ReacquireTarget(p))
                                {
                                    if (p.ZombieLifeTime > 0) p.UpdateZombie(true);
                                    var targetPos = Vector3D.Zero;
                                    if (p.T.Target.IsProjectile) targetPos = p.T.Target.Projectile.Position;
                                    else if (p.T.Target.Entity != null) targetPos = p.T.Target.Entity.PositionComp.WorldAABB.Center;

                                    if (p.T.System.TargetOffSet)
                                    {
                                        if (p.Age - p.LastOffsetTime > 300)
                                        {
                                            double dist;
                                            Vector3D.DistanceSquared(ref p.Position, ref targetPos, out dist);
                                            if (dist < p.OffsetSqr && Vector3.Dot(p.Direction, p.Position - targetPos) > 0)
                                                p.OffSetTarget(out p.TargetOffSet);
                                        }
                                        targetPos += p.TargetOffSet;
                                    }

                                    var physics = p.T.Target.Entity?.Physics ?? p.T.Target.Entity?.Parent?.Physics;

                                    if (!p.T.Target.IsProjectile && (physics == null || targetPos == Vector3D.Zero))
                                        p.PrevTargetPos = p.PredictedTargetPos;
                                    else p.PrevTargetPos = targetPos;

                                    var tVel = Vector3.Zero;
                                    if (p.T.Target.IsProjectile) tVel = p.T.Target.Projectile.Velocity;
                                    else if (physics != null) tVel = physics.LinearVelocity;

                                    p.PrevTargetVel = tVel;
                                }
                                else p.UpdateZombie();
                                var commandedAccel = MathFuncs.CalculateMissileIntercept(p.PrevTargetPos, p.PrevTargetVel, p.Position, p.Velocity, p.AccelPerSec, p.T.System.Values.Ammo.Trajectory.Smarts.Aggressiveness, p.T.System.Values.Ammo.Trajectory.Smarts.MaxLateralThrust);
                                newVel = p.Velocity + (commandedAccel * StepConst);
                                p.AccelDir = commandedAccel / p.AccelPerSec;
                                Vector3D.Normalize(ref p.Velocity, out p.Direction);
                            }
                            else newVel = p.Velocity += (p.Direction * p.AccelLength);
                            p.VelocityLengthSqr = newVel.LengthSquared();
                            
                            if (p.VelocityLengthSqr > p.MaxSpeedSqr) newVel = p.Direction * p.MaxSpeed;
                            p.Velocity = newVel;

                            if (p.EnableAv && Vector3D.Dot(p.VisualDir, p.AccelDir) < Session.VisDirToleranceCosine)
                            {
                                p.VisualStep += 0.0025;
                                if (p.VisualStep > 1) p.VisualStep = 1;

                                Vector3D lerpDir;
                                Vector3D.Lerp(ref p.VisualDir, ref p.AccelDir, p.VisualStep, out lerpDir);
                                Vector3D.Normalize(ref lerpDir, out p.VisualDir);
                            }
                            else if (p.EnableAv && Vector3D.Dot(p.VisualDir, p.AccelDir) >= Session.VisDirToleranceCosine)
                            {
                                p.VisualDir = p.AccelDir;
                                p.VisualStep = 0;
                            }
                        }
                        else
                        {
                            var accel = true;
                            Vector3D newVel;
                            if (p.IdleTime > 0)
                            {
                                var distToMax = p.MaxTrajectory - p.T.DistanceTraveled;

                                var stopDist = p.VelocityLengthSqr / 2 / (p.AccelPerSec);
                                if (distToMax <= stopDist)
                                    accel = false;

                                newVel = accel ? p.Velocity + p.AccelVelocity : p.Velocity - p.AccelVelocity;
                                p.VelocityLengthSqr = newVel.LengthSquared();

                                if (accel && p.VelocityLengthSqr > p.MaxSpeedSqr) newVel = p.Direction * p.MaxSpeed;
                                else if (!accel && distToMax < 0)
                                {
                                    newVel = Vector3D.Zero;
                                    p.VelocityLengthSqr = 0;
                                }
                                //Log.Line($"distToMax:{distToMax} - stopDist:{stopDist} - maxDist:{p.MaxTrajectory}({p.DistanceTraveled}) - Speed:{newVel.Length()}({p.VelocityLengthSqr}) - accel:{accel}");
                            }
                            else
                            {
                                newVel = p.Velocity + p.AccelVelocity;
                                p.VelocityLengthSqr = newVel.LengthSquared();
                                if (p.VelocityLengthSqr > p.MaxSpeedSqr) newVel = p.Direction * p.MaxSpeed;
                            }

                            p.Velocity = newVel;
                            //Log.Line($"accel:{accel} - Velocity:{p.Velocity.Length()}");
                        }
                    }
                    
                    if (p.State == ProjectileState.OneAndDone)
                    {
                        p.LastPosition = p.Position;
                        var beamEnd = p.Position + (p.Direction * p.MaxTrajectory);
                        p.TravelMagnitude = p.Position - beamEnd;
                        p.Position = beamEnd;
                    }
                    else
                    {
                        if (p.ConstantSpeed || p.VelocityLengthSqr > 0)
                            p.LastPosition = p.Position;

                        p.TravelMagnitude = p.Velocity * StepConst;
                        p.Position += p.TravelMagnitude;
                    }
                    p.T.PrevDistanceTraveled = p.T.DistanceTraveled;
                    p.T.DistanceTraveled += Math.Abs(Vector3D.Dot(p.Direction, p.Velocity * StepConst));
                    if (p.ModelState == EntityState.Exists)
                    {
                        var matrix = MatrixD.CreateWorld(p.Position, p.VisualDir, MatrixD.Identity.Up);

                        if (p.PrimeModelId != -1)
                            p.T.PrimeMatrix = matrix;
                        if (p.TriggerModelId != -1 && p.T.TriggerGrowthSteps < p.T.System.AreaEffectSize)
                            p.T.TriggerMatrix = matrix;

                        if (p.EnableAv && p.AmmoEffect != null && p.T.System.AmmoParticle && p.PrimeModelId != -1)
                        {
                            var offVec = p.Position + Vector3D.Rotate(p.T.System.Values.Graphics.Particles.Ammo.Offset, p.T.PrimeMatrix);
                            p.AmmoEffect.WorldMatrix = p.T.PrimeMatrix;
                            p.AmmoEffect.SetTranslation(offVec);
                        }
                    }
                    else if (!p.ConstantSpeed && p.EnableAv && p.AmmoEffect != null && p.T.System.AmmoParticle)
                        p.AmmoEffect.Velocity = p.Velocity;

                    if (p.DynamicGuidance)
                        DynTrees.OnProjectileMoved(p, ref p.Velocity);

                    if (p.State != ProjectileState.OneAndDone)
                    {
                        if (p.T.DistanceTraveled * p.T.DistanceTraveled >= p.DistanceToTravelSqr)
                        {
                            if (p.IdleTime <= 0) Die(p, i);
                            else
                            {
                                p.IdleTime--;
                                if (p.IsMine && !p.MineSeeking && !p.MineActivated)
                                {
                                    p.T.Cloaked = p.T.System.Values.Ammo.Trajectory.Mines.Cloak;
                                    p.MineSeeking = true;
                                }
                            }
                        }
                        if (p.Ewar)
                        {
                            if (p.VelocityLengthSqr <= 0 && !p.T.Triggered && !p.IsMine)
                                p.T.Triggered = true;

                            if (p.T.Triggered)
                            {
                                var areaSize = p.T.System.AreaEffectSize;
                                if (p.T.TriggerGrowthSteps < areaSize)
                                {
                                    const int expansionPerTick = 100 / 60;
                                    var nextSize = (double)++p.T.TriggerGrowthSteps * expansionPerTick;
                                    if (nextSize <= areaSize)
                                    {
                                        var nextRound = nextSize + 1;
                                        if (nextRound > areaSize)
                                        {
                                            if (nextSize < areaSize)
                                            {
                                                nextSize = areaSize;
                                                ++p.T.TriggerGrowthSteps;
                                            }
                                        }
                                        MatrixD.Rescale(ref p.T.TriggerMatrix, nextSize);
                                    }
                                }
                            }

                            if (p.Age % p.PulseInterval == 0)
                                p.ElectronicWarfare();
                            else p.EwarActive = false;
                        }
                    }

                    if (Hit(p, i)) continue;

                    if (!p.EnableAv) continue;

                    if (p.T.System.AmmoParticle)
                    {
                        p.TestSphere.Center = p.Position;
                        if (camera.IsInFrustum(ref p.TestSphere))
                        {
                            if ((p.ParticleStopped || p.ParticleLateStart))
                                p.PlayAmmoParticle();
                        }
                        else if (!p.ParticleStopped && p.AmmoEffect != null)
                            p.DisposeAmmoEffect(false,true);
                    }

                    if (p.HasTravelSound)
                    {
                        if (!p.AmmoSound)
                        {
                            double dist;
                            Vector3D.DistanceSquared(ref p.Position, ref cameraPos, out dist);
                            if (dist <= p.AmmoTravelSoundRangeSqr) p.AmmoSoundStart();
                        }
                        else p.TravelEmitter.SetPosition(p.Position);
                    }

                    if (p.DrawLine)
                    {
                        if (p.State == ProjectileState.OneAndDone)
                            p.T.UpdateShape(p.Position, p.Direction, p.MaxTrajectory, ReSize.None);
                        else 
                        {
                            p.T.ProjectileDisplacement += Math.Abs(Vector3D.Dot(p.Direction, (p.Velocity - p.StartSpeed) * StepConst));
                            if (p.T.ProjectileDisplacement < p.TracerLength)
                            {
                                p.T.UpdateShape(p.Position, p.Direction, p.T.ProjectileDisplacement, ReSize.Grow);
                            }
                            else
                            {
                                var pointDir = (p.SmartsOn) ? p.VisualDir : p.Direction;
                                var drawStartPos = p.ConstantSpeed && p.AccelLength > p.TracerLength ? p.LastPosition : p.Position;
                                p.T.UpdateShape(drawStartPos, pointDir, p.TracerLength, ReSize.None);
                            }
                        }
                    }
                    if (p.ModelState == EntityState.Exists)
                    {
                        p.ModelSphereLast.Center = p.LastEntityPos;
                        p.ModelSphereCurrent.Center = p.Position;
                        if (p.T.Triggered)
                        {
                            var currentRadius = p.T.TriggerGrowthSteps < p.T.System.AreaEffectSize ? p.T.TriggerMatrix.Scale.AbsMax() : p.T.System.AreaEffectSize;
                            p.ModelSphereLast.Radius = currentRadius;
                            p.ModelSphereCurrent.Radius = currentRadius;
                        }
                        if (camera.IsInFrustum(ref p.ModelSphereLast) || camera.IsInFrustum(ref p.ModelSphereCurrent) || p.FirstOffScreen)
                        {
                            p.T.OnScreen = true;
                            p.FirstOffScreen = false;
                            p.LastEntityPos = p.Position;
                        }
                    }

                    if (!p.T.OnScreen && p.DrawLine)
                    {
                        if (p.T.System.Trail)
                        {
                            p.T.OnScreen = true;
                        }
                        else
                        {
                            var bb = new BoundingBoxD(Vector3D.Min(p.T.LineStart, p.T.Position), Vector3D.Max(p.T.LineStart, p.T.Position));
                            if (camera.IsInFrustum(ref bb)) p.T.OnScreen = true;
                        }
                    }

                    if (p.T.MuzzleId == -1)
                    {
                        CreateFakeBeams(p, null, drawList, true);
                        continue;
                    }

                    if (p.T.OnScreen)
                    {
                        p.T.Complete(null, DrawState.Default);
                        drawList.Add(p.T);
                    }
                }

                if (modelClose)
                    foreach (var e in entPool)
                        e.DeallocateAllMarked();

                vtPool.DeallocateAllMarked();
                pool.DeallocateAllMarked();
                Clean(i);
            }
        }

        private bool Intersected(Projectile p,  List<Trajectile> drawList, HitEntity hitEntity)
        {
            if (hitEntity?.HitPos == null) return false;
            if (p.EnableAv && (p.DrawLine || p.PrimeModelId != -1 || p.TriggerModelId != -1))
            {
                var hitPos = hitEntity.HitPos.Value;
                p.TestSphere.Center = hitPos;
                if (!p.T.OnScreen) CameraCheck(p);

                if (p.T.MuzzleId != -1)
                {
                    var length = Vector3D.Distance(p.LastPosition, hitPos);
                    var shrink = !p.T.System.IsBeamWeapon;
                    var reSize = shrink ? ReSize.Shrink : ReSize.None;
                    p.T.UpdateShape(hitPos, p.Direction, length, reSize);
                    p.T.Complete(hitEntity, DrawState.Hit);
                    drawList.Add(p.T);
                }
            }

            p.Colliding = true;
            if (!p.T.System.VirtualBeams) Session.Instance.Hits.Enqueue(p);
            else
            {
                p.T.WeaponCache.VirtualHit = true;
                p.T.WeaponCache.HitEntity.Entity = hitEntity.Entity;
                p.T.WeaponCache.HitEntity.HitPos = hitEntity.HitPos;
                p.T.WeaponCache.Hits = p.VrTrajectiles.Count;
                p.T.WeaponCache.HitDistance = Vector3D.Distance(p.LastPosition, hitEntity.HitPos.Value);

                if (hitEntity.Entity is MyCubeGrid) p.T.WeaponCache.HitBlock = hitEntity.Blocks[0];
                Session.Instance.Hits.Enqueue(p);
                if (p.EnableAv && p.T.OnScreen) CreateFakeBeams(p, hitEntity, drawList);
            }
            if (p.EnableAv && p.T.OnScreen) p.HitEffects();
            return true;
        }

        private void CreateFakeBeams(Projectile p, HitEntity hitEntity, List<Trajectile> drawList, bool miss = false)
        {
            for (int i = 0; i < p.VrTrajectiles.Count; i++)
            {
                var vt = p.VrTrajectiles[i];
                vt.OnScreen = p.T.OnScreen;
                if (vt.System.ConvergeBeams)
                {
                    var beam = !miss ? new LineD(vt.Origin, hitEntity.HitPos ?? p.Position) : new LineD(vt.LineStart, p.Position);
                    vt.UpdateVrShape(beam.To, beam.Direction, beam.Length, ReSize.None);
                }
                else
                {
                    Vector3D beamEnd;
                    var hit = !miss && hitEntity.HitPos.HasValue;
                    if (!hit)
                        beamEnd = vt.Origin + (vt.Direction * p.MaxTrajectory);
                    else
                        beamEnd = vt.Origin + (vt.Direction * p.T.WeaponCache.HitDistance);
                    var line = new LineD(vt.Origin, beamEnd);
                    //DsDebugDraw.DrawSingleVec(vt.PrevPosition, 0.5f, Color.Red);
                    if (!miss && hitEntity.HitPos.HasValue)
                        vt.UpdateVrShape(beamEnd, line.Direction, line.Length, ReSize.None);
                    else vt.UpdateVrShape(line.To, line.Direction, line.Length, ReSize.None);
                }
                vt.Complete(hitEntity, DrawState.Hit);
                drawList.Add(vt);
            }
        }

        private void Die(Projectile p, int poolId)
        {
            var dInfo = p.T.System.Values.Ammo.AreaEffect.Detonation;
            if (p.MoveToAndActivate || dInfo.DetonateOnEnd && (!dInfo.ArmOnlyOnHit || p.T.ObjectsHit > 0))
            {
                if (!Hit(p, poolId))
                    p.ProjectileClose(this, poolId);
            }
            else p.ProjectileClose(this, poolId);
        }

        private void Clean(int poolId)
        {
            var cleanUp = CleanUp[poolId];
            for (int j = 0; j < cleanUp.Count; j++)
            {
                var p = cleanUp[j];
                for (int i = 0; i < p.VrTrajectiles.Count; i++)
                    TrajectilePool[poolId].MarkForDeallocate(p.VrTrajectiles[i]);
                p.VrTrajectiles.Clear();
                p.T.Clean();
                ProjectilePool[poolId].MarkForDeallocate(p);

                if (p.DynamicGuidance)
                    DynTrees.UnregisterProjectile(p);
                p.PruningProxyId = -1;
                p.State = ProjectileState.Dead;
            }
        }

        private void CameraCheck(Projectile p)
        {
            if (p.ModelState == EntityState.Exists)
            {
                p.ModelSphereLast.Center = p.LastEntityPos;
                p.ModelSphereCurrent.Center = p.Position;
                if (Session.Instance.Camera.IsInFrustum(ref p.ModelSphereLast) || Session.Instance.Camera.IsInFrustum(ref p.ModelSphereCurrent) || p.FirstOffScreen)
                {
                    p.T.OnScreen = true;
                    p.FirstOffScreen = false;
                    p.LastEntityPos = p.Position;
                }
            }

            if (!p.T.OnScreen && p.DrawLine)
            {
                if (p.T.System.Trail)
                {
                    p.T.OnScreen = true;
                    return;
                }
                var bb = new BoundingBoxD(Vector3D.Min(p.T.LineStart, p.T.Position), Vector3D.Max(p.T.LineStart, p.T.Position));
                if (Session.Instance.Camera.IsInFrustum(ref bb)) p.T.OnScreen = true;
            }
        }
    }
}
