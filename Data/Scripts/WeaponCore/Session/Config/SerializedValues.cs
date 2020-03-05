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
        [ProtoMember(1)] public uint MId;
        [ProtoMember(2), DefaultValue(-1)] public float PowerLevel;
        [ProtoMember(3)] public bool Online;
        [ProtoMember(4)] public bool Overload;
        [ProtoMember(5)] public bool Message;
        [ProtoMember(6)] public int Heat;
        [ProtoMember(7)] public WeaponStateValues[] Weapons;
        [ProtoMember(9)] public bool ShootOn;
        [ProtoMember(10)] public bool ClickShoot;
        [ProtoMember(11)] public PlayerControl CurrentPlayerControl;
        [ProtoMember(12)] public float CurrentCharge;
        [ProtoMember(13)] public int Version = Session.VersionControl;

        public void Sync(CompStateValues syncFrom)
        {
            MId = syncFrom.MId;
            PowerLevel = syncFrom.PowerLevel;
            Online = syncFrom.Online;
            Overload = syncFrom.Overload;
            Message = syncFrom.Message;
            Heat = syncFrom.Heat;
            ShootOn = syncFrom.ShootOn;
            ClickShoot = syncFrom.ClickShoot;
            CurrentPlayerControl = syncFrom.CurrentPlayerControl;
            CurrentCharge = syncFrom.CurrentCharge;


            for(int i = 0; i < syncFrom.Weapons.Length; i++)
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
                Weapons[i].Sync.WeaponId = syncFrom.Weapons[i].Sync.WeaponId;
            }
        }
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
        [ProtoMember(9)] public MyObjectBuilder_Inventory Inventory = null;
        [ProtoMember(10)] public CompGroupOverrides Overrides;
        [ProtoMember(11)] public int Version = Session.VersionControl;

        public CompSettingsValues()
        {
            Overrides = new CompGroupOverrides();
        }

        public void Sync(WeaponComponent comp, CompSettingsValues syncFrom)
        {
            MId = syncFrom.MId;
            Guidance = syncFrom.Guidance;
            Modes = syncFrom.Modes;
            
            Range = syncFrom.Range;
            Inventory = syncFrom.Inventory;
            Overrides = syncFrom.Overrides;

            var updateDPS = false;

            if (Overload != syncFrom.Overload || RofModifier != syncFrom.RofModifier || DpsModifier != syncFrom.DpsModifier)
            {
                Overload = syncFrom.Overload;
                RofModifier = syncFrom.RofModifier;
                updateDPS = true;
            }


            for (int i = 0; i < syncFrom.Weapons.Length; i++)
            {
                Weapons[i].Enable = syncFrom.Weapons[i].Enable;

                if (Weapons[i].AmmoTypeId != syncFrom.Weapons[i].AmmoTypeId)
                {
                    updateDPS = true;
                    var w = comp.Platform.Weapons[i];

                    w.ActiveAmmoDef = w.System.WeaponAmmoTypes[syncFrom.Weapons[i].AmmoTypeId].AmmoDef;
                }

                Weapons[i].AmmoTypeId = syncFrom.Weapons[i].AmmoTypeId;
            }

            if(updateDPS)
                WepUi.SetDps(comp, syncFrom.DpsModifier, true);

        }

    }

    [ProtoContract]
    public class WeaponStateValues
    {
        [ProtoMember(1)] public int ShotsFired;
        [ProtoMember(2)] public TerminalActionState ManualShoot = TerminalActionState.ShootOff;
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
                LastHeatUpdateTick = tick - LastHeatUpdateTick > 20 ? 0 : (tick - LastHeatUpdateTick) - offset,
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

            if (comp.MyCube?.Storage == null) return;

            var sv = new WeaponValues {Targets = Targets, Timings = new WeaponTimings[comp.Platform.Weapons.Length]};

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var wid = comp.Platform.Weapons[i].WeaponId;
                sv.Timings[wid] = new WeaponTimings();
                var timings = Timings[wid];

                sv.Timings[wid] = timings.SyncOffsetServer(comp.Session.Tick);
            }

            var binary = MyAPIGateway.Utilities.SerializeToBinary(sv);
            comp.MyCube.Storage[id] = Convert.ToBase64String(binary);

        }

        public static void Load(WeaponComponent comp)
        {
            string rawData;
            if (comp.Session.IsClient && comp.MyCube.Storage.TryGetValue(comp.Session.MpTargetSyncGuid, out rawData))
            {
                var base64 = Convert.FromBase64String(rawData);
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
                comp.WeaponValues = new WeaponValues
                {
                    Targets = new TransferTarget[comp.Platform.Weapons.Length],
                    Timings = new WeaponTimings[comp.Platform.Weapons.Length]
                };
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

    /*
    [ProtoContract]
    public class GridAIValues
    {
        [ProtoMember(1)] public FakeTarget DummyTarget = new FakeTarget();
        [ProtoMember(2)] public PlayerToBlock[] ControllingPlayersStorage = new PlayerToBlock[0];

        public Dictionary<long, MyCubeBlock> ControllingPlayers = new Dictionary<long, MyCubeBlock>();

        Session _session;
        MyCubeGrid grid;

        public GridAIValues() { }

        public void Save()
        {
            if (_session.IsClient || !_session.MpActive) return;

            var binary = MyAPIGateway.Utilities.SerializeToBinary(this);

            if (grid.Storage != null)
                grid.Storage[_session.GridAiGuid] = Convert.ToBase64String(binary);
            else
            {
                grid.Storage = new MyModStorageComponent();
                grid.Storage[_session.GridAiGuid] = Convert.ToBase64String(binary);
            }
        }

        public void Load(GridAi ai)
        {
            
            _session = ai.Session;

            string rawData;
            byte[] base64;
            if (_session.IsClient && grid.Storage != null && grid.Storage.TryGetValue(_session.GridAiGuid, out rawData))
            {
                base64 = Convert.FromBase64String(rawData);
                ai.AIValues = MyAPIGateway.Utilities.SerializeFromBinary<GridAIValues>(base64);

                for (int i = 0; i < ControllingPlayersStorage.Length; i++)
                {
                    var playerBlock = ControllingPlayersStorage[i];

                    var block = MyEntities.GetEntityByIdOrDefault(playerBlock.EntityId) as MyCubeBlock;
                    if (block == null) continue;


                    Log.Line($"Player: {playerBlock.playerId} EntityID: {playerBlock.EntityId}");

                    ControllingPlayers.Add(playerBlock.playerId, block);
                }

            }
            else
            {
                if(grid.Storage == null)
                    grid.Storage = new MyModStorageComponent();

                grid.Storage

                ai.AIValues = new GridAIValues();
            }
        }
    }*/

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

        public CompGroupOverrides() { }

        public void Sync(CompGroupOverrides syncFrom)
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
}
