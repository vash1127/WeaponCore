using System;
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
            OutOfAmmo,
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

        internal void SyncTarget(TransferTarget target, Weapon weapon)
        {
            if (!weapon.System.Session.MpActive || weapon.System.Session.IsClient)
                return;

            target.EntityId = Entity?.EntityId ?? -1;
            target.TargetPos = TargetPos;
            target.HitShortDist = (float)HitShortDist;
            target.OrigDistance = (float)OrigDistance;
            target.TopEntityId = TopEntityId;
            target.WeaponId = weapon.WeaponId;

            if (IsProjectile)
                target.State = TransferTarget.TargetInfo.IsProjectile;
            else if (IsFakeTarget)
                target.State = TransferTarget.TargetInfo.IsFakeTarget;
            else if (HasTarget)
                target.State = TransferTarget.TargetInfo.IsEntity;

            if (!HasTarget)
                target.State = TransferTarget.TargetInfo.Expired;

            if (!weapon.System.Session.IsClient && weapon.System.Session.MpActive)
            {
                if (weapon.Comp.Session.WeaponsSyncCheck.Add(weapon))
                {

                    weapon.Comp.Session.WeaponsToSync.Add(weapon);
                    weapon.Comp.Ai.NumSyncWeapons++;
                    weapon.SendTarget = true;

                    if (weapon.Comp.Session.Tick - weapon.LastSyncTick > 20)
                        weapon.SendSync = true;

                    weapon.LastSyncTick = weapon.Comp.Session.Tick;
                }
            }
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

        internal void LockTarget(Weapon w, MyEntity ent)
        {
            double rayDist;
            var targetPos = ent.PositionComp.WorldAABB.Center;
            Vector3D.Distance(ref w.MyPivotPos, ref targetPos, out rayDist);
            var shortDist = rayDist - 1;
            var origDist = rayDist;
            var topEntId = ent.GetTopMostParent().EntityId;

            Set(ent, targetPos, shortDist, origDist, topEntId);
            if (w.System.Session.MpActive && !w.System.Session.IsClient) 
                SyncTarget(w.Comp.WeaponValues.Targets[w.WeaponId], w);
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

    public class ParticleEvent
    {
        private readonly Guid _uid;
        public readonly Dummy MyDummy;
        public readonly Vector4 Color;
        public readonly Vector3 Offset;
        public readonly Vector3D EmptyPos;
        public readonly string ParticleName;
        public readonly string EmptyNames;
        public readonly string[] MuzzleNames;
        public readonly string PartName;
        public readonly float MaxPlayTime;
        public readonly uint StartDelay;
        public readonly uint LoopDelay;
        public readonly float Scale;
        public readonly float Distance;
        public readonly bool DoesLoop;
        public readonly bool Restart;
        public readonly bool ForceStop;

        public bool Playing;
        public bool Stop;
        public bool Triggered;
        public uint PlayTick;
        public MyParticleEffect Effect;
        public MyCubeBlock BaseBlock;

        public ParticleEvent(string particleName, string emptyName, Vector4 color, Vector3 offset, float scale, float distance, float maxPlayTime, uint startDelay, uint loopDelay, bool loop, bool restart, bool forceStop, params string[] muzzleNames)
        {
            ParticleName = particleName;
            EmptyNames = emptyName;
            MuzzleNames = muzzleNames;
            Color = color;
            Offset = offset;
            Scale = scale;
            Distance = distance;
            MaxPlayTime = maxPlayTime;
            StartDelay = startDelay;
            LoopDelay = loopDelay;
            DoesLoop = loop;
            Restart = restart;
            ForceStop = forceStop;
            _uid = Guid.NewGuid();
        }

        public ParticleEvent(ParticleEvent copyFrom, Dummy myDummy, string partName, Vector3 pos)
        {
            MyDummy = myDummy;
            PartName = partName;
            EmptyNames = copyFrom.EmptyNames;
            MuzzleNames = copyFrom.MuzzleNames;
            ParticleName = copyFrom.ParticleName;
            Color = copyFrom.Color;
            Offset = copyFrom.Offset;
            EmptyPos = pos;
            Scale = copyFrom.Scale;
            Distance = copyFrom.Distance;
            MaxPlayTime = copyFrom.MaxPlayTime;
            StartDelay = copyFrom.StartDelay;
            LoopDelay = copyFrom.LoopDelay;
            DoesLoop = copyFrom.DoesLoop;
            Restart = copyFrom.Restart;
            ForceStop = copyFrom.ForceStop;
            _uid = Guid.NewGuid();
        }

        protected bool Equals(ParticleEvent other)
        {
            return Equals(_uid, other._uid);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ParticleEvent)obj);
        }

        public override int GetHashCode()
        {
            return _uid.GetHashCode();
        }
    }
}
