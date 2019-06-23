using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal class Projectile
    {
        //private static int _checkIntersectionCnt = 0;
        internal const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal const int EndSteps = 2;
        internal ProjectileState State;
        internal EntityState ModelState;
        internal MatrixD EntityMatrix;

        internal Vector3D Direction;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Vector3D Origin;
        internal Vector3D StartSpeed;
        internal Vector3D Velocity;
        internal Vector3D TravelMagnitude;
        internal Vector3D CameraStartPos;
        internal Vector3D LastEntityPos;
        internal Vector3D OriginTargetPos;
        internal Vector3D DistanceTraveled;
        internal Vector3D PredictedTargetPos;
        internal WeaponSystem WeaponSystem;
        internal WeaponDefinition WepDef;
        internal List<MyEntity> CheckList;
        internal MyCubeBlock FiringCube;
        internal MyCubeGrid FiringGrid;
        internal MyEntity HitEntity;
        internal float FinalSpeed;
        internal float FinalSpeedSqr;
        internal float SpeedLength;
        internal double CheckLength;
        internal float AmmoTravelSoundRangeSqr;
        internal float MaxTrajectory;
        internal double MaxTrajectorySqr;
        internal double DistanceTraveledSqr;
        internal double DistanceToTravelSqr;
        internal double ShotLength;
        internal double ScreenCheckRadius;
        internal double DistanceFromCameraSqr;
        internal LineD CurrentLine;

        internal uint Age;
        internal int ReSizeSteps;
        internal int GrowStep = 1;
        internal int EndStep;
        internal int ModelId;
        internal bool Grow;
        internal bool Shrink;
        internal bool Draw;
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
        internal bool VariableRange;
        internal bool DynamicGuidance;
        internal WeaponSystem.FiringSoundState FiringSoundState;
        internal AmmoTrajectory.GuidanceType Guidance;
        internal MyParticleEffect Effect1;
        internal readonly MyEntity3DSoundEmitter Sound1 = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyTimedItemCache VoxelRayCache = new MyTimedItemCache(4000);
        internal List<MyLineSegmentOverlapResult<MyEntity>> EntityRaycastResult = null;
        internal MyEntity Entity;
        internal MyEntity Target;
        internal MySoundPair FireSound = new MySoundPair();
        internal MySoundPair TravelSound = new MySoundPair();
        internal MySoundPair HitSound = new MySoundPair();


        internal void Start(List<MyEntity> checkList, bool noAv)
        {
            ModelState = EntityState.Stale;
            CameraStartPos = MyAPIGateway.Session.Camera.Position;
            Position = Origin;
            LastEntityPos = Origin;
            HitEntity = null;
            FirstOffScreen = true;
            AmmoSound = false;
            PositionChecked = false;
            EndStep = 0;
            GrowStep = 1;
            DistanceTraveledSqr = 0;

            WepDef = WeaponSystem.WeaponType;
            FiringGrid = FiringCube.CubeGrid;
            Guidance = WepDef.AmmoDef.Trajectory.Guidance;
            DynamicGuidance = Guidance != AmmoTrajectory.GuidanceType.None;
            LockedTarget = Target != null && !Target.MarkedForClose;

            if (Target != null && LockedTarget) OriginTargetPos = Target.PositionComp.WorldAABB.Center;
            CheckList = checkList;

            DrawLine = WepDef.GraphicDef.Line.Trail;
            MaxTrajectory = WepDef.AmmoDef.Trajectory.MaxTrajectory;
            MaxTrajectorySqr = MaxTrajectory * MaxTrajectory;
            ShotLength = WepDef.AmmoDef.ProjectileLength;
            SpeedLength = WepDef.AmmoDef.Trajectory.DesiredSpeed * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;// * MyUtils.GetRandomFloat(1f, 1.5f);

            StartSpeed = FiringGrid.Physics.LinearVelocity;
            FinalSpeed = WepDef.AmmoDef.Trajectory.DesiredSpeed;
            FinalSpeedSqr = FinalSpeed * FinalSpeed;

            Draw = WepDef.GraphicDef.VisualProbability >= MyUtils.GetRandomDouble(0.0f, 1f);

            if (LockedTarget) FoundTarget = true;
            else if (DynamicGuidance) SeekTarget = true;
            MoveToAndActivate = FoundTarget && Guidance == AmmoTrajectory.GuidanceType.TravelTo;

            if (MoveToAndActivate)
            {
                var distancePos = PredictedTargetPos != Vector3D.Zero ? PredictedTargetPos : OriginTargetPos;
                Vector3D.DistanceSquared(ref Origin, ref distancePos, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = MaxTrajectorySqr;

            FiringSoundState = WeaponSystem.FiringSound;
            AmmoTravelSoundRangeSqr = (WepDef.AudioDef.Ammo.TravelRange * WepDef.AudioDef.Ammo.TravelRange);
            Vector3D.DistanceSquared(ref CameraStartPos, ref Origin, out DistanceFromCameraSqr);
            //_desiredSpeed = wDef.DesiredSpeed * ((double)ammoDefinition.SpeedVar > 0.0 ? MyUtils.GetRandomFloat(1f - ammoDefinition.SpeedVar, 1f + ammoDefinition.SpeedVar) : 1f);
            //_checkIntersectionIndex = _checkIntersectionCnt % 5;
            //_checkIntersectionCnt += 3;

            ConstantSpeed = WepDef.AmmoDef.Trajectory.AccelPerSec <= 0;
            if (ConstantSpeed) Velocity = StartSpeed + (Direction * FinalSpeed);
            else Velocity = StartSpeed;
            TravelMagnitude = Velocity * StepConst;

            if (!noAv)
            {
                if (WeaponSystem.AmmoParticle) ProjectileParticleStart();

                if (WeaponSystem.AmmoTravelSound)
                {
                    HasTravelSound = true;
                    TravelSound.Init(WepDef.AudioDef.Ammo.TravelSound, false);
                }
                else HasTravelSound = false;

                if (WeaponSystem.AmmoHitSound)
                    HitSound.Init(WepDef.AudioDef.Ammo.HitSound, false);

                if (FiringSoundState == WeaponSystem.FiringSoundState.Simple)
                {
                    FireSound.Init(WepDef.AudioDef.Turret.FiringSoundStart, false);
                    FireSoundStart();
                }

                ModelId = WeaponSystem.ModelId;
                if (ModelId != -1)
                {
                    ModelState = EntityState.Exists;
                    ScreenCheckRadius = Entity.PositionComp.WorldVolume.Radius * 2;
                }
                else ModelState = EntityState.None;
            }

            if (SpeedLength > 0 && MaxTrajectory > 0)
            {
                var reSizeSteps = (int) (ShotLength / SpeedLength);
                ReSizeSteps = ModelState == EntityState.None && reSizeSteps > 0 ? reSizeSteps : 1;
                Grow = ReSizeSteps > 1;
                Shrink = Grow;
                State = ProjectileState.Alive;
            }
            else State = ProjectileState.OneAndDone;

            CheckLength = ShotLength > SpeedLength ? (ShotLength - SpeedLength) * 2 : SpeedLength * 2;
        }

        private void ProjectileParticleStart()
        {
            var to = Origin;
            to += -TravelMagnitude; // we are in a thread, draw is delayed a frame.
            var matrix = MatrixD.CreateTranslation(to);
            MyParticlesManager.TryCreateParticleEffect(WepDef.GraphicDef.Particles.AmmoParticle, ref matrix, ref to, uint.MaxValue, out Effect1); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (Effect1 == null) return;
            Effect1.DistanceMax = 5000;
            Effect1.UserColorMultiplier = WepDef.GraphicDef.Particles.AmmoColor;
            var reScale = (float)Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
            var scaler = reScale < 1 ? reScale : 1;

            Effect1.UserRadiusMultiplier = WepDef.GraphicDef.Particles.AmmoScale * scaler;
            Effect1.UserEmitterScale = 1 * scaler;
            Effect1.Velocity = Velocity;
        }

        internal void FireSoundStart()
        {
            Sound1.CustomMaxDistance = WepDef.AudioDef.Turret.FiringRange;
            Sound1.CustomVolume = WepDef.AudioDef.Turret.FiringVolume;
            Sound1.SetPosition(Origin);
            Sound1.PlaySoundWithDistance(FireSound.SoundId, false, false, false, true, false, false, false);
        }

        internal void AmmoSoundStart()
        {
            Sound1.CustomMaxDistance = WepDef.AudioDef.Ammo.TravelRange;
            Sound1.CustomVolume = WepDef.AudioDef.Ammo.TravelVolume;
            Sound1.SetPosition(Position);
            Sound1.PlaySoundWithDistance(TravelSound.SoundId, false, false, false, true, false, false, false);
            AmmoSound = true;
        }

        internal void ProjectileClose(ObjectsPool<Projectile> pool, MyConcurrentPool<List<MyEntity>> checkPool, bool noAv)
        {
            if (noAv)
            {
                checkPool.Return(CheckList);
                pool.MarkForDeallocate(this);
                State = ProjectileState.Dead;
            }
            else State = ProjectileState.Ending;
        }

        internal void Stop(ObjectsPool<Projectile> pool, MyConcurrentPool<List<MyEntity>> checkPool, EntityPool<MyEntity> entPool, List<Projectiles.DrawProjectile> drawList)
        {
            if (ModelState == EntityState.Exists)
            {
                drawList.Add(new Projectiles.DrawProjectile(WeaponSystem, Entity, EntityMatrix, 0, new LineD(), Velocity, Vector3D.Zero, null, true, 0, 0, false, true));
                entPool.MarkForDeallocate(Entity);
                ModelState = EntityState.Stale;
            }

            if (EndStep++ >= EndSteps)
            {
                if (WeaponSystem.AmmoParticle) DisposeEffect();

                if (WeaponSystem.AmmoHitSound)
                {
                    Sound1.CustomMaxDistance = WepDef.AudioDef.Ammo.HitRange;
                    Sound1.CustomVolume = WepDef.AudioDef.Ammo.HitVolume;
                    Sound1.SetPosition(Position);
                    Sound1.CanPlayLoopSounds = false;
                    Sound1.PlaySoundWithDistance(HitSound.SoundId, true, false, false, true, true, false, false);
                }
                else if (AmmoSound) Sound1.StopSound(false, true);

                checkPool.Return(CheckList);
                pool.MarkForDeallocate(this);
                State = ProjectileState.Dead;
            }
        }

        private void DisposeEffect()
        {
            if (Effect1 != null)
            {
                MyParticlesManager.RemoveParticleEffect(Effect1);
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
        }

        internal enum EntityState
        {
            Exists,
            Stale,
            None
        }
    }
}