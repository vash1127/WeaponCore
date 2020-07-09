using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Sync;
using VRage.Utils;
using WeaponCore.Platform;
using WeaponCore.Support;


namespace WeaponCore
{
    [ProtoContract]
    public class AiGroupValues
    {
        [ProtoMember(1)] public readonly Dictionary<string, GroupInfo> BlockGroups = new Dictionary<string, GroupInfo>();

        public bool Sync(GridAi ai, AiGroupValues sync)
        {
            if (BlockGroups.Count != sync.BlockGroups.Count)
                Log.Line($"BlockGroups mismatch");

            BlockGroups.Clear();
            foreach (var s in sync.BlockGroups)
                BlockGroups[s.Key] = s.Value;
            return true;
        }
    }

    [ProtoContract]
    public class AiDataValues
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public int Version = Session.VersionControl;
        [ProtoMember(3)] public long ActiveTerminal;
        [ProtoMember(4)] public readonly Dictionary<long, long> ControllingPlayers = new Dictionary<long, long>();
        [ProtoMember(5)] public readonly Focus Focus = new Focus();

        public bool Sync(GridAi ai, AiDataValues sync)
        {
            if (sync.Revision > Revision)
            {
                ActiveTerminal = sync.ActiveTerminal;

                if (Focus.HasFocus != sync.Focus.HasFocus)
                    Log.Line($"HasFocus mismatch: {Focus.HasFocus}({sync.Focus.HasFocus})");

                Focus.Sync(sync.Focus);

                if (ControllingPlayers.Count != sync.ControllingPlayers.Count)
                    Log.Line($"ControllingPlayers mismatch: is:{sync.ControllingPlayers.Count} - was:{ControllingPlayers.Count}");

                ControllingPlayers.Clear();
                foreach (var s in sync.ControllingPlayers)
                    ControllingPlayers[s.Key] = s.Value;

                Revision = sync.Revision;
                return true;
            }

            return false;
        }
    }

    [ProtoContract]
    public class Focus
    {
        [ProtoMember(1)] internal readonly long[] Target = new long[2];
        [ProtoMember(2)] internal int ActiveId;
        [ProtoMember(3)] internal bool HasFocus;
        [ProtoMember(4)] internal float DistToNearestFocusSqr;


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
