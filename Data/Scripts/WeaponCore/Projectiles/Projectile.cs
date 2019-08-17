using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
namespace WeaponCore.Projectiles
{
    internal class Projectile
    {
        internal const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal const int EndSteps = 2;
        internal ProjectileState State;
        internal EntityState ModelState;
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
        internal double AccelLength;
        internal double MaxTrajectorySqr;
        internal double DistanceTraveled;
        internal double DistanceToTravelSqr;
        internal double LineLength;
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
        internal int MaxChaseAge;
        internal int GrowStep = 1;
        internal int EndStep;
        internal int ModelId;
        internal int ZombieLifeTime;
        internal int LastOffsetTime;
        internal bool Grow;
        internal bool EnableAv;
        internal bool DrawLine;
        internal bool AmmoSound;
        internal bool FirstOffScreen;
        internal bool HasTravelSound;
        internal bool ConstantSpeed;
        internal bool PositionChecked;
        internal bool MoveToAndActivate;
        internal bool LockedTarget;
        internal bool FoundTarget;
        internal bool SeekTarget;
        internal bool DynamicGuidance;
        internal bool ParticleStopped;
        internal bool ParticleLateStart;
        internal bool PickTarget;
        internal bool GenerateShrapnel;
        internal bool Colliding;
        internal WeaponSystem.FiringSoundState FiringSoundState;
        internal AmmoTrajectory.GuidanceType Guidance;
        internal BoundingSphereD TestSphere = new BoundingSphereD(Vector3D.Zero, 200f);
        internal BoundingSphereD ModelSphereCurrent;
        internal BoundingSphereD ModelSphereLast;
        internal readonly MyTimedItemCache VoxelRayCache = new MyTimedItemCache(4000);
        internal List<MyLineSegmentOverlapResult<MyEntity>> EntityRaycastResult = null;
        internal Trajectile T = new Trajectile();
        internal MyParticleEffect AmmoEffect;
        internal MyParticleEffect HitEffect;
        internal readonly MyEntity3DSoundEmitter FireEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyEntity3DSoundEmitter TravelEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyEntity3DSoundEmitter HitEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly List<Trajectile> VrTrajectiles = new List<Trajectile>();
        internal readonly List<GridAi> Watchers = new List<GridAi>();
        internal readonly List<MyLineSegmentOverlapResult<MyEntity>> SegmentList = new List<MyLineSegmentOverlapResult<MyEntity>>();
        internal MySoundPair FireSound = new MySoundPair();
        internal MySoundPair TravelSound = new MySoundPair();
        internal MySoundPair HitSound = new MySoundPair();

        internal void Start(bool noAv, int poolId)
        {
            PoolId = poolId;

            Position = Origin;
            AccelDir = Direction;
            VisualDir = Direction;
            var cameraStart = MyAPIGateway.Session.Camera.Position;
            Vector3D.DistanceSquared(ref cameraStart, ref Origin, out DistanceFromCameraSqr);
            GenerateShrapnel = T.System.Values.Ammo.Shrapnel.Fragments > 0;
            var noSAv = T.IsShrapnel && T.System.Values.Ammo.Shrapnel.NoAudioVisual;
            var probability = T.System.Values.Graphics.VisualProbability;
            EnableAv = !noAv && !noSAv && DistanceFromCameraSqr <= Session.Instance.SyncDistSqr && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));

            T.EntityMatrix = MatrixD.Identity;
            ModelState = EntityState.None;
            LastEntityPos = Position;
            PrevTargetPos = PredictedTargetPos;
            PrevTargetVel = Vector3D.Zero;

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
            EndStep = 0;
            GrowStep = 1;
            DistanceTraveled = 0;
            Guidance = !(T.System.Values.Ammo.Shrapnel.NoGuidance && T.IsShrapnel) ? T.System.Values.Ammo.Trajectory.Guidance : AmmoTrajectory.GuidanceType.None;
            DynamicGuidance = Guidance != AmmoTrajectory.GuidanceType.None;

            if (Guidance == AmmoTrajectory.GuidanceType.Smart && !T.System.IsBeamWeapon)
                MaxChaseAge = T.System.Values.Ammo.Trajectory.Smarts.MaxChaseTime;
            else MaxChaseAge = int.MaxValue;

