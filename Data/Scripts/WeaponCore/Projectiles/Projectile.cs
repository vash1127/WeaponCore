using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.TrajectoryDef;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.AreaDamageDef;
using static WeaponCore.Support.AvShot;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace WeaponCore.Projectiles
{
    internal class Projectile
    {
        internal const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal ProjectileState State;
        internal EntityState ModelState;
        internal MyEntityQueryType PruneQuery;
        internal GuidanceType Guidance;
        internal Vector3D AccelDir;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Vector3D StartSpeed;
        internal Vector3D Velocity;
        internal Vector3D AccelVelocity;
        internal Vector3D MaxVelocity;
        internal Vector3D TravelMagnitude;
        internal Vector3D LastEntityPos;
        internal Vector3D OriginTargetPos;
        internal Vector3D PredictedTargetPos;
        internal Vector3D PrevTargetPos;
        internal Vector3D TargetOffSet;
        internal Vector3D PrevTargetOffset;
        internal Vector3 PrevTargetVel;
        internal Vector3? LastHitEntVel;
        internal Vector3 Gravity;
        internal Hit Hit = new Hit();
        internal LineD Beam;
        internal BoundingSphereD PruneSphere;
        internal BoundingSphereD DeadSphere;
        internal double AccelLength;
        internal double DistanceToTravelSqr;
        internal double VelocityLengthSqr;
        internal double DistanceFromCameraSqr;
        internal double OffsetSqr;
        internal double StepPerSec;
        internal double MaxSpeedSqr;
        internal double MaxSpeed;
        internal double VisualStep;
        internal double MaxTrajectorySqr;
        internal double PrevEndPointToCenterSqr;
        internal float DesiredSpeed;
        internal int ChaseAge;
        internal int FieldTime;
        internal int EndStep;
        internal int ZombieLifeTime;
        internal int LastOffsetTime;
        internal int PruningProxyId = -1;
        internal int CachedId;
        internal int MaxChaseTime;
        internal int NewTargets;
        internal bool PickTarget;
        internal bool EnableAv;
        internal bool ConstantSpeed;
        internal bool PositionChecked;
        internal bool MoveToAndActivate;
        internal bool LockedTarget;
        internal bool DynamicGuidance;
        internal bool GenerateShrapnel;
        internal bool LinePlanetCheck;
        internal bool SmartsOn;
        internal bool MineSeeking;
        internal bool MineActivated;
        internal bool MineTriggered;
        internal bool Miss;
        internal bool Active;
        internal bool CachedPlanetHit;
        internal bool AtMaxRange;
        internal bool ShieldBypassed;
        internal bool EarlyEnd;
        internal bool FeelsGravity;
        internal bool LineOrNotModel;
        internal bool EntitiesNear;
        internal bool FakeGravityNear;
        internal bool HadTarget;
        internal bool WasTracking;
        internal readonly ProInfo Info = new ProInfo();
        internal readonly List<MyLineSegmentOverlapResult<MyEntity>> SegmentList = new List<MyLineSegmentOverlapResult<MyEntity>>();
        internal readonly List<MyEntity> CheckList = new List<MyEntity>();
        internal readonly List<ProInfo> VrPros = new List<ProInfo>();
        internal readonly List<Projectile> EwaredProjectiles = new List<Projectile>();
        internal readonly List<GridAi> Watchers = new List<GridAi>();
        internal readonly HashSet<Projectile> Seekers = new HashSet<Projectile>();
        internal readonly List<IHitInfo> RayHits = new List<IHitInfo>();

        internal void Start()
        {
            Position = Info.Origin;
            AccelDir = Info.Direction;
            Info.VisualDir = Info.Direction;
            var cameraStart = Info.Ai.Session.CameraPos;
            Vector3D.DistanceSquared(ref cameraStart, ref Info.Origin, out DistanceFromCameraSqr);
            GenerateShrapnel = Info.AmmoDef.Const.ShrapnelId > -1;
            var probability = Info.AmmoDef.AmmoGraphics.VisualProbability;
            EnableAv = !Info.AmmoDef.Const.VirtualBeams && !Info.Ai.Session.DedicatedServer && DistanceFromCameraSqr <= Info.Ai.Session.SyncDistSqr && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));
            ModelState = EntityState.None;
            LastEntityPos = Position;
            Hit = new Hit();
            LastHitEntVel = null;
            Info.AvShot = null;
            Info.Age = 0;
            ChaseAge = 0;
            NewTargets = 0;
            ZombieLifeTime = 0;
            LastOffsetTime = 0;
            EntitiesNear = true;
            PruningProxyId = -1;
            Active = false;
            CachedPlanetHit = false;
            PositionChecked = false;
            MineSeeking = false;
            MineActivated = false;
            MineTriggered = false;
            LinePlanetCheck = false;
            AtMaxRange = false;
            ShieldBypassed = false;
            FakeGravityNear = false;
            HadTarget = false;
            WasTracking = false;
            EndStep = 0;
            Info.PrevDistanceTraveled = 0;
            Info.DistanceTraveled = 0;
            PrevEndPointToCenterSqr = double.MaxValue;
            CachedId = Info.MuzzleId == -1 ? Info.WeaponCache.VirutalId : Info.MuzzleId;
            Gravity = Vector3.Zero;
            Guidance = Info.AmmoDef.Trajectory.Guidance;
            DynamicGuidance = Guidance != GuidanceType.None && Guidance != GuidanceType.TravelTo && !Info.AmmoDef.Const.IsBeamWeapon && Info.EnableGuidance;
            if (DynamicGuidance) DynTrees.RegisterProjectile(this);
            FeelsGravity = Info.AmmoDef.Const.FeelsGravity;

            if (Guidance == GuidanceType.Smart && DynamicGuidance)
            {
                SmartsOn = true;
                MaxChaseTime = Info.AmmoDef.Const.MaxChaseTime;
            }
            else
            {
                MaxChaseTime = int.MaxValue;
                SmartsOn = false;
            }

            if (Info.Target.IsProjectile)
            {
                OriginTargetPos = Info.Target.Projectile.Position;
                Info.Target.Projectile.Seekers.Add(this);
            }
            else if (Info.Target.Entity != null) OriginTargetPos = Info.Target.Entity.PositionComp.WorldAABB.Center;
            else OriginTargetPos = Vector3D.Zero;
            LockedTarget = !Vector3D.IsZero(OriginTargetPos);

            if (SmartsOn && Info.AmmoDef.Const.TargetOffSet && LockedTarget)
            {
                OffSetTarget();
                OffsetSqr = Info.AmmoDef.Trajectory.Smarts.Inaccuracy * Info.AmmoDef.Trajectory.Smarts.Inaccuracy;
            }
            else
            {
                TargetOffSet = Vector3D.Zero;
                OffsetSqr = 0;
            }
            PrevTargetOffset = Vector3D.Zero;

            var targetSpeed = (float)(!Info.AmmoDef.Const.IsBeamWeapon ? Info.AmmoDef.Trajectory.DesiredSpeed : Info.MaxTrajectory * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
            if (Info.AmmoDef.Const.SpeedVariance && !Info.AmmoDef.Const.IsBeamWeapon)
            {
                var min = Info.AmmoDef.Trajectory.SpeedVariance.Start;
                var max = Info.AmmoDef.Trajectory.SpeedVariance.End;
                var speedVariance = (float)Info.WeaponRng.ClientProjectileRandom.NextDouble() * (max - min) + min;
                Info.WeaponRng.ClientProjectileCurrentCounter++;
                DesiredSpeed = targetSpeed + speedVariance;
            }
            else DesiredSpeed = targetSpeed;

            float variance = 0;
            if (Info.AmmoDef.Const.RangeVariance)
            {
                var min = Info.AmmoDef.Trajectory.RangeVariance.Start;
                var max = Info.AmmoDef.Trajectory.RangeVariance.End;
                variance = (float)Info.WeaponRng.ClientProjectileRandom.NextDouble() * (max - min) + min;
                Info.MaxTrajectory -= variance;
                Info.WeaponRng.ClientProjectileCurrentCounter++;
            }

            if (Vector3D.IsZero(PredictedTargetPos)) PredictedTargetPos = Position + (Info.Direction * Info.MaxTrajectory);
            PrevTargetPos = PredictedTargetPos;
            PrevTargetVel = Vector3D.Zero;
            Info.ObjectsHit = 0;
            Info.BaseHealthPool = Info.AmmoDef.Health;
            Info.BaseEwarPool = Info.AmmoDef.Health;
            Info.TracerLength = Info.AmmoDef.Const.TracerLength <= Info.MaxTrajectory ? Info.AmmoDef.Const.TracerLength : Info.MaxTrajectory;

            MaxTrajectorySqr = Info.MaxTrajectory * Info.MaxTrajectory;

            if (!Info.IsShrapnel) StartSpeed = Info.ShooterVel;

            MoveToAndActivate = LockedTarget && !Info.AmmoDef.Const.IsBeamWeapon && Guidance == GuidanceType.TravelTo;

            if (MoveToAndActivate)
            {
                var distancePos = !Vector3D.IsZero(PredictedTargetPos) ? PredictedTargetPos : OriginTargetPos;
                if (variance > 0)
                {
                    var forward = Info.WeaponRng.ClientProjectileRandom.Next(100) < 50;
                    Info.WeaponRng.ClientProjectileCurrentCounter++;
                    distancePos = forward ? distancePos + (Info.Direction * variance) : distancePos + (-Info.Direction * variance);
                }
                Vector3D.DistanceSquared(ref Info.Origin, ref distancePos, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = MaxTrajectorySqr;

            PickTarget = Info.AmmoDef.Trajectory.Smarts.OverideTarget && !Info.Target.IsFakeTarget && !Info.LockOnFireState;
            if (PickTarget || LockedTarget) NewTargets++;

            var staticIsInRange = Info.Ai.ClosestStaticSqr * 0.5 < MaxTrajectorySqr;
            var pruneStaticCheck = Info.Ai.ClosestPlanetSqr * 0.5 < MaxTrajectorySqr || Info.Ai.StaticGridInRange;
            PruneQuery = (DynamicGuidance && pruneStaticCheck) || FeelsGravity && staticIsInRange ? MyEntityQueryType.Both : MyEntityQueryType.Dynamic;
            
            if (DynamicGuidance && PruneQuery == MyEntityQueryType.Dynamic && staticIsInRange) CheckForNearVoxel(60);

            if (!DynamicGuidance && !FeelsGravity && staticIsInRange)
                StaticEntCheck();
            else if (Info.Ai.PlanetSurfaceInRange && Info.Ai.ClosestPlanetSqr <= MaxTrajectorySqr) 
                LinePlanetCheck = true;

            var accelPerSec = Info.AmmoDef.Trajectory.AccelPerSec;
            ConstantSpeed = accelPerSec <= 0;
            StepPerSec = accelPerSec > 0 ? accelPerSec : DesiredSpeed;
            var desiredSpeed = (Info.Direction * DesiredSpeed);
            var relativeSpeedCap = StartSpeed + desiredSpeed;
            MaxVelocity = relativeSpeedCap.LengthSquared() > desiredSpeed.LengthSquared() ? relativeSpeedCap : Vector3D.Zero + desiredSpeed;
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            AccelLength = accelPerSec * StepConst;
            AccelVelocity = (Info.Direction * AccelLength);

            if (ConstantSpeed)
            {
                Velocity = MaxVelocity;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = StartSpeed + AccelVelocity;

            TravelMagnitude = Velocity * StepConst;
            FieldTime = Info.AmmoDef.Const.Ewar || Info.AmmoDef.Const.IsMine ? Info.AmmoDef.Trajectory.FieldTime : 0;

            State = !Info.AmmoDef.Const.IsBeamWeapon ? ProjectileState.Alive : ProjectileState.OneAndDone;

            if (EnableAv)
            {
                Info.AvShot = Info.Ai.Session.Av.AvShotPool.Get();
                Info.AvShot.Init(Info, StepPerSec * StepConst, MaxSpeed);
                Info.AvShot.SetupSounds(DistanceFromCameraSqr);
                if (Info.AmmoDef.Const.HitParticle && !Info.AmmoDef.Const.IsBeamWeapon || Info.AmmoDef.Const.AreaEffect == AreaEffectType.Explosive && !Info.AmmoDef.AreaEffect.Explosions.NoVisuals && Info.AmmoDef.AreaEffect.AreaEffectRadius > 0 && Info.AmmoDef.AreaEffect.AreaEffectDamage > 0)
                {
                    var hitPlayChance = Info.AmmoDef.AmmoGraphics.Particles.Hit.Extras.HitPlayChance;
                    Info.AvShot.HitParticleActive = hitPlayChance >= 1 || hitPlayChance >= MyUtils.GetRandomDouble(0.0f, 1f);
                }
                Info.AvShot.FakeExplosion = Info.AvShot.HitParticleActive && Info.AmmoDef.Const.AreaEffect == AreaEffectType.Explosive && Info.AmmoDef.AmmoGraphics.Particles.Hit.Name == string.Empty;
            }

            if (!Info.AmmoDef.Const.PrimeModel && !Info.AmmoDef.Const.TriggerModel) ModelState = EntityState.None;
            else
            {
                if (EnableAv)
                {
                    ModelState = EntityState.Exists;

                    double triggerModelSize = 0;
                    double primeModelSize = 0;
                    if (Info.AmmoDef.Const.TriggerModel) triggerModelSize = Info.AvShot.TriggerEntity.PositionComp.WorldVolume.Radius;
                    if (Info.AmmoDef.Const.PrimeModel) primeModelSize = Info.AvShot.PrimeEntity.PositionComp.WorldVolume.Radius;
                    var largestSize = triggerModelSize > primeModelSize ? triggerModelSize : primeModelSize;

                    Info.AvShot.ModelSphereCurrent.Radius = largestSize * 2;
                }
            }

            if (EnableAv)
            {
                LineOrNotModel = Info.AmmoDef.Const.DrawLine || ModelState == EntityState.None && Info.AmmoDef.Const.AmmoParticle;
                Info.AvShot.ModelOnly = !LineOrNotModel && ModelState == EntityState.Exists;
            }
        }

        internal void StaticEntCheck()
        {
            var ai = Info.Ai;
            LinePlanetCheck = ai.PlanetSurfaceInRange && DynamicGuidance;
            var lineTest = new LineD(Position, Position + (Info.Direction * Info.MaxTrajectory));

            for (int i = 0; i < Info.Ai.StaticsInRange.Count; i++)
            {
                var staticEnt = ai.StaticsInRange[i];
                var rotMatrix = Quaternion.CreateFromRotationMatrix(staticEnt.WorldMatrix);
                var obb = new MyOrientedBoundingBoxD(staticEnt.PositionComp.WorldAABB.Center, staticEnt.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                var voxel = staticEnt as MyVoxelBase;
                var grid = staticEnt as MyCubeGrid;

                if (obb.Intersects(ref lineTest) != null || voxel != null && voxel.PositionComp.WorldAABB.Contains(Position) == ContainmentType.Contains)
                {
                    if (voxel != null && voxel == voxel.RootVoxel)
                    {
                        if (voxel == ai.MyPlanet)
                        {
                            if (!Info.AmmoDef.Const.IsBeamWeapon)
                            {
                                //Info.Ai.Session.Physics.CastRayParallel(ref lineTest.From, ref lineTest.To, RayHits, CollisionLayers.VoxelCollisionLayer, CouldHitPlanet);
                                LinePlanetCheck = true;
                            }
                            else if (!Info.WeaponCache.VoxelHits[CachedId].Cached(lineTest, Info))
                            {
                                LinePlanetCheck = true;
                            }
                            else CachedPlanetHit = true;

                            PruneQuery = MyEntityQueryType.Both;
                        }
                        else
                        {
                            LinePlanetCheck = true;
                            PruneQuery = MyEntityQueryType.Both;
                        }
                        break;
                    }
                    if (grid != null && grid.IsSameConstructAs(Info.Ai.MyGrid)) continue;
                    PruneQuery = MyEntityQueryType.Both;
                    if (LinePlanetCheck || !ai.PlanetSurfaceInRange) break;
                }
            }
        }

        internal void CouldHitPlanet(List<IHitInfo> hitInfos)
        {
            for (int i = 0; i < hitInfos.Count; i++)
            {
                var hit = hitInfos[i];
                var voxel = hit.HitEntity as MyVoxelBase;
                if (voxel?.RootVoxel is MyPlanet)
                {
                    LinePlanetCheck = true;
                    break;
                }
            }
            hitInfos.Clear();
        }

        internal void CheckForNearVoxel(uint steps)
        {
            var possiblePos = BoundingBoxD.CreateFromSphere(new BoundingSphereD(Position, ((MaxSpeed) * (steps + 1) * StepConst) + Info.AmmoDef.Const.CollisionSize));
            if (MyGamePruningStructure.AnyVoxelMapInBox(ref possiblePos))
            {
                PruneQuery = MyEntityQueryType.Both;
            }
        }

        internal bool Intersected(bool add = true)
        {
            if (Vector3D.IsZero(Hit.HitPos)) return false;

            if (EnableAv && (Info.AmmoDef.Const.DrawLine || Info.AmmoDef.Const.PrimeModel || Info.AmmoDef.Const.TriggerModel)) {
                var useCollisionSize = ModelState == EntityState.None && Info.AmmoDef.Const.AmmoParticle && !Info.AmmoDef.Const.DrawLine;
                Info.AvShot.TestSphere.Center = Hit.VisualHitPos;
                ShortStepAvUpdate(useCollisionSize, true);
            }

            if (!Info.AmmoDef.Const.VirtualBeams && add) Info.Ai.Session.Hits.Add(this);
            else if (Info.AmmoDef.Const.VirtualBeams) {
                Info.WeaponCache.VirtualHit = true;
                Info.WeaponCache.HitEntity.Entity = Hit.Entity;
                Info.WeaponCache.HitEntity.HitPos = Hit.VisualHitPos;
                Info.WeaponCache.Hits = VrPros.Count;
                Info.WeaponCache.HitDistance = Vector3D.Distance(LastPosition, Hit.VisualHitPos);

                if (Hit.Entity is MyCubeGrid) Info.WeaponCache.HitBlock = Hit.Block;
                if (add) Info.Ai.Session.Hits.Add(this);
                CreateFakeBeams(!add);
            }
            return true;
        }

        internal void ShortStepAvUpdate(bool useCollisionSize, bool hit)
        {
            var endPos = hit ? Hit.VisualHitPos : !EarlyEnd ? Position + -Info.Direction * (Info.DistanceTraveled - Info.MaxTrajectory) : Position;
            var stepSize = (Info.DistanceTraveled - Info.PrevDistanceTraveled);
            var avSize = useCollisionSize ? Info.AmmoDef.Const.CollisionSize : Info.TracerLength;
            double remainingTracer;
            double stepSizeToHit;
            if (Info.AmmoDef.Const.IsBeamWeapon)
            {
                double beamLength;
                Vector3D.Distance(ref Info.Origin, ref endPos, out beamLength);
                remainingTracer = MathHelperD.Clamp(beamLength, 0, avSize);
                stepSizeToHit = remainingTracer;
            }
            else
            {
                double overShot;
                Vector3D.Distance(ref endPos, ref Position, out overShot);
                stepSizeToHit = Math.Abs(stepSize - overShot);
                if (avSize < stepSize && !MyUtils.IsZero(avSize - stepSize, 1E-01F))
                {
                    remainingTracer = MathHelperD.Clamp(avSize - stepSizeToHit, 0, stepSizeToHit);
                }
                else if (avSize >= overShot)
                {
                    remainingTracer = MathHelperD.Clamp(avSize - overShot, 0, Math.Min(avSize, Info.PrevDistanceTraveled + stepSizeToHit));
                }
                else remainingTracer = 0;
            }

            if (MyUtils.IsZero(remainingTracer, 1E-01F)) remainingTracer = 0;
            Info.Ai.Session.Projectiles.DeferedAvDraw.Add(new DeferedAv { AvShot = Info.AvShot, StepSize = stepSize, VisualLength = remainingTracer, TracerFront = endPos, ShortStepSize = stepSizeToHit, Hit = hit, TriggerGrowthSteps = Info.TriggerGrowthSteps, Direction = Info.Direction, VisualDir = Info.VisualDir});
        }

        internal void CreateFakeBeams(bool miss = false)
        {
            Vector3D? hitPos = null;
            if (!Vector3D.IsZero(Hit.VisualHitPos)) hitPos = Hit.VisualHitPos;
            for (int i = 0; i < VrPros.Count; i++) {
                
                var vp = VrPros[i];
                var vs = vp.AvShot;

                vp.TracerLength = Info.TracerLength;
                vs.Init(vp, StepPerSec * StepConst, MaxSpeed);
                vs.Hit = Hit;
                if (Info.AmmoDef.Const.ConvergeBeams) {
                    var beam = !miss ? new LineD(vs.Origin, hitPos ?? Position) : new LineD(vs.Origin, Position);
                    Info.Ai.Session.Projectiles.DeferedAvDraw.Add(new DeferedAv { AvShot = vs, StepSize = Info.DistanceTraveled - Info.PrevDistanceTraveled, VisualLength = beam.Length, TracerFront = beam.To, ShortStepSize = beam.Length, Hit = !miss, TriggerGrowthSteps = Info.TriggerGrowthSteps, Direction = beam.Direction, VisualDir = beam.Direction });
                }
                else {
                    Vector3D beamEnd;
                    var hit = !miss && hitPos.HasValue;
                    if (!hit)
                        beamEnd = vs.Origin + (vp.Direction * Info.MaxTrajectory);
                    else
                        beamEnd = vs.Origin + (vp.Direction * Info.WeaponCache.HitDistance);

                    var line = new LineD(vs.Origin, beamEnd, !hit ? Info.MaxTrajectory : Info.WeaponCache.HitDistance);
                    if (!miss && hitPos.HasValue)
                        Info.Ai.Session.Projectiles.DeferedAvDraw.Add(new DeferedAv { AvShot = vs, StepSize = Info.DistanceTraveled - Info.PrevDistanceTraveled, VisualLength = line.Length, TracerFront = line.To, ShortStepSize = line.Length, Hit = true, TriggerGrowthSteps = Info.TriggerGrowthSteps, Direction = line.Direction, VisualDir = line.Direction });
                    else
                        Info.Ai.Session.Projectiles.DeferedAvDraw.Add(new DeferedAv { AvShot = vs, StepSize = Info.DistanceTraveled - Info.PrevDistanceTraveled, VisualLength = line.Length, TracerFront = line.To, ShortStepSize = line.Length, Hit = false, TriggerGrowthSteps = Info.TriggerGrowthSteps, Direction = line.Direction, VisualDir = line.Direction });
                }
            }
        }

        private void SpawnShrapnel()
        {
            var shrapnel = Info.Ai.Session.Projectiles.ShrapnelPool.Get();
            shrapnel.Init(this, Info.Ai.Session.Projectiles.FragmentPool);
            Info.Ai.Session.Projectiles.ShrapnelToSpawn.Add(shrapnel);
        }

        internal bool NewTarget()
        {
            var giveUp = HadTarget && ++NewTargets > Info.AmmoDef.Const.MaxTargets && Info.AmmoDef.Const.MaxTargets != 0;
            ChaseAge = Info.Age;
            PickTarget = false;
            if (giveUp || !GridAi.ReacquireTarget(this))
            {
                Info.Target.Entity = null;
                if (Info.Target.IsProjectile) UnAssignProjectile(true);
                return false;
            }

            if (Info.Target.IsProjectile) UnAssignProjectile(false);
            return true;
        }

        internal void ForceNewTarget()
        {
            ChaseAge = Info.Age;
            PickTarget = false;
        }

        internal void ActivateMine()
        {
            var ent = Info.Target.Entity;
            MineActivated = true;
            var targetPos = ent.PositionComp.WorldAABB.Center;
            var deltaPos = targetPos - Position;
            var targetVel = ent.Physics?.LinearVelocity ?? Vector3.Zero;
            var deltaVel = targetVel - Vector3.Zero;
            var timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, DesiredSpeed);
            var predictedPos = targetPos + (float)timeToIntercept * deltaVel;
            PredictedTargetPos = predictedPos;
            PrevTargetPos = predictedPos;
            PrevTargetVel = targetVel;
            LockedTarget = true;

            if (Guidance == GuidanceType.DetectFixed) return;

            Vector3D.DistanceSquared(ref Info.Origin, ref predictedPos, out DistanceToTravelSqr);
            Info.DistanceTraveled = 0;
            Info.PrevDistanceTraveled = 0;

            Info.Direction = Vector3D.Normalize(predictedPos - Position);
            AccelDir = Info.Direction;
            Info.VisualDir = Info.Direction;
            VelocityLengthSqr = 0;

            MaxVelocity = (Info.Direction * DesiredSpeed);
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            AccelVelocity = (Info.Direction * AccelLength);

            if (ConstantSpeed)
            {
                Velocity = MaxVelocity;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = AccelVelocity;

            if (Guidance == GuidanceType.DetectSmart)
            {
                SmartsOn = true;
                MaxChaseTime = Info.AmmoDef.Const.MaxChaseTime;
                if (SmartsOn && Info.AmmoDef.Const.TargetOffSet && LockedTarget)
                {
                    OffSetTarget();
                    OffsetSqr = Info.AmmoDef.Trajectory.Smarts.Inaccuracy * Info.AmmoDef.Trajectory.Smarts.Inaccuracy;
                }
                else
                {
                    TargetOffSet = Vector3D.Zero;
                    OffsetSqr = 0;
                }
            }

            TravelMagnitude = Velocity * StepConst;
        }

        internal void TriggerMine(bool startTimer)
        {
            DistanceToTravelSqr = double.MinValue;
            if (Info.AmmoDef.Const.Ewar)
            {
                Info.AvShot.Triggered = true;
                if (startTimer) FieldTime = Info.AmmoDef.Trajectory.Mines.FieldTime;
            }
            else if (startTimer) FieldTime = 0;
            MineTriggered = true;
        }

        internal void RunSmart()
        {
            Vector3D newVel;
            if ((AccelLength <= 0 || Vector3D.DistanceSquared(Info.Origin, Position) >= Info.AmmoDef.Const.SmartsDelayDistSqr))
            {
                var fake = Info.Target.IsFakeTarget;
                var gaveUpChase = !fake && Info.Age - ChaseAge > MaxChaseTime && HadTarget;
                var validTarget = fake || Info.Target.IsProjectile || Info.Target.Entity != null && !Info.Target.Entity.MarkedForClose;
                var isZombie = Info.AmmoDef.Const.CanZombie && HadTarget && !fake && !validTarget && ZombieLifeTime > 0 && ZombieLifeTime % 30 == 0;
                var seekFirstTarget = !HadTarget && !validTarget && Info.Age > 120 && Info.Age % 30 == 0;

                if ((PickTarget || gaveUpChase && validTarget || isZombie || seekFirstTarget) && NewTarget() || validTarget)
                {
                    HadTarget = true;
                    if (ZombieLifeTime > 0)
                    {
                        ZombieLifeTime = 0;
                        OffSetTarget();
                    }
                    var targetPos = Vector3D.Zero;
                    if (fake)
                        targetPos = Info.Ai.DummyTarget.Position;
                    else if (Info.Target.IsProjectile) targetPos = Info.Target.Projectile.Position;
                    else if (Info.Target.Entity != null) targetPos = Info.Target.Entity.PositionComp.WorldAABB.Center;


                    if (Info.AmmoDef.Const.TargetOffSet && WasTracking)
                    {
                        if (Info.Age - LastOffsetTime > 300)
                        {
                            double dist;
                            Vector3D.DistanceSquared(ref Position, ref targetPos, out dist);
                            if (dist < OffsetSqr + VelocityLengthSqr && Vector3.Dot(Info.Direction, Position - targetPos) > 0)
                                OffSetTarget();
                        }
                        targetPos += TargetOffSet;
                    }

                    PredictedTargetPos = targetPos;

                    var physics = Info.Target.Entity?.Physics ?? Info.Target.Entity?.Parent?.Physics;
                    if (!(Info.Target.IsProjectile || fake) && (physics == null || Vector3D.IsZero(targetPos))) {
                        PrevTargetPos = PredictedTargetPos;
                    }
                    else {
                        PrevTargetPos = targetPos;
                    }

                    var tVel = Vector3.Zero;
                    if (fake) tVel = Info.Ai.DummyTarget.LinearVelocity;
                    else if (Info.Target.IsProjectile) tVel = Info.Target.Projectile.Velocity;
                    else if (physics != null) tVel = physics.LinearVelocity;
                    if (!fake && Info.AmmoDef.Const.TargetLossDegree > 0 && Vector3D.DistanceSquared(Info.Origin, Position) >= Info.AmmoDef.Const.SmartsDelayDistSqr)
                    {
                        if (((WasTracking && (Info.System.Session.Tick20 || Vector3.Dot(Info.Direction, Position - targetPos) > 0)) || !WasTracking))
                        {
                            var targetDir = -Info.Direction;
                            var refDir = Vector3D.Normalize(Position - targetPos);
                            if (!MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref refDir, Info.AmmoDef.Const.TargetLossDegree)) {
                                if (WasTracking) 
                                    PickTarget = true;
                            }
                            else if (!WasTracking) {
                                WasTracking = true;
                            }
                        }
                    }

                    PrevTargetVel = tVel;
                }
                else
                {
                    var roam = Info.AmmoDef.Trajectory.Smarts.Roam;
                    PrevTargetPos = roam ? PredictedTargetPos : Position + (Info.Direction * Info.MaxTrajectory);
                    if (ZombieLifeTime++ > Info.AmmoDef.Const.TargetLossTime && (Info.AmmoDef.Trajectory.Smarts.NoTargetExpire || HadTarget))
                    {
                        DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                        EarlyEnd = true;
                    }

                    if (roam && Info.Age - LastOffsetTime > 300 && HadTarget)
                    {
                        double dist;
                        Vector3D.DistanceSquared(ref Position, ref PrevTargetPos, out dist);
                        if (dist < OffsetSqr + VelocityLengthSqr && Vector3.Dot(Info.Direction, Position - PrevTargetPos) > 0)
                        {
                            OffSetTarget(true);
                            PrevTargetPos += TargetOffSet;
                            PredictedTargetPos = PrevTargetPos;
                        }
                    }
                }

                Vector3D commandedAccel;

                var missileToTarget = Vector3D.Normalize(PrevTargetPos - Position);
                var relativeVelocity = PrevTargetVel - Velocity;

                var normalMissileAcceleration = (relativeVelocity - (relativeVelocity.Dot(missileToTarget) * missileToTarget)) * Info.AmmoDef.Trajectory.Smarts.Aggressiveness;

                if (Vector3D.IsZero(normalMissileAcceleration)) commandedAccel =  (missileToTarget * StepPerSec);
                else
                {
                    var maxLateralThrust = StepPerSec * Math.Min(1, Math.Max(0, Info.AmmoDef.Const.MaxLateralThrust));
                    if (normalMissileAcceleration.LengthSquared() > maxLateralThrust * maxLateralThrust)
                    {
                        Vector3D.Normalize(ref normalMissileAcceleration, out normalMissileAcceleration);
                        normalMissileAcceleration *= maxLateralThrust;
                    }
                    commandedAccel =  Math.Sqrt(Math.Max(0, StepPerSec * StepPerSec - normalMissileAcceleration.LengthSquared())) * missileToTarget + normalMissileAcceleration;
                }

                newVel = Velocity + (commandedAccel * StepConst);

                AccelDir = commandedAccel / StepPerSec;
                Vector3D.Normalize(ref Velocity, out Info.Direction);
            }
            else
                newVel = Velocity += (Info.Direction * AccelLength);
            VelocityLengthSqr = newVel.LengthSquared();

            if (VelocityLengthSqr > MaxSpeedSqr) newVel = Info.Direction * MaxSpeed;
            Velocity = newVel;
        }

        internal void RunEwar()
        {
            if (Info.AmmoDef.Const.Pulse && !Info.TriggeredPulse && VelocityLengthSqr <= 0 && !Info.AmmoDef.Const.IsMine)
            {
                Info.TriggeredPulse = true;
                Velocity = Vector3D.Zero;
                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
            }

            if (Info.TriggeredPulse)
            {
                var areaSize = Info.AmmoDef.Const.AreaEffectSize;
                var maxSteps = Info.AmmoDef.Const.PulseGrowTime;
                if (Info.TriggerGrowthSteps < areaSize)
                {
                    var expansionPerTick = areaSize / maxSteps;
                    var nextSize = ++Info.TriggerGrowthSteps * expansionPerTick;
                    if (nextSize <= areaSize)
                    {
                        var nextRound = nextSize + 1;
                        if (nextRound > areaSize)
                        {
                            if (nextSize < areaSize)
                            {
                                nextSize = areaSize;
                                ++Info.TriggerGrowthSteps;
                            }
                        }
                        MatrixD.Rescale(ref Info.TriggerMatrix, nextSize);
                        if (EnableAv)
                        {
                            Info.AvShot.Triggered = true;
                            Info.AvShot.TriggerMatrix = Info.TriggerMatrix;
                        }
                    }
                }
            }

            if (!Info.AmmoDef.Const.Pulse || Info.AmmoDef.Const.Pulse && Info.Age % Info.AmmoDef.Const.PulseInterval == 0)
                EwarEffects();
            else Info.EwarActive = false;
        }

        internal void EwarEffects()
        {
            switch (Info.AmmoDef.Const.AreaEffect)
            {
                case AreaEffectType.AntiSmart:
                    var eWarSphere = new BoundingSphereD(Position, Info.AmmoDef.Const.AreaEffectSize);
                    DynTrees.GetAllProjectilesInSphere(Info.Ai.Session, ref eWarSphere, EwaredProjectiles, false);
                    for (int j = 0; j < EwaredProjectiles.Count; j++)
                    {
                        var netted = EwaredProjectiles[j];
                        if (netted.Info.Ai.MyGrid.IsSameConstructAs(Info.Ai.MyGrid) || netted.Info.Target.IsProjectile) continue;
                        if (Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f < Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        {
                            Info.BaseEwarPool -= Info.AmmoDef.AreaEffect.AreaEffectDamage;
                            if (Info.BaseEwarPool <= 0)
                            {
                                Info.EwarActive = true;
                                netted.Info.Target.Projectile = this;
                                netted.Info.Target.IsProjectile = true;
                                Seekers.Add(netted);
                            }
                        }

                        Info.WeaponRng.ClientProjectileCurrentCounter++;
                    }
                    EwaredProjectiles.Clear();
                    return;
                case AreaEffectType.PushField:
                    if (Info.TriggeredPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() *  100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.PullField:
                    if (Info.TriggeredPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.JumpNullField:
                    if (Info.TriggeredPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.AnchorField:
                    if (Info.TriggeredPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.EnergySinkField:
                    if (Info.TriggeredPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.EmpField:
                    if (Info.TriggeredPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.OffenseField:
                    if (Info.TriggeredPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.NavField:
                    if (!Info.AmmoDef.Const.Pulse || Info.TriggeredPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.DotField:
                    if (Info.TriggeredPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse) {
                        Info.EwarActive = true;
                    }
                    break;
            }
            Info.WeaponRng.ClientProjectileCurrentCounter++;
        }


        internal void SeekEnemy()
        {
            var mineInfo = Info.AmmoDef.Trajectory.Mines;
            var detectRadius = mineInfo.DetectRadius;
            var deCloakRadius = mineInfo.DeCloakRadius;

            var wakeRadius = detectRadius > deCloakRadius ? detectRadius : deCloakRadius;
            PruneSphere = new BoundingSphereD(Position, wakeRadius);
            var inRange = false;
            var activate = false;
            var minDist = double.MaxValue;
            if (!MineActivated)
            {
                MyEntity closestEnt = null;
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref PruneSphere, CheckList, MyEntityQueryType.Dynamic);
                for (int i = 0; i < CheckList.Count; i++)
                {
                    var ent = CheckList[i];
                    var grid = ent as MyCubeGrid;
                    var character = ent as IMyCharacter;
                    if (grid == null && character == null || ent.MarkedForClose || !ent.InScene) continue;
                    Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo;
                    if (!Info.Ai.CreateEntInfo(ent, Info.Ai.MyOwner, out entInfo)) continue;
                    switch (entInfo.Relationship)
                    {
                        case MyRelationsBetweenPlayerAndBlock.Owner:
                            continue;
                        case MyRelationsBetweenPlayerAndBlock.FactionShare:
                            continue;
                    }
                    var entSphere = ent.PositionComp.WorldVolume;
                    entSphere.Radius += Info.AmmoDef.Const.CollisionSize;
                    var dist = MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref Position, ref entSphere);
                    if (dist >= minDist) continue;
                    minDist = dist;
                    closestEnt = ent;
                }

                if (closestEnt != null)
                {
                    ForceNewTarget();
                    Info.Target.Entity = closestEnt;
                }
            }
            else if (Info.Target.Entity != null && !Info.Target.Entity.MarkedForClose)
            {
                var entSphere = Info.Target.Entity.PositionComp.WorldVolume;
                entSphere.Radius += Info.AmmoDef.Const.CollisionSize;
                minDist = MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref Position, ref entSphere);
            }
            else
                TriggerMine(true);

            if (Info.AvShot.Cloaked && minDist <= deCloakRadius) Info.AvShot.Cloaked = false;
            else if (!Info.AvShot.Cloaked && minDist > deCloakRadius) Info.AvShot.Cloaked = true;

            if (minDist <= Info.AmmoDef.Const.CollisionSize) activate = true;
            if (minDist <= detectRadius) inRange = true;
            if (MineActivated)
            {
                if (!inRange)
                    TriggerMine(true);
            }
            else if (inRange) ActivateMine();

            if (activate)
            {
                TriggerMine(false);
                SegmentList.Add(new MyLineSegmentOverlapResult<MyEntity> { Distance = minDist, Element = Info.Target.Entity });
            }
            CheckList.Clear();
        }

        internal void OffSetTarget(bool roam = false)
        {
            var randAzimuth = (Info.WeaponRng.TurretRandom.NextDouble() * 1) * 2 * Math.PI;
            var randElevation = ((Info.WeaponRng.TurretRandom.NextDouble() * 1) * 2 - 1) * 0.5 * Math.PI;

            Info.WeaponRng.TurretCurrentCounter += 2;

            var offsetAmount = roam ? 100 : Info.AmmoDef.Trajectory.Smarts.Inaccuracy;
            Vector3D randomDirection;
            Vector3D.CreateFromAzimuthAndElevation(randAzimuth, randElevation, out randomDirection); // this is already normalized
            PrevTargetOffset = TargetOffSet;
            TargetOffSet = (randomDirection * offsetAmount);
            VisualStep = 0;
            if (Info.Age != 0) LastOffsetTime = Info.Age;
        }

        internal void DestroyProjectile()
        {
            if (State == ProjectileState.Destroy)
            {
                Hit = new Hit {Block = null, Entity = null, HitPos = Position, VisualHitPos = Position, HitVelocity = Velocity, HitTick = Info.System.Session.Tick};
                if (EnableAv || Info.AmmoDef.Const.VirtualBeams)
                {
                    Info.AvShot.ForceHitParticle = true;
                    Info.AvShot.Hit = Hit;
                }
                Intersected(false);
            }

            State = ProjectileState.Depleted;
        }

        internal void UnAssignProjectile(bool clear)
        {
            Info.Target.Projectile.Seekers.Remove(this);
            if (clear) Info.Target.Reset(Info.Ai.Session.Tick, Target.States.ProjectileClosed);
            else
            {
                Info.Target.IsProjectile = false;
                Info.Target.IsFakeTarget = false;
                Info.Target.Projectile = null;
            }
        }

        internal void ProjectileClose()
        {
            if (GenerateShrapnel)
                SpawnShrapnel();
            else Info.IsShrapnel = false;

            for (int i = 0; i < Watchers.Count; i++) Watchers[i].DeadProjectiles.Add(this);
            Watchers.Clear();

            foreach (var seeker in Seekers) seeker.Info.Target.Reset(Info.Ai.Session.Tick, Target.States.ProjectileClosed);
            Seekers.Clear();

            if (EnableAv && Info.AvShot.ForceHitParticle)
                Info.AvShot.HitEffects();

            State = ProjectileState.Dead;
            if (EnableAv)
            {
                if (ModelState == EntityState.Exists)
                    ModelState = EntityState.None;

                if (!Info.AvShot.Active)
                    Info.Ai.Session.Av.AvShotPool.Return(Info.AvShot);
                else Info.AvShot.EndState = new AvClose {EndPos = Position, Dirty = true, DetonateFakeExp = Info.AmmoDef.AreaEffect.Detonation.DetonateOnEnd && Info.AvShot.FakeExplosion };
            }
            else if (Info.AmmoDef.Const.VirtualBeams)
            {
                for (int i = 0; i < VrPros.Count; i++)
                {
                    var vp = VrPros[i];
                    if (!vp.AvShot.Active)
                        Info.Ai.Session.Av.AvShotPool.Return(vp.AvShot);
                    else vp.AvShot.EndState = new AvClose { EndPos = Position, Dirty = true, DetonateFakeExp = Info.AmmoDef.AreaEffect.Detonation.DetonateOnEnd && Info.AvShot.FakeExplosion };
                    
                    Info.System.Session.Projectiles.VirtInfoPool.Return(vp);
                }
                VrPros.Clear();
            }
            if (DynamicGuidance)
                DynTrees.UnregisterProjectile(this);

            PruningProxyId = -1;
            Info.Clean();
        }

        internal enum ProjectileState
        {
            Start,
            Alive,
            Detonate,
            OneAndDone,
            Dead,
            Depleted,
            Destroy,
        }

        internal enum EntityState
        {
            Exists,
            None
        }
    }
}