using System;
using System.ComponentModel;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.TargetingDefinition;

namespace WeaponCore
{


    [ProtoContract]
    public class CompStateValues
    {
        [ProtoMember(1)] public uint MId;
        [ProtoMember(2), DefaultValue(-1)] public float PowerLevel;
        [ProtoMember(3)] public bool Online;
        [ProtoMember(4)] public bool Overload;
        [ProtoMember(5)] public bool Message;
        [ProtoMember(6)] public int Heat;
        [ProtoMember(7)] public WeaponStateValues[] Weapons;
        [ProtoMember(8), DefaultValue(-1)] public long PlayerIdInTerminal = -1;
        [ProtoMember(9)] public bool ShootOn;
        [ProtoMember(10)] public bool ClickShoot;
        [ProtoMember(11)] public PlayerControl CurrentPlayerControl;
        [ProtoMember(12)] public float CurrentCharge;

    }

    [ProtoContract]
    public class CompSettingsValues
    {
        [ProtoMember(1)] public uint MId;
        [ProtoMember(2)] public bool Guidance = true;
        [ProtoMember(3)] public int Overload = 1;
        [ProtoMember(4)] public long Modes;
        [ProtoMember(5)] public float DpsModifier = 1;
        [ProtoMember(6)] public float RofModifier = 1;
        [ProtoMember(7)] public WeaponSettingsValues[] Weapons;
        [ProtoMember(8)] public float Range = 100;
        [ProtoMember(9)] internal MyObjectBuilder_Inventory Inventory = null;
        [ProtoMember(10)] public CompGroupOverrides Overrides;

        public CompSettingsValues()
        {
            Overrides = new CompGroupOverrides();
        }

    }

    [ProtoContract]
    public class WeaponStateValues
    {
        [ProtoMember(1)] public float Heat;
        [ProtoMember(2)] public int CurrentAmmo;
        [ProtoMember(3)] public MyFixedPoint CurrentMags;
        [ProtoMember(4)] public int ShotsFired;
        [ProtoMember(5)] public TerminalActionState ManualShoot = TerminalActionState.ShootOff;
        [ProtoMember(6)] public int SingleShotCounter;
        [ProtoMember(7)] public float CurrentCharge;
        [ProtoMember(8)] public bool Overheated;
        [ProtoMember(9)] public bool Reloading;
        [ProtoMember(10)] public bool Charging;

    }

    [ProtoContract]
    public struct WeaponSyncValues
    {
        [ProtoMember(1)] public float Heat;
        [ProtoMember(2)] public int CurrentAmmo;
        [ProtoMember(3)] public float CurrentCharge;
        [ProtoMember(4)] public bool Overheated;
        [ProtoMember(5)] public bool Reloading;
        [ProtoMember(6)] public bool Charging;
        [ProtoMember(7)] public int WeaponId;
        [ProtoMember(8)] public MyFixedPoint currentMags;

        public void SetState (WeaponStateValues wState)
        {
            wState.Heat = Heat;
            wState.CurrentAmmo = CurrentAmmo;
            wState.CurrentMags = currentMags;
            wState.CurrentCharge = CurrentCharge;
            wState.Overheated = Overheated;
            wState.Reloading = Reloading;
            wState.Charging = Charging;
        }
    }

    [ProtoContract]
    public class PlayerControl
    {
        [ProtoMember(1), DefaultValue(-1)] public long PlayerId = -1;
        [ProtoMember(2)] public ControlType CurrentControlType;
    }
    
    public enum ControlType
    {
        UI,
        Toolbar,
        None
    }

    [ProtoContract]
    public class WeaponSettingsValues
    {
        [ProtoMember(1)] public bool Enable = true;
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

        public uint Offset = 4;

        public WeaponTimings SyncOffsetServer(uint tick)
        {
            var offset = tick + Offset;

            return new WeaponTimings
            {
                ChargeDelayTicks = ChargeDelayTicks,
                AnimationDelayTick = AnimationDelayTick > tick ? AnimationDelayTick >= offset ? AnimationDelayTick - offset : 0 : 0,
                ChargeUntilTick = ChargeUntilTick > tick ? ChargeUntilTick >= offset ? ChargeUntilTick - offset : 0 : 0,
                OffDelay = OffDelay > tick ? OffDelay >= offset ? OffDelay - offset : 0 : 0,
                ShootDelayTick = ShootDelayTick > tick ? ShootDelayTick >= offset ? ShootDelayTick - offset : 0 : 0,
                WeaponReadyTick = WeaponReadyTick > tick ? WeaponReadyTick >= offset ? WeaponReadyTick - offset : 0 : 0,
                LastHeatUpdateTick = tick - LastHeatUpdateTick > 20 ? 0 : (tick - LastHeatUpdateTick) - offset >= 0 ? (tick - LastHeatUpdateTick) - offset : 0,
                ReloadedTick = ReloadedTick > tick ? ReloadedTick > offset ? ReloadedTick - offset : 0 : 0,
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
    }

    [ProtoContract]
    public class WeaponValues
    {
        [ProtoMember(1)] public TransferTarget[] Targets;
        [ProtoMember(2)] public WeaponTimings[] Timings;

        public void Save(WeaponComponent comp, Guid id)
        {
            if (!comp.Session.MpActive) return;

            if (comp.MyCube == null || comp.MyCube.Storage == null) return;

            var sv = new WeaponValues();
            sv.Targets = Targets;
            sv.Timings = new WeaponTimings[comp.Platform.Weapons.Length];

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var wid = comp.Platform.Weapons[i].WeaponId;
                sv.Timings[wid] = new WeaponTimings();
                var timings = Timings[wid];

                sv.Timings[wid] = timings.SyncOffsetServer(comp.Session.Tick);
            }

            var binary = MyAPIGateway.Utilities.SerializeToBinary(this);
            comp.MyCube.Storage[id] = Convert.ToBase64String(binary);

        }

        public static void Load(WeaponComponent comp, Guid id)
        {
            string rawData;
            byte[] base64;
            if (comp.Session.IsClient && comp.MyCube.Storage.TryGetValue(id, out rawData))
            {
                base64 = Convert.FromBase64String(rawData);
                comp.WeaponValues = MyAPIGateway.Utilities.SerializeFromBinary<WeaponValues>(base64);

                var timings = comp.WeaponValues.Timings;

                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];
                    var values = new WeaponSyncValues();
                    var wTiming = timings[w.WeaponId].SyncOffsetClient(comp.Session.Tick);

                    comp.Session.SyncWeapon(w, wTiming, ref values, false);
                }

            }
            else
            {
                comp.WeaponValues = new WeaponValues();
                comp.WeaponValues.Targets = new TransferTarget[comp.Platform.Weapons.Length];
                comp.WeaponValues.Timings = new WeaponTimings[comp.Platform.Weapons.Length];
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];

                    comp.WeaponValues.Targets[w.WeaponId] = new TransferTarget();
                    w.Timings = comp.WeaponValues.Timings[w.WeaponId] = new WeaponTimings();
                }
            }
        }
        public WeaponValues() { }
    }

    [ProtoContract]
    public class CompGroupOverrides
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
    }
}
