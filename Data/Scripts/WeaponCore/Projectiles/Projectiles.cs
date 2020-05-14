using System;
using System.Collections.Generic;
using ParallelTasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Profiler;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectile;
using static WeaponCore.Support.AvShot;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.TrajectoryDef;

namespace WeaponCore.Projectiles
{
    public partial class Projectiles
    {
        private const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal readonly Session Session;
        internal readonly MyConcurrentPool<ProInfo> VirtInfoPool = new MyConcurrentPool<ProInfo>(128, vInfo => vInfo.Clean());
        internal readonly MyConcurrentPool<Fragments> ShrapnelPool = new MyConcurrentPool<Fragments>(32);
        internal readonly MyConcurrentPool<Fragment> FragmentPool = new MyConcurrentPool<Fragment>(32);
        internal readonly MyConcurrentPool<HitEntity> HitEntityPool = new MyConcurrentPool<HitEntity>(32, hitEnt => hitEnt.Clean());

        internal readonly List<Projectile> AddTargets = new List<Projectile>();
        internal readonly List<Fragments> ShrapnelToSpawn = new List<Fragments>(32);
        internal readonly List<Projectile> ValidateHits = new List<Projectile>(128);
        internal readonly Stack<Projectile> ProjectilePool = new Stack<Projectile>(2048);
        internal readonly List<Projectile> ActiveProjetiles = new List<Projectile>(2048);
        internal readonly List<DeferedAv> DeferedAvDraw = new List<DeferedAv>(256);

        internal ulong CurrentProjectileId;

        internal Projectiles(Session session)
        {
            Session = session;
        }

        internal void Stage1() // Methods highly inlined due to keen's mod profiler
        {


            if (!Session.DedicatedServer) 
                DeferedAvStateUpdates(Session);

            if (AddTargets.Count > 0)
                AddProjectileTargets();

            Session.StallReporter.Start("UpdateState", 17);
            if (ActiveProjetiles.Count > 0) 
                UpdateState();
            Session.StallReporter.End();

            Session.StallReporter.Start("Spawn", 17);
            if (ShrapnelToSpawn.Count > 0)
                SpawnFragments();
            Session.StallReporter.End();

            Session.StallReporter.Start("CheckHits", 17);
            if (false && ActiveProjetiles.Count > 0)
                Session.PTask = MyAPIGateway.Parallel.StartBackground(CheckHits);
            else if (ActiveProjetiles.Count > 0)
                CheckHits();
            Session.StallReporter.End();
        }

        internal void Stage2() // Methods highly inlined due to keen's mod profiler
        {
            Session.StallReporter.Start("Stage2-TaskWait", 17);
            if (!Session.PTask.IsComplete)
                Session.PTask.WaitOrExecute();

            if (Session.PTask.IsComplete && Session.PTask.valid && Session.PTask.Exceptions != null)
                Session.TaskHasErrors(ref Session.PTask, "PTask");
            Session.StallReporter.End();

            Session.StallReporter.Start("ConfirmHit", 17);
            ConfirmHit();
            Session.StallReporter.End();

            if (!Session.DedicatedServer)
                UpdateAv();
        }



        private void SpawnFragments()
        {
            if (Session.FragmentsNeedingEntities.Count > 0)
                PrepFragmentEntities();

            int spawned = 0;
            for (int j = 0; j < ShrapnelToSpawn.Count; j++)
            {
                int count;
                ShrapnelToSpawn[j].Spawn(out count);
                spawned += count;
            }
            ShrapnelToSpawn.Clear();

            if (AddTargets.Count > 0)
                AddProjectileTargets();

            var end = ActiveProjetiles.Count - spawned;
            UpdateState(end);
        }

        internal void PrepFragmentEntities()
        {
            for (int i = 0; i < Session.FragmentsNeedingEntities.Count; i++)
            {
                var frag = Session.FragmentsNeedingEntities[i];
                if (frag.AmmoDef.Const.PrimeModel && frag.PrimeEntity == null) frag.PrimeEntity = frag.AmmoDef.Const.PrimeEntityPool.Get();
                if (frag.AmmoDef.Const.TriggerModel && frag.TriggerEntity == null) frag.TriggerEntity = Session.TriggerEntityPool.Get();
            }
            Session.FragmentsNeedingEntities.Clear();
        }

