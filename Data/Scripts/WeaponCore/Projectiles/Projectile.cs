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
        internal bool PositionChecked;
        internal double LineReSizeLen;
        internal int ReSizeSteps;
        internal int GrowStep = 1;
        internal bool Grow;
        internal bool Shrink;
        internal MyParticleEffect Effect1 = new MyParticleEffect();
        internal readonly MyTimedItemCache VoxelRayCache = new MyTimedItemCache(4000);
        internal List<MyLineSegmentOverlapResult<MyEntity>> EntityRaycastResult = null;

        internal void Start(Projectiles.Shot fired, Weapon weapon, List<MyEntity> checkList)
        {
            Weapon = weapon;
            var wDef = weapon.WeaponType;
            MaxTrajectory = wDef.MaxTrajectory;
            ShotLength = wDef.ShotLength;
            SpeedLength = wDef.DesiredSpeed;// * MyUtils.GetRandomFloat(1f, 1.5f);
            LineReSizeLen = SpeedLength / 60;
            GrowStep = 1;
            var reSizeSteps = (int)(ShotLength / LineReSizeLen);
            ReSizeSteps = reSizeSteps > 0 ? reSizeSteps : 1;
            Grow = ReSizeSteps > 1;
            Shrink = Grow;
            Origin = fired.Position;
            Direction = fired.Direction;
            Position = Origin;
            MyGrid = weapon.Logic.MyGrid;
            CheckList = checkList;
            StartSpeed = Weapon.Logic.Turret.CubeGrid.Physics.LinearVelocity;
            AddSpeed = Direction * SpeedLength;
            FinalSpeed = StartSpeed + AddSpeed;
            CurrentSpeed = FinalSpeed;
            State = ProjectileState.Alive;
            PositionChecked = false;
            //_desiredSpeed = wDef.DesiredSpeed * ((double)ammoDefinition.SpeedVar > 0.0 ? MyUtils.GetRandomFloat(1f - ammoDefinition.SpeedVar, 1f + ammoDefinition.SpeedVar) : 1f);
            //_checkIntersectionIndex = _checkIntersectionCnt % 5;
            //_checkIntersectionCnt += 3;
            if (wDef.ParticleTrail) ProjectileParticleStart();
        }


        private void ProjectileParticleStart()
        {
            var wDef = Weapon.WeaponType;
            var color = wDef.ParticleColor;
            var mainParticle = wDef.CustomEffect; // ShipWelderArc
            var to = Position;
            var matrix = MatrixD.CreateTranslation(to);
            MyParticlesManager.TryCreateParticleEffect(mainParticle, ref matrix, ref to, uint.MaxValue, out Effect1); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (Effect1 == null) return;
            //Log.Line($"{Effect1.Name}");
            Effect1.UserDraw = true;
            Effect1.DistanceMax = 5000;
            Effect1.UserColorMultiplier = color;
            Effect1.UserRadiusMultiplier = 1f;
            Effect1.UserEmitterScale = 1f;
            Effect1.Velocity = CurrentSpeed;
        }

        internal void ProjectileClose(ObjectsPool<Projectile> pool, MyConcurrentPool<List<MyEntity>> checkPool)
        {
            State = ProjectileState.Dead;
            Effect1.Stop(false);
            checkPool.Return(CheckList);
            pool.MarkForDeallocate(this);
        }

        internal enum ProjectileState
        {
            Alive,
            Dead,
        }
    }
}