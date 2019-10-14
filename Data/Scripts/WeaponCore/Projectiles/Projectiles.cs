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
        internal const int PoolCount = 8;
        internal readonly Session Session;
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
        internal readonly bool[] ModelClosed = new bool[PoolCount];

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

        internal Projectiles(Session session)
        {
            Session = session;
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
            if (Session.HighLoad)
            {
                MyAPIGateway.Parallel.For(0, Wait.Length, i =>
                {
                    lock (Wait[i])
                    {
                        UpdateState(i);
                        CheckHits(i);
                        UpdateAv(i);
                    }
                }, 1);
            }
            else
            {
                for (int i = 0; i < Wait.Length; i++)
                {
                    lock (Wait[i])
                    {
                        UpdateState(i);
                        CheckHits(i);
                        UpdateAv(i);
                    }
                }
            }

            for (int i = 0; i < Wait.Length; i++)
                lock (Wait[i])
                    Clean(i);
        }

        private void UpdateState(int i)
        {
            var noAv = Session.DedicatedServer;
            ModelClosed[i] = false;
            var pool = ProjectilePool[i];
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
                p.Active = false;

                switch (p.State)
                {
                    case ProjectileState.Dead:
                        continue;
                    case ProjectileState.Start:
                        p.Start(this, noAv, i);
                        if (p.ModelState == EntityState.NoDraw)
                            ModelClosed[i] = p.CloseModel();
                        break;
                    case ProjectileState.Ending:
                    case ProjectileState.OneAndDone:
                    case ProjectileState.Depleted:
                        if (p.State == ProjectileState.Depleted)
                            p.ProjectileClose();
                        if (p.ModelState != EntityState.Exists) p.Stop();
                        else
                        {
                            ModelClosed[i] = p.CloseModel();
                            p.Stop();
                        }
                        continue;
                    case ProjectileState.Alive:
                        p.T.Target.IsProjectile = p.T.Target.IsProjectile && (p.T.Target.Projectile.T.BaseHealthPool > 0);
                        break;
                }

                if (p.AccelLength > 0)
                {
                    if (p.SmartsOn) p.RunSmart();
                    else
                    {
                        var accel = true;
                        Vector3D newVel;
                        if (p.FieldTime > 0)
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
                        }
                        else
                        {
                            newVel = p.Velocity + p.AccelVelocity;
                            p.VelocityLengthSqr = newVel.LengthSquared();
                            if (p.VelocityLengthSqr > p.MaxSpeedSqr) newVel = p.Direction * p.MaxSpeed;
                        }
                        p.Velocity = newVel;
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

                    if (p.T.System.PrimeModelId != -1)
                        p.T.PrimeMatrix = matrix;
                    if (p.T.System.TriggerModelId != -1 && p.T.TriggerGrowthSteps < p.T.System.AreaEffectSize)
                        p.T.TriggerMatrix = matrix;

                    if (p.EnableAv && p.AmmoEffect != null && p.T.System.AmmoParticle && p.T.System.PrimeModelId != -1)
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
                        if (p.FieldTime > 0) 
                        {
                            p.FieldTime--;
                            if (p.T.System.IsMine && !p.MineSeeking && !p.MineActivated)
                            {
                                p.T.Cloaked = p.T.System.Values.Ammo.Trajectory.Mines.Cloak;
                                p.MineSeeking = true;
                            }
                        }
                    }
                }

                if (p.Ewar)
                    p.RunEwar();
                p.Active = true;
            }
        }

        private void CheckHits(int poolId)
        {
            var pool = ProjectilePool[poolId];
            foreach (var p in pool.Active)
            {
                p.Miss = false;
                if (!p.Active) continue;
                var beam = new LineD(p.LastPosition, p.Position);

                if ((p.FieldTime <= 0 && p.State != ProjectileState.OneAndDone && p.T.DistanceTraveled * p.T.DistanceTraveled >= p.DistanceToTravelSqr))
                {
                    p.T.End = true;
                    var dInfo = p.T.System.Values.Ammo.AreaEffect.Detonation;

                    p.PruneSphere.Center = p.Position;
                    p.PruneSphere.Radius = dInfo.DetonationRadius;
                    if (p.MoveToAndActivate || dInfo.DetonateOnEnd && (!dInfo.ArmOnlyOnHit || p.T.ObjectsHit > 0))
                    {
                        var checkList = CheckPool[poolId].Get();
                        MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref p.PruneSphere, checkList, p.PruneQuery);
                        for (int i = 0; i < checkList.Count; i++)
                            p.SegmentList.Add(new MyLineSegmentOverlapResult<MyEntity> { Distance = 0, Element = checkList[i] });

                        checkList.Clear();
                        CheckPool[poolId].Return(checkList);
                        p.HitEffects(true);
                    }
                }
                else if (p.MineSeeking && !p.MineTriggered)
                    SeekEnemy(p, poolId);
                else if (p.T.System.CollisionIsLine)
                {
                    p.PruneSphere.Center = p.Position;
                    p.PruneSphere.Radius = p.T.System.CollisionSize;
                    if (p.PruneSphere.Contains(new BoundingSphereD(p.T.Origin, p.DeadZone)) == ContainmentType.Disjoint)
                        MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, p.SegmentList, p.PruneQuery);
                }
                else
                {
                    p.PruneSphere = new BoundingSphereD(p.Position, 0).Include(new BoundingSphereD(p.LastPosition, 0));
                    var currentRadius = p.T.TriggerGrowthSteps < p.T.System.AreaEffectSize ? p.T.TriggerMatrix.Scale.AbsMax() : p.T.System.AreaEffectSize;
                    if (p.EwarActive && p.PruneSphere.Radius < currentRadius)
                    {
                        p.PruneSphere.Center = p.Position;
                        p.PruneSphere.Radius = currentRadius;
                    }
                    else if (p.PruneSphere.Radius < p.T.System.CollisionSize)
                    {
                        p.PruneSphere.Center = p.Position;
                        p.PruneSphere.Radius = p.T.System.CollisionSize;
                    }

                    if (!(p.SelfDamage && !p.EwarActive && p.PruneSphere.Contains(new BoundingSphereD(p.T.Origin, p.DeadZone)) != ContainmentType.Disjoint))
                    {
                        var checkList = CheckPool[poolId].Get();
                        MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref p.PruneSphere, checkList, p.PruneQuery);
                        for (int i = 0; i < checkList.Count; i++)
                            p.SegmentList.Add(new MyLineSegmentOverlapResult<MyEntity> { Distance = 0, Element = checkList[i] });

                        checkList.Clear();
                        CheckPool[poolId].Return(checkList);
                    }
                }

                if (p.SegmentList.Count > 0)
                {
                    var nearestHitEnt = GetAllEntitiesInLine(p, beam, poolId);
                    if (nearestHitEnt != null && p.Intersected(p, DrawProjectiles[poolId], nearestHitEnt))
                        continue;
                }
                if (p.T.End) p.ProjectileClose();

                p.Miss = true;
                p.T.HitList.Clear();
            }
        }

        private void UpdateAv(int poolId)
        {
            var drawList = DrawProjectiles[poolId];
            var camera = Session.Camera;

            var pool = ProjectilePool[poolId];
            foreach (var p in pool.Active)
            {
                if (!p.EnableAv || !p.Miss) continue;

                if (p.SmartsOn)
                {
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

                if (p.T.System.AmmoParticle)
                {
                    p.TestSphere.Center = p.Position;
                    if (camera.IsInFrustum(ref p.TestSphere))
                    {
                        if ((p.ParticleStopped || p.ParticleLateStart))
                            p.PlayAmmoParticle();
                    }
                    else if (!p.ParticleStopped && p.AmmoEffect != null)
                        p.DisposeAmmoEffect(false, true);
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
                    p.CreateFakeBeams(p, null, drawList, true);
                    continue;
                }

                if (p.T.OnScreen)
                {
                    p.T.Complete(null, DrawState.Default);
                    drawList.Add(p.T);
                }
            }
        }

        private void Clean(int poolId)
        {
            lock (Wait[poolId])
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
                }
                cleanUp.Clear();
                if (ModelClosed[poolId])
                    foreach (var e in EntityPool[poolId])
                        e.DeallocateAllMarked();

                TrajectilePool[poolId].DeallocateAllMarked();
                ProjectilePool[poolId].DeallocateAllMarked();
            }
        }
    }
}