        internal void AddProjectileTargets()
        {
            for (int i = 0; i < AddTargets.Count; i++) {

                var p = AddTargets[i];
                for (int t = 0; t < p.Info.Ai.TargetAis.Count; t++) {

                    var targetAi = p.Info.Ai.TargetAis[t];
                    var addProjectile = p.Info.AmmoDef.Trajectory.Guidance != GuidanceType.None && targetAi.PointDefense;
                    if (!addProjectile && targetAi.PointDefense) {

                        if (Vector3.Dot(p.Info.Direction, p.Info.Origin - targetAi.MyGrid.PositionComp.WorldMatrixRef.Translation) < 0) {

                            var targetSphere = targetAi.MyGrid.PositionComp.WorldVolume;
                            targetSphere.Radius *= 3;
                            var testRay = new RayD(p.Info.Origin, p.Info.Direction);
                            var quickCheck = Vector3D.IsZero(targetAi.GridVel, 0.025) && targetSphere.Intersects(testRay) != null;
                            
                            if (!quickCheck) {

                                var deltaPos = targetSphere.Center - p.Info.Origin;
                                var deltaVel = targetAi.GridVel - p.Info.Ai.GridVel;
                                var timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, p.Info.AmmoDef.Const.DesiredProjectileSpeed);
                                var predictedPos = targetSphere.Center + (float)timeToIntercept * deltaVel;
                                targetSphere.Center = predictedPos;
                            }

                            if (quickCheck || targetSphere.Intersects(testRay) != null)
                                addProjectile = true;
                        }
                    }
                    if (addProjectile) {

                        targetAi.LiveProjectile.Add(p);
                        targetAi.LiveProjectileTick = Session.Tick;
                        targetAi.NewProjectileTick = Session.Tick;
                        p.Watchers.Add(targetAi);
                    }
                }
            }
            AddTargets.Clear();
        }