            if (T.Target.Projectile != null) OriginTargetPos = T.Target.Projectile.Position;
            else if (T.Target.Entity != null) OriginTargetPos = T.Target.Entity.PositionComp.WorldAABB.Center;
            else OriginTargetPos = Vector3D.Zero;
            LockedTarget = OriginTargetPos != Vector3D.Zero;

            if (T.System.TargetOffSet && LockedTarget)
            {
                OffSetTarget(out TargetOffSet);
                OffsetSqr = T.System.Values.Ammo.Trajectory.Smarts.Inaccuracy * T.System.Values.Ammo.Trajectory.Smarts.Inaccuracy;
            }
            else
            {
                TargetOffSet = Vector3D.Zero;
                OffsetSqr = 0;
            }

            DrawLine = T.System.Values.Graphics.Line.Trail;
            if (T.System.RangeVariance)
            {
                var min = T.System.Values.Ammo.Trajectory.RangeVariance.Start;
                var max = T.System.Values.Ammo.Trajectory.RangeVariance.End;
                MaxTrajectory = T.System.Values.Ammo.Trajectory.MaxTrajectory - MyUtils.GetRandomFloat(min, max);
            }
            else MaxTrajectory = T.System.Values.Ammo.Trajectory.MaxTrajectory;


            T.ObjectsHit = 0;
            T.BaseDamagePool = T.System.Values.Ammo.BaseDamage;
            T.BaseHealthPool = T.System.Values.Ammo.Health;
            LineLength = T.System.Values.Graphics.Line.Length;

            if (T.IsShrapnel)
            {
                var shrapnel = T.System.Values.Ammo.Shrapnel;
                T.BaseDamagePool = shrapnel.BaseDamage;
                MaxTrajectory = shrapnel.MaxTrajectory;
                LineLength = LineLength / shrapnel.Fragments >= 1 ? LineLength / shrapnel.Fragments : 1;
            }

            MaxTrajectorySqr = MaxTrajectory * MaxTrajectory;

            var smartsDelayDist = LineLength * T.System.Values.Ammo.Trajectory.Smarts.TrackingDelay;
            SmartsDelayDistSqr = smartsDelayDist * smartsDelayDist;

            if (!T.IsShrapnel) StartSpeed = GridVel;

            if (T.System.SpeedVariance && !T.System.IsBeamWeapon)
            {
                var min = T.System.Values.Ammo.Trajectory.SpeedVariance.Start;
                var max = T.System.Values.Ammo.Trajectory.SpeedVariance.End;
                DesiredSpeed = T.System.Values.Ammo.Trajectory.DesiredSpeed - MyUtils.GetRandomFloat(min, max);
            }
            else DesiredSpeed = T.System.Values.Ammo.Trajectory.DesiredSpeed;


            if (LockedTarget) FoundTarget = true;
            else if (DynamicGuidance) SeekTarget = true;
            MoveToAndActivate = FoundTarget && !T.System.IsBeamWeapon && Guidance == AmmoTrajectory.GuidanceType.TravelTo;

