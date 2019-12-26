using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectile;
using static WeaponCore.Support.ProInfo;
namespace WeaponCore.Projectiles
{
    public partial class Projectiles
    {
        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal readonly Session Session;
        internal readonly MyConcurrentPool<Fragments> ShrapnelPool = new MyConcurrentPool<Fragments>();
        internal readonly MyConcurrentPool<Fragment> FragmentPool = new MyConcurrentPool<Fragment>();
        internal readonly List<Fragments> ShrapnelToSpawn = new List<Fragments>();

        internal readonly MyConcurrentPool<List<MyEntity>> CheckPool = new MyConcurrentPool<List<MyEntity>>();
        internal readonly ObjectsPool<Projectile> ProjectilePool = new ObjectsPool<Projectile>(150);
        internal readonly MyConcurrentPool<HitEntity> HitEntityPool = new MyConcurrentPool<HitEntity>();
        internal readonly ObjectsPool<ProInfo> InfoPool = new ObjectsPool<ProInfo>(150);
        internal readonly List<ProInfo> DrawProjectiles = new List<ProInfo>();
        internal readonly List<Projectile> CleanUp = new List<Projectile>();

        internal readonly MyConcurrentPool<List<Vector3I>> V3Pool = new MyConcurrentPool<List<Vector3I>>();
        internal EntityPool<MyEntity>[] EntityPool;
        internal ulong CurrentProjectileId;

