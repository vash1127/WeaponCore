using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectiles;
namespace WeaponCore.Projectiles
{
    internal class Projectile
    {
        internal const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal const int EndSteps = 2;
        internal volatile float BaseDamagePool;
        internal volatile bool Colliding;
        internal MyCubeGrid FiringGrid;
        internal GridAi Ai;
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
        internal int ObjectsHit;
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
        internal bool IsShrapnel;
        internal WeaponSystem.FiringSoundState FiringSoundState;
        internal AmmoTrajectory.GuidanceType Guidance;
        internal BoundingSphereD TestSphere = new BoundingSphereD(Vector3D.Zero, 200f);
        internal BoundingSphereD ModelSphereCurrent;
        internal BoundingSphereD ModelSphereLast;
        internal readonly MyTimedItemCache VoxelRayCache = new MyTimedItemCache(4000);
        internal List<MyLineSegmentOverlapResult<MyEntity>> EntityRaycastResult = null;
        internal Target Target = new Target();
        internal Trajectile Trajectile = new Trajectile();
        internal MyParticleEffect AmmoEffect;
        internal MyParticleEffect HitEffect;
        internal WeaponDamageFrame DamageFrame;
        internal readonly MyEntity3DSoundEmitter FireEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyEntity3DSoundEmitter TravelEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyEntity3DSoundEmitter HitEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly List<HitEntity> HitList = new List<HitEntity>();
        internal readonly List<Trajectile> VrTrajectiles = new List<Trajectile>();

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
            var probability = Trajectile.System.Values.Graphics.VisualProbability;
            EnableAv = !noAv && DistanceFromCameraSqr <= Session.Instance.SyncDistSqr && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));

            Trajectile.EntityMatrix = MatrixD.Identity;
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
            ObjectsHit = 0;
            Colliding = false;
            ParticleStopped = false;
            ParticleLateStart = false;
            Trajectile.OnScreen = true;
            FirstOffScreen = true;
            AmmoSound = false;
            PositionChecked = false;
            EndStep = 0;
            GrowStep = 1;
            DistanceTraveled = 0;

            FiringGrid = Trajectile.FiringCube.CubeGrid;

            Guidance = Trajectile.System.Values.Ammo.Trajectory.Guidance;
            DynamicGuidance = Guidance != AmmoTrajectory.GuidanceType.None;

            if (Guidance == AmmoTrajectory.GuidanceType.Smart && !Trajectile.System.IsBeamWeapon)
                MaxChaseAge = Trajectile.System.Values.Ammo.Trajectory.Smarts.MaxChaseTime;
            else MaxChaseAge = int.MaxValue;

            Target.System = Trajectile.System;
            Target.MyCube = Trajectile.FiringCube;
            LockedTarget = Target.Entity != null && !Target.Entity.MarkedForClose;
            if (Target.Entity != null && LockedTarget) OriginTargetPos = Target.Entity.PositionComp.WorldAABB.Center;

            if (Trajectile.System.TargetOffSet && LockedTarget)
            {
                OffSetTarget(out TargetOffSet);
                OffsetSqr = Trajectile.System.Values.Ammo.Trajectory.Smarts.Inaccuracy * Trajectile.System.Values.Ammo.Trajectory.Smarts.Inaccuracy;
            }
            else
            {
                TargetOffSet = Vector3D.Zero;
                OffsetSqr = 0;
            }

            DrawLine = Trajectile.System.Values.Graphics.Line.Trail;
            if (Trajectile.System.RangeVariance)
            {
                var min = Trajectile.System.Values.Ammo.Trajectory.RangeVariance.Start;
                var max = Trajectile.System.Values.Ammo.Trajectory.RangeVariance.End;
                MaxTrajectory = Trajectile.System.Values.Ammo.Trajectory.MaxTrajectory - MyUtils.GetRandomFloat(min, max);
            }
            else MaxTrajectory = Trajectile.System.Values.Ammo.Trajectory.MaxTrajectory;

            BaseDamagePool = Trajectile.System.Values.Ammo.BaseDamage;
            LineLength = Trajectile.System.Values.Graphics.Line.Length;

            if (IsShrapnel)
            {
                var shrapnel = Trajectile.System.Values.Ammo.Shrapnel;
                BaseDamagePool = shrapnel.BaseDamage;
                MaxTrajectory = shrapnel.MaxTrajectory;
                LineLength = LineLength / shrapnel.Fragments >= 1 ? LineLength / shrapnel.Fragments : 1;
            }

            MaxTrajectorySqr = MaxTrajectory * MaxTrajectory;

            var smartsDelayDist = LineLength * Trajectile.System.Values.Ammo.Trajectory.Smarts.TrackingDelay;
            SmartsDelayDistSqr = smartsDelayDist * smartsDelayDist;

            StartSpeed = FiringGrid.Physics.LinearVelocity;

            if (Trajectile.System.SpeedVariance && !Trajectile.System.IsBeamWeapon)
            {
                var min = Trajectile.System.Values.Ammo.Trajectory.SpeedVariance.Start;
                var max = Trajectile.System.Values.Ammo.Trajectory.SpeedVariance.End;
                DesiredSpeed = Trajectile.System.Values.Ammo.Trajectory.DesiredSpeed - MyUtils.GetRandomFloat(min, max);
            }
            else DesiredSpeed = Trajectile.System.Values.Ammo.Trajectory.DesiredSpeed;


            if (LockedTarget) FoundTarget = true;
            else if (DynamicGuidance) SeekTarget = true;
            MoveToAndActivate = FoundTarget && !Trajectile.System.IsBeamWeapon && Guidance == AmmoTrajectory.GuidanceType.TravelTo;

            if (MoveToAndActivate)
            {
                var distancePos = PredictedTargetPos != Vector3D.Zero ? PredictedTargetPos : OriginTargetPos;
                Vector3D.DistanceSquared(ref Origin, ref distancePos, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = MaxTrajectorySqr;

            PickTarget = LockedTarget && Trajectile.System.Values.Ammo.Trajectory.Smarts.OverideTarget;
            FiringSoundState = Trajectile.System.FiringSound;
            AmmoTravelSoundRangeSqr = Trajectile.System.AmmoTravelSoundDistSqr;

            if (EnableAv)
            {
                if (!Trajectile.System.IsBeamWeapon && Trajectile.System.AmmoTravelSound)
                {
                    HasTravelSound = true;
                    TravelSound.Init(Trajectile.System.Values.Audio.Ammo.TravelSound, false);
                }
                else HasTravelSound = false;

                if (Trajectile.System.HitSound)
                    HitSound.Init(Trajectile.System.Values.Audio.Ammo.HitSound, false);

                if (FiringSoundState == WeaponSystem.FiringSoundState.PerShot)
                {
                    FireSound.Init(Trajectile.System.Values.Audio.HardPoint.FiringSound, false);
                    FireSoundStart();
                }
            }

            ModelId = Trajectile.System.ModelId;
            if (ModelId == -1 || Trajectile.System.IsBeamWeapon) ModelState = EntityState.None;
            else
            {
                if (EnableAv)
                {
                    ModelState = EntityState.Exists;
                    ModelSphereCurrent.Radius = Trajectile.Entity.PositionComp.WorldVolume.Radius * 2;
                    ModelSphereLast.Radius = Trajectile.Entity.PositionComp.WorldVolume.Radius * 2;
                }
                else ModelState = EntityState.NoDraw;
            }

            var accelPerSec = Trajectile.System.Values.Ammo.Trajectory.AccelPerSec;
            ConstantSpeed = accelPerSec <= 0;
            AccelPerSec = accelPerSec > 0 ? accelPerSec : DesiredSpeed; 
            MaxVelocity = StartSpeed + (Direction * DesiredSpeed);
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            Trajectile.MaxSpeedLength = MaxSpeed * StepConst;
            AccelLength = Trajectile.System.Values.Ammo.Trajectory.AccelPerSec * StepConst;
            AccelVelocity = (Direction * AccelLength);
            Velocity = ConstantSpeed ? MaxVelocity : StartSpeed + AccelVelocity;
            TravelMagnitude = Velocity * StepConst;
            if (!Trajectile.System.IsBeamWeapon)
            {
                var reSizeSteps = (int) (LineLength / Trajectile.MaxSpeedLength);
                Trajectile.ReSizeSteps = ModelState == EntityState.None && reSizeSteps > 0 ? reSizeSteps : 1;
                Grow = Trajectile.ReSizeSteps > 1 || AccelLength > 0 && AccelLength < LineLength;
                Trajectile.Shrink = Grow;
                State = ProjectileState.Alive;
            }
            else State = ProjectileState.OneAndDone;

            if (Trajectile.System.AmmoParticle && EnableAv && !Trajectile.System.IsBeamWeapon) PlayAmmoParticle();
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
            if (!IsShrapnel && Trajectile.System.Values.Ammo.Shrapnel.Fragments > 0) SpawnShrapnel();

            if (!EnableAv && ModelId == -1)
            {
                HitList.Clear();
                VrTrajectiles.Clear();
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
                    if (Trajectile.System.AmmoParticle) DisposeAmmoEffect(false, false);
                    HitEffects();
                    if (AmmoSound) TravelEmitter.StopSound(false, true);
                }

                HitList.Clear();
                VrTrajectiles.Clear();
                manager.ProjectilePool[poolId].MarkForDeallocate(this);
                State = ProjectileState.Dead;
            }
        }

        internal bool CloseModel(Projectiles manager, int poolId)
        {
            Trajectile.EntityMatrix = MatrixD.Identity;
            Trajectile.Complete(null, true);
            manager.DrawProjectiles[poolId].Add(Trajectile);
            manager.EntityPool[poolId][ModelId].MarkForDeallocate(Trajectile.Entity);
            ModelState = EntityState.None;
            return true;
        }

        internal bool EndChase()
        {
            ChaseAge = Age;
            PickTarget = false;
            var reaquire = GridAi.ReacquireTarget(this);
            if (!reaquire) Target.Entity = null;
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
                if (ZombieLifeTime++ > Trajectile.System.TargetLossTime) DistanceToTravelSqr = DistanceTraveled * DistanceTraveled;
            }
        }

        internal void OffSetTarget(out Vector3D targetOffset)
        {
            var randAzimuth = MyUtils.GetRandomDouble(0, 1) * 2 * Math.PI;
            var randElevation = (MyUtils.GetRandomDouble(0, 1) * 2 - 1) * 0.5 * Math.PI;

            Vector3D randomDirection;
            Vector3D.CreateFromAzimuthAndElevation(randAzimuth, randElevation, out randomDirection); // this is already normalized
            targetOffset = (randomDirection * Trajectile.System.Values.Ammo.Trajectory.Smarts.Inaccuracy);
            VisualStep = 0;
            if (Age != 0) LastOffsetTime = Age;
        }

        internal void HitEffects()
        {
            if (Colliding)
            {
                if (Trajectile.System.HitParticle && !Trajectile.System.IsBeamWeapon) PlayHitParticle();
                if (Trajectile.System.HitSound)
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
                matrix = MatrixD.CreateWorld(Position, AccelDir, Trajectile.Entity.PositionComp.WorldMatrix.Up);
                if (IsShrapnel) MatrixD.Rescale(ref matrix, 0.5f);
                var offVec = Position + Vector3D.Rotate(Trajectile.System.Values.Graphics.Particles.Ammo.Offset, matrix);
                matrix.Translation = offVec;
                Trajectile.EntityMatrix = matrix;
            }
            else
            {
                matrix = MatrixD.CreateWorld(Position, AccelDir, OriginUp);
                var offVec = Position + Vector3D.Rotate(Trajectile.System.Values.Graphics.Particles.Ammo.Offset, matrix);
                matrix.Translation = offVec;
            }

            MyParticlesManager.TryCreateParticleEffect(Trajectile.System.Values.Graphics.Particles.Ammo.Name, ref matrix, ref Position, uint.MaxValue, out AmmoEffect); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (AmmoEffect == null) return;
            AmmoEffect.DistanceMax = Trajectile.System.Values.Graphics.Particles.Ammo.Extras.MaxDistance;
            AmmoEffect.UserColorMultiplier = Trajectile.System.Values.Graphics.Particles.Ammo.Color;
            //var reScale = (float)Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
            var scaler = !IsShrapnel ? 1 : 0.5f;

            AmmoEffect.UserRadiusMultiplier = Trajectile.System.Values.Graphics.Particles.Ammo.Extras.Scale * scaler;
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
                MyParticlesManager.TryCreateParticleEffect(Trajectile.System.Values.Graphics.Particles.Hit.Name, ref matrix, ref pos, uint.MaxValue, out HitEffect);
                if (HitEffect == null) return;
                HitEffect.Loop = false;
                HitEffect.DurationMax = Trajectile.System.Values.Graphics.Particles.Hit.Extras.MaxDuration;
                HitEffect.DistanceMax = Trajectile.System.Values.Graphics.Particles.Hit.Extras.MaxDistance;
                HitEffect.UserColorMultiplier = Trajectile.System.Values.Graphics.Particles.Hit.Color;
                //var reScale = (float)Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
                var reScale = 1;
                var scaler = reScale < 1 ? reScale : 1;

                HitEffect.UserRadiusMultiplier = Trajectile.System.Values.Graphics.Particles.Hit.Extras.Scale * scaler;
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