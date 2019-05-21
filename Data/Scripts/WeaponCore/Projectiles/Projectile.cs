using System.Collections.Generic;
using Sandbox.Game.Entities;
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

        public enum ProjectileState
        {
            Alive,
            Dead,
        }
    }
}