using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
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
        internal WeaponSystem.FiringSoundState FiringSoundState;
        internal AmmoTrajectory.GuidanceType Guidance;
        internal Vector3D Direction;
        internal Vector3D OriginUp;
        internal Vector3D AccelDir;
        internal Vector3D VisualDir;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Vector3D Origin;
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
        internal float DesiredSpeed;
        internal float AmmoTravelSoundRangeSqr;
        internal float MaxTrajectory;
        internal int PoolId;
        internal int Age;
        internal int ChaseAge;
        internal int IdleTime;
        internal int MaxChaseAge;
        internal int EndStep;
        internal int PrimeModelId;
        internal int TriggerModelId;
        internal int ZombieLifeTime;
        internal int LastOffsetTime;
        internal int PruningProxyId = -1;
        internal int PulseChance;
        internal int PulseInterval;
        internal bool EnableAv;
        internal bool DrawLine;
        internal bool AmmoSound;
        internal bool FirstOffScreen;
        internal bool HasTravelSound;
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
        internal bool FieldActive;
        internal bool FieldEffect;
        internal bool SelfDamage;
        internal bool IsMine;
        internal bool MineSeeking;
        internal bool MineActivated;
        internal bool MineTriggered;
        internal bool Miss;
        //internal readonly MyTimedItemCache VoxelRayCache = new MyTimedItemCache(4000);
        internal List<MyLineSegmentOverlapResult<MyEntity>> EntityRaycastResult = null;
        internal Trajectile T = new Trajectile();
        internal MyParticleEffect AmmoEffect;
        internal MyParticleEffect HitEffect;
        internal MySoundPair FireSound = new MySoundPair();
        internal MySoundPair TravelSound = new MySoundPair();
        internal MySoundPair HitSound = new MySoundPair();
        internal readonly MyEntity3DSoundEmitter FireEmitter = new MyEntity3DSoundEmitter(null, true, 1f);
        internal readonly MyEntity3DSoundEmitter TravelEmitter = new MyEntity3DSoundEmitter(null, true, 1f);
        internal readonly MyEntity3DSoundEmitter HitEmitter = new MyEntity3DSoundEmitter(null, true, 1f);
        internal readonly List<MyLineSegmentOverlapResult<MyEntity>> SegmentList = new List<MyLineSegmentOverlapResult<MyEntity>>();
        internal readonly List<Trajectile> VrTrajectiles = new List<Trajectile>();
        internal readonly List<Projectile> EwaredProjectiles = new List<Projectile>();
        internal readonly List<GridAi> Watchers = new List<GridAi>();

        internal void Start(bool noAv, int poolId)
        {
            PoolId = poolId;

            Position = Origin;
            AccelDir = Direction;
            VisualDir = Direction;
            var cameraStart = Session.Instance.CameraPos;
            Vector3D.DistanceSquared(ref cameraStart, ref Origin, out DistanceFromCameraSqr);
            GenerateShrapnel = T.System.Values.Ammo.Shrapnel.Fragments > 0;
            var noSAv = T.IsShrapnel && T.System.Values.Ammo.Shrapnel.NoAudioVisual;
            var probability = T.System.Values.Graphics.VisualProbability;
            EnableAv = !noAv && !noSAv && DistanceFromCameraSqr <= Session.Instance.SyncDistSqr && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));

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
            ParticleStopped = false;
            ParticleLateStart = false;
            T.OnScreen = true;
            FirstOffScreen = true;
            AmmoSound = false;
            PositionChecked = false;
            EwarActive = false;
            FieldActive = false;
            MineSeeking = false;
            MineActivated = false;
            MineTriggered = false;
            T.Cloaked = false;
            EndStep = 0;
            T.PrevDistanceTraveled = 0;
            T.DistanceTraveled = 0;

            Guidance = !(T.System.Values.Ammo.Shrapnel.NoGuidance && T.IsShrapnel) ? T.System.Values.Ammo.Trajectory.Guidance : AmmoTrajectory.GuidanceType.None;
            IsMine = Guidance == AmmoTrajectory.GuidanceType.DetectFixed || Guidance == AmmoTrajectory.GuidanceType.DetectSmart || Guidance == AmmoTrajectory.GuidanceType.DetectTravelTo;
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
                Vector3D.DistanceSquared(ref Origin, ref distancePos, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = MaxTrajectorySqr;

            PickTarget = LockedTarget && T.System.Values.Ammo.Trajectory.Smarts.OverideTarget;
            FiringSoundState = T.System.FiringSound;
            AmmoTravelSoundRangeSqr = T.System.AmmoTravelSoundDistSqr;
            AreaEffect = T.System.Values.Ammo.AreaEffect.AreaEffect;
            Ewar = AreaEffect > (AreaEffectType) 2;
            FieldEffect = AreaEffect > (AreaEffectType) 3;
            PulseInterval = T.System.Values.Ammo.AreaEffect.Pulse.Interval;
            PulseChance = T.System.Values.Ammo.AreaEffect.Pulse.PulseChance;

            PruneQuery = DynamicGuidance ? MyEntityQueryType.Both : MyEntityQueryType.Dynamic;
            if (T.Ai.StaticEntitiesInRange && !DynamicGuidance) StaticEntCheck();
            else CheckPlanet = false;

            if (EnableAv)
            {
                if (!T.System.IsBeamWeapon && T.System.AmmoTravelSound)
                {
                    HasTravelSound = true;
                    TravelSound.Init(T.System.Values.Audio.Ammo.TravelSound, false);
                }
                else HasTravelSound = false;

                if (T.System.HitSound)
                    HitSound.Init(T.System.Values.Audio.Ammo.HitSound, false);

                if (FiringSoundState == WeaponSystem.FiringSoundState.PerShot)
                {
                    FireSound.Init(T.System.Values.Audio.HardPoint.FiringSound, false);
                    FireSoundStart();
                }
            }

            PrimeModelId = T.System.PrimeModelId;
            TriggerModelId = T.System.TriggerModelId;
            if (PrimeModelId == -1 && TriggerModelId == -1 || T.System.IsBeamWeapon) ModelState = EntityState.None;
            else
            {
                if (EnableAv)
                {
                    ModelState = EntityState.Exists;

                    double triggerModelSize = 0;
                    double primeModelSize = 0;
                    if (TriggerModelId != -1) triggerModelSize = T.TriggerEntity.PositionComp.WorldVolume.Radius;
                    if (PrimeModelId != -1) primeModelSize = T.PrimeEntity.PositionComp.WorldVolume.Radius;
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

            if (ConstantSpeed)
            {
                Velocity = MaxVelocity;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = StartSpeed + AccelVelocity;

            TravelMagnitude = Velocity * StepConst;

            IdleTime = T.System.Values.Ammo.Trajectory.RestTime;

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
                        var check = State == ProjectileState.OneAndDone;
                        if (!check)
                        {
                            Vector3D? voxelHit;
                            using (voxel.Pin()) voxel.GetIntersectionWithLine(ref lineTest, out voxelHit);
                            check = voxelHit.HasValue;
                        }

                        if (check)
                        {
                            PruneQuery = MyEntityQueryType.Both;
                            if (voxel == ai.MyPlanet) CheckPlanet = true;
                            break;
                        }
                    }
                    else
                    {
                        if (grid != null && grid.IsSameConstructAs(T.Ai.MyGrid)) continue;
                        PruneQuery = MyEntityQueryType.Both;
                        if (CheckPlanet || !ai.PlanetSurfaceInRange) break;
                    }
                }
            }
        }

        internal void FireSoundStart()
        {
            FireEmitter.SetPosition(Origin);
            FireEmitter.PlaySound(FireSound, true);
        }

        internal void AmmoSoundStart()
        {
            TravelEmitter.SetPosition(Position);
            TravelEmitter.PlaySound(TravelSound, true);

            AmmoSound = true;
        }

        private void SpawnShrapnel()
        {
            var shrapnel = Session.Instance.Projectiles.ShrapnelPool[PoolId].Get();
            shrapnel.Init(this, Session.Instance.Projectiles.FragmentPool[PoolId]);
            Session.Instance.Projectiles.ShrapnelToSpawn[PoolId].Add(shrapnel);
        }

        internal void ProjectileClose(Projectiles manager, int poolId)
        {
            if (!T.IsShrapnel && GenerateShrapnel) SpawnShrapnel();
            else T.IsShrapnel = false;

            if (Watchers.Count > 0 && !(State == ProjectileState.Ending || State == ProjectileState.Ending))
            {
                for (int i = 0; i < Watchers.Count; i++) Watchers[i].DeadProjectiles.Enqueue(this);
                Watchers.Clear();
            }

            if (!EnableAv && PrimeModelId == -1 && TriggerModelId == -1)
            {
                State = ProjectileState.Dead;
                manager.CleanUp[poolId].Add(this);
            }
            else State = ProjectileState.Ending;
        }

        internal void Stop(Projectiles manager, int poolId)
        {
            if (EndStep++ >= EndSteps)
            {
                if (EnableAv)
                {
                    if (T.System.AmmoParticle) DisposeAmmoEffect(false, false);
                    HitEffects();
                    if (AmmoSound) TravelEmitter.StopSound(false, true);
                }
                State = ProjectileState.Dead;
                manager.CleanUp[poolId].Add(this);
            }
        }

        internal bool CloseModel(Projectiles manager, int poolId)
        {
            T.PrimeMatrix = MatrixD.Identity;
            T.TriggerMatrix = MatrixD.Identity;
            T.Complete(null, DrawState.Last);
            manager.DrawProjectiles[poolId].Add(T);
            if (PrimeModelId != -1) manager.EntityPool[poolId][PrimeModelId].MarkForDeallocate(T.PrimeEntity);
            if (TriggerModelId != -1) manager.EntityPool[poolId][TriggerModelId].MarkForDeallocate(T.TriggerEntity);
            ModelState = EntityState.None;
            return true;
        }

        internal bool EndChase()
        {
            ChaseAge = Age;
            PickTarget = false;
            var reaquire = GridAi.ReacquireTarget(this);
            if (!reaquire)
            {
                T.Target.Entity = null;
                T.Target.IsProjectile = false;
            }

            return reaquire;
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

            Log.Line($"Activated Mine: Ewar{Ewar}");
            if (Guidance == AmmoTrajectory.GuidanceType.DetectFixed) return;

            Vector3D.DistanceSquared(ref Origin, ref predictedPos, out DistanceToTravelSqr);
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
                if (startTimer) IdleTime = T.System.Values.Ammo.Trajectory.Mines.FieldTime;
            }
            else if (startTimer) IdleTime = 0;
            MineTriggered = true;
            Log.Line($"[Mine] Ewar:{Ewar} - Activated:{MineActivated} - active:{EwarActive} - Triggered:{T.Triggered} - IdleTime:{IdleTime}");
        }

        internal void RunSmart()
        {
            Vector3D newVel;
            if ((AccelLength <= 0 || Vector3D.DistanceSquared(Origin, Position) >= SmartsDelayDistSqr))
            {
                var giveUpChase = Age - ChaseAge > MaxChaseAge;
                var newChase = (giveUpChase || PickTarget);

                var validTarget = T.Target.IsProjectile || T.Target.Entity != null && !T.Target.Entity.MarkedForClose;
                if (newChase && EndChase() || validTarget || !IsMine && ZombieLifeTime % 30 == 0 && GridAi.ReacquireTarget(this))
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
            }
        }

        internal void RunEwar()
        {
            if (VelocityLengthSqr <= 0 && !T.Triggered && !IsMine)
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

            if (Age % PulseInterval == 0)
                PulseField();
            else EwarActive = false;
        }

        internal void PulseField()
        {
            switch (AreaEffect)
            {
                case AreaEffectType.AntiSmart:
                    var eWarSphere = new BoundingSphereD(Position, T.System.AreaEffectSize);
                    DynTrees.GetAllProjectilesInSphere(ref eWarSphere, EwaredProjectiles, false);
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
                        Log.Line($"jumpNullField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
                case AreaEffectType.AnchorField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        Log.Line($"jumpAnchorFieldNullField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
                case AreaEffectType.EnergySinkField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        Log.Line($"EnergySinkField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
                case AreaEffectType.EmpField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        Log.Line($"EmpField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
                case AreaEffectType.OffenseField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        Log.Line($"OffenseField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
                case AreaEffectType.NavField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        Log.Line($"NavField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }

                    break;
                case AreaEffectType.DotField:
                    if (T.Triggered && MyUtils.GetRandomInt(0, 100) < PulseChance)
                    {
                        Log.Line($"DotField Pulse - Time:{IdleTime} - distTravel:{T.DistanceTraveled}({T.DistanceTraveled * T.DistanceTraveled} >= {DistanceToTravelSqr})");
                        EwarActive = true;
                    }
                    break;
            }
        }

        internal void OffSetTarget(out Vector3D targetOffset)
        {
            var randAzimuth = MyUtils.GetRandomDouble(0, 1) * 2 * Math.PI;
            var randElevation = (MyUtils.GetRandomDouble(0, 1) * 2 - 1) * 0.5 * Math.PI;

            Vector3D randomDirection;
            Vector3D.CreateFromAzimuthAndElevation(randAzimuth, randElevation, out randomDirection); // this is already normalized
            targetOffset = (randomDirection * T.System.Values.Ammo.Trajectory.Smarts.Inaccuracy);
            VisualStep = 0;
            if (Age != 0) LastOffsetTime = Age;
        }

        internal void HitEffects()
        {
            if (Colliding)
            {
                if (T.System.HitParticle && !T.System.IsBeamWeapon) PlayHitParticle();
                if (T.System.HitSound)
                {
                    HitEmitter.SetPosition(Position);
                    HitEmitter.CanPlayLoopSounds = false;
                    //HitEmitter.PlaySoundWithDistance(HitSound.SoundId, true, false, false, true, true, false, false);
                    HitEmitter.PlaySound(HitSound, true);

                }
            }
            Colliding = false;
        }

        internal void PlayAmmoParticle()
        {
            if (Age == 0 && !ParticleLateStart)
            {
                TestSphere.Center = Position;
                if (!Session.Instance.Session.Camera.IsInFrustum(ref TestSphere))
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
                matrix = MatrixD.CreateWorld(Position, AccelDir, OriginUp);
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

        private void PlayHitParticle()
        {
            if (HitEffect != null) DisposeHitEffect(false);
            if (LastHitPos.HasValue)
            {
                var pos = LastHitPos.Value;
                var matrix = MatrixD.CreateTranslation(pos);
                MyParticlesManager.TryCreateParticleEffect(T.System.Values.Graphics.Particles.Hit.Name, ref matrix, ref pos, uint.MaxValue, out HitEffect);
                if (HitEffect == null) return;
                HitEffect.Loop = false;
                HitEffect.DurationMax = T.System.Values.Graphics.Particles.Hit.Extras.MaxDuration;
                HitEffect.DistanceMax = T.System.Values.Graphics.Particles.Hit.Extras.MaxDistance;
                HitEffect.UserColorMultiplier = T.System.Values.Graphics.Particles.Hit.Color;
                //var reScale = (float)Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
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