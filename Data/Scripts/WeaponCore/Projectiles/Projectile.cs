using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.AreaDamage;
using static WeaponCore.Support.Trajectile;

namespace WeaponCore.Projectiles
{
    internal class Projectile
    {
        internal const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal const int EndSteps = 1;
        internal ProjectileState State;
        internal EntityState ModelState;
        internal MyEntityQueryType PruneQuery;
        internal AreaEffectType AreaEffect;
        internal AmmoTrajectory.GuidanceType Guidance;
        internal Vector3D Direction;
        internal Vector3D AccelDir;
        internal Vector3D VisualDir;
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
        internal Vector3 PrevTargetVel;
        internal Vector3 GridVel;
        internal Vector3D? LastHitPos;
        internal Vector3? LastHitEntVel;
        internal BoundingSphereD TestSphere = new BoundingSphereD(Vector3D.Zero, 200f);
        internal BoundingSphereD ModelSphereCurrent;
        internal BoundingSphereD ModelSphereLast;
        internal BoundingSphereD PruneSphere;
        internal double AccelLength;
        internal double MaxTrajectorySqr;
        internal double DistanceToTravelSqr;
        internal double TracerLength;
        internal double VelocityLengthSqr;
        internal double SmartsDelayDistSqr;
        internal double DistanceFromCameraSqr;
        internal double OffsetSqr;
        internal double AccelPerSec;
        internal double MaxSpeedSqr;
        internal double MaxSpeed;
        internal double VisualStep;
        internal double DeadZone;
        internal float DesiredSpeed;
        internal float MaxTrajectory;
        internal int Age;
        internal int ChaseAge;
        internal int FieldTime;
        internal int MaxChaseAge;
        internal int EndStep;
        internal int ZombieLifeTime;
        internal int LastOffsetTime;
        internal int PruningProxyId = -1;
        internal int PulseChance;
        internal int PulseInterval;
        internal bool EnableAv;
        internal bool DrawLine;

        internal bool FirstOffScreen;
        internal bool ConstantSpeed;
        internal bool PositionChecked;
        internal bool MoveToAndActivate;
        internal bool LockedTarget;
        internal bool DynamicGuidance;
        internal bool ParticleStopped;
        internal bool ParticleLateStart;
        internal bool PickTarget;
        internal bool GenerateShrapnel;
        internal bool Colliding;
        internal bool CheckPlanet;
        internal bool SmartsOn;
        internal bool Ewar;
        internal bool EwarActive;
        internal bool EwarEffect;
        internal bool SelfDamage;
        internal bool MineSeeking;
        internal bool MineActivated;
        internal bool MineTriggered;
        internal bool Miss;
        internal bool Active;
        internal bool HitParticleActive;
        internal bool CachedPlanetHit;
        internal Trajectile T = new Trajectile();
        internal MyParticleEffect AmmoEffect;
        internal MyParticleEffect HitEffect;
        internal Projectiles Manager;
        internal readonly List<MyLineSegmentOverlapResult<MyEntity>> SegmentList = new List<MyLineSegmentOverlapResult<MyEntity>>();
        internal readonly List<Trajectile> VrTrajectiles = new List<Trajectile>();
        internal readonly List<Projectile> EwaredProjectiles = new List<Projectile>();
        internal readonly List<GridAi> Watchers = new List<GridAi>();

        internal void Start(Projectiles manager)
        {
            Manager = manager;
            Position = T.Origin;
            AccelDir = Direction;
            VisualDir = Direction;
            var cameraStart = T.Ai.Session.CameraPos;
            Vector3D.DistanceSquared(ref cameraStart, ref T.Origin, out DistanceFromCameraSqr);
            GenerateShrapnel = T.System.Values.Ammo.Shrapnel.Fragments > 0;
            var noSAv = T.IsShrapnel && T.System.Values.Ammo.Shrapnel.NoAudioVisual;
            var probability = T.System.Values.Graphics.VisualProbability;
            EnableAv = !T.Ai.Session.DedicatedServer && !noSAv && DistanceFromCameraSqr <= T.Ai.Session.SyncDistSqr && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));

            T.PrimeMatrix = MatrixD.Identity;
            T.TriggerMatrix = MatrixD.Identity;
            ModelState = EntityState.None;
            LastEntityPos = Position;

