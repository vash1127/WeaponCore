using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using static WeaponCore.Support.PartDefinition.TargetingDef;

namespace WeaponCore.Support
{
    internal class Target
    {
        internal States PreviousState = States.NotSet;
        internal States CurrentState = States.NotSet;
        internal bool HasTarget;
        internal bool IsAligned;
        internal bool IsProjectile;
        internal bool IsFakeTarget;
        internal bool TargetChanged;
        internal bool ParentIsPart;
        internal bool IsTargetStorage;
        internal bool ClientDirty;
        internal bool CoreIsCube;
        internal Part Part;
        internal MyEntity CoreEntity;
        internal MyEntity CoreParent;
        internal MyEntity TargetEntity;
        internal MyCubeBlock CoreCube;
        internal Projectile Projectile;
        internal int[] TargetDeck = new int[0];
        internal int[] BlockDeck = new int[0];
        internal int TargetPrevDeckLen;
        internal int BlockPrevDeckLen;
        internal uint ExpiredTick;
        internal uint ResetTick;
        internal BlockTypes LastBlockType;
        internal Vector3D TargetPos;
        internal double HitShortDist;
        internal double OrigDistance;
        internal long TargetId;
        internal long TopEntityId;
        internal readonly List<MyCubeBlock> Top5 = new List<MyCubeBlock>();

        public enum States
        {
            NotSet,
            ControlReset,
            Expired,
            ClearTargets,
            WeaponNotReady,
            AnimationOff,
            Designator,
            Acquired,
            NoTargetsSeen,
            ProjectileClosed,
            RayCheckFailed,
            RayCheckSelfHit,
            RayCheckFriendly,
            RayCheckDistOffset,
            RayCheckVoxel,
            RayCheckProjectile,
            RayCheckDeadBlock,
            RayCheckDistExceeded,
            RayCheckOther,
            RayCheckMiss,
            ServerReset,
            Transfered,
            Invalid,
            Fake,
            FiredBurst,
            NoMagsToLoad,
            AiLost,
            Offline,
            LostTracking,
        }

        internal Target(Part part = null, bool main = false)
        {
            ParentIsPart = part?.Comp?.CoreEntity != null;
            CoreEntity = part?.Comp?.CoreEntity;
            CoreParent = part?.Comp?.TopEntity;
            CoreCube = part?.Comp?.Cube;
            CoreIsCube = CoreCube != null;
            Part = part;
            IsTargetStorage = main;
        }

        internal void PushTargetToClient(Weapon weapon)
        {
            if (!weapon.System.Session.MpActive || weapon.System.Session.IsClient)
                return;

            weapon.TargetData.TargetPos = TargetPos;
            weapon.TargetData.PartId = weapon.PartId;
            weapon.TargetData.EntityId = weapon.Target.TargetId;
            weapon.System.Session.SendTargetChange(weapon.Comp, weapon.PartId);
        }

        internal void ClientUpdate(Weapon w, TransferTarget tData)
        {
            MyEntity targetEntity = null;
            if (tData.EntityId <= 0 || MyEntities.TryGetEntityById(tData.EntityId, out targetEntity, true))
            {
                TargetEntity = targetEntity;
                
                if (tData.EntityId == 0)
                    w.Target.Reset(w.System.Session.Tick, States.ServerReset);
                else  {
                    StateChange(true, tData.EntityId == -2 ? States.Fake : States.Acquired);
                    
                    if (w.Target.IsProjectile) {

                        Ai.TargetType targetType;
                        Ai.AcquireProjectile(w, out targetType);

                        if (targetType == Ai.TargetType.None) {
                            if (w.NewTarget.CurrentState != States.NoTargetsSeen)
                                w.NewTarget.Reset(w.Comp.Session.Tick, States.NoTargetsSeen);
                            if (w.Target.CurrentState != States.NoTargetsSeen) w.Target.Reset(w.Comp.Session.Tick, States.NoTargetsSeen, !w.Comp.Data.Repo.Base.State.TrackingReticle);
                        }
                    }
                }
                w.TargetData.WeaponRandom.AcquireCurrentCounter = w.TargetData.WeaponRandom.AcquireTmpCounter;
                w.TargetData.WeaponRandom.AcquireRandom = new Random(w.TargetData.WeaponRandom.CurrentSeed);
                ClientDirty = false;

                //Log.Line($"UpdateTarget: id:{tData.EntityId}({TargetId}) - entity:{Entity != null}({targetEntity != null}) - state:{CurrentState}({PreviousState}) - hasTarget:{tData.EntityId != 0}({HasTarget})");
            }
        }

        internal void TransferTo(Target target, uint expireTick)
        {
            target.TargetEntity = TargetEntity;
            target.Projectile = Projectile;
            target.IsProjectile = target.Projectile != null;
            target.IsFakeTarget = IsFakeTarget;
            target.TargetPos = TargetPos;
            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;
            target.StateChange(HasTarget, CurrentState);
            Reset(expireTick, States.Transfered);
        }


        internal void Set(MyEntity ent, Vector3D pos, double shortDist, double origDist, long topEntId, Projectile projectile = null, bool isFakeTarget = false)
        {
            TargetEntity = ent;
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
                PushTargetToClient(w);
        }

        internal void SetFake(uint expiredTick, Vector3D pos)
        {
            Reset(expiredTick, States.Fake, false);
            IsFakeTarget = true;
            TargetPos = pos;
            StateChange(true, States.Fake);
        }

        internal void Reset(uint expiredTick, States reason, bool expire = true)
        {
            TargetEntity = null;
            IsProjectile = false;
            IsFakeTarget = false;
            IsAligned = false;
            Projectile = null;
            TargetPos = Vector3D.Zero;
            HitShortDist = 0;
            OrigDistance = 0;
            TopEntityId = 0;
            TargetId = 0;
            ResetTick = expiredTick;
            if (expire)
            {
                StateChange(false, reason);
                ExpiredTick = expiredTick;
            }
        }

        internal void StateChange(bool setTarget, States reason)
        {
            SetTargetId(setTarget, reason);
            TargetChanged = !HasTarget && setTarget || HasTarget && !setTarget;

            if (TargetChanged && ParentIsPart && IsTargetStorage) {

                if (setTarget) {
                    Part.Comp.Ai.WeaponsTracking++;
                    Part.Comp.PartTracking++;
                }
                else {
                    Part.Comp.Ai.WeaponsTracking--;
                    Part.Comp.PartTracking--;
                }
            }
            HasTarget = setTarget;
            PreviousState = CurrentState;
            CurrentState = reason;
        }

        internal void SetTargetId(bool setTarget, States reason)
        {
            if (IsProjectile)
                TargetId = -1;
            else if (IsFakeTarget)
                TargetId = -2;
            else if (TargetEntity != null)
                TargetId = TargetEntity.EntityId;
            else TargetId = 0;
        }
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
