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
        internal const int EndSteps = 3;
        internal ProjectileState State;
        internal EntityState ModelState;
        internal Vector3D Direction;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal WeaponSystem WeaponSystem;
        internal WeaponDefinition WepDef;
        internal List<MyEntity> CheckList;
        internal MyCubeBlock FiringCube;
        internal MyCubeGrid FiringGrid;
        internal MyEntity HitEntity;
        internal Vector3D Origin;
        internal Vector3D StartSpeed;
        internal Vector3D AddSpeed;
        internal Vector3D CurrentSpeed;
        internal Vector3D FinalSpeed;
        internal Vector3D CurrentMagnitude;
        internal Vector3D CameraStartPos;
        internal Vector3D LastEntityPos;
        internal LineD CurrentLine;
        internal double ShotLength;
        internal float SpeedLength;
        internal float MaxTrajectory;
        internal float AmmoTravelSoundRangeSqr;
        internal double ScreenCheckRadius;
        internal double DistanceFromCameraSqr;
        internal bool PositionChecked;
        internal double LineReSizeLen;
        internal int ReSizeSteps;
        internal int GrowStep = 1;
        internal int EndStep;
        internal bool Grow;
        internal bool Shrink;
        internal bool Draw;
        internal bool DrawLine;
        internal bool AmmoSound;
        internal bool FirstOffScreen;
        internal MyParticleEffect Effect1;
        internal readonly MyEntity3DSoundEmitter Sound1 = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyTimedItemCache VoxelRayCache = new MyTimedItemCache(4000);
        internal List<MyLineSegmentOverlapResult<MyEntity>> EntityRaycastResult = null;
        internal MyEntity Entity;
        internal MatrixD EntityMatrix;
        internal int ModelId;
        internal MySoundPair FireSound = new MySoundPair();
        internal MySoundPair TravelSound = new MySoundPair();
        internal MySoundPair ReloadSound = new MySoundPair();
        internal MySoundPair HitSound = new MySoundPair();
        internal bool HasTravelSound;

        internal void Start(List<MyEntity> checkList, bool noAv)
        {
            FirstOffScreen = true;
            ModelState = EntityState.Stale;
            WepDef = WeaponSystem.WeaponType;
            FiringGrid = FiringCube.CubeGrid;
            DrawLine = WepDef.GraphicDef.ProjectileTrail;
            MaxTrajectory = WepDef.AmmoDef.MaxTrajectory;
            ShotLength = WepDef.AmmoDef.ProjectileLength;
            SpeedLength = WepDef.AmmoDef.DesiredSpeed;// * MyUtils.GetRandomFloat(1f, 1.5f);
            LineReSizeLen = SpeedLength / 60;
            AmmoTravelSoundRangeSqr = (WepDef.AudioDef.AmmoTravelSoundRange * WepDef.AudioDef.AmmoTravelSoundRange);
            GrowStep = 1;
            var reSizeSteps = (int)(ShotLength / LineReSizeLen);
            ReSizeSteps = reSizeSteps > 0 ? reSizeSteps : 1;
            Grow = ReSizeSteps > 1;
            Shrink = Grow;
            Position = Origin;
            LastEntityPos = Origin;
            HitEntity = null;
            CheckList = checkList;
            StartSpeed = FiringGrid.Physics.LinearVelocity;
            AddSpeed = Direction * SpeedLength;
            FinalSpeed = StartSpeed + AddSpeed;
            CurrentSpeed = FinalSpeed;
            CurrentMagnitude = CurrentSpeed * StepConst;
            PositionChecked = false;
            AmmoSound = false;
            Draw = WepDef.GraphicDef.VisualProbability >= (double)MyUtils.GetRandomFloat(0.0f, 1f);
            EndStep = 0;
            CameraStartPos = MyAPIGateway.Session.Camera.Position;
            Vector3D.DistanceSquared(ref CameraStartPos, ref Origin, out DistanceFromCameraSqr);
            //_desiredSpeed = wDef.DesiredSpeed * ((double)ammoDefinition.SpeedVar > 0.0 ? MyUtils.GetRandomFloat(1f - ammoDefinition.SpeedVar, 1f + ammoDefinition.SpeedVar) : 1f);
            //_checkIntersectionIndex = _checkIntersectionCnt % 5;
            //_checkIntersectionCnt += 3;

            if (!noAv)
            {
                if (Draw && WepDef.GraphicDef.ParticleTrail ) ProjectileParticleStart();

                if (WepDef.AudioDef.AmmoTravelSound != string.Empty)
                {
                    HasTravelSound = true;
                    TravelSound.Init(WepDef.AudioDef.AmmoTravelSound, false);
                }
                else HasTravelSound = false;

                if (WepDef.AudioDef.ReloadSound != string.Empty)
                    ReloadSound.Init(WepDef.AudioDef.ReloadSound, false);

                if (WepDef.AudioDef.AmmoHitSound != string.Empty)
                    HitSound.Init(WepDef.AudioDef.AmmoHitSound, false);

                if (WepDef.AudioDef.FiringSound != string.Empty)
                {
                    FireSound.Init(WepDef.AudioDef.FiringSound, false);
                    FireSoundStart();
                }
                ModelId = WeaponSystem.ModelId;
                if (ModelId != -1)
                {
                    ModelState = EntityState.Exists;
                    ScreenCheckRadius = Entity.PositionComp.WorldVolume.Radius * 2;
                }
                ModelState = ModelId != -1 ? EntityState.Exists : EntityState.None;
            }
            State = ProjectileState.Alive;
        }

        private void ProjectileParticleStart()
        {
            var to = Origin;
            to += -CurrentMagnitude; // we are in a thread, draw is delayed a frame.
            var matrix = MatrixD.CreateTranslation(to);
            MyParticlesManager.TryCreateParticleEffect(WepDef.GraphicDef.CustomEffect, ref matrix, ref to, uint.MaxValue, out Effect1); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (Effect1 == null) return;
            Effect1.DistanceMax = 5000;
            Effect1.UserColorMultiplier = WepDef.GraphicDef.ParticleColor;
            var reScale = (float) Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
            var scaler = reScale < 1 ? reScale : 1;

            Effect1.UserRadiusMultiplier = WepDef.GraphicDef.ParticleRadiusMultiplier * scaler;
            Effect1.UserEmitterScale = 1 * scaler;
            Effect1.Velocity = CurrentSpeed;
        }

        internal void FireSoundStart()
        {
            Sound1.CustomMaxDistance = WepDef.AudioDef.FiringSoundRange;
            Sound1.CustomVolume = WepDef.AudioDef.FiringSoundVolume;
            Sound1.SetPosition(Origin);
            Sound1.PlaySoundWithDistance(FireSound.SoundId, false, false, false, true, false, false, false);
        }

        internal void AmmoSoundStart()
        {
            Sound1.CustomMaxDistance = WepDef.AudioDef.AmmoTravelSoundRange;
            Sound1.CustomVolume = WepDef.AudioDef.AmmoTravelSoundVolume;
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
                drawList.Add(new Projectiles.DrawProjectile(WeaponSystem, Entity, EntityMatrix, 0, new LineD(), CurrentSpeed, Vector3D.Zero, null, true, 0, 0, false, true));
                entPool.MarkForDeallocate(Entity);
                ModelState = EntityState.Stale;
            }

            if (EndStep++ >= EndSteps)
            {
                if (WepDef.GraphicDef.ParticleTrail) DisposeEffect();

                if (WepDef.AudioDef.AmmoHitSound != string.Empty)
                {
                    Sound1.CustomMaxDistance = WepDef.AudioDef.AmmoHitSoundRange;
                    Sound1.CustomVolume = WepDef.AudioDef.AmmoHitSoundVolume;
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
        }

        internal enum EntityState
        {
            Exists,
            Stale,
            None
        }
    }
}