        private void UpdateState(int end = 0)
        {
            var t = 0;
            for (int i = ActiveProjetiles.Count - 1; i >= end; i--)
            {
                t++;
                var p = ActiveProjetiles[i];
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
                        ProjectilePool.Push(p);
                        ActiveProjetiles.RemoveAtFast(i);
                        continue;
                }
                if (p.Info.Target.IsProjectile)
                    if (p.Info.Target.Projectile.State != ProjectileState.Alive)
                        p.UnAssignProjectile(true);

                if (p.FeelsGravity)
                {
                    var update = p.FakeGravityNear || p.EntitiesNear || p.Info.Ai.InPlanetGravity && p.Info.Age % 30 == 0 || !p.Info.Ai.InPlanetGravity && p.Info.Age % 10 == 0;
                    if (update)
                    {
                        p.Gravity = MyParticlesManager.CalculateGravityInPoint(p.Position);

                        if (!p.Info.Ai.InPlanetGravity && !MyUtils.IsZero(p.Gravity)) p.FakeGravityNear = true;
                        else p.FakeGravityNear = false;
                    }
                    p.Velocity += (p.Gravity * p.Info.AmmoDef.Trajectory.GravityMultiplier) * Projectile.StepConst;
                    Vector3D.Normalize(ref p.Velocity, out p.Info.Direction);
                }

                if (p.AccelLength > 0 && !p.Info.TriggeredPulse)
                {
                    if (p.SmartsOn) p.RunSmart();
                    else
                    {
                        var accel = true;
                        Vector3D newVel;
                        if (p.FieldTime > 0)
                        {
                            var distToMax = p.Info.MaxTrajectory - p.Info.DistanceTraveled;

                            var stopDist = p.VelocityLengthSqr / 2 / (p.StepPerSec);
                            if (distToMax <= stopDist)
                                accel = false;

                            newVel = accel ? p.Velocity + p.AccelVelocity : p.Velocity - p.AccelVelocity;
                            p.VelocityLengthSqr = newVel.LengthSquared();

                            if (accel && p.VelocityLengthSqr > p.MaxSpeedSqr) newVel = p.Info.Direction * p.MaxSpeed;
                            else if (!accel && distToMax <= 0)
                            {
                                newVel = Vector3D.Zero;
                                p.VelocityLengthSqr = 0;
                            }
                        }
                        else
                        {
                            newVel = p.Velocity + p.AccelVelocity;
                            p.VelocityLengthSqr = newVel.LengthSquared();
                            if (p.VelocityLengthSqr > p.MaxSpeedSqr) newVel = p.Info.Direction * p.MaxSpeed;
                        }

                        p.Velocity = newVel;
                    }
                }

                if (p.State == ProjectileState.OneAndDone)
                {
                    p.LastPosition = p.Position;
                    var beamEnd = p.Position + (p.Info.Direction * p.Info.MaxTrajectory);
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

                double distChanged;
                Vector3D.Dot(ref p.Info.Direction, ref p.TravelMagnitude, out distChanged);
                p.Info.DistanceTraveled += Math.Abs(distChanged);
                if (p.ModelState == EntityState.Exists)
                {
                    var up = MatrixD.Identity.Up;
                    MatrixD matrix;
                    MatrixD.CreateWorld(ref p.Position, ref p.Info.VisualDir, ref up, out matrix);
                    
                    if (p.Info.AmmoDef.Const.PrimeModel)
                        p.Info.AvShot.PrimeMatrix = matrix;
                    if (p.Info.AmmoDef.Const.TriggerModel && p.Info.TriggerGrowthSteps < p.Info.AmmoDef.Const.AreaEffectSize)
                        p.Info.TriggerMatrix = matrix;
                }

                if (p.DynamicGuidance)
                {
                    if (p.PruningProxyId != -1)
                    {
                        var sphere = new BoundingSphereD(p.Position, p.Info.AmmoDef.Const.AreaEffectSize);
                        BoundingBoxD result;
                        BoundingBoxD.CreateFromSphere(ref sphere, out result);
                        Session.ProjectileTree.MoveProxy(p.PruningProxyId, ref result, p.Velocity);
                    }
                }

                if (p.State != ProjectileState.OneAndDone)
                {
                    if (!p.SmartsOn && p.Info.Age > p.Info.AmmoDef.Const.MaxLifeTime)
                    {
                        p.DistanceToTravelSqr = p.Info.DistanceTraveled * p.Info.DistanceTraveled;
                        p.EarlyEnd = true;
                    }

                    if (p.Info.DistanceTraveled * p.Info.DistanceTraveled >= p.DistanceToTravelSqr)
                    {
                        p.AtMaxRange = true;
                        if (p.FieldTime > 0)
                        {
                            p.FieldTime--;
                            if (p.Info.AmmoDef.Const.IsMine && !p.MineSeeking && !p.MineActivated)
                            {
                                if (p.EnableAv) p.Info.AvShot.Cloaked = p.Info.AmmoDef.Trajectory.Mines.Cloak;
                                p.MineSeeking = true;
                            }
                        }
                    }
                }
                else p.AtMaxRange = true;
                if (p.Info.AmmoDef.Const.Ewar)
                    p.RunEwar();

                p.Active = true;
            }
        }

