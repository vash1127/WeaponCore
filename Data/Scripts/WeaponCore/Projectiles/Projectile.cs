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
        internal LineD CurrentLine;
        internal double ShotLength;
        internal float SpeedLength;
        internal float MaxTrajectory;
        internal float AmmoTravelSoundRangeSqr;
        internal bool PositionChecked;
        internal double LineReSizeLen;
        internal int ReSizeSteps;
        internal int GrowStep = 1;
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
            DrawLine = WepDef.LineTrail;
            MaxTrajectory = WepDef.MaxTrajectory;
            ShotLength = WepDef.LineLength;
            SpeedLength = WepDef.DesiredSpeed;// * MyUtils.GetRandomFloat(1f, 1.5f);
            LineReSizeLen = SpeedLength / 60;
            AmmoTravelSoundRangeSqr = (WepDef.AmmoTravelSoundRange * WepDef.AmmoTravelSoundRange);
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
            PositionChecked = false;
            AmmoSound = false;
            Draw = WepDef.VisualProbability >= (double)MyUtils.GetRandomFloat(0.0f, 1f);
            //_desiredSpeed = wDef.DesiredSpeed * ((double)ammoDefinition.SpeedVar > 0.0 ? MyUtils.GetRandomFloat(1f - ammoDefinition.SpeedVar, 1f + ammoDefinition.SpeedVar) : 1f);
            //_checkIntersectionIndex = _checkIntersectionCnt % 5;
            //_checkIntersectionCnt += 3;
            if (Draw && WepDef.ParticleTrail && !noAv) ProjectileParticleStart();
            if (!noAv && WepDef.FiringSound != null) FireSoundStart();
            State = ProjectileState.Alive;
        }

        private void ProjectileParticleStart()
        {
            var to = Origin;
            var matrix = MatrixD.CreateTranslation(to);
            MyParticlesManager.TryCreateParticleEffect(WepDef.CustomEffect, ref matrix, ref to, uint.MaxValue, out Effect1); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (Effect1 == null) return;
            //Effect1.UserDraw = true;
            //Effect1.DistanceMax = 5000;
            Effect1.UserColorMultiplier = WepDef.ParticleColor;
            Effect1.UserRadiusMultiplier = WepDef.ParticleRadiusMultiplier;
            Effect1.UserEmitterScale = 1f;
            Effect1.Velocity = CurrentSpeed;
        }

        internal void FireSoundStart()
        {
            Sound1.CustomMaxDistance = WepDef.FiringSoundRange;
            Sound1.CustomVolume = WepDef.FiringSoundVolume;
            Sound1.SetPosition(Origin);
            Sound1.PlaySoundWithDistance(WepDef.FiringSound.SoundId, false, false, false, true, false, false, false);
        }

        internal void AmmoSoundStart()
        {
            Sound1.CustomMaxDistance = WepDef.AmmoTravelSoundRange;
            Sound1.CustomVolume = WepDef.AmmoTravelSoundVolume;
            Sound1.SetPosition(Position);
            Sound1.PlaySoundWithDistance(WepDef.AmmoTravelSound.SoundId, false, false, false, true, false, false, false);
            AmmoSound = true;
        }

        internal void ProjectileClose(ObjectsPool<Projectile> pool, MyConcurrentPool<List<MyEntity>> checkPool, bool noAv)
        {
            State = ProjectileState.Dead;
            if (!noAv)
            {
                if (WepDef.ParticleTrail) DisposeEffect();

                if (AmmoSound) Sound1.StopSound(true, true);
                if (WepDef.AmmoHitSound != null)
                {
                    Sound1.CustomMaxDistance = WepDef.AmmoHitSoundRange;
                    Sound1.CustomVolume = WepDef.AmmoHitSoundVolume;
                    Sound1.SetPosition(Position);
                    Sound1.PlaySoundWithDistance(Weapon.WeaponType.AmmoHitSound.SoundId, false, false, false, true, true, false, false);
                }
            }

            checkPool.Return(CheckList);
            pool.MarkForDeallocate(this);
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