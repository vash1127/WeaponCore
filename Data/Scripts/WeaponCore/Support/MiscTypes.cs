using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;

namespace WeaponCore.Support
{
    internal class Target
    {
        internal States PreviousState = States.Expired;
        internal States CurrentState = States.Expired;
        internal bool HasTarget;
        internal bool IsTracking;
        internal bool IsAligned;
        internal bool IsProjectile;
        internal bool IsFakeTarget;
        internal bool TargetLock;
        internal bool TargetChanged;
        internal bool ParentIsWeapon;
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

        public enum States
        {
            Expired,
            Acquired,
            NoTargetsSeen,
            ProjectileClosed,
            RayCheckFailed,
            ServerReset,
            Transfered,
            Invalid,
            Fake,
            FiredBurst,
        }

        internal Target(MyCubeBlock firingCube = null)
        {
            ParentIsWeapon = firingCube != null;
            FiringCube = firingCube;
        }

        internal void TransferTo(Target target, uint expireTick, bool reset = true)
        {
            target.Entity = Entity;
            target.Projectile = Projectile;
            target.IsProjectile = target.Projectile != null;
            target.IsFakeTarget = IsFakeTarget;
            target.TargetPos = TargetPos;
            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;
            target.StateChange(HasTarget, CurrentState);
            if (reset) Reset(expireTick, States.Transfered);
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
                target.State = TransferTarget.TargetInfo.IsProjectile;
            else if (IsFakeTarget)
                target.State = TransferTarget.TargetInfo.IsFakeTarget;
            else if (HasTarget)
                target.State = TransferTarget.TargetInfo.IsEntity;

            if (!HasTarget)
                target.State = TransferTarget.TargetInfo.Expired;
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
            StateChange(true, States.Acquired);
        }

        internal void SetFake(uint expiredTick, Vector3D pos)
        {
            Reset(expiredTick, States.Fake,false);
            IsFakeTarget = true;
            TargetPos = pos;
            StateChange(true, States.Fake);
        }

        internal void Reset(uint expiredTick, States reason, bool expire = true)
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
                StateChange(false, reason);
                ExpiredTick = expiredTick;
            }
            TargetLock = false;
        }

        internal void StateChange(bool hasTarget, States reason)
        {
            TargetChanged = !HasTarget && hasTarget || HasTarget && !hasTarget;
            HasTarget = hasTarget;
            PreviousState = CurrentState;
            CurrentState = reason;
        }
    }

    public struct WeaponAmmoTypes
    {
        public MyDefinitionId AmmoDefinitionId;
        public WeaponDefinition.AmmoDef AmmoDef;
        public string AmmoName;
        public bool IsShrapnel;

        public override bool Equals(object obj)
        {
            if (obj.GetType() != GetType()) return false;

            return AmmoDef.Equals(((WeaponAmmoTypes)obj).AmmoDef);
        }
    }

    public class WeaponAmmoMoveRequest
    {
        public Weapon Weapon;
        public List<InventoryMags> Inventories = new List<InventoryMags>();
    }

    public struct InventoryMags
    {
        public MyInventory Inventory;
        public int Amount;
    }
}
