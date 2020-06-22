using System;
using System.ComponentModel;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage;
using VRage.Utils;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;

namespace WeaponCore
{


    [ProtoContract]
    public class CompStateValues
    {
        [ProtoMember(1)] public bool Online; //don't save
        [ProtoMember(2)] public WeaponStateValues[] Weapons;
        [ProtoMember(3)] public bool ShootOn; //don't save
        [ProtoMember(4)] public bool ClickShoot; //don't save
        [ProtoMember(5)] public PlayerControl CurrentPlayerControl; //don't save
        [ProtoMember(6)] public float CurrentCharge; //save
        [ProtoMember(7)] public int Version = Session.VersionControl; //save
        [ProtoMember(8)] public string CurrentBlockGroup; //don't save
        [ProtoMember(9)] public bool OtherPlayerTrackingReticle; //don't save

        public void Sync(CompStateValues syncFrom, WeaponComponent comp)
        {
            Online = syncFrom.Online;
            ShootOn = syncFrom.ShootOn;
            ClickShoot = syncFrom.ClickShoot;
            CurrentPlayerControl = syncFrom.CurrentPlayerControl;
            CurrentCharge = syncFrom.CurrentCharge;
            CurrentBlockGroup = syncFrom.CurrentBlockGroup;
            OtherPlayerTrackingReticle = syncFrom.OtherPlayerTrackingReticle;
            for (int i = 0; i < syncFrom.Weapons.Length; i++)
            {
                var ws = Weapons[i];
                var sws = syncFrom.Weapons[i];
                var w = comp.Platform.Weapons[i];

                if (comp.Session.Tick - w.LastAmmoUpdateTick > 3600 || ws.Sync.CurrentAmmo < sws.Sync.CurrentAmmo || ws.Sync.CurrentCharge < sws.Sync.CurrentCharge) {
                    ws.Sync.CurrentAmmo = sws.Sync.CurrentAmmo;
                    ws.Sync.CurrentCharge = sws.Sync.CurrentCharge;
                    w.LastAmmoUpdateTick = comp.Session.Tick;
                }

                ws.ShotsFired = sws.ShotsFired;
                ws.ManualShoot = sws.ManualShoot;
                ws.SingleShotCounter = sws.SingleShotCounter;
                ws.Sync.CurrentMags = sws.Sync.CurrentMags;
                ws.Sync.Heat = sws.Sync.Heat;
                ws.Sync.Overheated = sws.Sync.Overheated;
                ws.Sync.HasInventory = sws.Sync.HasInventory;

            }
        }

        public void ResetToFreshLoadState()
        {
            Online = false;
            CurrentPlayerControl.ControlType = ControlType.None;
            CurrentPlayerControl.PlayerId = -1;
            CurrentBlockGroup = string.Empty;
            OtherPlayerTrackingReticle = false;

            foreach (var w in Weapons)
            {
                w.ShotsFired = 0;
                w.Sync.Heat = 0;
                w.Sync.Overheated = false;
                w.Sync.HasInventory = w.Sync.CurrentMags > 0;
            }
        }

    }

    [ProtoContract]
    public class CompSettingsValues
    {
        [ProtoMember(1), DefaultValue(true)] public bool Guidance = true;
        [ProtoMember(2), DefaultValue(1)] public int Overload = 1;
        [ProtoMember(3)] public long Modes;
        [ProtoMember(4), DefaultValue(1)] public float DpsModifier = 1;
        [ProtoMember(5), DefaultValue(1)] public float RofModifier = 1;
        [ProtoMember(6)] public WeaponSettingsValues[] Weapons;
        [ProtoMember(7), DefaultValue(100)] public float Range = 100;
        [ProtoMember(8)] public GroupOverrides Overrides;
        [ProtoMember(9)] public int Version = Session.VersionControl;

        public CompSettingsValues()
        {
            Overrides = new GroupOverrides();
        }

        public void Sync(WeaponComponent comp, CompSettingsValues syncFrom)
        {
            Guidance = syncFrom.Guidance;
            Modes = syncFrom.Modes;
            
            Range = syncFrom.Range;

            foreach (var w in comp.Platform.Weapons)
                w.UpdateWeaponRange();

            Overrides.Sync(syncFrom.Overrides);

            if (Overload != syncFrom.Overload || Math.Abs(RofModifier - syncFrom.RofModifier) > 0.0001f || Math.Abs(DpsModifier - syncFrom.DpsModifier) > 0.0001f )
            {
                Overload = syncFrom.Overload;
                RofModifier = syncFrom.RofModifier;
                WepUi.SetDps(comp, syncFrom.DpsModifier, true);
            }
        }

    }

    [ProtoContract]
    public class WeaponStateValues
    {
        [ProtoMember(1)] public int ShotsFired; //don't know??
        [ProtoMember(2), DefaultValue(ManualShootActionState.ShootOff)] public ManualShootActionState ManualShoot = ManualShootActionState.ShootOff; // save
        [ProtoMember(3)] public int SingleShotCounter; // save
        [ProtoMember(4)] public WeaponSyncValues Sync;

    }

    [ProtoContract]
    public class WeaponSyncValues
    {
        [ProtoMember(1)] public float Heat; // don't save
        [ProtoMember(2)] public int CurrentAmmo; //save
        [ProtoMember(3)] public float CurrentCharge; //save
        [ProtoMember(4)] public bool Overheated; //don't save
        [ProtoMember(5)] public int WeaponId; // save
        [ProtoMember(6)] public MyFixedPoint CurrentMags; // save
        [ProtoMember(7)] public bool HasInventory; // save

