using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Projectiles;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;

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
        internal bool Client;
        internal MyCubeBlock FiringCube;
        internal MyEntity Entity;
        internal Projectile Projectile;
        internal int[] TargetDeck = new int[0];
        internal int[] BlockDeck = new int[0];
        internal int TargetPrevDeckLen;
        internal int BlockPrevDeckLen;
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
            invalid,
        }

        internal Target(MyCubeBlock firingCube = null, bool client = false)
        {
            FiringCube = firingCube;
            Client = client;
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

        internal void SyncTarget(TransferTarget target, int weaponId)
        {
            target.EntityId = Entity?.EntityId ?? -1;
            target.TargetPos = TargetPos;
            target.HitShortDist = (float)HitShortDist;
            target.OrigDistance = (float)OrigDistance;
            target.TopEntityId = TopEntityId;
            target.WeaponId = weaponId;

            if (IsProjectile)
                target.Info = TransferTarget.TargetInfo.IsProjectile;
            else if (IsFakeTarget)
                target.Info = TransferTarget.TargetInfo.IsFakeTarget;
            else if (State == Targets.Acquired)
                target.Info = TransferTarget.TargetInfo.IsEntity;

            if (State == Targets.Expired)
                target.Info = TransferTarget.TargetInfo.Expired;

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

        internal void Reset(uint expiredTick, bool expire = true, bool dontLog = false)
        {
            //if (Client) return;
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
                State = Targets.Expired;
                if (expiredTick != uint.MaxValue) ExpiredTick = expiredTick;
            }
            TargetLock = false;
        }
    }

    public struct WeaponAmmoTypes
    {
        public MyDefinitionId AmmoDefinitionId;
        public WeaponDefinition.AmmoDef AmmoDef;
        public string AmmoName;
        public bool IsShrapnel;
    }
}
