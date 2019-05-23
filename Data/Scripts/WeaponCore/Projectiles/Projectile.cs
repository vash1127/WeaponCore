using System.Collections.Generic;
using Sandbox.Game.Entities;
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
        internal ProjectileState State;
        internal Vector3D Direction;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Weapon Weapon;
        internal List<MyEntity> CheckList;
        internal MyCubeGrid MyGrid;
        internal MyEntity HitEntity;
        internal Vector3D Origin;
        internal Vector3D StartSpeed;
        internal Vector3D AddSpeed;
        internal Vector3D CurrentSpeed;
        internal Vector3D FinalSpeed;
        internal Vector3D CurrentMagnitude;
        internal LineD CurrentLine;
        internal double ShotLength;
        internal float SpeedLength;
        internal float MaxTrajectory;
        internal float AmmoAudioRangeBuffSqr;
        internal bool PositionChecked;
        internal double LineReSizeLen;
        internal int ReSizeSteps;
        internal int GrowStep = 1;
        internal bool Grow;
        internal bool Shrink;
        internal bool Draw;
        internal bool DrawLine;
        internal bool StartSound;
        internal MyParticleEffect Effect1;
        internal readonly MyEntity3DSoundEmitter Sound1 = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyTimedItemCache VoxelRayCache = new MyTimedItemCache(4000);
        internal List<MyLineSegmentOverlapResult<MyEntity>> EntityRaycastResult = null;

        internal void Start(List<MyEntity> checkList)
        {
            var wDef = Weapon.WeaponType;
            DrawLine = wDef.LineTrail;
            MaxTrajectory = wDef.MaxTrajectory;
            ShotLength = wDef.LineLength;
            SpeedLength = wDef.DesiredSpeed;// * MyUtils.GetRandomFloat(1f, 1.5f);
            LineReSizeLen = SpeedLength / 60;
            AmmoAudioRangeBuffSqr = (wDef.AmmoAudioRange * wDef.AmmoAudioRange);
            GrowStep = 1;
            var reSizeSteps = (int)(ShotLength / LineReSizeLen);
            ReSizeSteps = reSizeSteps > 0 ? reSizeSteps : 1;
            Grow = ReSizeSteps > 1;
            Shrink = Grow;
            Position = Origin;
            MyGrid = Weapon.Logic.MyGrid;
            HitEntity = null;
            CheckList = checkList;
            StartSpeed = Weapon.Logic.Turret.CubeGrid.Physics.LinearVelocity;
            AddSpeed = Direction * SpeedLength;
            FinalSpeed = StartSpeed + AddSpeed;
            CurrentSpeed = FinalSpeed;
            PositionChecked = false;
            StartSound = false;
            Draw = wDef.VisualProbability >= (double)MyUtils.GetRandomFloat(0.0f, 1f);
            //_desiredSpeed = wDef.DesiredSpeed * ((double)ammoDefinition.SpeedVar > 0.0 ? MyUtils.GetRandomFloat(1f - ammoDefinition.SpeedVar, 1f + ammoDefinition.SpeedVar) : 1f);
            //_checkIntersectionIndex = _checkIntersectionCnt % 5;
            //_checkIntersectionCnt += 3;
            if (Draw && wDef.ParticleTrail) ProjectileParticleStart();
            Sound1.CustomMaxDistance = 150f;
            Sound1.CustomVolume = 10f;
            Sound1.SetPosition(Origin);
            Sound1.PlaySoundWithDistance(wDef.FiringSound.SoundId, false, false, false, true, false, false, false);
            State = ProjectileState.Alive;
        }

        private void ProjectileParticleStart()
        {
            var wDef = Weapon.WeaponType;
            var to = Origin;
            var matrix = MatrixD.CreateTranslation(to);
            MyParticlesManager.TryCreateParticleEffect(wDef.CustomEffect, ref matrix, ref to, uint.MaxValue, out Effect1); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (Effect1 == null) return;
            //Effect1.UserDraw = true;
            //Effect1.DistanceMax = 5000;
            Effect1.UserColorMultiplier = wDef.ParticleColor;
            Effect1.UserRadiusMultiplier = wDef.ParticleRadiusMultiplier;
            Effect1.UserEmitterScale = 1f;
            Effect1.Velocity = CurrentSpeed;
        }

        internal void SoundStart()
        {
            var wDef = Weapon.WeaponType;
            Sound1.CustomMaxDistance = wDef.AmmoAudioRange;
            Sound1.CustomVolume = 1f;
            Sound1.SetPosition(Position);
            Sound1.PlaySoundWithDistance(wDef.AmmoTravelSound.SoundId, false, false, false, true, false, false, false);
            StartSound = true;
        }

        internal void ProjectileClose(ObjectsPool<Projectile> pool, MyConcurrentPool<List<MyEntity>> checkPool)
        {
            State = ProjectileState.Dead;

            DisposeEffect();
            checkPool.Return(CheckList);
            pool.MarkForDeallocate(this);
            if (!StartSound) SoundStart();
            Sound1.StopSound(true, true);
            Sound1.CustomMaxDistance = 150f;
            Sound1.CustomVolume = 3;
            Sound1.SetPosition(Position);
            Sound1.PlaySoundWithDistance(Weapon.WeaponType.AmmoHitSound.SoundId, false, false, false, true, true, false, false);
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
            Dead,
        }
    }
}