using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
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
                TrajectilePool[i] = new ObjectsPool<Trajectile>(100);
            }
        }

        internal static MyEntity EntityActivator(string model)
        {
            var ent = new MyEntity();
            Log.Line($"{model}");
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
                            if ((p.AccelLength <= 0 || Vector3D.DistanceSquared(p.Origin, p.Position) > p.SmartsDelayDistSqr))
                            {
                                var giveUpChase = p.Age - p.ChaseAge > p.MaxChaseAge;
                                var newChase = giveUpChase || p.PickTarget;
                                var targetIsProjectile = p.T.Target.IsProjectile;
                                if (!targetIsProjectile && p.T.Target.Projectile != null)
                                    p.ForceNewTarget(!targetIsProjectile);

                                var validTarget = targetIsProjectile || p.T.Target.Entity != null && !p.T.Target.Entity.MarkedForClose;

                                if (newChase && p.EndChase() || validTarget || p.ZombieLifeTime % 30 == 0 && GridAi.ReacquireTarget(p))
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
                            }
                            else newVel = p.Velocity += (p.Direction * p.AccelLength);
                            p.VelocityLengthSqr = newVel.LengthSquared();

                            Vector3D.Normalize(ref p.Velocity, out p.Direction);
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
                                var distToMax = p.MaxTrajectory - p.DistanceTraveled;

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
                        if (p.VelocityLengthSqr > 0) p.LastPosition = p.Position;

                        p.TravelMagnitude = p.Velocity * StepConst;
                        p.Position += p.TravelMagnitude;
                    }
                    p.DistanceTraveled += Vector3D.Dot(p.Direction, p.Velocity * StepConst);

                    if (p.ModelState == EntityState.Exists)
                    {
                        var matrix = MatrixD.CreateWorld(p.Position, p.VisualDir, MatrixD.Identity.Up);
                        if (p.PrimeModelId != -1) p.T.PrimeMatrix = matrix;
                        if (p.TriggerModelId != -1) p.T.TriggerMatrix = matrix;
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
                        if (p.DistanceTraveled * p.DistanceTraveled >= p.DistanceToTravelSqr)
                        {
                            if (p.IdleTime == 0) Die(p, i);
                            else
                                p.IdleTime--;
                        }
                        if (p.Ewar)
                        {
                            if (p.VelocityLengthSqr <= 0 && !p.T.Triggered)
                            {
                                Log.Line($"trigger active - Age:{p.Age} - {p.VelocityLengthSqr}");
                                p.T.Triggered = true;
                            }

                            if (p.Age % p.PulseInterval == 0)
                                p.ElectronicWarfare();
                            else p.EwarActive = false;
                        }
                        else if (p.Detect)
                        {

                        }
                    }

                    if (Hit(p, i, p.T.System.CollisionIsLine)) continue;

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
                        if (p.Grow)
                        {
                            if (p.AccelLength <= 0 && p.GrowStep++ >= p.T.ReSizeSteps) p.Grow = false;
                            else if (Vector3D.DistanceSquared(p.Origin, p.Position) > p.LineLength * p.LineLength) p.Grow = false;
                            p.T.UpdateShape(p.Position + -(p.Direction * p.DistanceTraveled), p.Position, p.Direction, p.DistanceTraveled);
                        }
                        else if (p.State == ProjectileState.OneAndDone)
                            p.T.UpdateShape(p.LastPosition, p.Position, p.Direction, p.MaxTrajectory);
                        else
                        {
                            var pointDir = (p.SmartsOn) ? p.VisualDir : p.Direction;
                            p.T.UpdateShape(p.Position + -(pointDir * p.LineLength), p.Position, pointDir, p.LineLength);
                        }

                    }

                    p.T.OnScreen = false;
                    if (p.ModelState == EntityState.Exists)
                    {
                        p.ModelSphereLast.Center = p.LastEntityPos;
                        p.ModelSphereCurrent.Center = p.Position;
                        if (camera.IsInFrustum(ref p.ModelSphereLast) || camera.IsInFrustum(ref p.ModelSphereCurrent) || p.FirstOffScreen)
                        {
                            p.T.OnScreen = true;
                            p.FirstOffScreen = false;
                            p.LastEntityPos = p.Position;
                        }
                    }

                    if (!p.T.OnScreen && p.DrawLine)
                    {
                        var bb = new BoundingBoxD(Vector3D.Min(p.T.PrevPosition, p.T.Position), Vector3D.Max(p.T.PrevPosition, p.T.Position));
                        if (camera.IsInFrustum(ref bb)) p.T.OnScreen = true;
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
                    p.T.UpdateShape(p.LastPosition, hitPos, p.Direction, length);
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
                    var beam = !miss ? new LineD(vt.PrevPosition, hitEntity.HitPos ?? p.Position) : new LineD(vt.PrevPosition, p.Position);
                    vt.UpdateVrShape(beam.From, beam.To, beam.Direction, beam.Length);
                }
                else
                {
                    Vector3D beamEnd;
                    var hit = !miss && hitEntity.HitPos.HasValue;
                    if (!hit)
                        beamEnd = vt.PrevPosition + (vt.Direction * p.MaxTrajectory);
                    else
                        beamEnd = vt.PrevPosition + (vt.Direction * p.T.WeaponCache.HitDistance);

                    var line = new LineD(vt.PrevPosition, beamEnd);
                    //DsDebugDraw.DrawSingleVec(vt.PrevPosition, 0.5f, Color.Red);
                    if (!miss && hitEntity.HitPos.HasValue)
                        vt.UpdateVrShape(line.From, hitEntity.HitPos.Value, line.Direction, line.Length);
                    else vt.UpdateVrShape(line.From, line.To, line.Direction, line.Length);
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
                if (!Hit(p, poolId, false))
                    p.ProjectileClose(this, poolId);
            }
            else p.ProjectileClose(this, poolId);
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
                var bb = new BoundingBoxD(Vector3D.Min(p.T.PrevPosition, p.T.Position), Vector3D.Max(p.T.PrevPosition, p.T.Position));
                if (Session.Instance.Camera.IsInFrustum(ref bb)) p.T.OnScreen = true;
            }
        }
    }
}
