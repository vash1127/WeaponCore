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
        [ProtoMember(2)] public CompSettingsValues Set;
        [ProtoMember(3)] public CompStateValues State;
        [ProtoMember(4)] public WeaponValues WepVal;
        [ProtoMember(5)] public int Version = Session.VersionControl;

        public void Sync(WeaponComponent comp, CompDataValues data)
        {
            if (data.Revision > Revision) {

                Revision = data.Revision;
                
                Set.Sync(comp, data.Set);
                State.Sync(comp, data.State);
                WepVal.Sync(comp, data.WepVal);
            }
        }
    }

    [ProtoContract]
    public class CompSettingsValues
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2), DefaultValue(true)] public bool Guidance = true;
        [ProtoMember(3), DefaultValue(1)] public int Overload = 1;
        [ProtoMember(4), DefaultValue(1)] public float DpsModifier = 1;
        [ProtoMember(5), DefaultValue(1)] public float RofModifier = 1;
        [ProtoMember(6)] public WeaponSettingsValues[] Weapons;
        [ProtoMember(7), DefaultValue(100)] public float Range = 100;
        [ProtoMember(8)] public GroupOverrides Overrides;
        [ProtoMember(9)] public ShootActions TerminalAction;


        public CompSettingsValues()
        {
            Overrides = new GroupOverrides();
        }

        public bool Sync(WeaponComponent comp, CompSettingsValues sync)
        {
            if (sync.Revision > Revision) {

                Revision = sync.Revision;
                Guidance = sync.Guidance;
                Range = sync.Range;

                for (int i = 0; i < comp.Platform.Weapons.Length; i++) {
                    var w = comp.Platform.Weapons[i];
                    var ws = Weapons[i];
                    var sws = sync.Weapons[i];
                    ws.WeaponMode(comp, sws.Action);
                    w.UpdateWeaponRange();
                }

                Overrides.Sync(sync.Overrides);

                if (Overload != sync.Overload || Math.Abs(RofModifier - sync.RofModifier) > 0.0001f || Math.Abs(DpsModifier - sync.DpsModifier) > 0.0001f) {
                    Overload = sync.Overload;
                    RofModifier = sync.RofModifier;
                    WepUi.SetDps(comp, sync.DpsModifier, true);
                }

                return true;
            }
            return false;
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

        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public WeaponStateValues[] Weapons;
        [ProtoMember(3)] public bool OtherPlayerTrackingReticle; //don't save
        [ProtoMember(4), DefaultValue(-1)] public long PlayerId = -1;
        [ProtoMember(5), DefaultValue(ControlMode.None)] public ControlMode Control = ControlMode.None;
        public void Sync(WeaponComponent comp, CompStateValues syncFrom)
        {
            Revision = syncFrom.Revision;
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

        public bool PlayerControlSync(WeaponComponent comp, ControllingPlayerPacket packet)
        {
            if (comp.Data.Repo.WepVal.MIds[(int) packet.PType] < packet.MId) {
                comp.Data.Repo.WepVal.MIds[(int) packet.PType] = packet.MId;

                PlayerId = packet.PlayerId;
                Control = packet.Control;
                return true;
            }

            return false;
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
        [ProtoMember(1)] public uint Revision; //dont save
        [ProtoMember(2)] public float Heat; // don't save
        [ProtoMember(3)] public int CurrentAmmo; //save
        [ProtoMember(4)] public float CurrentCharge; //save
        [ProtoMember(5)] public bool Overheated; //don't save
        [ProtoMember(6)] public int WeaponId; // save
        [ProtoMember(7)] public MyFixedPoint CurrentMags; // save
        [ProtoMember(8)] public bool HasInventory; // save

        public void Sync(WeaponStateValues sync, Weapon weapon)
        {
            if (weapon.System.Session.Tick - weapon.LastAmmoUpdateTick > 3600 || sync.CurrentAmmo < CurrentAmmo || sync.CurrentCharge < CurrentCharge) { // Check order on these
                sync.CurrentAmmo = CurrentAmmo;
                sync.CurrentCharge = CurrentCharge;
                weapon.LastAmmoUpdateTick = weapon.System.Session.Tick;
            }
            
            sync.Heat = Heat;
            sync.CurrentMags = CurrentMags;

            sync.Overheated = Overheated;
            sync.HasInventory = HasInventory;
        }
    }

    [ProtoContract]
    public class WeaponValues
    {
        [ProtoMember(1)] public TransferTarget[] Targets;
        [ProtoMember(2)] public WeaponRandomGenerator[] WeaponRandom;
        [ProtoMember(3)] public uint[] MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];

        public void Sync(WeaponComponent comp, WeaponValues sync)
        {
            for (int i = 0; i < Targets.Length; i++)
            {
                Targets[i] = sync.Targets[i];
                WeaponRandom[i] = sync.WeaponRandom[i];
            }

            for (int i = 0; i < MIds.Length; i++)
                MIds[i] = sync.MIds[i];
        }

        public static void Init(WeaponComponent comp)
        {
            comp.Data.Repo.WepVal = new WeaponValues {
                Targets = new TransferTarget[comp.Platform.Weapons.Length],
                WeaponRandom = new WeaponRandomGenerator[comp.Platform.Weapons.Length],
                MIds = new uint[Enum.GetValues(typeof(PacketType)).Length]
            };

            for (int i = 0; i < comp.Platform.Weapons.Length; i++) {

                var w = comp.Platform.Weapons[i];

                comp.Data.Repo.WepVal.Targets[w.WeaponId] = new TransferTarget();
                comp.Data.Repo.WepVal.WeaponRandom[w.WeaponId] = new WeaponRandomGenerator();
                comp.Data.Repo.WepVal.WeaponRandom[w.WeaponId].Init(w.UniqueId);
                var rand = comp.Data.Repo.WepVal.WeaponRandom[w.WeaponId];
                rand.CurrentSeed = w.UniqueId;
                rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                rand.TurretRandom = new Random(rand.CurrentSeed);
                rand.AcquireRandom = new Random(rand.CurrentSeed);

                comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, ref w.State, false); }, null, 1);
            }
        }

        public static void RefreshClient(WeaponComponent comp)
        {
            try
            {
                if (!comp.Session.IsClient || comp.Data.Repo.WepVal.MIds == null || comp.Data.Repo.WepVal.MIds?.Length != Enum.GetValues(typeof(PacketType)).Length)
                    comp.Data.Repo.WepVal.MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];

                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {

                    var w = comp.Platform.Weapons[i];
                    var rand = comp.Data.Repo.WepVal.WeaponRandom[w.WeaponId];

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

            Init(comp);
        }

        public WeaponValues() { }
    }

   
    [ProtoContract]
    public struct ControllingPlayersSync
    {
        [ProtoMember (1)] public PlayerToBlock[] PlayersToControlledBlock;
    }

    [ProtoContract]
    public struct PlayerToBlock
    {
        [ProtoMember(1)] public long PlayerId;
        [ProtoMember(2)] public long EntityId;
    }

    [ProtoContract]
    public class WeaponRandomGenerator
    {
        [ProtoMember(1)] public int TurretCurrentCounter;
        [ProtoMember(2)] public int ClientProjectileCurrentCounter;
        [ProtoMember(3)] public int CurrentSeed;
        public Random TurretRandom;
        public Random ClientProjectileRandom;
        public Random AcquireRandom;

        public enum RandomType
        {
            Deviation,
            ReAcquire,
            Acquire,
        }

        public WeaponRandomGenerator() { }

        public void Init(int uniqueId)
        {
            CurrentSeed = uniqueId;
            TurretRandom = new Random(CurrentSeed);
            ClientProjectileRandom = new Random(CurrentSeed);
            AcquireRandom = new Random(CurrentSeed);
        }

        public void Sync(WeaponRandomGenerator syncFrom)
        {
            CurrentSeed = syncFrom.CurrentSeed;

            TurretCurrentCounter = syncFrom.TurretCurrentCounter;
            ClientProjectileCurrentCounter = syncFrom.ClientProjectileCurrentCounter;

            TurretRandom = new Random(CurrentSeed);
            ClientProjectileRandom = new Random(CurrentSeed);
        }
    }
}
