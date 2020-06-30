using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Sync;
using VRage.Utils;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
using static WeaponCore.Support.WeaponComponent;

namespace WeaponCore
{
    [ProtoContract]
    public class AiDataValues
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public int Version = Session.VersionControl;
        [ProtoMember(3)] public readonly Dictionary<long, long> ControllingPlayers = new Dictionary<long, long>();
        [ProtoMember(4)] public readonly Dictionary<string, GroupInfo> BlockGroups = new Dictionary<string, GroupInfo>();
        [ProtoMember(5)] public readonly Focus Focus = new Focus();
        [ProtoMember(6)] public readonly ActiveTerminal ActiveTerminal = new ActiveTerminal();

        public bool Sync(GridAi ai, AiDataValues sync)
        {
            if (sync.Revision > Revision)
            {
                if (Focus.HasFocus != sync.Focus.HasFocus)
                    Log.Line($"HasFocus mismatch: {Focus.HasFocus}({sync.Focus.HasFocus})");

                Focus.Sync(sync.Focus);

                //if (ActiveTerminal.ActiveCubeId != sync.ActiveTerminal.ActiveCubeId)
                    //Log.Line($"ActiveCubeId mismatch: {ActiveTerminal.ActiveCubeId}({sync.ActiveTerminal.ActiveCubeId})");

                ActiveTerminal.Sync(sync.ActiveTerminal);

                if (ControllingPlayers.Count != sync.ControllingPlayers.Count)
                    Log.Line($"ControllingPlayers mismatch");

                ControllingPlayers.Clear();
                foreach (var s in sync.ControllingPlayers)
                    ControllingPlayers[s.Key] = s.Value;

                if (BlockGroups.Count != sync.BlockGroups.Count)
                    Log.Line($"BlockGroups mismatch");

                BlockGroups.Clear();
                foreach (var s in sync.BlockGroups)
                    BlockGroups[s.Key] = s.Value;

                Revision = sync.Revision;
                return true;
            }

            return false;
        }
    }

    [ProtoContract]
    public class ActiveTerminal
    {
        [ProtoMember(1)] internal long MyGridId;
        [ProtoMember(2)] internal long ActiveCubeId;
        [ProtoMember(3)] internal bool Active;

        internal void Sync(ActiveTerminal sync)
        {
            MyGridId = sync.MyGridId;
            ActiveCubeId = sync.ActiveCubeId;
            Active = sync.Active;
        }

        internal void Clean()
        {
            MyGridId = 0;
            ActiveCubeId = 0;
            Active = false;
        }
    }

    [ProtoContract]
    public class Focus
    {
        [ProtoMember(1)] internal readonly long[] Target = new long[2];
        [ProtoMember(2)] internal int ActiveId;
        [ProtoMember(3)] internal bool HasFocus;
        [ProtoMember(4)] internal double DistToNearestFocusSqr;


        internal void Sync(Focus sync)
        {
            Target[0] = sync.Target[0];
            Target[1] = sync.Target[1];
            ActiveId = sync.ActiveId;
            HasFocus = sync.HasFocus;
            DistToNearestFocusSqr = sync.DistToNearestFocusSqr;
        }

        internal void AddFocus(MyEntity target, GridAi ai, bool alreadySynced = false)
        {
            var session = ai.Session;
            Target[ActiveId] = target.EntityId;
            ai.TargetResetTick = session.Tick + 1;
            IsFocused(ai);
            if (session.MpActive && session.HandlesInput && !alreadySynced)
                session.SendFocusTargetUpdate(ai, target.EntityId);
        }

        internal bool GetPriorityTarget(out MyEntity target)
        {
            if (Target[ActiveId] > 0 && MyEntities.TryGetEntityById(Target[ActiveId], out target, true))
                return true;

            for (int i = 0; i < Target.Length; i++)
                if (MyEntities.TryGetEntityById(Target[i], out target, true)) return true;

            target = null;
            return false;
        }

        internal bool ReassignTarget(MyEntity target, int focusId, GridAi ai, bool alreadySynced = false)
        {
            if (focusId >= Target.Length || target == null || target.MarkedForClose) return false;
            Target[focusId] = target.EntityId;
            IsFocused(ai);
            if (ai.Session.MpActive && ai.Session.HandlesInput && !alreadySynced)
                ai.Session.SendReassignTargetUpdate(ai, target.EntityId, focusId);

            return true;
        }

        internal void NextActive(bool addSecondary, GridAi ai, bool alreadySynced = false)
        {
            var prevId = ActiveId;
            var newActiveId = prevId;
            if (newActiveId + 1 > Target.Length - 1) newActiveId -= 1;
            else newActiveId += 1;

            if (addSecondary && Target[newActiveId] <= 0) {
                Target[newActiveId] = Target[prevId];
                ActiveId = newActiveId;
            }
            else if (!addSecondary && Target[newActiveId] > 0)
                ActiveId = newActiveId;

            IsFocused(ai);

            if (ai.Session.MpActive && ai.Session.HandlesInput && !alreadySynced)
                ai.Session.SendNextActiveUpdate(ai, addSecondary);
        }

        internal void ReleaseActive(GridAi ai, bool alreadySynced = false)
        {
            Target[ActiveId] = -1;

            IsFocused(ai);

            if (ai.Session.MpActive && ai.Session.HandlesInput && !alreadySynced)
                ai.Session.SendReleaseActiveUpdate(ai);
        }

        internal bool IsFocused(GridAi ai)
        {
            HasFocus = false;
            for (int i = 0; i < Target.Length; i++) {

                if (Target[i] > 0) {

                    if (MyEntities.GetEntityById(Target[ActiveId]) != null)
                        HasFocus = true;
                    else
                        Target[i] = -1;
                }

                if (Target[0] <= 0 && HasFocus) {

                    Target[0] = Target[i];
                    Target[i] = -1;
                    ActiveId = 0;
                }
            }

            UpdateSubGrids(ai);

            return HasFocus;
        }


        internal void UpdateSubGrids(GridAi ai, bool resetTick = false)
        {
            foreach (var sub in ai.SubGrids) {

                if (ai.MyGrid == sub) continue;

                GridAi gridAi;
                if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi)) {

                    if (resetTick) gridAi.TargetResetTick = gridAi.Session.Tick + 1;
                    for (int i = 0; i < gridAi.Data.Repo.Focus.Target.Length; i++)
                    {
                        gridAi.Data.Repo.Focus.Target[i] = Target[i];
                        gridAi.Data.Repo.Focus.HasFocus = HasFocus;
                        gridAi.Data.Repo.Focus.ActiveId = ActiveId;
                    }
                }
            }
        }

        internal bool FocusInRange(Weapon w)
        {
            DistToNearestFocusSqr = float.MaxValue;
            for (int i = 0; i < Target.Length; i++) {
                if (Target[i] <= 0)
                    continue;

                MyEntity target;
                if (MyEntities.TryGetEntityById(Target[i], out target)) {
                    var sphere = target.PositionComp.WorldVolume;
                    var distSqr = MyUtils.GetSmallestDistanceToSphere(ref w.MyPivotPos, ref sphere);
                    distSqr *= distSqr;
                    if (distSqr < DistToNearestFocusSqr)
                        DistToNearestFocusSqr = distSqr;
                }

            }
            return DistToNearestFocusSqr <= w.MaxTargetDistanceSqr;
        }

        internal void Clean()
        {
            for (int i = 0; i < Target.Length; i++)
                Target[i] = -1;
        }
    }


    [ProtoContract]
    public class GroupInfo
    {
        [ProtoMember(1)] public readonly List<long> CompIds = new List<long>();

        [ProtoMember(2)] public readonly Dictionary<string, int> Settings = new Dictionary<string, int>()
        {
            {"Active", 1},
            {"Neutrals", 0},
            {"Projectiles", 0 },
            {"Biologicals", 0 },
            {"Meteors", 0 },
            {"Friendly", 0},
            {"Unowned", 0},
            {"TargetPainter", 0},
            {"ManualControl", 0},
            {"FocusTargets", 0},
            {"FocusSubSystem", 0},
            {"SubSystems", 0},
        };

        [ProtoMember(3)] internal string Name;
        [ProtoMember(4)] internal ChangeStates ChangeState;

        internal enum ChangeStates
        {
            None,
            Add,
            Modify
        }

        internal void RequestApplySettings(GridAi ai, string setting, int value, Session session)
        {
            if (session.IsServer)
            {
                Log.Line($"RequestApplySettings: Group:{Name} - setting:{setting} - value:{value}");
                Settings[setting] = value;
                ApplySettings(ai);

                if (session.MpActive) 
                    session.SendAiSync(ai);
            }
            else if (session.IsClient)
            {
                Log.Line($"RequestApplySettings: Group:{Name} - setting:{setting} - value:{value}");
                session.SendOverRidesClientAi(ai, Name, setting, value);
            }
        }

        internal void RequestSetValue(WeaponComponent comp, string setting, int value)
        {
            if (comp.Session.IsServer)
            {
                Log.Line($"RequestSetValue: Group:{Name} - setting:{setting} - value:{value}");
                SetValue(comp, setting, value);
            }
            else if (comp.Session.IsClient)
            {
                Log.Line($"RequestSetValue: Group:{Name} - setting:{setting} - value:{value}");
                comp.Session.SendOverRidesClientComp(comp, Name, setting, value);
            }
        }

        internal void ApplySettings(GridAi ai)
        {
            for (int i = 0; i < CompIds.Count; i++)
            {
                WeaponComponent comp;
                if (!ai.IdToCompMap.TryGetValue(CompIds[i], out comp))
                    continue;

                var o = comp.Data.Repo.Set.Overrides;
                var change = false;

                foreach (var setting in Settings)
                {

                    var v = setting.Value;
                    var enabled = v > 0;
                    switch (setting.Key)
                    {
                        case "Active":
                            if (!change && o.Activate != enabled) change = true;
                            o.Activate = enabled;
                            if (!comp.Session.IsClient && !o.Activate) ClearTargets(comp);
                            break;
                        case "SubSystems":
                            var blockType = (BlockTypes)v;
                            if (!change && o.SubSystem != blockType) change = true;
                            o.SubSystem = blockType;
                            break;
                        case "FocusSubSystem":
                            if (!change && o.FocusSubSystem != enabled) change = true;
                            o.FocusSubSystem = enabled;
                            break;
                        case "FocusTargets":
                            if (!change && o.FocusTargets != enabled) change = true;
                            o.FocusTargets = enabled;
                            break;
                        case "ManualControl":
                            if (!change && o.ManualControl != enabled) change = true;
                            o.ManualControl = enabled;
                            break;
                        case "TargetPainter":
                            if (!change && o.TargetPainter != enabled) change = true;
                            o.TargetPainter = enabled;
                            break;
                        case "Unowned":
                            if (!change && o.Unowned != enabled) change = true;
                            o.Unowned = enabled;
                            break;
                        case "Friendly":
                            if (!change && o.Friendly != enabled) change = true;
                            o.Friendly = enabled;
                            break;
                        case "Meteors":
                            if (!change && o.Meteors != enabled) change = true;
                            o.Meteors = enabled;
                            break;
                        case "Biologicals":
                            if (!change && o.Biologicals != enabled) change = true;
                            o.Biologicals = enabled;
                            break;
                        case "Projectiles":
                            if (!change && o.Projectiles != enabled) change = true;
                            o.Projectiles = enabled;
                            break;
                        case "Neutrals":
                            if (!change && o.Neutrals != enabled) change = true;
                            o.Neutrals = enabled;
                            break;
                    }
                }

                if (change)
                {
                    Log.Line($"ApplySettings change detected");
                    ResetCompState(comp, true);
                    if (comp.Session.MpActive)
                        comp.Session.SendCompData(comp);
                }
            }
        }

        internal void SetValue(WeaponComponent comp, string setting, int v)
        {
            var o = comp.Data.Repo.Set.Overrides;
            var enabled = v > 0;
            switch (setting)
            {

                case "Active":
                    o.Activate = enabled;
                    if (!comp.Session.IsClient && !o.Activate) ClearTargets(comp);
                    break;
                case "SubSystems":
                    o.SubSystem = (BlockTypes)v;
                    break;
                case "FocusSubSystem":
                    o.FocusSubSystem = enabled;
                    break;
                case "FocusTargets":
                    o.FocusTargets = enabled;
                    break;
                case "ManualControl":
                    o.ManualControl = enabled;
                    break;
                case "TargetPainter":
                    o.TargetPainter = enabled;
                    break;
                case "Unowned":
                    o.Unowned = enabled;
                    break;
                case "Friendly":
                    o.Friendly = enabled;
                    break;
                case "Meteors":
                    o.Meteors = enabled;
                    break;
                case "Biologicals":
                    o.Biologicals = enabled;
                    break;
                case "Projectiles":
                    o.Projectiles = enabled;
                    break;
                case "Neutrals":
                    o.Neutrals = enabled;
                    break;
            }

            ResetCompState(comp, false);

            if (comp.Session.MpActive)
            {
                Log.Line($"change group state and send compdata");
                comp.Session.SendCompData(comp);
            }
        }

        internal int GetCompSetting(string setting, WeaponComponent comp)
        {
            var value = 0;
            var o = comp.Data.Repo.Set.Overrides;
            switch (setting)
            {

                case "Active":
                    value = o.Activate ? 1 : 0;
                    break;
                case "SubSystems":
                    value = (int)o.SubSystem;
                    break;
                case "FocusSubSystem":
                    value = o.FocusSubSystem ? 1 : 0;
                    break;
                case "FocusTargets":
                    value = o.FocusTargets ? 1 : 0;
                    break;
                case "ManaulControl":
                    value = o.ManualControl ? 1 : 0;
                    break;
                case "TargetPainter":
                    value = o.TargetPainter ? 1 : 0;
                    break;
                case "Unowned":
                    value = o.Unowned ? 1 : 0;
                    break;
                case "Friendly":
                    value = o.Friendly ? 1 : 0;
                    break;
                case "Meteors":
                    value = o.Meteors ? 1 : 0;
                    break;
                case "Biologicals":
                    value = o.Biologicals ? 1 : 0;
                    break;
                case "Projectiles":
                    value = o.Projectiles ? 1 : 0;
                    break;
                case "Neutrals":
                    value = o.Neutrals ? 1 : 0;
                    break;
            }
            return value;
        }

        internal void ResetCompState(WeaponComponent comp, bool apply)
        {
            var o = comp.Data.Repo.Set.Overrides;
            var userControl = o.ManualControl || o.TargetPainter;

            if (userControl)
            {
                comp.Data.Repo.State.PlayerId = comp.Session.PlayerId;
                comp.Data.Repo.State.Control = CompStateValues.ControlMode.Ui;

                if (o.ManualControl)
                {
                    o.TargetPainter = false;
                    if (apply) Settings["TargetPainter"] = 0;
                }
                else
                {
                    o.ManualControl = false;
                    if (apply) Settings["ManualControl"] = 0;
                }
                comp.Data.Repo.Set.TerminalActionSetter(comp, ShootActions.ShootOff);
            }
            else
            {
                comp.Data.Repo.State.PlayerId = -1;
                comp.Data.Repo.State.Control = CompStateValues.ControlMode.None;
            }
        }

        private void ClearTargets(WeaponComponent comp)
        {
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {

                var weapon = comp.Platform.Weapons[i];
                if (weapon.Target.HasTarget)
                    comp.Platform.Weapons[i].Target.Reset(comp.Session.Tick, Target.States.Expired);
            }
        }
    }
}