        public void SetState (WeaponSyncValues sync, Weapon weapon)
        {
            if (weapon.System.Session.Tick - weapon.LastAmmoUpdateTick > 3600 || sync.CurrentAmmo < CurrentAmmo || sync.CurrentCharge < CurrentCharge) {
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
    public class PlayerControl
    {
        [ProtoMember(1), DefaultValue(-1)] public long PlayerId = -1;
        [ProtoMember(2), DefaultValue(ControlType.None)] public ControlType ControlType = ControlType.None;

        public PlayerControl() { }

        public void Sync(PlayerControl syncFrom)
        {
            PlayerId = syncFrom.PlayerId;
            ControlType = syncFrom.ControlType;
        }
    }
    
    public enum ControlType
    {
        None,
        Ui,
        Toolbar,
        Camera        
    }

    [ProtoContract]
    public class WeaponSettingsValues
    {
        [ProtoMember(1)] public bool Enable = true;
        [ProtoMember(2)] public int AmmoTypeId;
    }

    [ProtoContract]
    public class WeaponValues
    {
        [ProtoMember(1)] public TransferTarget[] Targets;
        [ProtoMember(3)] public WeaponRandomGenerator[] WeaponRandom;
        [ProtoMember(4)] public uint[] MIds;

        public void Save(WeaponComponent comp)
        {
            if (comp.MyCube?.Storage == null) return;
            var sv = new WeaponValues {Targets = Targets, WeaponRandom = WeaponRandom, MIds = comp.MIds };
            var binary = MyAPIGateway.Utilities.SerializeToBinary(sv);
            comp.MyCube.Storage[comp.Session.MpWeaponSyncGuid] = Convert.ToBase64String(binary);

        }

        public static void Load(WeaponComponent comp)
        {
            string rawData;
            if (comp.MyCube.Storage.TryGetValue(comp.Session.MpWeaponSyncGuid, out rawData))
            {
                var base64 = Convert.FromBase64String(rawData);
                try
                {
                    comp.WeaponValues = MyAPIGateway.Utilities.SerializeFromBinary<WeaponValues>(base64);

                    if (!comp.Session.IsClient || comp.WeaponValues.MIds == null || comp.WeaponValues.MIds?.Length != Enum.GetValues(typeof(PacketType)).Length)
                        comp.WeaponValues.MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];

                    comp.MIds = comp.WeaponValues.MIds;
                    var targets = comp.WeaponValues.Targets;

                    for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                    {
                        var w = comp.Platform.Weapons[i];
                        var rand = comp.WeaponValues.WeaponRandom[w.WeaponId];

                        if (comp.Session.IsServer)
                        {
                            targets[w.WeaponId] = new TransferTarget();
                            comp.WeaponValues.WeaponRandom[w.WeaponId] = new WeaponRandomGenerator(w.UniqueId);

                            rand.CurrentSeed = w.UniqueId;
                            rand.AcquireRandom = new Random(rand.CurrentSeed);
                        }

                        rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                        rand.TurretRandom = new Random(rand.CurrentSeed);

                        for (int j = 0; j < rand.TurretCurrentCounter; j++)
                            rand.TurretRandom.Next();

                        for (int j = 0; j < rand.ClientProjectileCurrentCounter; j++)
                            rand.ClientProjectileRandom.Next();

                        comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, ref w.State.Sync, false); }, null, 1);
                    }
                    return;
                }
                catch (Exception e)
                {
                    Log.Line($"Weapon Values Failed To load re-initing");
                }

            }            

            comp.WeaponValues = new WeaponValues
            {
                Targets = new TransferTarget[comp.Platform.Weapons.Length],
                WeaponRandom = new WeaponRandomGenerator[comp.Platform.Weapons.Length],
                MIds = new uint[Enum.GetValues(typeof(PacketType)).Length]
            };

            comp.MIds = comp.WeaponValues.MIds;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];

                comp.WeaponValues.Targets[w.WeaponId] = new TransferTarget();
                comp.WeaponValues.WeaponRandom[w.WeaponId] = new WeaponRandomGenerator(w.UniqueId);

                var rand = comp.WeaponValues.WeaponRandom[w.WeaponId];
                rand.CurrentSeed = w.UniqueId;
                rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                rand.TurretRandom = new Random(rand.CurrentSeed);
                rand.AcquireRandom = new Random(rand.CurrentSeed);

                comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, ref w.State.Sync, false); }, null, 1);
            }


        }

        public WeaponValues() { }
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

        protected bool Equals(GroupOverrides other)
        {
            return Activate == other.Activate && Neutrals == other.Neutrals && Unowned == other.Unowned && Friendly == other.Friendly && TargetPainter == other.TargetPainter && ManualControl == other.ManualControl && FocusTargets == other.FocusTargets && FocusSubSystem == other.FocusSubSystem && SubSystem == other.SubSystem && Meteors == other.Meteors && Biologicals == other.Biologicals && Projectiles == other.Projectiles;
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
                hashCode = (hashCode * 397) ^ (int) SubSystem;
                hashCode = (hashCode * 397) ^ Meteors.GetHashCode();
                hashCode = (hashCode * 397) ^ Biologicals.GetHashCode();
                hashCode = (hashCode * 397) ^ Projectiles.GetHashCode();
                return hashCode;
            }
        }
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

        public WeaponRandomGenerator(int uniqueId)
        {
            CurrentSeed = uniqueId;
            TurretRandom = new Random(uniqueId);
            ClientProjectileRandom = new Random(uniqueId);
            AcquireRandom = new Random(uniqueId);
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
