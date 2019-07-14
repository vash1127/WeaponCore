﻿using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
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
        //private static int _checkIntersectionCnt = 0;
        internal const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal const int EndSteps = 2;
        internal volatile float DamagePool;
        internal volatile bool Colliding;
        internal ProjectileState State;
        internal EntityState ModelState;
        internal MatrixD EntityMatrix;
        internal Trajectile Trajectile;
        internal Vector3D Direction;
        internal Vector3D OriginUp;
        internal Vector3D AccelDir;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Vector3D Origin;
        internal Vector3D StartSpeed;
        internal Vector3D Velocity;
        internal Vector3D AccelVelocity;
        internal Vector3D MaxVelocity;
        internal Vector3D TravelMagnitude;
        internal Vector3D CameraStartPos;
        internal Vector3D LastEntityPos;
        internal Vector3D OriginTargetPos;
        internal Vector3D PredictedTargetPos;
        internal Vector3D PrevTargetPos;
        internal Vector3 PrevTargetVel;
        internal Vector3D? LastHitPos;
        internal Vector3D? LastHitEntVel;
        internal WeaponSystem System;
        internal List<HitEntity> HitList;
        internal MyCubeBlock FiringCube;
        internal MyCubeGrid FiringGrid;
        internal float DesiredSpeed;
        internal float DesiredSpeedSqr;
        internal double MaxSpeedLength;
        internal double AccelLength;
        internal double CheckLength;
        internal float AmmoTravelSoundRangeSqr;
        internal float MaxTrajectory;
        internal float SmartsFactor;
        internal double MaxTrajectorySqr;
        internal double DistanceTraveled;
        internal double DistanceToTravelSqr;
        internal double ShotLength;
        internal double SmartsDelayDistSqr;
        internal double ScreenCheckRadius;
        internal double DistanceFromCameraSqr;
        internal double AccelPerSec;
        internal int Age;
        internal int ObjectsHit;
        internal int ReSizeSteps;
        internal int GrowStep = 1;
        internal int EndStep;
        internal int ModelId;
        internal int WeaponId;
        internal int MuzzleId;
        internal bool Grow;
        internal bool Shrink;
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
        internal bool IsBeamWeapon;
        internal bool OnScreen;
        internal bool ParticleStopped;
        internal bool ParticleLateStart;
        internal WeaponSystem.FiringSoundState FiringSoundState;
        internal AmmoTrajectory.GuidanceType Guidance;
        internal BoundingSphereD TestSphere = new BoundingSphereD(Vector3D.Zero, 5f);
        internal readonly MyTimedItemCache VoxelRayCache = new MyTimedItemCache(4000);
        internal List<MyLineSegmentOverlapResult<MyEntity>> EntityRaycastResult = null;
        internal MyEntity Entity;
        internal MyEntity Target;
        internal MyParticleEffect Effect1;
        internal readonly MyEntity3DSoundEmitter FireEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyEntity3DSoundEmitter TravelEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyEntity3DSoundEmitter HitEmitter = new MyEntity3DSoundEmitter(null, false, 1f);

        internal MySoundPair FireSound = new MySoundPair();
        internal MySoundPair TravelSound = new MySoundPair();
        internal MySoundPair HitSound = new MySoundPair();

        internal void Start(List<HitEntity> hitList, bool noAv)
        {
            ModelState = EntityState.None;
            CameraStartPos = MyAPIGateway.Session.Camera.Position;
            Position = Origin;
            AccelDir = Direction;
            LastEntityPos = Position;
            PrevTargetPos = PredictedTargetPos;
            PrevTargetVel = Vector3D.Zero;
            LastHitPos = null;
            LastHitEntVel = null;
            FiringGrid = FiringCube.CubeGrid;
            Age = 0;
            ObjectsHit = 0;
            Colliding = false;

            ParticleStopped = false;
            ParticleLateStart = false;
            OnScreen = true;
            FirstOffScreen = true;
            AmmoSound = false;
            PositionChecked = false;
            EndStep = 0;
            GrowStep = 1;
            DistanceTraveled = 0;
            DamagePool = System.Values.Ammo.DefaultDamage;
            Guidance = System.Values.Ammo.Trajectory.Guidance;
            DynamicGuidance = Guidance != AmmoTrajectory.GuidanceType.None;
            LockedTarget = Target != null && !Target.MarkedForClose;
            SmartsFactor = System.Values.Ammo.Trajectory.SmartsFactor;
            if (Target != null && LockedTarget) OriginTargetPos = Target.PositionComp.WorldAABB.Center;
            HitList = hitList;

            DrawLine = System.Values.Graphics.Line.Trail;
            if (System.RangeVariance)
            {
                var min = System.Values.Ammo.Trajectory.RangeVariance.Start;
                var max = System.Values.Ammo.Trajectory.RangeVariance.End;
                MaxTrajectory = System.Values.Ammo.Trajectory.MaxTrajectory - MyUtils.GetRandomFloat(min, max);
            }
            else MaxTrajectory = System.Values.Ammo.Trajectory.MaxTrajectory;

            MaxTrajectorySqr = MaxTrajectory * MaxTrajectory;
            ShotLength = System.Values.Ammo.ProjectileLength;

            var smartsDelayDist = ShotLength * System.Values.Ammo.Trajectory.SmartsTrackingDelay;
            SmartsDelayDistSqr = smartsDelayDist * smartsDelayDist;

            StartSpeed = FiringGrid.Physics.LinearVelocity;

            if (System.SpeedVariance)
            {
                var min = System.Values.Ammo.Trajectory.SpeedVariance.Start;
                var max = System.Values.Ammo.Trajectory.SpeedVariance.End;
                DesiredSpeed = System.Values.Ammo.Trajectory.DesiredSpeed - MyUtils.GetRandomFloat(min, max);
            }
            else DesiredSpeed = System.Values.Ammo.Trajectory.DesiredSpeed;

            DesiredSpeedSqr = DesiredSpeed * DesiredSpeed;
            Vector3D.DistanceSquared(ref CameraStartPos, ref Origin, out DistanceFromCameraSqr);

            var probability = System.Values.Graphics.VisualProbability;
            EnableAv = !noAv && DistanceFromCameraSqr <= Session.Instance.SyncDistSqr && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));
            if (LockedTarget) FoundTarget = true;
            else if (DynamicGuidance) SeekTarget = true;
            MoveToAndActivate = FoundTarget && Guidance == AmmoTrajectory.GuidanceType.TravelTo;

            if (MoveToAndActivate)
            {
                var distancePos = PredictedTargetPos != Vector3D.Zero ? PredictedTargetPos : OriginTargetPos;
                Vector3D.DistanceSquared(ref Origin, ref distancePos, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = MaxTrajectorySqr;

            FiringSoundState = System.FiringSound;
            AmmoTravelSoundRangeSqr = System.AmmoTravelSoundDistSqr;

            if (EnableAv)
            {
                if (System.AmmoTravelSound)
                {
                    HasTravelSound = true;
                    TravelSound.Init(System.Values.Audio.Ammo.TravelSound, false);
                }
                else HasTravelSound = false;

                if (System.HitSound)
                    HitSound.Init(System.Values.Audio.Ammo.HitSound, false);

                if (FiringSoundState == WeaponSystem.FiringSoundState.PerShot)
                {
                    FireSound.Init(System.Values.Audio.HardPoint.FiringSound, false);
                    FireSoundStart();
                }
            }

            ModelId = System.ModelId;
            if (ModelId == -1) ModelState = EntityState.None;
            else
            {
                if (EnableAv)
                {
                    ModelState = EntityState.Exists;
                    ScreenCheckRadius = Entity.PositionComp.WorldVolume.Radius * 2;
                }
                else ModelState = EntityState.NoDraw;
            }

            var accelPerSec = System.Values.Ammo.Trajectory.AccelPerSec;
            ConstantSpeed = accelPerSec <= 0;
            AccelPerSec = accelPerSec > 0 ? accelPerSec : DesiredSpeed; 
            MaxVelocity = StartSpeed + (Direction * DesiredSpeed);
            MaxSpeedLength = MaxVelocity.Length() * StepConst;
            AccelLength = System.Values.Ammo.Trajectory.AccelPerSec * StepConst;
            AccelVelocity = (Direction * AccelLength);
            Velocity = ConstantSpeed ? MaxVelocity : StartSpeed + AccelVelocity;
            TravelMagnitude = Velocity * StepConst;
            if (!IsBeamWeapon)
            {
                var reSizeSteps = (int) (ShotLength / MaxSpeedLength);
                ReSizeSteps = ModelState == EntityState.None && reSizeSteps > 0 ? reSizeSteps : 1;
                Grow = ReSizeSteps > 1 || AccelLength > 0 && AccelLength < ShotLength;
                Shrink = Grow;
                State = ProjectileState.Alive;
            }
            else State = ProjectileState.OneAndDone;

            CheckLength = MaxSpeedLength * 2;
            if (System.AmmoParticle && EnableAv) ProjectileParticleStart();
        }

        internal void ProjectileParticleStart()
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

            var parentId = uint.MaxValue;
            MatrixD matrix;
            if (ModelState == EntityState.Exists)
            {
                matrix = MatrixD.CreateWorld(Position, AccelDir, Entity.PositionComp.WorldMatrix.Up);
                var offVec = Position + Vector3D.Rotate(System.Values.Graphics.Particles.AmmoOffset, matrix);
                matrix.Translation = offVec;
                EntityMatrix = matrix;
            }
            else
            {
                matrix = MatrixD.CreateWorld(Position, AccelDir, OriginUp);
                var offVec = Position + Vector3D.Rotate(System.Values.Graphics.Particles.AmmoOffset, matrix);
                matrix.Translation = offVec;
            }

            MyParticlesManager.TryCreateParticleEffect(System.Values.Graphics.Particles.AmmoParticle, ref matrix, ref Position, parentId, out Effect1); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (Effect1 == null) return;
            if (ParticleStopped)
            {
                if (ConstantSpeed) Effect1.Velocity = Velocity;
                ParticleStopped = false;
                return;
            }
            //Log.Line($"create particle: p:{Position} - v:{Velocity} - c:{ConstantSpeed} - dSpeed:{DesiredSpeed} - sSpeed:{StartSpeed.Length()} - age:{Age}");
            Effect1.DistanceMax = 5000;
            Effect1.UserColorMultiplier = System.Values.Graphics.Particles.AmmoColor;
            var reScale = (float)Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
            var scaler = reScale < 1 ? reScale : 1;

            Effect1.UserRadiusMultiplier = System.Values.Graphics.Particles.AmmoScale * scaler;
            Effect1.UserEmitterScale = 1 * scaler;
            if (ConstantSpeed) Effect1.Velocity = Velocity;
            ParticleLateStart = false;
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

        internal void ProjectileClose(ObjectsPool<Projectile> pool, MyConcurrentPool<List<HitEntity>> hitPool, bool noAv)
        {
            if (noAv && ModelId == -1)
            {
                HitList.Clear();
                hitPool.Return(HitList);
                pool.MarkForDeallocate(this);
                State = ProjectileState.Dead;
            }
            else State = ProjectileState.Ending;
        }

        internal void Stop(ObjectsPool<Projectile> pool, MyConcurrentPool<List<HitEntity>> hitPool)
        {
            if (EndStep++ >= EndSteps)
            {
                if (EnableAv)
                {
                    if (System.AmmoParticle) DisposeEffect();
                    HitEffects();
                    if (AmmoSound) TravelEmitter.StopSound(false, true);
                }

                HitList.Clear();
                hitPool.Return(HitList);
                pool.MarkForDeallocate(this);
                State = ProjectileState.Dead;
            }
        }

        internal bool CloseModel(EntityPool<MyEntity> entPool, List<DrawProjectile> drawList)
        {
            EntityMatrix = MatrixD.Identity;
            drawList.Add(new DrawProjectile(System, FiringCube, WeaponId, MuzzleId, Entity, EntityMatrix, null, new Trajectile(), MaxSpeedLength, ReSizeSteps, Shrink, true, OnScreen));
            entPool.MarkForDeallocate(Entity);
            ModelState = EntityState.None;
            return true;
        }

        internal void HitEffects()
        {
            if (Colliding)
            {
                if (System.HitParticle && !IsBeamWeapon) PlayHitParticle();
                if (System.HitSound)
                {
                    HitEmitter.SetPosition(Position);
                    HitEmitter.CanPlayLoopSounds = false;
                    HitEmitter.PlaySoundWithDistance(HitSound.SoundId, true, false, false, true, true, false, false);
                }
            }
            Colliding = false;
        }

        private void PlayHitParticle()
        {
            if (LastHitPos.HasValue)
            {
                var pos = LastHitPos.Value;
                var matrix = MatrixD.CreateTranslation(pos);
                var parentId = uint.MaxValue;
                MyParticlesManager.TryCreateParticleEffect(System.Values.Graphics.Particles.HitParticle, ref matrix, ref pos, parentId, out Effect1);
                if (Effect1 == null) return;
                Effect1.Loop = false;
                Effect1.DurationMax = 1f;
                Effect1.DistanceMax = 5000;
                Effect1.UserColorMultiplier = System.Values.Graphics.Particles.HitColor;
                var reScale = (float)Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
                var scaler = reScale < 1 ? reScale : 1;

                Effect1.UserRadiusMultiplier = System.Values.Graphics.Particles.HitScale * scaler;
                Effect1.UserEmitterScale = 1 * scaler;
                var hitVel = LastHitEntVel ?? Vector3.Zero;
                if (ModelState != EntityState.Exists) Effect1.Velocity = hitVel;
            }
        }

        private void DisposeEffect()
        {
            if (Effect1 != null)
            {
                Effect1.Stop(false);
                Effect1 = null;
            }
        }

        internal enum ProjectileState
        {
            Start,
            Alive,
            Ending,
            Dead,
            OneAndDone,
            Zombie,
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