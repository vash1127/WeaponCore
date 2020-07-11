using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Utils;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
using static WeaponCore.Support.WeaponComponent;
using static WeaponCore.Support.GridAi;
namespace WeaponCore
{
    [ProtoContract]
    public class ConstructDataValues
    {
        [ProtoMember(1)] public readonly Dictionary<string, GroupInfo> BlockGroups = new Dictionary<string, GroupInfo>();
        [ProtoMember(2)] public readonly Focus Focus = new Focus();

        public bool Sync(ConstructDataValues sync)
        {
            Focus.Sync(sync.Focus);
            BlockGroups.Clear();
            foreach (var s in sync.BlockGroups)
                BlockGroups[s.Key] = s.Value;
            return true;
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

        internal void RequestApplySettings(GridAi ai, string setting, int value, Session session, long playerId)
        {
            if (session.IsServer)
            {
                Log.Line($"RequestApplySettings: Group:{Name} - setting:{setting} - value:{value}");
                Settings[setting] = value;
                ApplySettings(ai, playerId);

                if (session.MpActive) 
                    session.SendAiData(ai);
            }
            else if (session.IsClient)
            {
                Log.Line($"RequestApplySettings: Group:{Name} - setting:{setting} - value:{value}");
                session.SendOverRidesClientAi(ai, Name, setting, value);
            }
        }

        internal void RequestSetValue(WeaponComponent comp, string setting, int value, long playerId)
        {
            if (comp.Session.IsServer)
            {
                Log.Line($"RequestSetValue: Group:{Name} - setting:{setting} - value:{value}");
                SetValue(comp, setting, value, playerId);
            }
            else if (comp.Session.IsClient)
            {
                Log.Line($"RequestSetValue: Group:{Name} - setting:{setting} - value:{value}");
                comp.Session.SendOverRidesClientComp(comp, Name, setting, value);
            }
        }

        internal void ApplySettings(GridAi ai, long playerId)
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
                    ResetCompState(comp, true, playerId);
                    if (comp.Session.MpActive)
                        comp.Session.SendCompData(comp);
                }
            }
        }

        internal void SetValue(WeaponComponent comp, string setting, int v, long playerId)
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

            ResetCompState(comp, false, playerId);

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

        internal void ResetCompState(WeaponComponent comp, bool apply, long playerId)
        {
            var o = comp.Data.Repo.Set.Overrides;
            var userControl = o.ManualControl || o.TargetPainter;

            if (userControl)
            {
                comp.Data.Repo.State.PlayerId = playerId;
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
                comp.Data.Repo.State.TerminalActionSetter(comp, ShootActions.ShootOff);
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
    [ProtoContract]
    public class Focus
    {
        [ProtoMember(1)] internal uint Revision;
        [ProtoMember(2)] internal readonly long[] Target = new long[2];
        [ProtoMember(3)] internal int ActiveId;
        [ProtoMember(4)] internal bool HasFocus;
        [ProtoMember(5)] internal float DistToNearestFocusSqr;

        internal void Sync(Focus sync)
        {
            if (sync.Revision > Revision)
            {
                Revision = sync.Revision;
                Target[0] = sync.Target[0];
                Target[1] = sync.Target[1];
                ActiveId = sync.ActiveId;
                HasFocus = sync.HasFocus;
                DistToNearestFocusSqr = sync.DistToNearestFocusSqr;
            }
        }

        internal void ServerAddFocus(MyEntity target, GridAi ai)
        {
            var session = ai.Session;
            Target[ActiveId] = target.EntityId;
            ai.TargetResetTick = session.Tick + 1;
            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(Constructs.UpdateType.Focus);
        }

        internal void RequestAddFocus(MyEntity target, GridAi ai)
        {
            if (ai.Session.IsServer)
                ServerAddFocus(target, ai);
            else
                ai.Session.SendFocusTargetUpdate(ai, target.EntityId);
        }

        internal void ServerNextActive(bool addSecondary, GridAi ai)
        {
            var prevId = ActiveId;
            var newActiveId = prevId;
            if (newActiveId + 1 > Target.Length - 1) newActiveId -= 1;
            else newActiveId += 1;

            if (addSecondary && Target[newActiveId] <= 0)
            {
                Target[newActiveId] = Target[prevId];
                ActiveId = newActiveId;
            }
            else if (!addSecondary && Target[newActiveId] > 0)
                ActiveId = newActiveId;

            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(Constructs.UpdateType.Focus);

        }

        internal void RequestNextActive(bool addSecondary, GridAi ai)
        {
            if (ai.Session.IsServer)

                ServerNextActive(addSecondary, ai);
            else
                ai.Session.SendNextActiveUpdate(ai, addSecondary);
        }

        internal void ServerReleaseActive(GridAi ai)
        {
            Target[ActiveId] = -1;

            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(Constructs.UpdateType.Focus);
        }

        internal void RequestReleaseActive(GridAi ai)
        {
            if (ai.Session.IsServer)
                ServerReleaseActive(ai);
            else
                ai.Session.SendReleaseActiveUpdate(ai);

        }

        internal bool ServerIsFocused(GridAi ai)
        {
            HasFocus = false;
            for (int i = 0; i < Target.Length; i++)
            {

                if (Target[i] > 0)
                {

                    if (MyEntities.GetEntityById(Target[ActiveId]) != null)
                        HasFocus = true;
                    else
                        Target[i] = -1;
                }

                if (Target[0] <= 0 && HasFocus)
                {

                    Target[0] = Target[i];
                    Target[i] = -1;
                    ActiveId = 0;
                }
            }

            //UpdateSubGrids(ai);

            return HasFocus;
        }

        /*
        internal void UpdateSubGrids(GridAi ai, bool resetTick = false)
        {
            foreach (var sub in ai.SubGrids)
            {

                if (ai.MyGrid == sub) continue;

                GridAi gridAi;
                if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                {

                    if (resetTick) gridAi.TargetResetTick = gridAi.Session.Tick + 1;
                    for (int i = 0; i < gridAi.Construct.Data.Repo.Focus.Target.Length; i++)
                    {
                        gridAi.Construct.Data.Repo.Focus.Target[i] = Target[i];
                        gridAi.Construct.Data.Repo.Focus.HasFocus = HasFocus;
                        gridAi.Construct.Data.Repo.Focus.ActiveId = ActiveId;
                    }
                }
            }
        }
        */

        internal bool ClientIsFocused(GridAi ai)
        {
            if (ai.Session.IsServer)
                return ServerIsFocused(ai);

            bool focus = false;
            for (int i = 0; i < Target.Length; i++)
            {

                if (Target[i] > 0)
                    if (MyEntities.GetEntityById(Target[ActiveId]) != null)
                        focus = true;
            }

            return focus;
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

        internal void ReassignTarget(MyEntity target, int focusId, GridAi ai)
        {
            if (focusId >= Target.Length || target == null || target.MarkedForClose) return;
            Target[focusId] = target.EntityId;
            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(Constructs.UpdateType.Focus);
        }

        internal bool FocusInRange(Weapon w)
        {
            DistToNearestFocusSqr = float.MaxValue;
            for (int i = 0; i < Target.Length; i++)
            {
                if (Target[i] <= 0)
                    continue;

                MyEntity target;
                if (MyEntities.TryGetEntityById(Target[i], out target))
                {
                    var sphere = target.PositionComp.WorldVolume;
                    var distSqr = (float)MyUtils.GetSmallestDistanceToSphere(ref w.MyPivotPos, ref sphere);
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
}
