using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Projectiles;
using static WeaponCore.Support.TargetingDefinition;

namespace WeaponCore.Support
{
    internal class Target
    {
        internal Targets State = Targets.Expired;
        internal bool IsTracking;
        internal bool IsAligned;
        internal bool IsProjectile;
        internal bool IsFakeTarget;
        internal bool TargetLock;
        internal MyCubeBlock FiringCube;
        internal MyEntity Entity;
        internal Projectile Projectile;
        internal int[] TargetDeck = new int[0];
        internal int[] BlockDeck = new int[0];
        internal int TargetPrevDeckLen;
        internal int BlockPrevDeckLen;
        internal uint CheckTick;
        internal BlockTypes LastBlockType;
        internal Vector3D TargetPos;
        internal double HitShortDist;
        internal double OrigDistance;
        internal long TopEntityId;
        internal readonly List<MyCubeBlock> Top5 = new List<MyCubeBlock>();

        public enum Targets
        {
            Expired,
            StillSeeking,
            Acquired,
        }

        internal Target(MyCubeBlock firingCube = null)
        {
            FiringCube = firingCube;
        }

        internal void TransferTo(Target target, bool reset = true)
        {
            target.Entity = Entity;
            target.Projectile = Projectile;
            target.IsProjectile = target.Projectile != null;
            target.IsFakeTarget = IsFakeTarget;
            target.TargetPos = TargetPos;
            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;
            target.State = State;
            if (reset) Reset();
        }

        internal void Set(MyEntity ent, Vector3D pos, double shortDist, double origDist, long topEntId, Projectile projectile = null, bool isFakeTarget = false)
        {
            Entity = ent;
            Projectile = projectile;
            IsProjectile = projectile != null;
            IsFakeTarget = isFakeTarget;
            TargetPos = pos;
            HitShortDist = shortDist;
            OrigDistance = origDist;
            TopEntityId = topEntId;
            State = Targets.Acquired;
        }

        internal void SetFake(Vector3D pos)
        {
            Reset(false);
            IsFakeTarget = true;
            TargetPos = pos;
            State = Targets.Acquired;
        }

        internal void Reset(bool expire = true, bool resetTimer = true)
        {
            Entity = null;
            IsProjectile = false;
            IsFakeTarget = false;
            IsTracking = false;
            IsAligned = false;
            Projectile = null;
            TargetPos = Vector3D.Zero;
            HitShortDist = 0;
            OrigDistance = 0;
            TopEntityId = 0;
            if (expire)
            {
                if (resetTimer) CheckTick = 0;
                State = Targets.Expired;
            }
            TargetLock = false;
        }
    }
}