            if (MoveToAndActivate)
            {
                var distancePos = PredictedTargetPos != Vector3D.Zero ? PredictedTargetPos : OriginTargetPos;
                Vector3D.DistanceSquared(ref Origin, ref distancePos, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = MaxTrajectorySqr;

            PickTarget = LockedTarget && T.System.Values.Ammo.Trajectory.Smarts.OverideTarget;
            FiringSoundState = T.System.FiringSound;
            AmmoTravelSoundRangeSqr = T.System.AmmoTravelSoundDistSqr;

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

            ModelId = T.System.ModelId;
            if (ModelId == -1 || T.System.IsBeamWeapon) ModelState = EntityState.None;
            else
            {
                if (EnableAv)
                {
                    ModelState = EntityState.Exists;
                    ModelSphereCurrent.Radius = T.Entity.PositionComp.WorldVolume.Radius * 2;
                    ModelSphereLast.Radius = T.Entity.PositionComp.WorldVolume.Radius * 2;
                }
                else ModelState = EntityState.NoDraw;
            }

            var accelPerSec = T.System.Values.Ammo.Trajectory.AccelPerSec;
            ConstantSpeed = accelPerSec <= 0;
            AccelPerSec = accelPerSec > 0 ? accelPerSec : DesiredSpeed; 
            MaxVelocity = StartSpeed + (Direction * DesiredSpeed);
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            T.MaxSpeedLength = MaxSpeed * StepConst;
            AccelLength = T.System.Values.Ammo.Trajectory.AccelPerSec * StepConst;
            AccelVelocity = (Direction * AccelLength);
            Velocity = ConstantSpeed ? MaxVelocity : StartSpeed + AccelVelocity;
            TravelMagnitude = Velocity * StepConst;
            if (!T.System.IsBeamWeapon)
            {
                var reSizeSteps = (int) (LineLength / T.MaxSpeedLength);
                T.ReSizeSteps = ModelState == EntityState.None && reSizeSteps > 0 ? reSizeSteps : 1;
                Grow = T.ReSizeSteps > 1 || AccelLength > 0 && AccelLength < LineLength;
                T.Shrink = Grow;
                State = ProjectileState.Alive;
            }
            else State = ProjectileState.OneAndDone;

            if (T.System.AmmoParticle && EnableAv && !T.System.IsBeamWeapon) PlayAmmoParticle();
        }

        internal void FireSoundStart()
        {
            FireEmitter.SetPosition(Origin);
            FireEmitter.PlaySoundWithDistance(FireSound.SoundId, false, false, false, true, false, false, false);
        }

        internal void AmmoSoundStart()
        {
            TravelEmitter.SetPosition(Position);
            TravelEmitter.PlaySoundWithDistance(TravelSound.SoundId, false, false, false, true, false, false, false);
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

            if (!EnableAv && ModelId == -1)
            {
                for (int i = 0; i < VrTrajectiles.Count; i++)
                    manager.TrajectilePool[poolId].MarkForDeallocate(VrTrajectiles[i]);
                VrTrajectiles.Clear();
                T.HitList.Clear();
                T.Target.Reset();
                manager.ProjectilePool[poolId].MarkForDeallocate(this);
                State = ProjectileState.Dead;
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
                for (int i = 0; i < VrTrajectiles.Count; i++)
                    manager.TrajectilePool[poolId].MarkForDeallocate(VrTrajectiles[i]);

                VrTrajectiles.Clear();
                T.HitList.Clear();
                T.Target.Reset();
                manager.ProjectilePool[poolId].MarkForDeallocate(this);
                State = ProjectileState.Dead;
            }
        }

        internal bool CloseModel(Projectiles manager, int poolId)
        {
            T.EntityMatrix = MatrixD.Identity;
            T.Complete(null, true);
            manager.DrawProjectiles[poolId].Add(T);
            manager.EntityPool[poolId][ModelId].MarkForDeallocate(T.Entity);
            ModelState = EntityState.None;
            return true;
        }

        internal bool EndChase()
        {
            ChaseAge = Age;
            PickTarget = false;
            Log.Line($"EndChase");
            var reaquire = GridAi.ReacquireTarget(this);
            if (!reaquire)
            {
                T.Target.Entity = null;
                T.Target.Projectile = null;
                T.Target.IsProjectile = false;
            }

            return reaquire;
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
                if (ZombieLifeTime++ > T.System.TargetLossTime) DistanceToTravelSqr = DistanceTraveled * DistanceTraveled;
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
                    HitEmitter.PlaySoundWithDistance(HitSound.SoundId, true, false, false, true, true, false, false);
                }
            }
            Colliding = false;
        }

        internal void PlayAmmoParticle()
        {
            if (Age == 0 && !ParticleLateStart)
            {
                TestSphere.Center = Position;
                if (!MyAPIGateway.Session.Camera.IsInFrustum(ref TestSphere))
                {
                    ParticleLateStart = true;
                    return;
                }
            }
            MatrixD matrix;
            if (ModelState == EntityState.Exists)
            {
                matrix = MatrixD.CreateWorld(Position, AccelDir, T.Entity.PositionComp.WorldMatrix.Up);
                if (T.IsShrapnel) MatrixD.Rescale(ref matrix, 0.5f);
                var offVec = Position + Vector3D.Rotate(T.System.Values.Graphics.Particles.Ammo.Offset, matrix);
                matrix.Translation = offVec;
                T.EntityMatrix = matrix;
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