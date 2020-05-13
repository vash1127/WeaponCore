using System;
using System.Collections.Generic;
using System.ComponentModel;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;

namespace WeaponCore
{


    [ProtoContract]
    public class CompStateValues
    {
        [ProtoMember(1), DefaultValue(-1)] public float PowerLevel;
        [ProtoMember(2)] public bool Online;
        [ProtoMember(3)] public bool Overload;
        [ProtoMember(4)] public bool Message;
        [ProtoMember(5)] public int Heat;
        [ProtoMember(6)] public WeaponStateValues[] Weapons;
        [ProtoMember(7)] public bool ShootOn;
        [ProtoMember(8)] public bool ClickShoot;
        [ProtoMember(9)] public PlayerControl CurrentPlayerControl;
        [ProtoMember(10)] public float CurrentCharge;
        [ProtoMember(11)] public int Version = Session.VersionControl;
        [ProtoMember(12)] public string CurrentBlockGroup;
        [ProtoMember(13)] public bool OtherPlayerTrackingReticle;
        [ProtoMember(14)] public int RandIncAmount;

        public void Sync(CompStateValues syncFrom)
        {
            PowerLevel = syncFrom.PowerLevel;
            Online = syncFrom.Online;
            Overload = syncFrom.Overload;
            Message = syncFrom.Message;
            Heat = syncFrom.Heat;
            ShootOn = syncFrom.ShootOn;
            ClickShoot = syncFrom.ClickShoot;
            CurrentPlayerControl = syncFrom.CurrentPlayerControl;
            CurrentCharge = syncFrom.CurrentCharge;
            CurrentBlockGroup = syncFrom.CurrentBlockGroup;
            OtherPlayerTrackingReticle = syncFrom.OtherPlayerTrackingReticle;

            for (int i = 0; i < syncFrom.Weapons.Length; i++)
            {
                Weapons[i].ShotsFired = syncFrom.Weapons[i].ShotsFired;
                Weapons[i].ManualShoot = syncFrom.Weapons[i].ManualShoot;
                Weapons[i].SingleShotCounter = syncFrom.Weapons[i].SingleShotCounter;

                Weapons[i].Sync.Charging = syncFrom.Weapons[i].Sync.Charging;
                Weapons[i].Sync.CurrentAmmo = syncFrom.Weapons[i].Sync.CurrentAmmo;
                Weapons[i].Sync.CurrentCharge = syncFrom.Weapons[i].Sync.CurrentCharge;
                Weapons[i].Sync.CurrentMags = syncFrom.Weapons[i].Sync.CurrentMags;
                Weapons[i].Sync.Heat = syncFrom.Weapons[i].Sync.Heat;
                Weapons[i].Sync.Overheated = syncFrom.Weapons[i].Sync.Overheated;
                Weapons[i].Sync.Reloading = syncFrom.Weapons[i].Sync.Reloading;
            }
        }
    }

    [ProtoContract]
    public class CompSettingsValues
    {
        [ProtoMember(1)] public bool Guidance = true;
        [ProtoMember(2)] public int Overload = 1;
        [ProtoMember(3)] public long Modes;
        [ProtoMember(4)] public float DpsModifier = 1;
        [ProtoMember(5)] public float RofModifier = 1;
        [ProtoMember(6)] public WeaponSettingsValues[] Weapons;
        [ProtoMember(7)] public float Range = 100;
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

            if (Overload != syncFrom.Overload || RofModifier != syncFrom.RofModifier || DpsModifier != syncFrom.DpsModifier)
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
        [ProtoMember(1)] public int ShotsFired;
        [ProtoMember(2)] public ManualShootActionState ManualShoot = ManualShootActionState.ShootOff;
        [ProtoMember(3)] public int SingleShotCounter;
        [ProtoMember(4)] public WeaponSyncValues Sync;

    }

    [ProtoContract]
    public class WeaponSyncValues
    {
        [ProtoMember(1)] public float Heat;
        [ProtoMember(2)] public int CurrentAmmo;
        [ProtoMember(3)] public float CurrentCharge;
        [ProtoMember(4)] public bool Overheated;
        [ProtoMember(5)] public bool Reloading;
        [ProtoMember(6)] public bool Charging;
        [ProtoMember(7)] public int WeaponId;
        [ProtoMember(8)] public MyFixedPoint CurrentMags;

        public void SetState (WeaponSyncValues sync)
        {
            sync.Heat = Heat;
            sync.CurrentAmmo = CurrentAmmo;
            sync.CurrentMags = CurrentMags;
            sync.CurrentCharge = CurrentCharge;
            sync.Overheated = Overheated;
            sync.Reloading = Reloading;
            sync.Charging = Charging;
        }
    }

    [ProtoContract]
    public class PlayerControl
    {
        [ProtoMember(1), DefaultValue(-1)] public long PlayerId = -1;
        [ProtoMember(2)] public ControlType ControlType = ControlType.None;

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
    public class WeaponTimings
    {
        [ProtoMember(1)] public uint ChargeDelayTicks;
        [ProtoMember(2)] public uint ChargeUntilTick;
        [ProtoMember(3)] public uint AnimationDelayTick;
        [ProtoMember(4)] public uint OffDelay;
        [ProtoMember(5)] public uint ShootDelayTick;
        [ProtoMember(6)] public uint WeaponReadyTick;
        [ProtoMember(7)] public uint LastHeatUpdateTick;
        [ProtoMember(8)] public uint ReloadedTick;
        