            LastHitPos = null;
            LastHitEntVel = null;
            Age = 0;
            ChaseAge = 0;
            ZombieLifeTime = 0;
            LastOffsetTime = 0;
            Colliding = false;
            CachedPlanetHit = false;
            ParticleStopped = false;
            ParticleLateStart = false;
            T.OnScreen = true;
            FirstOffScreen = true;
            PositionChecked = false;
            EwarActive = false;
            MineSeeking = false;
            MineActivated = false;
            MineTriggered = false;
            T.Cloaked = false;
            HitParticleActive = false;
            EndStep = 0;
            T.PrevDistanceTraveled = 0;
            T.DistanceTraveled = 0;

            Guidance = !(T.System.Values.Ammo.Shrapnel.NoGuidance && T.IsShrapnel) ? T.System.Values.Ammo.Trajectory.Guidance : AmmoTrajectory.GuidanceType.None;
            DynamicGuidance = Guidance != AmmoTrajectory.GuidanceType.None && Guidance != AmmoTrajectory.GuidanceType.TravelTo && !T.System.IsBeamWeapon && T.EnableGuidance;
            if (DynamicGuidance) DynTrees.RegisterProjectile(this);

            if (Guidance == AmmoTrajectory.GuidanceType.Smart && DynamicGuidance)
            {
                SmartsOn = true;
                MaxChaseAge = T.System.Values.Ammo.Trajectory.Smarts.MaxChaseTime;
            }
            else
            {
                MaxChaseAge = int.MaxValue;
                SmartsOn = false;
            }

            if (T.Target.IsProjectile)
            {
                OriginTargetPos = T.Target.Projectile.Position;
                T.Target.IsProjectile = T.Target.Projectile.T.BaseHealthPool > 0;
            }
            else if (T.Target.Entity != null) OriginTargetPos = T.Target.Entity.PositionComp.WorldAABB.Center;
            else OriginTargetPos = Vector3D.Zero;
            LockedTarget = OriginTargetPos != Vector3D.Zero;

            if (SmartsOn && T.System.TargetOffSet && LockedTarget)
            {
                OffSetTarget(out TargetOffSet);
                OffsetSqr = T.System.Values.Ammo.Trajectory.Smarts.Inaccuracy * T.System.Values.Ammo.Trajectory.Smarts.Inaccuracy;
            }
            else
            {
                TargetOffSet = Vector3D.Zero;
                OffsetSqr = 0;
            }

            DrawLine = T.System.Values.Graphics.Line.Tracer.Enable;
            if (T.System.RangeVariance)
            {
                var min = T.System.Values.Ammo.Trajectory.RangeVariance.Start;
                var max = T.System.Values.Ammo.Trajectory.RangeVariance.End;
                MaxTrajectory = T.System.Values.Ammo.Trajectory.MaxTrajectory - MyUtils.GetRandomFloat(min, max);
            }
            else MaxTrajectory = T.System.Values.Ammo.Trajectory.MaxTrajectory;
            if (PredictedTargetPos == Vector3D.Zero) PredictedTargetPos = Position + (Direction * MaxTrajectory);
            PrevTargetPos = PredictedTargetPos;
            PrevTargetVel = Vector3D.Zero;
            T.ObjectsHit = 0;
            T.BaseHealthPool = T.System.Values.Ammo.Health;
            TracerLength = T.System.Values.Graphics.Line.Tracer.Length;

            if (T.IsShrapnel)
            {
                var shrapnel = T.System.Values.Ammo.Shrapnel;
                T.BaseDamagePool = shrapnel.BaseDamage;
                T.DetonationDamage = T.System.Values.Ammo.AreaEffect.Detonation.DetonationDamage;
                T.AreaEffectDamage = T.System.Values.Ammo.AreaEffect.AreaEffectDamage;

                SelfDamage = T.System.SelfDamage;
                MaxTrajectory = shrapnel.MaxTrajectory;
                TracerLength = TracerLength / shrapnel.Fragments >= 1 ? TracerLength / shrapnel.Fragments : 1;
            }

            MaxTrajectorySqr = MaxTrajectory * MaxTrajectory;