        private void CheckHits()
        {
            for (int x = ActiveProjetiles.Count - 1; x >= 0; x--) {

                var p = ActiveProjetiles[x];
                p.Miss = false;

                if (!p.Active || (int)p.State > 3) continue;
                var triggerRange = p.Info.AmmoDef.Const.EwarTriggerRange > 0 && !p.Info.TriggeredPulse ? p.Info.AmmoDef.Const.EwarTriggerRange : 0;
                var useEwarSphere = triggerRange > 0 || p.Info.EwarActive;
                p.Beam = useEwarSphere ? new LineD(p.Position + (-p.Info.Direction * p.Info.AmmoDef.Const.EwarTriggerRange), p.Position + (p.Info.Direction * p.Info.AmmoDef.Const.EwarTriggerRange)) : new LineD(p.LastPosition, p.Position);
                if ((p.FieldTime <= 0 && p.State != ProjectileState.OneAndDone && p.Info.DistanceTraveled * p.Info.DistanceTraveled >= p.DistanceToTravelSqr)) {
                    
                    var dInfo = p.Info.AmmoDef.AreaEffect.Detonation;
                    p.PruneSphere.Center = p.Position;
                    p.PruneSphere.Radius = dInfo.DetonationRadius;

                    if (p.MoveToAndActivate || dInfo.DetonateOnEnd && (!dInfo.ArmOnlyOnHit || p.Info.ObjectsHit > 0)) {
                        MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref p.PruneSphere, p.CheckList,
                            p.PruneQuery);
                        for (int i = 0; i < p.CheckList.Count; i++)
                            p.SegmentList.Add(new MyLineSegmentOverlapResult<MyEntity>
                            { Distance = 0, Element = p.CheckList[i] });
                        if (p.Info.System.TrackProjectile)
                            foreach (var lp in p.Info.Ai.LiveProjectile)
                                if (p.PruneSphere.Contains(lp.Position) != ContainmentType.Disjoint && lp != p.Info.Target.Projectile)
                                    ProjectileHit(p, lp, p.Info.AmmoDef.Const.CollisionIsLine, ref p.Beam);

                        p.CheckList.Clear();
                        p.State = ProjectileState.Detonate;

                        if (p.EnableAv)
                            p.Info.AvShot.ForceHitParticle = true;
                    }
                    else
                        p.State = ProjectileState.Detonate;

                    p.EarlyEnd = true;
                    p.Hit.HitPos = p.Position;

                }

                var sphere = false;
                var line = false;

                if (p.MineSeeking && !p.MineTriggered)
                    p.SeekEnemy();
                else if (useEwarSphere) {
                    if (p.Info.EwarActive) {
                        p.PruneSphere = new BoundingSphereD(p.Position, 0).Include(new BoundingSphereD(p.LastPosition, 0));
                        var currentRadius = p.Info.TriggerGrowthSteps < p.Info.AmmoDef.Const.AreaEffectSize ? p.Info.TriggerMatrix.Scale.AbsMax() : p.Info.AmmoDef.Const.AreaEffectSize;
                        if (p.PruneSphere.Radius < currentRadius) {
                            p.PruneSphere.Center = p.Position;
                            p.PruneSphere.Radius = currentRadius;
                        }
                    }
                    else
                        p.PruneSphere = new BoundingSphereD(p.Position, triggerRange);

                    if (p.PruneSphere.Contains(p.DeadSphere) == ContainmentType.Disjoint)
                        sphere = true;
                }
                else if (p.Info.AmmoDef.Const.CollisionIsLine) {
                    p.PruneSphere.Center = p.Position;
                    p.PruneSphere.Radius = p.Info.AmmoDef.Const.CollisionSize;
                    if (p.Info.AmmoDef.Const.IsBeamWeapon || p.PruneSphere.Contains(p.DeadSphere) == ContainmentType.Disjoint)
                        line = true;
                }
                else {
                    sphere = true;
                    p.PruneSphere = new BoundingSphereD(p.Position, 0).Include(new BoundingSphereD(p.LastPosition, 0));
                    if (p.PruneSphere.Radius < p.Info.AmmoDef.Const.CollisionSize) {
                        p.PruneSphere.Center = p.Position;
                        p.PruneSphere.Radius = p.Info.AmmoDef.Const.CollisionSize;
                    }
                }

                if (sphere) {
                    if (p.DynamicGuidance && p.PruneQuery == MyEntityQueryType.Dynamic && p.Info.Ai.Session.Tick60) 
                        p.CheckForNearVoxel(60);

                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref p.PruneSphere, p.CheckList, p.PruneQuery);
                    for (int i = 0; i < p.CheckList.Count; i++)
                        p.SegmentList.Add(new MyLineSegmentOverlapResult<MyEntity> { Distance = 0, Element = p.CheckList[i] });
                    p.CheckList.Clear();
                }
                else if (line) {
                    if (p.DynamicGuidance && p.PruneQuery == MyEntityQueryType.Dynamic && p.Info.Ai.Session.Tick60) p.CheckForNearVoxel(60);
                    MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref p.Beam, p.SegmentList, p.PruneQuery);
                }

                if (p.Info.Target.IsProjectile || p.SegmentList.Count > 0) {
                    ValidateHits.Add(p);
                    continue;
                }

                p.Miss = true;
                p.Info.HitList.Clear();
            }
        }

        private void ConfirmHit()
        {
            for (int i = 0; i < ValidateHits.Count; i++) {
                
                var p = ValidateHits[i];
                if (GetAllEntitiesInLine(p, p.Beam) && p.Intersected())
                    continue;

                p.Miss = true;
                p.Info.HitList.Clear();

            }
            ValidateHits.Clear();
        }

        private void UpdateAv()
        {
            for (int x = ActiveProjetiles.Count - 1; x >= 0; x--) {

                var p = ActiveProjetiles[x];

                if (!p.Miss || (int)p.State > 3) continue;
                if (p.Info.MuzzleId == -1)
                {
                    p.CreateFakeBeams(true);
                    continue;
                }
                if (!p.EnableAv) continue;

                if (p.SmartsOn)
                    p.Info.VisualDir = p.Info.Direction;
                else if (p.FeelsGravity) p.Info.VisualDir = p.Info.Direction;

                if (p.LineOrNotModel)
                {
                    if (p.State == ProjectileState.OneAndDone)
                        DeferedAvDraw.Add(new DeferedAv { AvShot = p.Info.AvShot, StepSize = p.Info.MaxTrajectory, VisualLength = p.Info.MaxTrajectory, TracerFront = p.Position, TriggerGrowthSteps = p.Info.TriggerGrowthSteps, Direction = p.Info.Direction, VisualDir = p.Info.VisualDir });
                    else if (p.ModelState == EntityState.None && p.Info.AmmoDef.Const.AmmoParticle && !p.Info.AmmoDef.Const.DrawLine)
                    {
                        if (p.AtMaxRange) p.ShortStepAvUpdate(true, false);
                        else
                            DeferedAvDraw.Add(new DeferedAv { AvShot = p.Info.AvShot, StepSize = p.Info.DistanceTraveled - p.Info.PrevDistanceTraveled, VisualLength = p.Info.AmmoDef.Const.CollisionSize, TracerFront = p.Position, TriggerGrowthSteps = p.Info.TriggerGrowthSteps, Direction = p.Info.Direction, VisualDir = p.Info.VisualDir});
                    }
                    else
                    {
                        var dir = (p.Velocity - p.StartSpeed) * StepConst;
                        double distChanged;
                        Vector3D.Dot(ref p.Info.Direction, ref dir, out distChanged);

                        p.Info.ProjectileDisplacement += Math.Abs(distChanged);
                        var displaceDiff = p.Info.ProjectileDisplacement - p.Info.TracerLength;
                        if (p.Info.ProjectileDisplacement < p.Info.TracerLength && Math.Abs(displaceDiff) > 0.0001)
                        {
                            if (p.AtMaxRange) p.ShortStepAvUpdate(false, false);
                            else
                            {
                                DeferedAvDraw.Add(new DeferedAv { AvShot = p.Info.AvShot, StepSize = p.Info.DistanceTraveled - p.Info.PrevDistanceTraveled, VisualLength = p.Info.ProjectileDisplacement, TracerFront = p.Position, TriggerGrowthSteps = p.Info.TriggerGrowthSteps, Direction = p.Info.Direction, VisualDir = p.Info.VisualDir });
                            }
                        }
                        else
                        {
                            if (p.AtMaxRange) p.ShortStepAvUpdate(false, false);
                            else
                                DeferedAvDraw.Add(new DeferedAv { AvShot = p.Info.AvShot, StepSize = p.Info.DistanceTraveled - p.Info.PrevDistanceTraveled, VisualLength = p.Info.TracerLength, TracerFront = p.Position, TriggerGrowthSteps = p.Info.TriggerGrowthSteps, Direction = p.Info.Direction, VisualDir = p.Info.VisualDir });
                        }
                    }
                }

                if (p.Info.AvShot.ModelOnly)
                    DeferedAvDraw.Add(new DeferedAv { AvShot = p.Info.AvShot, StepSize = p.Info.DistanceTraveled - p.Info.PrevDistanceTraveled, VisualLength = p.Info.TracerLength, TracerFront = p.Position, TriggerGrowthSteps = p.Info.TriggerGrowthSteps, Direction = p.Info.Direction, VisualDir = p.Info.VisualDir });
            }
        }

    }
}
