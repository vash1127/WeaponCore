using System;
using System.ComponentModel;
using ProtoBuf;
using Sandbox.Game.Entities;
using VRage;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
using static WeaponCore.Support.WeaponComponent;
using static WeaponCore.WeaponStateValues;

namespace WeaponCore
{
    [ProtoContract]
    public class CompDataValues
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public int Version = Session.VersionControl;
        [ProtoMember(3)] public CompSettingsValues Set;
        [ProtoMember(4)] public CompStateValues State;
        [ProtoMember(5)] public TransferTarget[] Targets;


        public bool Sync(WeaponComponent comp, CompDataValues sync)
        {
            if (sync.Revision > Revision)
            {
                Set.Sync(comp, sync.Set);
                State.Sync(comp, sync.State);
                for (int i = 0; i < Targets.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];
                    var syncT = sync.Targets[i];
                    syncT.SyncTarget(w);
                    w.TargetData = syncT;
                }
                Revision = sync.Revision;
                return true;
            }

            return false;
        }

        public void ResetToFreshLoadState()
        {
            Revision = 0;
            Set.Overrides.TargetPainter = false;
            Set.Overrides.ManualControl = false;
            State.Revision = 0;
            State.Control = CompStateValues.ControlMode.None;
            State.PlayerId = -1;
            State.TrackingReticle = false;
            State.TerminalAction = ShootActions.ShootOff;
            foreach (var w in State.Weapons)
            {
                w.Heat = 0;
                w.Overheated = false;
                w.HasInventory = w.CurrentMags > 0;
                w.Action = ShootActions.ShootOff;
            }
        }
    }


    [ProtoContract]
    public class CompSettingsValues
    {
        [ProtoMember(1), DefaultValue(true)] public bool Guidance = true;
        [ProtoMember(2), DefaultValue(1)] public int Overload = 1;
        [ProtoMember(3), DefaultValue(1)] public float DpsModifier = 1;
        [ProtoMember(4), DefaultValue(1)] public float RofModifier = 1;
        [ProtoMember(5), DefaultValue(100)] public float Range = 100;
        [ProtoMember(6)] public GroupOverrides Overrides;


        public CompSettingsValues()
        {
            Overrides = new GroupOverrides();
        }

        public void  Sync(WeaponComponent comp, CompSettingsValues sync)
        {
            Guidance = sync.Guidance;
            Range = sync.Range;

            Overrides.Sync(sync.Overrides);

            if (Overload != sync.Overload || Math.Abs(RofModifier - sync.RofModifier) > 0.0001f || Math.Abs(DpsModifier - sync.DpsModifier) > 0.0001f) {
                Overload = sync.Overload;
                RofModifier = sync.RofModifier;
                DpsModifier = sync.DpsModifier;
                WepUi.SetDps(comp, sync.DpsModifier, true);
            }

        }

    }

    [ProtoContract]
    public class GroupOverrides
    {
        [ProtoMember(1), DefaultValue(true)] public bool Activate = true;
        [ProtoMember(2)] public bool Neutrals;
        [ProtoMember(3)] public bool Unowned;
        [ProtoMember(4)] public bool Friendly;
        [ProtoMember(5)] public bool TargetPainter;
        [ProtoMember(6)] public bool ManualControl;
        [ProtoMember(7)] public bool FocusTargets;
        [ProtoMember(8)] public bool FocusSubSystem;
        [ProtoMember(9)] public BlockTypes SubSystem = BlockTypes.Any;
        [ProtoMember(10), DefaultValue(true)] public bool Meteors;
        [ProtoMember(11), DefaultValue(true)] public bool Biologicals;
        [ProtoMember(12), DefaultValue(true)] public bool Projectiles;

        public GroupOverrides() { }

        public void Sync(GroupOverrides syncFrom)
        {
            Activate = syncFrom.Activate;
            Neutrals = syncFrom.Neutrals;
            Unowned = syncFrom.Unowned;
            Friendly = syncFrom.Friendly;
            TargetPainter = syncFrom.TargetPainter;
            ManualControl = syncFrom.ManualControl;
            FocusTargets = syncFrom.FocusTargets;
            FocusSubSystem = syncFrom.FocusSubSystem;
            SubSystem = syncFrom.SubSystem;
            Meteors = syncFrom.Meteors;
            Biologicals = syncFrom.Biologicals;
            Projectiles = syncFrom.Projectiles;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            var compared = (GroupOverrides)obj;

            return (
                compared.Activate.Equals(Activate) &&
                compared.Neutrals.Equals(Neutrals) &&
                compared.Unowned.Equals(Unowned) &&
                compared.Friendly.Equals(Friendly) &&
                compared.TargetPainter.Equals(TargetPainter) &&
                compared.ManualControl.Equals(ManualControl) &&
                compared.FocusTargets.Equals(FocusTargets) &&
                compared.FocusSubSystem.Equals(FocusSubSystem) &&
                compared.SubSystem.Equals(SubSystem) &&
                compared.Meteors.Equals(Meteors) &&
                compared.Biologicals.Equals(Biologicals) &&
                compared.Projectiles.Equals(Projectiles)
            );
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Activate.GetHashCode();
                hashCode = (hashCode * 397) ^ Neutrals.GetHashCode();
                hashCode = (hashCode * 397) ^ Unowned.GetHashCode();
                hashCode = (hashCode * 397) ^ Friendly.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetPainter.GetHashCode();
                hashCode = (hashCode * 397) ^ ManualControl.GetHashCode();
                hashCode = (hashCode * 397) ^ FocusTargets.GetHashCode();
                hashCode = (hashCode * 397) ^ FocusSubSystem.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)SubSystem;
                hashCode = (hashCode * 397) ^ Meteors.GetHashCode();
                hashCode = (hashCode * 397) ^ Biologicals.GetHashCode();
                hashCode = (hashCode * 397) ^ Projectiles.GetHashCode();
                return hashCode;
            }
        }
    }

    [ProtoContract]
    public class CompStateValues
    {
        public enum ControlMode
        {
            None,
            Ui,
            Toolbar,
            Camera
        }

        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public WeaponStateValues[] Weapons;
        [ProtoMember(3)] public bool TrackingReticle; //don't save
        [ProtoMember(4), DefaultValue(-1)] public long PlayerId = -1;
        [ProtoMember(5), DefaultValue(ControlMode.None)] public ControlMode Control = ControlMode.None;
        [ProtoMember(6)] public ShootActions TerminalAction;

        public void Sync(WeaponComponent comp, CompStateValues sync)
        {
            if (sync.Revision > Revision)
            {
                Revision = sync.Revision;
                TrackingReticle = sync.TrackingReticle;
                PlayerId = sync.PlayerId;
                Control = sync.Control;
                TerminalAction = sync.TerminalAction;
                for (int i = 0; i < sync.Weapons.Length; i++)
                {
                    var ws = Weapons[i];
                    var sws = sync.Weapons[i];
                    var w = comp.Platform.Weapons[i];

                    if (comp.Session.Tick - w.LastAmmoUpdateTick > 3600 || ws.CurrentAmmo < sws.CurrentAmmo || ws.CurrentCharge < sws.CurrentCharge)
                    { // check order on these
                        ws.CurrentAmmo = sws.CurrentAmmo;
                        ws.CurrentCharge = sws.CurrentCharge;
                        w.LastAmmoUpdateTick = comp.Session.Tick;
                    }
                    ws.CurrentMags = sws.CurrentMags;
                    ws.Heat = sws.Heat;
                    ws.Overheated = sws.Overheated;
                    ws.HasInventory = sws.HasInventory;
                    ws.AmmoTypeId = sws.AmmoTypeId;
                    ws.Action = sws.Action;
                    w.ChangeActiveAmmo(w.System.AmmoTypes[w.State.AmmoTypeId]);
                }
            }
        }

        public void TerminalActionSetter(WeaponComponent comp, ShootActions action, string caller = null)
        {
            TerminalAction = action;
            for (int i = 0; i < Weapons.Length; i++)
                Weapons[i].WeaponMode(comp, action, true);

            if (caller != null)
                Log.Line(caller);
        }

    }

    [ProtoContract]
    public class WeaponStateValues
    {
        [ProtoMember(1)] public float Heat; // don't save
        [ProtoMember(2)] public int CurrentAmmo; //save
        [ProtoMember(3)] public float CurrentCharge; //save
        [ProtoMember(4)] public bool Overheated; //don't save
        [ProtoMember(5)] public MyFixedPoint CurrentMags; // save
        [ProtoMember(6)] public bool HasInventory; // save
        [ProtoMember(7)] public WeaponRandomGenerator WeaponRandom = new WeaponRandomGenerator(); // save
        [ProtoMember(8)] public int AmmoTypeId;
        [ProtoMember(9), DefaultValue(ShootActions.ShootOff)] public ShootActions Action = ShootActions.ShootOff; // save

        public void WeaponInit(Weapon w)
        {
            w.State.WeaponRandom.Init(w.UniqueId);

            var rand = w.State.WeaponRandom;
            rand.CurrentSeed = w.UniqueId;
            rand.ClientProjectileRandom = new Random(rand.CurrentSeed);

            rand.TurretRandom = new Random(rand.CurrentSeed);
            rand.AcquireRandom = new Random(rand.CurrentSeed);
            //comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, ref w.State, false); }, null, 1);
        }

        public void WeaponRefreshClient(Weapon w)
        {
            try
            {
                var rand = w.State.WeaponRandom;

                rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                rand.TurretRandom = new Random(rand.CurrentSeed);
                w.TargetData = w.Comp.Data.Repo.Targets[w.WeaponId];
                for (int j = 0; j < rand.TurretCurrentCounter; j++)
                    rand.TurretRandom.Next();

                for (int j = 0; j < rand.ClientProjectileCurrentCounter; j++)
                    rand.ClientProjectileRandom.Next();

                //comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, ref w.State, false); }, null, 1);
                return;
            }
            catch (Exception e) { Log.Line($"Client Weapon Values Failed To load re-initing... how?"); }

            WeaponInit(w);
        }

        public void WeaponMode(WeaponComponent comp, ShootActions action, bool calledByTerminal = false)
        {
            if (!calledByTerminal)
                comp.Data.Repo.State.TerminalAction = ShootActions.ShootOff;

            Action = action;
        }

        [ProtoContract]
        public class TransferTarget
        {
            [ProtoMember(1)] public uint Revision;
            [ProtoMember(2)] public long EntityId;
            [ProtoMember(3)] public Vector3 TargetPos;
            [ProtoMember(4)] public int WeaponId;

            internal void SyncTarget(Weapon w, bool allowChange = true)
            {
                if (Revision > w.TargetData.Revision || allowChange)  {

                    w.TargetData.Revision = Revision;
                    if (allowChange && !w.Reloading && w.ActiveAmmoDef.AmmoDef.Const.Reloadable && !w.System.DesignatorWeapon)
                        w.Reload();

                    var target = w.Target;
                    target.IsProjectile = EntityId == -1;
                    target.IsFakeTarget = EntityId == -2;
                    target.TargetPos = TargetPos;
                    target.Entity = EntityId > 0 ? MyEntities.GetEntityByIdOrDefault(EntityId) : null;

                    var state = EntityId != 0 ? Target.States.Acquired : Target.States.Expired;
                    target.StateChange(EntityId != 0, state);

                    if (!allowChange)
                        target.TargetChanged = false;

                    if (w.Target.HasTarget && allowChange)  {

                        if (!w.Target.IsProjectile && !w.Target.IsFakeTarget && w.Target.Entity == null)  {
                            var oldChange = w.Target.TargetChanged;
                            w.Target.StateChange(true, Target.States.Invalid);
                            w.Target.TargetChanged = !w.FirstSync && oldChange;
                            w.FirstSync = false;
                        }
                        else if (w.Target.IsProjectile)  {

                            GridAi.TargetType targetType;
                            GridAi.AcquireProjectile(w, out targetType);

                            if (targetType == GridAi.TargetType.None)  {
                                if (w.NewTarget.CurrentState != Target.States.NoTargetsSeen)
                                    w.NewTarget.Reset(w.Comp.Session.Tick, Target.States.NoTargetsSeen);
                                if (w.Target.CurrentState != Target.States.NoTargetsSeen) w.Target.Reset(w.Comp.Session.Tick, Target.States.NoTargetsSeen, !w.Comp.Data.Repo.State.TrackingReticle);
                            }
                        }
                    }
                }
            }

            internal void ClearTarget()
            {
                ++Revision;
                EntityId = 0;
                TargetPos = Vector3.Zero;
            }

            public TransferTarget() { }
        }
    }
}
