using System;
using System.ComponentModel;
using ProtoBuf;
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

        public void Sync(WeaponComponent comp, CompDataValues sync)
        {
            if (sync.Revision > Revision)
            {
                Revision = sync.Revision;
                Set.Sync(comp, sync.Set);
                State.Sync(comp, sync.State, CompStateValues.Caller.CompData);

                for (int i = 0; i < Targets.Length; i++) {
                    var w = comp.Platform.Weapons[i];
                    sync.Targets[i].SyncTarget(w);
                }

                var s = comp.Session;
                s.Wheel.Dirty = true;
                if (s.Wheel.WheelActive && string.IsNullOrEmpty(s.Wheel.ActiveGroupName))
                    s.Wheel.ForceUpdate();
            }
            else Log.Line($"CompDataValues older revision");

        }

        public void ResetToFreshLoadState()
        {
            Set.Overrides.TargetPainter = false;
            Set.Overrides.ManualControl = false;
            State.Control = CompStateValues.ControlMode.None;
            State.PlayerId = -1;
            State.TrackingReticle = false;
            State.TerminalAction = ShootActions.ShootOff;
            foreach (var w in State.Weapons)
            {
                w.Heat = 0;
                w.Overheated = false;
                w.Action = ShootActions.ShootOff;
            }
            ResetCompDataRevisions();
        }

        public void UpdateCompDataPacketInfo(WeaponComponent comp, PacketType type)
        {
            ++Revision;
            ++State.Revision;
            if (type == PacketType.CompData)
                comp.Session.PrunedPacketsToClient.Remove(comp.Data.Repo.State);

            for (int i = 0; i < Targets.Length; i++)
            {
                var t = Targets[i];
                var ws = State.Weapons[i];
                
                if (type == PacketType.CompData) {
                    comp.Session.PrunedPacketsToClient.Remove(t);
                    comp.Session.PrunedPacketsToClient.Remove(ws);
                }
                else if (type == PacketType.CompState)
                    comp.Session.PrunedPacketsToClient.Remove(ws);

                ++t.Revision;
                t.WeaponRandom.ReInitRandom();
                ++ws.Revision;
            }
        }

        public void ResetCompDataRevisions()
        {
            Revision = 0;
            State.Revision = 0;
            for (int i = 0; i < Targets.Length; i++)  {
                Targets[i].Revision = 0;
                State.Weapons[i].Revision = 0;
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
            SetRange(comp);

            Overrides.Sync(sync.Overrides);

            if (Overload != sync.Overload || Math.Abs(RofModifier - sync.RofModifier) > 0.0001f || Math.Abs(DpsModifier - sync.DpsModifier) > 0.0001f) {
                Overload = sync.Overload;
                RofModifier = sync.RofModifier;
                DpsModifier = sync.DpsModifier;
                SetRof(comp);
            }
        }

    }

    [ProtoContract]
    public class CompStateValues
    {
        public enum Caller
        {
            Direct,
            CompData,
        }

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

        public void Sync(WeaponComponent comp, CompStateValues sync, Caller caller)
        {
            if (sync.Revision > Revision)
            {
                Revision = sync.Revision;
                TrackingReticle = sync.TrackingReticle;
                PlayerId = sync.PlayerId;
                Control = sync.Control;
                TerminalAction = sync.TerminalAction;
                for (int i = 0; i < sync.Weapons.Length; i++) {
                    var w = comp.Platform.Weapons[i];
                    w.State.Sync(w, sync.Weapons[i]);
                }
            }
            else Log.Line($"CompStateValues older revision: {sync.Revision} > {Revision} - caller:{caller}");
        }

        public void TerminalActionSetter(WeaponComponent comp, ShootActions action)
        {
            TerminalAction = action;
            for (int i = 0; i < Weapons.Length; i++)
                Weapons[i].WeaponMode(comp, action, true);
        }

    }

    [ProtoContract]
    public class WeaponStateValues
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public float Heat; // don't save
        [ProtoMember(3)] public bool Overheated; //don't save
        [ProtoMember(4)] public int CurrentAmmo; //save
        [ProtoMember(5)] public float CurrentCharge; //save
        [ProtoMember(6)] public MyFixedPoint CurrentMags; // save
        [ProtoMember(7)] public int AmmoTypeId; //save
        [ProtoMember(8)] public int ReloadId; //save
        [ProtoMember(9), DefaultValue(ShootActions.ShootOff)] public ShootActions Action = ShootActions.ShootOff; // save

        public void Sync(Weapon w, WeaponStateValues sync)
        {
            if (sync.Revision > Revision)
            {
                Revision = sync.Revision;
                ReloadId = sync.ReloadId;
                CurrentCharge = sync.CurrentCharge;
                CurrentMags = sync.CurrentMags;
                Heat = sync.Heat;
                Overheated = sync.Overheated;
                AmmoTypeId = sync.AmmoTypeId;
                Action = sync.Action;
                
                if (w.ClientSimShots != 0) {

                    if (w.ClientReloadId != ReloadId) {

                        if (ReloadId - w.ClientReloadId > 1)
                            Log.Line($"ReloadId skipped: {ReloadId - w.ClientReloadId}");

                        var simLoss = w.ClientSimShots - CurrentAmmo;
                        CurrentAmmo = sync.CurrentAmmo - simLoss;
                        w.ClientSimShots = 0;

                        Log.Line($"simLoss: {simLoss} - newAmount:{CurrentAmmo} - wasAmount:{sync.CurrentAmmo}");
                    }
                }
                else 
                    CurrentAmmo = sync.CurrentAmmo;
                

                w.ChangeActiveAmmo();
                if (CurrentAmmo == 0) 
                    w.ClientReload(true);
            }
            else Log.Line($"WeaponStateValues older revision:  {sync.Revision}  > {Revision}");
        }

        public void WeaponMode(WeaponComponent comp, ShootActions action, bool calledByTerminal = false)
        {
            if (!calledByTerminal)
                comp.Data.Repo.State.TerminalAction = ShootActions.ShootOff;

            Action = action;
            if (comp.Session.MpActive && comp.Session.IsServer)
                comp.Session.SendCompState(comp);
        }

    }

    [ProtoContract]
    public class TransferTarget
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public long EntityId;
        [ProtoMember(3)] public Vector3 TargetPos;
        [ProtoMember(4)] public int WeaponId;
        [ProtoMember(5)] public WeaponRandomGenerator WeaponRandom; // save

        internal void SyncTarget(Weapon w)
        {
            if (Revision > w.TargetData.Revision)
            {

                w.TargetData.Revision = Revision;
                w.TargetData.EntityId = EntityId;
                w.TargetData.TargetPos = TargetPos;
                w.WeaponId = WeaponId;
                w.TargetData.WeaponRandom.Sync(WeaponRandom);

                var target = w.Target;
                target.IsProjectile = EntityId == -1;
                target.IsFakeTarget = EntityId == -2;
                target.TargetPos = TargetPos;
                target.ClientDirty = true;
            }
            else Log.Line($"TransferTarget older revision:  {Revision}  > {w.TargetData.Revision}");
        }

        public void WeaponInit(Weapon w)
        {
            WeaponRandom.Init(w.UniqueId);

            var rand = WeaponRandom;
            rand.CurrentSeed = w.UniqueId;
            rand.ClientProjectileRandom = new Random(rand.CurrentSeed);

            rand.TurretRandom = new Random(rand.CurrentSeed);
            rand.AcquireRandom = new Random(rand.CurrentSeed);
        }

        public void WeaponRefreshClient(Weapon w)
        {
            try
            {
                var rand = WeaponRandom;

                rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                rand.TurretRandom = new Random(rand.CurrentSeed);
                for (int j = 0; j < rand.TurretCurrentCounter; j++)
                    rand.TurretRandom.Next();

                for (int j = 0; j < rand.ClientProjectileCurrentCounter; j++)
                    rand.ClientProjectileRandom.Next();

                return;
            }
            catch (Exception e) { Log.Line($"Client Weapon Values Failed To load re-initing... how?"); }

            WeaponInit(w);
        }
        internal void ClearTarget()
        {
            ++Revision;
            EntityId = 0;
            TargetPos = Vector3.Zero;
        }

        public TransferTarget() { }
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
    }
}
