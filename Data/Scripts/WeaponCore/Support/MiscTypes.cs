using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
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
        internal int DelayReleaseCnt;
        internal uint CheckTick;
        internal uint ExpiredTick;
        internal BlockTypes LastBlockType;
        internal Vector3D TargetPos;
        internal double HitShortDist;
        internal double OrigDistance;
        internal long TopEntityId;
        internal readonly List<MyCubeBlock> Top5 = new List<MyCubeBlock>();

        public enum Targets
        {
            Expired,
            Acquired,
        }

        internal Target(MyCubeBlock firingCube = null)
        {
            FiringCube = firingCube;
        }

        internal void TransferTo(Target target, uint resetTick, bool reset = true)
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
            if (reset) Reset(resetTick);
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
            Reset(0, false);
            IsFakeTarget = true;
            TargetPos = pos;
            State = Targets.Acquired;
        }

        internal void ResetCanDelay(Weapon weapon, bool expire = true, bool dontLog = false)
        {
            if (weapon.DelayCeaseFire && ++DelayReleaseCnt < weapon.System.TimeToCeaseFire) return;
            Log.Line($"delayedReset: {DelayReleaseCnt} < {weapon.System.TimeToCeaseFire} - {weapon.System.Values.HardPoint.DelayCeaseFire} - {weapon.System.WeaponName}");
            Reset(weapon.Comp.Session.Tick, expire, dontLog);
        }

        internal void Reset(uint expiredTick, bool expire = true, bool dontLog = false)
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
            DelayReleaseCnt = 0;
            if (expire)
            {
                State = Targets.Expired;
                ExpiredTick = expiredTick;
            }
            TargetLock = false;
        }
    }
}
