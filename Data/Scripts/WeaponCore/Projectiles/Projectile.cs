using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore.Projectiles
{
    internal class Projectile
    {
        //private static int _checkIntersectionCnt = 0;
        internal const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal const int EndSteps = 3;
        internal ProjectileState State;
        internal Vector3D Direction;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Weapon Weapon;
        internal WeaponDefinition WepDef;
        internal List<MyEntity> CheckList;
        internal MyCubeGrid MyGrid;
        internal MyEntity HitEntity;
        internal Vector3D Origin;
        internal Vector3D StartSpeed;
        internal Vector3D AddSpeed;
        internal Vector3D CurrentSpeed;
        internal Vector3D FinalSpeed;
        internal Vector3D CurrentMagnitude;
        internal Vector3D CameraStartPos;
        internal LineD CurrentLine;
        internal double ShotLength;
        internal float SpeedLength;
        internal float MaxTrajectory;
        internal float AmmoTravelSoundRangeSqr;
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
        internal MyParticleEffect Effect1;
        internal readonly MyEntity3DSoundEmitter Sound1 = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyTimedItemCache VoxelRayCache = new MyTimedItemCache(4000);
        internal List<MyLineSegmentOverlapResult<MyEntity>> EntityRaycastResult = null;

        internal void Start(List<MyEntity> checkList, bool noAv)
        {
            WepDef = Weapon.WeaponType;
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
            MyGrid = Weapon.Comp.MyGrid;
            HitEntity = null;
            CheckList = checkList;
            StartSpeed = Weapon.Comp.Turret.CubeGrid.Physics.LinearVelocity;
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
            if (Draw && WepDef.GraphicDef.ParticleTrail && !noAv) ProjectileParticleStart();
            if (!noAv && WepDef.AudioDef.FiringSound != null) FireSoundStart();
            State = ProjectileState.Alive;
        }

        private void ProjectileParticleStart()
        {
            var to = Origin;
            to += -CurrentMagnitude; // we are in a thread, draw is delayed a frame.
            var matrix = MatrixD.CreateTranslation(to);
            MyParticlesManager.TryCreateParticleEffect(WepDef.GraphicDef.CustomEffect, ref matrix, ref to, uint.MaxValue, out Effect1); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (Effect1 == null) return;
            //Effect1.UserDraw = true;
            Effect1.DistanceMax = 5000;
            Effect1.UserColorMultiplier = WepDef.GraphicDef.ParticleColor;
            var reScale = (float) Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
            var scaler = reScale < 1 ? reScale : 1;

            Effect1.UserRadiusMultiplier = WepDef.GraphicDef.ParticleRadiusMultiplier * scaler;
            Effect1.UserEmitterScale = 1 * scaler;
            Effect1.Velocity = CurrentSpeed;
            // Log.Line($"Radius:{Effect1.UserRadiusMultiplier} - UserEmitterScale:{Effect1.UserEmitterScale} - UserScale:{Effect1.UserScale} - Scaler:{scaler} - scale:{reScale}");
        }

        internal void FireSoundStart()
        {
            Sound1.CustomMaxDistance = WepDef.AudioDef.FiringSoundRange;
            Sound1.CustomVolume = WepDef.AudioDef.FiringSoundVolume;
            Sound1.SetPosition(Origin);
            Sound1.PlaySoundWithDistance(Weapon.FiringSoundPair.SoundId, false, false, false, true, false, false, false);
        }

        internal void AmmoSoundStart()
        {
            Sound1.CustomMaxDistance = WepDef.AudioDef.AmmoTravelSoundRange;
            Sound1.CustomVolume = WepDef.AudioDef.AmmoTravelSoundVolume;
            Sound1.SetPosition(Position);
            Sound1.PlaySoundWithDistance(Weapon.AmmoTravelSoundPair.SoundId, false, false, false, true, false, false, false);
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

        internal void Stop(ObjectsPool<Projectile> pool, MyConcurrentPool<List<MyEntity>> checkPool)
        {
            if (EndStep++ >= EndSteps)
            {
                if (WepDef.GraphicDef.ParticleTrail) DisposeEffect();

                Sound1.StopSound(true, true);
                if (WepDef.AudioDef.AmmoHitSound != null)
                {
                    Sound1.CustomMaxDistance = WepDef.AudioDef.AmmoHitSoundRange;
                    Sound1.CustomVolume = WepDef.AudioDef.AmmoHitSoundVolume;
                    Sound1.SetPosition(Position);
                    Sound1.CanPlayLoopSounds = false;
                    Sound1.PlaySoundWithDistance(Weapon.AmmoHitSoundPair.SoundId, false, false, false, true, true, false, false);
                }
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
    }
}