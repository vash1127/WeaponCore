using System;
using System.ComponentModel;
using ProtoBuf;
using VRage;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
using static WeaponCore.Support.WeaponComponent;
namespace WeaponCore
{
    [ProtoContract]
    public class CompDataValues
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public int Version = Session.VersionControl;
        [ProtoMember(3)] public CompSettingsValues Set;
        [ProtoMember(4)] public CompStateValues State;

        public bool Sync(WeaponComponent comp, CompDataValues sync)
        {
            if (sync.Revision > Revision)
            {
                Set.Sync(comp, sync.Set);
                State.Sync(comp, sync.State);

                Revision = sync.Revision;

                return true;
            }

            return false;
        }
    }

    [ProtoContract]
    public class CompSettingsValues
    {
        [ProtoMember(1), DefaultValue(true)] public bool Guidance = true;
        [ProtoMember(2), DefaultValue(1)] public int Overload = 1;
        [ProtoMember(3), DefaultValue(1)] public float DpsModifier = 1;
        [ProtoMember(4), DefaultValue(1)] public float RofModifier = 1;
        [ProtoMember(5)] public WeaponSettingsValues[] Weapons;
        [ProtoMember(6), DefaultValue(100)] public float Range = 100;
        [ProtoMember(7)] public GroupOverrides Overrides;
        [ProtoMember(8)] public ShootActions TerminalAction;


        public CompSettingsValues()
        {
            Overrides = new GroupOverrides();
        }

        public void  Sync(WeaponComponent comp, CompSettingsValues sync)
        {
            Guidance = sync.Guidance;
            Range = sync.Range;
            var was = TerminalAction;
            TerminalAction = sync.TerminalAction;
            Log.Line($"action:{TerminalAction} -  was:{was}");
            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                var w = comp.Platform.Weapons[i];
                w.ChangeActiveAmmo(w.System.AmmoTypes[w.Set.AmmoTypeId]);
            }

            Overrides.Sync(sync.Overrides);

            if (Overload != sync.Overload || Math.Abs(RofModifier - sync.RofModifier) > 0.0001f || Math.Abs(DpsModifier - sync.DpsModifier) > 0.0001f) {
                Overload = sync.Overload;
                RofModifier = sync.RofModifier;
                DpsModifier = sync.DpsModifier;
                WepUi.SetDps(comp, sync.DpsModifier, true);
            }

        }

        public void TerminalActionSetter(WeaponComponent comp, ShootActions action)
        {
            TerminalAction = action;
            for (int i = 0; i < Weapons.Length; i++)
                Weapons[i].WeaponMode(comp, action, true);
        }
    }

    [ProtoContract]
    public class WeaponSettingsValues
    {
        [ProtoMember(1)] public bool Enable = true;
        [ProtoMember(2)] public int AmmoTypeId;
        [ProtoMember(3), DefaultValue(ShootActions.ShootOff)] public ShootActions Action = ShootActions.ShootOff; // save

        public void WeaponMode(WeaponComponent comp, ShootActions action, bool calledByTerminal = false)
        {
            if (!calledByTerminal)
                comp.Data.Repo.Set.TerminalAction = ShootActions.ShootOff;

            Action = action;
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

        [ProtoMember(1)] public WeaponStateValues[] Weapons;
        [ProtoMember(2)] public bool OtherPlayerTrackingReticle; //don't save
        [ProtoMember(3), DefaultValue(-1)] public long PlayerId = -1;
        [ProtoMember(4), DefaultValue(ControlMode.None)] public ControlMode Control = ControlMode.None;
        public void Sync(WeaponComponent comp, CompStateValues syncFrom)
        {
            OtherPlayerTrackingReticle = syncFrom.OtherPlayerTrackingReticle;
            PlayerId = syncFrom.PlayerId;
            Control = syncFrom.Control;

            for (int i = 0; i < syncFrom.Weapons.Length; i++)
            {
                var ws = Weapons[i];
                var sws = syncFrom.Weapons[i];
                var w = comp.Platform.Weapons[i];

                if (comp.Session.Tick - w.LastAmmoUpdateTick > 3600 || ws.CurrentAmmo < sws.CurrentAmmo || ws.CurrentCharge < sws.CurrentCharge) { // check order on these
                    ws.CurrentAmmo = sws.CurrentAmmo;
                    ws.CurrentCharge = sws.CurrentCharge;
                    w.LastAmmoUpdateTick = comp.Session.Tick;
                }

                ws.CurrentMags = sws.CurrentMags;
                ws.Heat = sws.Heat;
                ws.Overheated = sws.Overheated;
                ws.HasInventory = sws.HasInventory;

            }
        }

        public void ResetToFreshLoadState()
        {
            Control = ControlMode.None;
            PlayerId = -1;
            OtherPlayerTrackingReticle = false;

            foreach (var w in Weapons)
            {
                w.Heat = 0;
                w.Overheated = false;
                w.HasInventory = w.CurrentMags > 0;
            }
        }

    }

    [ProtoContract]
    public class WeaponStateValues
    {
        [ProtoMember(1)] public float Heat; // don't save
        [ProtoMember(2)] public int CurrentAmmo; //save
        [ProtoMember(3)] public float CurrentCharge; //save
        [ProtoMember(4)] public bool Overheated; //don't save
        [ProtoMember(5)] public int WeaponId; // save
        [ProtoMember(6)] public MyFixedPoint CurrentMags; // save
        [ProtoMember(7)] public bool HasInventory; // save
        [ProtoMember(8)] public TransferTarget Target = new TransferTarget(); // save
        [ProtoMember(9)] public WeaponRandomGenerator WeaponRandom = new WeaponRandomGenerator(); // save

        public void Sync(WeaponStateValues sync, Weapon weapon)
        {
            if (weapon.System.Session.Tick - weapon.LastAmmoUpdateTick > 3600 || sync.CurrentAmmo < CurrentAmmo || sync.CurrentCharge < CurrentCharge) { // Check order on these
                sync.CurrentAmmo = CurrentAmmo;
                sync.CurrentCharge = CurrentCharge;
                weapon.LastAmmoUpdateTick = weapon.System.Session.Tick;
            }

            sync.Target = Target;
            sync.WeaponRandom = WeaponRandom;

            sync.Heat = Heat;
            sync.CurrentMags = CurrentMags;

            sync.Overheated = Overheated;
            sync.HasInventory = HasInventory;
        }


        public void WeaponInit(WeaponComponent comp)
        {
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {

                var w = comp.Platform.Weapons[i];
                var wState = comp.Data.Repo.State.Weapons[i];
                wState.WeaponRandom.Init(w.UniqueId);

                var rand = wState.WeaponRandom;
                rand.CurrentSeed = w.UniqueId;
                rand.ClientProjectileRandom = new Random(rand.CurrentSeed);

                rand.TurretRandom = new Random(rand.CurrentSeed);
                rand.AcquireRandom = new Random(rand.CurrentSeed);
                comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, ref w.State, false); }, null, 1);
            }
        }

        public void WeaponRefreshClient(WeaponComponent comp)
        {
            try
            {
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];
                    var rand = w.State.WeaponRandom;

                    rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                    rand.TurretRandom = new Random(rand.CurrentSeed);

                    for (int j = 0; j < rand.TurretCurrentCounter; j++)
                        rand.TurretRandom.Next();

                    for (int j = 0; j < rand.ClientProjectileCurrentCounter; j++)
                        rand.ClientProjectileRandom.Next();

                    comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, ref w.State, false); }, null, 1);
                }
                return;
            }
            catch (Exception e) { Log.Line($"Client Weapon Values Failed To load re-initing... how?"); }

            WeaponInit(comp);
        }
    }

}