            var smartsDelayDist = T.System.CollisionSize * T.System.Values.Ammo.Trajectory.Smarts.TrackingDelay;
            SmartsDelayDistSqr = smartsDelayDist * smartsDelayDist;

            if (!T.IsShrapnel) StartSpeed = GridVel;

            if (T.System.SpeedVariance && !T.System.IsBeamWeapon)
            {
                var min = T.System.Values.Ammo.Trajectory.SpeedVariance.Start;
                var max = T.System.Values.Ammo.Trajectory.SpeedVariance.End;
                DesiredSpeed = T.System.Values.Ammo.Trajectory.DesiredSpeed - MyUtils.GetRandomFloat(min, max);
            }
            else DesiredSpeed = T.System.Values.Ammo.Trajectory.DesiredSpeed;

            MoveToAndActivate = LockedTarget && !T.System.IsBeamWeapon && Guidance == AmmoTrajectory.GuidanceType.TravelTo;

            if (MoveToAndActivate)
            {
                var distancePos = PredictedTargetPos != Vector3D.Zero ? PredictedTargetPos : OriginTargetPos;
                Vector3D.DistanceSquared(ref T.Origin, ref distancePos, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = MaxTrajectorySqr;

            PickTarget = LockedTarget && T.System.Values.Ammo.Trajectory.Smarts.OverideTarget;

            AreaEffect = T.System.Values.Ammo.AreaEffect.AreaEffect;
            Ewar = AreaEffect > (AreaEffectType) 2;
            EwarEffect = AreaEffect > (AreaEffectType) 3;
            PulseInterval = T.System.Values.Ammo.AreaEffect.Pulse.Interval;
            PulseChance = T.System.Values.Ammo.AreaEffect.Pulse.PulseChance;

            PruneQuery = DynamicGuidance || T.Ai.ShieldNear ? MyEntityQueryType.Both : MyEntityQueryType.Dynamic;
            if (T.Ai.StaticEntitiesInRange && !DynamicGuidance && PruneQuery == MyEntityQueryType.Dynamic)
                StaticEntCheck();
            else CheckPlanet = false;

            if (EnableAv)
            {
                T.SetupSounds(DistanceFromCameraSqr);
                if (T.System.HitParticle && !T.System.IsBeamWeapon || AreaEffect == AreaEffectType.Explosive && !T.System.Values.Ammo.AreaEffect.Explosions.NoVisuals)
                {
                    var hitPlayChance = T.System.Values.Graphics.Particles.Hit.Extras.HitPlayChance;
                    HitParticleActive = hitPlayChance >= 1 || hitPlayChance >= MyUtils.GetRandomDouble(0.0f, 1f);
                }
            }

            if (T.System.PrimeModelId == -1 && T.System.TriggerModelId == -1 || T.IsShrapnel) ModelState = EntityState.None;
            else
            {
                if (EnableAv)
                {
                    ModelState = EntityState.Exists;

                    double triggerModelSize = 0;
                    double primeModelSize = 0;
                    if (T.System.TriggerModelId != -1) triggerModelSize = T.TriggerEntity.PositionComp.WorldVolume.Radius;
                    if (T.System.PrimeModelId != -1) primeModelSize = T.PrimeEntity.PositionComp.WorldVolume.Radius;
                    var largestSize = triggerModelSize > primeModelSize ? triggerModelSize : primeModelSize;

                    ModelSphereCurrent.Radius = largestSize * 2;
                    ModelSphereLast.Radius = largestSize * 2;
                }
                else ModelState = EntityState.NoDraw;
            }

            var accelPerSec = T.System.Values.Ammo.Trajectory.AccelPerSec;
            ConstantSpeed = accelPerSec <= 0;
            AccelPerSec = accelPerSec > 0 ? accelPerSec : DesiredSpeed; 
            MaxVelocity = StartSpeed + (Direction * DesiredSpeed);
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            AccelLength = T.System.Values.Ammo.Trajectory.AccelPerSec * StepConst;
            AccelVelocity = (Direction * AccelLength);
            DeadZone = 3;

            if (ConstantSpeed)
            {
                Velocity = MaxVelocity;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = StartSpeed + AccelVelocity;

            TravelMagnitude = Velocity * StepConst;

            FieldTime = T.System.Values.Ammo.Trajectory.FieldTime;

            State = !T.System.IsBeamWeapon ? ProjectileState.Alive : ProjectileState.OneAndDone;

            if (T.System.AmmoParticle && EnableAv && !T.System.IsBeamWeapon) PlayAmmoParticle();
        }

        internal void StaticEntCheck()
        {
            var ai = T.Ai;
            CheckPlanet = false;
            for (int i = 0; i < T.Ai.StaticsInRange.Count; i++)
            {
                var staticEnt = ai.StaticsInRange[i];
                var rotMatrix = Quaternion.CreateFromRotationMatrix(staticEnt.WorldMatrix);
                var obb = new MyOrientedBoundingBoxD(staticEnt.PositionComp.WorldAABB.Center, staticEnt.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                var lineTest = new LineD(Position, Position + (Direction * MaxTrajectory));
                var voxel = staticEnt as MyVoxelBase;
                var grid = staticEnt as MyCubeGrid;
                if (obb.Intersects(ref lineTest) != null || voxel != null && voxel.PositionComp.WorldAABB.Contains(Position) == ContainmentType.Contains)
                {
                    if (voxel != null)
                    {
                        if (voxel == ai.MyPlanet)
                        {
                            if (!T.System.IsBeamWeapon)
                            {
                                CheckPlanet = true;
                            }
                            else if (!T.WeaponCache.VoxelHits[T.WeaponId].Cached(lineTest))
                            {
                                Log.Line("query");
                                CheckPlanet = true;
                            }
                            else CachedPlanetHit = true;

                            PruneQuery = MyEntityQueryType.Both;
                        }
                        else
                        {
                            CheckPlanet = true;
                            PruneQuery = MyEntityQueryType.Both;
                        }
                        break;
                    }
                    if (grid != null && grid.IsSameConstructAs(T.Ai.MyGrid)) continue;
                    PruneQuery = MyEntityQueryType.Both;
                    if (CheckPlanet || !ai.PlanetSurfaceInRange) break;
                }
            }
        }

        internal bool Intersected(Projectile p, List<Trajectile> drawList, HitEntity hitEntity)
        {
            if (hitEntity?.HitPos == null) return false;
            if (p.EnableAv && (p.DrawLine || p.T.System.PrimeModelId != -1 || p.T.System.TriggerModelId != -1))
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
            if (!p.T.System.VirtualBeams) T.Ai.Session.Hits.Enqueue(p);
            else
            {
                p.T.WeaponCache.VirtualHit = true;
                p.T.WeaponCache.HitEntity.Entity = hitEntity.Entity;
                p.T.WeaponCache.HitEntity.HitPos = hitEntity.HitPos;
                p.T.WeaponCache.Hits = p.VrTrajectiles.Count;
                p.T.WeaponCache.HitDistance = Vector3D.Distance(p.LastPosition, hitEntity.HitPos.Value);

                if (hitEntity.Entity is MyCubeGrid) p.T.WeaponCache.HitBlock = hitEntity.Blocks[0];
                T.Ai.Session.Hits.Enqueue(p);
                if (p.EnableAv && p.T.OnScreen) CreateFakeBeams(p, hitEntity, drawList);
            }

            if (p.EnableAv)
                p.HitEffects();

            return true;
        }

        internal void CreateFakeBeams(Projectile p, HitEntity hitEntity, List<Trajectile> drawList, bool miss = false)
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

        private void CameraCheck(Projectile p)
        {
            if (p.ModelState == EntityState.Exists)
            {
                p.ModelSphereLast.Center = p.LastEntityPos;
                p.ModelSphereCurrent.Center = p.Position;
                if (T.Ai.Session.Camera.IsInFrustum(ref p.ModelSphereLast) || T.Ai.Session.Camera.IsInFrustum(ref p.ModelSphereCurrent) || p.FirstOffScreen)
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
                if (T.Ai.Session.Camera.IsInFrustum(ref bb)) p.T.OnScreen = true;
            }
        }

        private void SpawnShrapnel()
        {
            var shrapnel = T.Ai.Session.Projectiles.ShrapnelPool.Get();
            shrapnel.Init(this, T.Ai.Session.Projectiles.FragmentPool);
            T.Ai.Session.Projectiles.ShrapnelToSpawn.Add(shrapnel);
        }

        internal bool NewTarget()
        {
            ChaseAge = Age;
            PickTarget = false;
            if (!GridAi.ReacquireTarget(this))
            {
                T.Target.Entity = null;
                T.Target.IsProjectile = false;
                return false;
            }
            return true;
        }

        internal void ForceNewTarget()
        {
            ChaseAge = Age;
            PickTarget = false;
        }

        internal void ActivateMine()
        {
            var ent = T.Target.Entity;
            MineActivated = true;
            var targetPos = ent.PositionComp.WorldAABB.Center;
            var deltaPos = targetPos - Position;
            var targetVel = ent.Physics?.LinearVelocity ?? Vector3.Zero;
            var deltaVel = targetVel - Vector3.Zero;
            var timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, T.System.Values.Ammo.Trajectory.DesiredSpeed);
            var predictedPos = targetPos + (float)timeToIntercept * deltaVel;
            PredictedTargetPos = predictedPos;
            PrevTargetPos = predictedPos;
            PrevTargetVel = targetVel;
            LockedTarget = true;

            if (Guidance == AmmoTrajectory.GuidanceType.DetectFixed) return;

            Vector3D.DistanceSquared(ref T.Origin, ref predictedPos, out DistanceToTravelSqr);
            T.DistanceTraveled = 0;
            T.PrevDistanceTraveled = 0;

            Direction = Vector3D.Normalize(predictedPos - Position);
            AccelDir = Direction;
            VisualDir = Direction;
            VelocityLengthSqr = 0;

            MaxVelocity = (Direction * DesiredSpeed);
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            AccelVelocity = (Direction * AccelLength);

            if (ConstantSpeed)
            {
                Velocity = MaxVelocity;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = AccelVelocity;

            if (Guidance == AmmoTrajectory.GuidanceType.DetectSmart)
            {
                SmartsOn = true;
                var smartsDelayDist = T.System.CollisionSize * T.System.Values.Ammo.Trajectory.Smarts.TrackingDelay;
                SmartsDelayDistSqr = smartsDelayDist * smartsDelayDist;
                MaxChaseAge = T.System.Values.Ammo.Trajectory.Smarts.MaxChaseTime;
                if (SmartsOn && T.System.TargetOffSet && LockedTarget)
                {
                    OffSetTarget(out TargetOffSet);
                    OffsetSqr = T.System.Values.Ammo.Trajectory.Smarts.Inaccuracy * T.System.Values.Ammo.Trajectory.Smarts.Inaccuracy;
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
            if (Ewar)
            {
                T.Triggered = true;
                if (startTimer) FieldTime = T.System.Values.Ammo.Trajectory.Mines.FieldTime;
            }
            else if (startTimer) FieldTime = 0;
            MineTriggered = true;
            Log.Line($"[Mine] Ewar:{Ewar} - Activated:{MineActivated} - active:{EwarActive} - Triggered:{T.Triggered} - IdleTime:{FieldTime}");
        }

        internal void RunSmart()
        {
            Vector3D newVel;
            if ((AccelLength <= 0 || Vector3D.DistanceSquared(T.Origin, Position) >= SmartsDelayDistSqr))
            {
                var gaveUpChase = Age - ChaseAge > MaxChaseAge;
                var validTarget = T.Target.IsProjectile || T.Target.Entity != null && !T.Target.Entity.MarkedForClose;
                var isZombie = !T.System.IsMine && ZombieLifeTime > 0 && ZombieLifeTime % 30 == 0;
                if ((gaveUpChase || PickTarget || isZombie) && NewTarget() || validTarget)
                {
                    if (ZombieLifeTime > 0) UpdateZombie(true);
                    var targetPos = Vector3D.Zero;
                    if (T.Target.IsProjectile) targetPos = T.Target.Projectile.Position;
                    else if (T.Target.Entity != null) targetPos = T.Target.Entity.PositionComp.WorldAABB.Center;

                    if (T.System.TargetOffSet)
                    {
                        if (Age - LastOffsetTime > 300)
                        {
                            double dist;
                            Vector3D.DistanceSquared(ref Position, ref targetPos, out dist);
                            if (dist < OffsetSqr && Vector3.Dot(Direction, Position - targetPos) > 0)
                                OffSetTarget(out TargetOffSet);
                        }
                        targetPos += TargetOffSet;
                    }

                    var physics = T.Target.Entity?.Physics ?? T.Target.Entity?.Parent?.Physics;

                    if (!T.Target.IsProjectile && (physics == null || targetPos == Vector3D.Zero))
                        PrevTargetPos = PredictedTargetPos;
                    else PrevTargetPos = targetPos;

                    var tVel = Vector3.Zero;
                    if (T.Target.IsProjectile) tVel = T.Target.Projectile.Velocity;
                    else if (physics != null) tVel = physics.LinearVelocity;

                    PrevTargetVel = tVel;
                }
                else UpdateZombie();
                var commandedAccel = MathFuncs.CalculateMissileIntercept(PrevTargetPos, PrevTargetVel, Position, Velocity, AccelPerSec, T.System.Values.Ammo.Trajectory.Smarts.Aggressiveness, T.System.Values.Ammo.Trajectory.Smarts.MaxLateralThrust);
                newVel = Velocity + (commandedAccel * StepConst);
                AccelDir = commandedAccel / AccelPerSec;
                Vector3D.Normalize(ref Velocity, out Direction);
            }
            else newVel = Velocity += (Direction * AccelLength);
            VelocityLengthSqr = newVel.LengthSquared();

            if (VelocityLengthSqr > MaxSpeedSqr) newVel = Direction * MaxSpeed;
            Velocity = newVel;
        }

        internal void UpdateZombie(bool reset = false)
        {
            if (reset)
            {
                ZombieLifeTime = 0;
                OffSetTarget(out TargetOffSet);
            }
            else
            {
                PrevTargetPos = PredictedTargetPos;
                if (ZombieLifeTime++ > T.System.TargetLossTime) DistanceToTravelSqr = T.DistanceTraveled * T.DistanceTraveled;
                if (Age - LastOffsetTime > 300)
                {
                    double dist;
                    Vector3D.DistanceSquared(ref Position, ref PrevTargetPos, out dist);
                    if (dist < OffsetSqr && Vector3.Dot(Direction, Position - PrevTargetPos) > 0)
                    {
                        OffSetTarget(out TargetOffSet, true);
                        PrevTargetPos += TargetOffSet;
                        PredictedTargetPos = PrevTargetPos;
                    }
                }
            }
        }

        internal void RunEwar()
        {
            if (VelocityLengthSqr <= 0 && !T.Triggered && !T.System.IsMine)
                T.Triggered = true;

            if (T.Triggered)
            {
                var areaSize = T.System.AreaEffectSize;
                if (T.TriggerGrowthSteps < areaSize)
                {
                    const int expansionPerTick = 100 / 60;
                    var nextSize = (double)++T.TriggerGrowthSteps * expansionPerTick;
                    if (nextSize <= areaSize)
                    {
                        var nextRound = nextSize + 1;
                        if (nextRound > areaSize)
                        {
                            if (nextSize < areaSize)
                            {
                                nextSize = areaSize;
                                ++T.TriggerGrowthSteps;
                            }
                        }
                        MatrixD.Rescale(ref T.TriggerMatrix, nextSize);
                    }
                }
            }

            if (Age % PulseInterval == 0 || State == ProjectileState.OneAndDone)
                PulseEffect();
            else EwarActive = false;
        }

        internal void PulseEffect()
        {
            switch (AreaEffect)
            {
                case AreaEffectType.AntiSmart:
                    var eWarSphere = new BoundingSphereD(Position, T.System.AreaEffectSize);
                    DynTrees.GetAllProjectilesInSphere(T.Ai.Session, ref eWarSphere, EwaredProjectiles, false);
                    for (int j = 0; j < EwaredProjectiles.Count; j++)
                    {
                        var netted = EwaredProjectiles[j];
                        if (netted.T.Ai == T.Ai || netted.T.Target.Projectile != null) continue;
                        Log.Line("netted");
                        if (MyUtils.GetRandomInt(0, 100) < PulseChance)
                        {
                            EwarActive = true;
                            Log.Line("change course");
                            netted.T.Target.Projectile = this;
                        }
                    }
                    EwaredProjectiles.Clear();
                    break;
                case AreaEffectType.JumpNullField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        //Log.Line($"jumpNullField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
                case AreaEffectType.AnchorField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        //Log.Line($"jumpAnchorFieldNullField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
                case AreaEffectType.EnergySinkField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        //Log.Line($"EnergySinkField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
                case AreaEffectType.EmpField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        //Log.Line($"EmpField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
                case AreaEffectType.OffenseField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        //Log.Line($"OffenseField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
                case AreaEffectType.NavField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        //Log.Line($"NavField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }

                    break;
                case AreaEffectType.DotField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        //Log.Line($"DotField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
            }
        }

        internal void OffSetTarget(out Vector3D targetOffset, bool roam = false)
        {
            var randAzimuth = MyUtils.GetRandomDouble(0, 1) * 2 * Math.PI;
            var randElevation = (MyUtils.GetRandomDouble(0, 1) * 2 - 1) * 0.5 * Math.PI;

            var offsetAmount = roam ? 100 : T.System.Values.Ammo.Trajectory.Smarts.Inaccuracy;
            Vector3D randomDirection;
            Vector3D.CreateFromAzimuthAndElevation(randAzimuth, randElevation, out randomDirection); // this is already normalized
            targetOffset = (randomDirection * offsetAmount);
            VisualStep = 0;
            if (Age != 0) LastOffsetTime = Age;
        }

        internal void HitEffects(bool force = false)
        {
            if (Colliding || force)
            {
                var distToCameraSqr = Vector3D.DistanceSquared(Position, T.Ai.Session.CameraPos);
                var closeToCamera = distToCameraSqr < 360000;
                if (force) LastHitPos = Position;
                if (T.OnScreen && HitParticleActive && T.System.HitParticle) PlayHitParticle();
                else if (HitParticleActive && (T.OnScreen || closeToCamera)) T.FakeExplosion = true;
                T.HitSoundActived = T.System.HitSound && (T.HitSoundActive && (force || distToCameraSqr < T.System.HitSoundDistSqr || LastHitPos.HasValue && (!T.LastHitShield || T.System.Values.Audio.Ammo.HitPlayShield)));

                if (T.HitSoundActived) T.HitEmitter.Entity = T.HitEntity?.Entity;
                T.LastHitShield = false;
            }
            Colliding = false;
        }

        internal void PlayAmmoParticle()
        {
            if (Age == 0 && !ParticleLateStart)
            {
                TestSphere.Center = Position;
                if (!T.Ai.Session.Camera.IsInFrustum(ref TestSphere))
                {
                    ParticleLateStart = true;
                    return;
                }
            }
            MatrixD matrix;
            if (ModelState == EntityState.Exists)
            {
                matrix = MatrixD.CreateWorld(Position, AccelDir, T.PrimeEntity.PositionComp.WorldMatrix.Up);
                if (T.IsShrapnel) MatrixD.Rescale(ref matrix, 0.5f);
                var offVec = Position + Vector3D.Rotate(T.System.Values.Graphics.Particles.Ammo.Offset, matrix);
                matrix.Translation = offVec;
                T.PrimeMatrix = matrix;
            }
            else
            {
                matrix = MatrixD.CreateWorld(Position, AccelDir, T.OriginUp);
                var offVec = Position + Vector3D.Rotate(T.System.Values.Graphics.Particles.Ammo.Offset, matrix);
                matrix.Translation = offVec;
            }

            MyParticlesManager.TryCreateParticleEffect(T.System.Values.Graphics.Particles.Ammo.Name, ref matrix, ref Position, uint.MaxValue, out AmmoEffect); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (AmmoEffect == null) return;
            AmmoEffect.DistanceMax = T.System.Values.Graphics.Particles.Ammo.Extras.MaxDistance;
            AmmoEffect.UserColorMultiplier = T.System.Values.Graphics.Particles.Ammo.Color;
            //var reScale = (float)Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
            var scaler = !T.IsShrapnel ? 1 : 0.5f;

            AmmoEffect.UserRadiusMultiplier = T.System.Values.Graphics.Particles.Ammo.Extras.Scale * scaler;
            AmmoEffect.UserEmitterScale = 1 * scaler;
            if (ConstantSpeed) AmmoEffect.Velocity = Velocity;
            ParticleStopped = false;
            ParticleLateStart = false;
        }

        internal void PlayHitParticle()
        {
            if (HitEffect != null) DisposeHitEffect(false);
            if (LastHitPos.HasValue)
            {
                if (!T.System.Values.Graphics.Particles.Hit.ApplyToShield && T.LastHitShield)
                    return;

                var pos = LastHitPos.Value;
                var matrix = MatrixD.CreateTranslation(pos);
                MyParticlesManager.TryCreateParticleEffect(T.System.Values.Graphics.Particles.Hit.Name, ref matrix, ref pos, uint.MaxValue, out HitEffect);
                if (HitEffect == null) return;
                HitEffect.Loop = false;
                HitEffect.DurationMax = T.System.Values.Graphics.Particles.Hit.Extras.MaxDuration;
                HitEffect.DistanceMax = T.System.Values.Graphics.Particles.Hit.Extras.MaxDistance;
                HitEffect.UserColorMultiplier = T.System.Values.Graphics.Particles.Hit.Color;
                var reScale = 1;
                var scaler = reScale < 1 ? reScale : 1;

                HitEffect.UserRadiusMultiplier = T.System.Values.Graphics.Particles.Hit.Extras.Scale * scaler;
                HitEffect.UserEmitterScale = 1 * scaler;
                var hitVel = LastHitEntVel ?? Vector3.Zero;
                Vector3.ClampToSphere(ref hitVel, (float)MaxSpeed);
                HitEffect.Velocity = hitVel;
            }
        }

        internal void DisposeAmmoEffect(bool instant, bool pause)
        {
            if (AmmoEffect != null)
            {
                AmmoEffect.Stop(instant);
                AmmoEffect = null;
            }

            if (pause) ParticleStopped = true;
        }

        private void DisposeHitEffect(bool instant)
        {
            if (HitEffect != null)
            {
                HitEffect.Stop(instant);
                HitEffect = null;
            }
        }

        internal void PauseAv()
        {
            DisposeAmmoEffect(true, true);
            DisposeHitEffect(true);
        }

        internal void ProjectileClose()
        {
            if (!T.IsShrapnel && GenerateShrapnel) SpawnShrapnel();
            else T.IsShrapnel = false;

            //if (Watchers.Count > 0 && State != ProjectileState.Ending))
            //{
                for (int i = 0; i < Watchers.Count; i++) Watchers[i].DeadProjectiles.Enqueue(this);
                Watchers.Clear();
            //}

            if (!EnableAv && T.System.PrimeModelId == -1 && T.System.TriggerModelId == -1)
            {
                State = ProjectileState.Dead;
                T.Target.IsProjectile = false;
                Manager.CleanUp.Add(this);
            }
            else State = ProjectileState.Ending;
        }

        internal void Stop()
        {
            //if (EndStep++ >= EndSteps)
            {
                if (EnableAv)
                {
                    if (T.System.AmmoParticle) DisposeAmmoEffect(false, false);
                    HitEffects();
                }
                State = ProjectileState.Dead;
                T.Target.IsProjectile = false;
                Manager.CleanUp.Add(this);
            }
        }

        internal bool CloseModel()
        {
            T.PrimeMatrix = MatrixD.Identity;
            T.TriggerMatrix = MatrixD.Identity;
            T.Complete(null, DrawState.Last);
            Manager.DrawProjectiles.Add(T);
            if (T.System.PrimeModelId != -1) Manager.EntityPool[T.System.PrimeModelId].MarkForDeallocate(T.PrimeEntity);
            if (T.System.TriggerModelId != -1) Manager.EntityPool[T.System.TriggerModelId].MarkForDeallocate(T.TriggerEntity);
            ModelState = EntityState.None;
            return true;
        }

        internal enum ProjectileState
        {
            Start,
            Alive,
            Ending,
            Dead,
            OneAndDone,
            Depleted,
        }

        internal enum EntityState
        {
            Exists,
            NoDraw,
            None
        }
    }
}