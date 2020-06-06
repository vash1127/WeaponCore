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
        internal bool IsTargetStorage;
        internal Weapon Weapon;
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
            AiLost,
            Offline,
        }

        internal Target(Weapon weapon = null, bool main = false)
        {
            ParentIsWeapon = weapon?.Comp?.MyCube != null;
            FiringCube = weapon?.Comp?.MyCube;
            Weapon = weapon;
            IsTargetStorage = main;
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
            Reset(expiredTick, States.Fake, false);
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

            if (TargetChanged && ParentIsWeapon && IsTargetStorage) {

                if (hasTarget) {
                    Weapon.Comp.Ai.WeaponsTracking++;
                    Weapon.Comp.WeaponsTracking++;
                }
                else {
                    Weapon.Comp.Ai.WeaponsTracking--;
                    Weapon.Comp.WeaponsTracking--;
                }
            }

            HasTarget = hasTarget;
            PreviousState = CurrentState;
            CurrentState = reason;
        }
    }

    internal class TerminalMonitor
    {
        internal Session Session;
        internal WeaponComponent Comp;
        internal int OriginalAiVersion;
        internal bool Active;

        internal TerminalMonitor(Session session)
        {
            Session = session;
        }

        internal void Update(WeaponComponent comp, bool isCaller = false)
        {
            Comp = comp;
            Active = true;
            OriginalAiVersion = comp.Ai.Version;
            comp.Ai.Construct.RootAi.ActiveWeaponTerminal = comp.MyCube;

            if (comp.IsAsleep)
                comp.WakeupComp();

            if (Session.IsClient && isCaller) {
                //SyncGoesHere
            }
        }

        internal void Clean(bool isCaller = false)
        {
            if (Comp != null && Comp.Ai.Version == OriginalAiVersion) {
                Comp.Ai.Construct.RootAi.ActiveWeaponTerminal = null;

                if (Session.IsClient && isCaller) {
                    //SyncGoesHere
                }

            }

            Comp = null;
            OriginalAiVersion = -1;
            Active = false;
        }

        internal void Monitor()
        {
            if (IsActive()) {

                if (Session.Tick20) 
                    Comp.TerminalRefresh();
            }
            else if (Active)
                Clean();
        }

        internal bool IsActive()
        {
            if (Comp?.Ai == null) return false;

            var sameVersion = Comp.Ai.Version == OriginalAiVersion;
            var nothingMarked = !Comp.MyCube.MarkedForClose && !Comp.Ai.MyGrid.MarkedForClose && !Comp.Ai.MyGrid.MarkedForClose;
            var sameGrid = Comp.MyCube.CubeGrid == Comp.Ai.MyGrid;
            var inTerminalWindow = Session.InMenu && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;
            var compReady = Comp.Platform.State == MyWeaponPlatform.PlatformState.Ready;
            var sameTerminalBlock = Session.LastTerminal == Comp.Ai.Construct.RootAi?.ActiveWeaponTerminal;

            return (sameVersion && nothingMarked && sameGrid && compReady && inTerminalWindow && sameTerminalBlock);
        }


        internal void Purge()
        {
            Clean();
            Session = null;
        }
    }

    internal class AcquireManager
    {
        internal Session Session;
        internal readonly HashSet<WeaponAcquire> Awake = new HashSet<WeaponAcquire>();
        internal readonly HashSet<WeaponAcquire> Asleep = new HashSet<WeaponAcquire>();

        internal readonly List<WeaponAcquire> Collector = new List<WeaponAcquire>();
        internal readonly List<WeaponAcquire> Removal = new List<WeaponAcquire>();

        internal int LastSleepSlot = -1;
        internal int LastAwakeSlot = -1;
        internal int WasAwake;
        internal int WasAsleep;

        internal AcquireManager(Session session)
        {
            Session = session;
        }

        internal void Awaken(WeaponAcquire wa)
        {
            var notValid = !wa.Weapon.Set.Enable || !wa.Weapon.Comp.State.Value.Online || !wa.Weapon.Comp.Set.Value.Overrides.Activate || !wa.Weapon.TrackTarget || Session.IsClient;
            if (notValid)
            {
                if (!Session.IsClient) Log.Line($"cannot awaken: wEnable:{wa.Weapon.Set.Enable} - cOnline:{wa.Weapon.Comp.State.Value.Online} - cOverride:{wa.Weapon.Comp.Set.Value.Overrides.Activate} - tracking:{wa.Weapon.TrackTarget}");
                return;
            }

            wa.CreatedTick = Session.Tick;

            if (!wa.Asleep)
                return;

            Asleep.Remove(wa);

            AddAwake(wa);
        }

        internal void AddAwake(WeaponAcquire wa)
        {
            var notValid = !wa.Weapon.Set.Enable || !wa.Weapon.Comp.State.Value.Online || !wa.Weapon.Comp.Set.Value.Overrides.Activate || !wa.Weapon.TrackTarget || Session.IsClient;
            if (notValid)
            {
                if (!Session.IsClient) Log.Line($"cannot add: wEnable:{wa.Weapon.Set.Enable} - cOnline:{wa.Weapon.Comp.State.Value.Online} - cOverride:{wa.Weapon.Comp.Set.Value.Overrides.Activate} - tracking:{wa.Weapon.TrackTarget}");
                return;
            }

            wa.Enabled = true;
            wa.Asleep = false;
            wa.CreatedTick = Session.Tick;

            if (LastAwakeSlot < Session.AwakeBuckets - 1) {

                wa.SlotId = ++LastAwakeSlot;

                Awake.Add(wa);
            }
            else {

                wa.SlotId = LastAwakeSlot = 0;

                Awake.Add(wa);
            }
        }

        internal void Remove(WeaponAcquire wa)
        {
            wa.Enabled = false;

            if (wa.Asleep) {

                wa.Asleep = false;
                Asleep.Remove(wa);
            }
            else {
                Awake.Remove(wa);
            }
        }


        internal void UpdateAsleep()
        {
            WasAwake = 0;
            WasAwake += Awake.Count;

            foreach (var wa in Awake) {

                if (wa.Weapon.Target.HasTarget) {
                    Removal.Add(wa);
                    continue;
                }

                if (Session.Tick - wa.CreatedTick > 599) {

                    if (LastSleepSlot < Session.AsleepBuckets - 1) {

                        wa.SlotId = ++LastSleepSlot;
                        wa.Asleep = true;

                        Asleep.Add(wa);
                        Removal.Add(wa);
                    }
                    else {

                        wa.SlotId = LastSleepSlot = 0;
                        wa.Asleep = true;

                        Asleep.Add(wa);
                        Removal.Add(wa);
                    }
                }
            }

            for (int i = 0; i < Removal.Count; i++)
                Awake.Remove(Removal[i]);

            Removal.Clear();
        }


        internal void ReorderSleep()
        {
            foreach (var wa in Asleep) {
                
                var remove = wa.Weapon.Target.HasTarget || wa.Weapon.Comp.IsAsleep || !wa.Weapon.Set.Enable || !wa.Weapon.Comp.State.Value.Online || !wa.Weapon.Comp.Set.Value.Overrides.Activate || Session.IsClient || !wa.Weapon.TrackTarget;

                if (remove) {
                    Removal.Add(wa);
                    continue;
                }
                Collector.Add(wa);
            }

            Asleep.Clear();

            for (int i = 0; i < Removal.Count; i++)
                Remove(Removal[i]);

            WasAsleep = Collector.Count;

            ShellSort(Collector);

            LastSleepSlot = -1;
            for (int i = 0; i < Collector.Count; i++) {

                var wa = Collector[i];
                if (LastSleepSlot < Session.AsleepBuckets - 1) {

                    wa.SlotId = ++LastSleepSlot;

                    Asleep.Add(wa);
                }
                else {

                    wa.SlotId = LastSleepSlot = 0;

                    Asleep.Add(wa);
                }
            }
            Collector.Clear();
            Removal.Clear();
        }

        static void ShellSort(List<WeaponAcquire> list)
        {
            int length = list.Count;

            for (int h = length / 2; h > 0; h /= 2)
            {
                for (int i = h; i < length; i += 1)
                {
                    var tempValue = list[i];
                    var temp = list[i].Weapon.UniqueId;

                    int j;
                    for (j = i; j >= h && list[j - h].Weapon.UniqueId > temp; j -= h)
                    {
                        list[j] = list[j - h];
                    }

                    list[j] = tempValue;
                }
            }
        }

        internal void Clean()
        {
            Awake.Clear();
            Asleep.Clear();
            Collector.Clear();
            Removal.Clear();
        }

    }

    internal class WeaponAcquire
    {
        internal Weapon Weapon;
        internal uint CreatedTick;
        internal int SlotId;
        internal bool Asleep;
        internal bool Enabled;

        internal WeaponAcquire(Weapon weapon)
        {
            Weapon = weapon;
        }
    }

    public class WeaponAmmoTypes
    {
        public MyDefinitionId AmmoDefinitionId;
        public WeaponDefinition.AmmoDef AmmoDef;
        public string AmmoName;
        public bool IsShrapnel;
    }

    public class WeaponAmmoMoveRequest
    {
        public Weapon Weapon;
        public List<InventoryMags> Inventories = new List<InventoryMags>();

        public void Clean()
        {
            Weapon = null;
            Inventories.Clear();
        }
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