        public WeaponTimings SyncOffsetServer(uint tick)
        {
            var offset = tick + Session.ServerTickOffset;

            return new WeaponTimings
            {
                ChargeDelayTicks = ChargeDelayTicks,
                AnimationDelayTick = AnimationDelayTick > offset ? AnimationDelayTick - offset : 0,
                ChargeUntilTick = ChargeUntilTick > offset ? ChargeUntilTick - offset : 0,
                OffDelay = OffDelay >= offset ? OffDelay - offset : 0,
                ShootDelayTick = ShootDelayTick > offset ? ShootDelayTick - offset : 0,
                WeaponReadyTick = WeaponReadyTick > offset ? WeaponReadyTick - offset : 0,
                LastHeatUpdateTick = tick - LastHeatUpdateTick > 20 ? 0 : (tick - LastHeatUpdateTick) - offset,
                ReloadedTick = ReloadedTick > offset ? ReloadedTick - offset : 0,
            };

        }

        public WeaponTimings SyncOffsetClient(uint tick)
        {
            return new WeaponTimings
            {
                ChargeDelayTicks = ChargeDelayTicks,
                AnimationDelayTick = AnimationDelayTick > 0 ? AnimationDelayTick + tick : 0,
                ChargeUntilTick = ChargeUntilTick > 0 ? ChargeUntilTick + tick : 0,
                OffDelay = OffDelay > 0 ? OffDelay + tick : 0,
                ShootDelayTick = ShootDelayTick > 0 ? ShootDelayTick + tick : 0,
                WeaponReadyTick = WeaponReadyTick > 0 ? WeaponReadyTick + tick : 0,
                ReloadedTick = ReloadedTick,
            };
        }

        public void Sync(WeaponTimings syncFrom)
        {
            ChargeDelayTicks = syncFrom.ChargeDelayTicks;
            ChargeUntilTick = syncFrom.ChargeUntilTick;
            AnimationDelayTick = syncFrom.AnimationDelayTick;
            OffDelay = syncFrom.OffDelay;
            ShootDelayTick = syncFrom.ShootDelayTick;
            WeaponReadyTick = syncFrom.WeaponReadyTick;
            LastHeatUpdateTick = syncFrom.LastHeatUpdateTick;
            ReloadedTick = syncFrom.ReloadedTick;
        }
    }

    [ProtoContract]
    public class WeaponValues
    {
        [ProtoMember(1)] public TransferTarget[] Targets;
        [ProtoMember(2)] public WeaponTimings[] Timings;
        [ProtoMember(3)] public WeaponRandomGenerator[] WeaponRandom;
        [ProtoMember(4)] public uint[] MIds;

        public void Save(WeaponComponent comp)
        {
            if (comp.MyCube?.Storage == null) return;

            var sv = new WeaponValues {Targets = Targets, WeaponRandom = WeaponRandom, MIds = comp.MIds, Timings = new WeaponTimings[comp.Platform.Weapons.Length]};

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];
                sv.Timings[w.WeaponId] = w.Timings.SyncOffsetServer(comp.Session.Tick);
            }

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
                    var timings = comp.WeaponValues.Timings;

                    for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                    {
                        var w = comp.Platform.Weapons[i];
                        if (comp.Session.IsServer)
                            timings[w.WeaponId] = new WeaponTimings();

                        var wTiming = comp.Session.IsServer ? timings[w.WeaponId] : timings[w.WeaponId].SyncOffsetClient(comp.Session.Tick);

                        var rand = comp.WeaponValues.WeaponRandom[w.WeaponId];
                        rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                        rand.TurretRandom = new Random(rand.CurrentSeed);

                        for (int j = 0; j < rand.TurretCurrentCounter; j++)
                            rand.TurretRandom.Next();

                        for (int j = 0; j < rand.ClientProjectileCurrentCounter; j++)
                            rand.ClientProjectileRandom.Next();

                        comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, wTiming, ref w.State.Sync, false); }, null, 1);
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
                Timings = new WeaponTimings[comp.Platform.Weapons.Length],
                WeaponRandom = new WeaponRandomGenerator[comp.Platform.Weapons.Length],
                MIds = new uint[Enum.GetValues(typeof(PacketType)).Length]
            };

            comp.MIds = comp.WeaponValues.MIds;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];

                comp.WeaponValues.Targets[w.WeaponId] = new TransferTarget();
                w.Timings = comp.WeaponValues.Timings[w.WeaponId] = new WeaponTimings();
                comp.WeaponValues.WeaponRandom[w.WeaponId] = new WeaponRandomGenerator();

                var rand = comp.WeaponValues.WeaponRandom[w.WeaponId];
                rand.CurrentSeed = Guid.NewGuid().GetHashCode();
                rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                rand.TurretRandom = new Random(rand.CurrentSeed);
                rand.AcquireRandom = new Random(rand.CurrentSeed);

                comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, w.Timings, ref w.State.Sync, false); }, null, 1);
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
        [ProtoMember(10)] public bool Meteors;
        [ProtoMember(11)] public bool Biologicals;
        [ProtoMember(12)] public bool Projectiles;

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
        public Random TurretRandom = new Random();
        public Random ClientProjectileRandom = new Random();
        public Random AcquireRandom = new Random();

        public enum RandomType
        {
            Deviation,
            ReAcquire,
            Acquire,
        }

        public WeaponRandomGenerator() { }

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