        internal Projectiles(Session session)
        {
            Session = session;
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

        internal void Update() // Methods highly inlined due to keen's mod profiler
        {
            Clean();
            SpawnFragments();
            UpdateState();
            CheckHits();
            UpdateAv();
        }

        internal void Clean()
        {
            for (int j = 0; j < CleanUp.Count; j++)
            {
                var p = CleanUp[j];
                for (int i = 0; i < p.VrInfos.Count; i++)
                {
                    var virtInfo = p.VrInfos[i];
                    virtInfo.Clean();
                    InfoPool.MarkForDeallocate(virtInfo);
                }
                p.VrInfos.Clear();

                if (p.DynamicGuidance)
                    DynTrees.UnregisterProjectile(p);
                p.PruningProxyId = -1;

                p.Info.Clean();
                ProjectilePool.MarkForDeallocate(p);
            }

            CleanUp.Clear();
            if (EntityPool != null)
            {
                foreach (var e in EntityPool)
                    e.DeallocateAllMarked();
            }

            InfoPool.DeallocateAllMarked();
            ProjectilePool.DeallocateAllMarked();
        }

        private void SpawnFragments()
        {
            for (int j = 0; j < ShrapnelToSpawn.Count; j++)
                ShrapnelToSpawn[j].Spawn();
            ShrapnelToSpawn.Clear();
        }

        private void UpdateState()
        {
            foreach (var p in ProjectilePool.Active)
            {
                p.Info.Age++;
                p.Active = false;
                switch (p.State)
                {
                    case ProjectileState.Destroy:
                        p.DestroyProjectile();
                        continue;
                    case ProjectileState.Dead:
                        continue;
                    case ProjectileState.Start:
                        p.Start();
                        break;
                    case ProjectileState.OneAndDone:
                    case ProjectileState.Depleted:
                    case ProjectileState.Detonate:
                        p.ProjectileClose();
                        continue;
                }
                if (p.Info.Target.IsProjectile)
                    if (p.Info.Target.Projectile.State != ProjectileState.Alive)
                        p.UnAssignProjectile(true);

                p.Info.OnScreen = Screen.None;

                if (p.AccelLength > 0)
                {
                    if (p.SmartsOn) p.RunSmart();
                    else
                    {
                        var accel = true;
                        Vector3D newVel;
                        if (p.FieldTime > 0)
                        {
                            var distToMax = p.MaxTrajectory - p.Info.DistanceTraveled;

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

                p.Info.PrevDistanceTraveled = p.Info.DistanceTraveled;
                p.Info.DistanceTraveled += Math.Abs(Vector3D.Dot(p.Direction, p.Velocity * StepConst));
                if (p.ModelState == EntityState.Exists)
                {
                    var matrix = MatrixD.CreateWorld(p.Position, p.VisualDir, MatrixD.Identity.Up);

                    if (p.Info.System.PrimeModelId != -1)
                        p.Info.PrimeMatrix = matrix;
                    if (p.Info.System.TriggerModelId != -1 && p.Info.TriggerGrowthSteps < p.Info.System.AreaEffectSize)
                        p.Info.TriggerMatrix = matrix;

                    if (p.EnableAv && p.AmmoEffect != null && p.Info.System.AmmoParticle && p.Info.System.PrimeModelId != -1)
                    {
                        var offVec = p.Position + Vector3D.Rotate(p.Info.System.Values.Graphics.Particles.Ammo.Offset, p.Info.PrimeMatrix);
                        p.AmmoEffect.WorldMatrix = p.Info.PrimeMatrix;
                        p.AmmoEffect.SetTranslation(offVec);
                    }
                }
                else if (!p.ConstantSpeed && p.EnableAv && p.AmmoEffect != null && p.Info.System.AmmoParticle)
                    p.AmmoEffect.Velocity = p.Velocity;

                if (p.DynamicGuidance)
                    DynTrees.OnProjectileMoved(p, ref p.Velocity);

                if (p.State != ProjectileState.OneAndDone)
                {
                    if (p.Info.DistanceTraveled * p.Info.DistanceTraveled >= p.DistanceToTravelSqr)
                    {
                        if (p.FieldTime > 0) 
                        {
                            p.FieldTime--;
                            if (p.Info.System.IsMine && !p.MineSeeking && !p.MineActivated)
                            {
                                p.Info.Cloaked = p.Info.System.Values.Ammo.Trajectory.Mines.Cloak;
                                p.MineSeeking = true;
                            }
                        }
                    }
                }

                if (p.Info.System.Ewar)
                    p.RunEwar();
                p.Active = true;
            }
        }

        private void CheckHits()
        {
            foreach (var p in ProjectilePool.Active)
            {
                p.Miss = false;

                if (!p.Active || (int)p.State > 3) continue;
                var beam = new LineD(p.LastPosition, p.Position);

                if ((p.FieldTime <= 0 && p.State != ProjectileState.OneAndDone && p.Info.DistanceTraveled * p.Info.DistanceTraveled >= p.DistanceToTravelSqr))
                {
                    var dInfo = p.Info.System.Values.Ammo.AreaEffect.Detonation;

                    p.PruneSphere.Center = p.Position;
                    p.PruneSphere.Radius = dInfo.DetonationRadius;

                    if (p.MoveToAndActivate || dInfo.DetonateOnEnd && (!dInfo.ArmOnlyOnHit || p.Info.ObjectsHit > 0))
                    {
                        var checkList = CheckPool.Get();
                        MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref p.PruneSphere, checkList,
                            p.PruneQuery);
                        for (int i = 0; i < checkList.Count; i++)
                            p.SegmentList.Add(new MyLineSegmentOverlapResult<MyEntity>
                                {Distance = 0, Element = checkList[i]});

                        if (p.Info.System.TrackProjectile)
                            foreach (var lp in p.Info.Ai.LiveProjectile)
                                if (p.PruneSphere.Contains(lp.Position) != ContainmentType.Disjoint &&
                                    lp != p.Info.Target.Projectile)
                                    ProjectileHit(p, lp, p.Info.System.CollisionIsLine);

                        checkList.Clear();
                        CheckPool.Return(checkList);
                        p.State = ProjectileState.Detonate;
                        p.ForceHitParticle = true;
                    }
                    else p.State = ProjectileState.Detonate;
                }
                else if (p.MineSeeking && !p.MineTriggered)
                    p.SeekEnemy();
                else if (p.Info.System.CollisionIsLine)
                {
                    p.PruneSphere.Center = p.Position;
                    p.PruneSphere.Radius = p.Info.System.CollisionSize;
                    if (p.PruneSphere.Contains(new BoundingSphereD(p.Info.Origin, p.DeadZone)) == ContainmentType.Disjoint)
                        MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref beam, p.SegmentList, p.PruneQuery);
                }
                else
                {
                    p.PruneSphere = new BoundingSphereD(p.Position, 0).Include(new BoundingSphereD(p.LastPosition, 0));
                    var currentRadius = p.Info.TriggerGrowthSteps < p.Info.System.AreaEffectSize ? p.Info.TriggerMatrix.Scale.AbsMax() : p.Info.System.AreaEffectSize;
                    if (p.EwarActive && p.PruneSphere.Radius < currentRadius)
                    {
                        p.PruneSphere.Center = p.Position;
                        p.PruneSphere.Radius = currentRadius;
                    }
                    else if (p.PruneSphere.Radius < p.Info.System.CollisionSize)
                    {
                        p.PruneSphere.Center = p.Position;
                        p.PruneSphere.Radius = p.Info.System.CollisionSize;
                    }

                    if (!((p.Info.System.SelfDamage || p.Gunner) && !p.EwarActive && p.PruneSphere.Contains(new BoundingSphereD(p.Info.Origin, p.DeadZone)) != ContainmentType.Disjoint))
                    {
                        var checkList = CheckPool.Get();
                        MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref p.PruneSphere, checkList, p.PruneQuery);
                        for (int i = 0; i < checkList.Count; i++)
                            p.SegmentList.Add(new MyLineSegmentOverlapResult<MyEntity> { Distance = 0, Element = checkList[i] });

                        checkList.Clear();
                        CheckPool.Return(checkList);
                    }
                }

                if (p.Info.Target.IsProjectile || p.SegmentList.Count > 0)
                {
                    var nearestHitEnt = GetAllEntitiesInLine(p, beam);

                    if (nearestHitEnt != null && p.Intersected(nearestHitEnt))
                        continue;
                }

                p.Miss = true;
                p.Info.HitList.Clear();
            }
        }

        private void UpdateAv()
        {
            foreach (var p in ProjectilePool.Active)
            {
                if (!p.EnableAv || !p.Miss || (int)p.State > 3) continue;
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

                if (p.Info.System.AmmoParticle)
                {
                    p.TestSphere.Center = p.Position;
                    if (Session.Camera.IsInFrustum(ref p.TestSphere))
                    {
                        if (!p.Info.System.IsBeamWeapon && !p.ParticleStopped && p.AmmoEffect != null && p.Info.System.AmmoParticleShrinks)
                            p.AmmoEffect.UserEmitterScale = MathHelper.Clamp(MathHelper.Lerp(p.BaseAmmoParticleScale, 0, p.Info.DistanceToLine / p.Info.System.Values.Graphics.Particles.Hit.Extras.MaxDistance), 0, p.BaseAmmoParticleScale);

                        if ((p.ParticleStopped || p.ParticleLateStart))
                            p.PlayAmmoParticle();
                    }
                    else if (!p.ParticleStopped && p.AmmoEffect != null)
                        p.DisposeAmmoEffect(false, true);
                }

                if (p.Info.System.DrawLine || p.ModelState == EntityState.None && p.Info.System.AmmoParticle)
                {
                    if (p.State == ProjectileState.OneAndDone)
                        p.Info.UpdateShape(p.Position, p.Direction, p.MaxTrajectory, ReSize.None);
                    else if (p.ModelState == EntityState.None && p.Info.System.AmmoParticle && !p.Info.System.DrawLine)
                        p.Info.UpdateShape(p.Position, p.Direction, p.Info.System.CollisionSize, ReSize.None);
                    else
                    {
                        p.Info.ProjectileDisplacement += Math.Abs(Vector3D.Dot(p.Direction, (p.Velocity - p.StartSpeed) * StepConst));
                        var displaceDiff = p.Info.ProjectileDisplacement - p.TracerLength;
                        if (p.Info.ProjectileDisplacement < p.TracerLength && Math.Abs(displaceDiff) > 0.001)
                        {
                            p.Info.UpdateShape(p.Position, p.Direction, p.Info.ProjectileDisplacement, ReSize.Grow);
                        }
                        else
                        {
                            var pointDir = (p.SmartsOn) ? p.VisualDir : p.Direction;
                            var drawStartPos = p.ConstantSpeed && p.AccelLength > p.TracerLength ? p.LastPosition : p.Position;
                            p.Info.UpdateShape(drawStartPos, pointDir, p.TracerLength, ReSize.None);
                        }
                    }
                }
                if (p.ModelState == EntityState.Exists)
                {
                    p.ModelSphereLast.Center = p.LastEntityPos;
                    p.ModelSphereCurrent.Center = p.Position;
                    if (p.Info.Triggered)
                    {
                        var currentRadius = p.Info.TriggerGrowthSteps < p.Info.System.AreaEffectSize ? p.Info.TriggerMatrix.Scale.AbsMax() : p.Info.System.AreaEffectSize;
                        p.ModelSphereLast.Radius = currentRadius;
                        p.ModelSphereCurrent.Radius = currentRadius;
                    }
                    if (Session.Camera.IsInFrustum(ref p.ModelSphereLast) || Session.Camera.IsInFrustum(ref p.ModelSphereCurrent) || p.FirstOffScreen)
                    {
                        p.Info.OnScreen = Screen.Tracer;
                        p.FirstOffScreen = false;
                        p.LastEntityPos = p.Position;
                    }
                }

                if (p.Info.OnScreen == Screen.None && (p.Info.System.DrawLine || p.ModelState == EntityState.None && p.Info.System.AmmoParticle))
                {

                    var bb = new BoundingBoxD(Vector3D.Min(p.Info.LineStart, p.Info.Position), Vector3D.Max(p.Info.LineStart, p.Info.Position));
                    if (Session.Camera.IsInFrustum(ref bb)) p.Info.OnScreen = Screen.Tracer;
                    
                    if (p.Info.OnScreen == Screen.None && p.Info.System.Trail)
                        p.Info.OnScreen = Screen.Tail;
                }

                if (p.Info.MuzzleId == -1)
                {
                    p.CreateFakeBeams(null, DrawProjectiles, true);
                    continue;
                }

                if (p.Info.OnScreen != Screen.None)
                {
                    p.Info.Complete(null, DrawState.Default);
                    DrawProjectiles.Add(p.Info);
                }
            }
        }
    }
}
