using System.Collections.Generic;
using ProtoBuf;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
using static WeaponCore.Support.WeaponComponent;
using static WeaponCore.Support.GridAi;
namespace WeaponCore
{
    [ProtoContract]
    public class ConstructDataValues
    {
        [ProtoMember(1)] public int Version = Session.VersionControl;
        [ProtoMember(2)] public readonly Dictionary<string, GroupInfo> BlockGroups = new Dictionary<string, GroupInfo>();
        [ProtoMember(3)] public FocusData FocusData;

        public bool Sync(Constructs construct, ConstructDataValues sync, bool localCall = false)
        {
            FocusData.Sync(construct.RootAi, sync.FocusData, localCall);
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
            {"MaxSize", 16384},
            {"MinSize", 0},
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

        public readonly Dictionary<string, int> DefaultSettings = new Dictionary<string, int>()
        {
            {"MaxSize", 16384}, {"MinSize", 0}, {"Neutrals", 0},  {"Projectiles", 0 },  {"Biologicals", 0 },  {"Meteors", 0 },  {"Friendly", 0},  {"Unowned", 0},  {"TargetPainter", 0},  {"ManualControl", 0},  {"FocusTargets", 0},  {"FocusSubSystem", 0},  {"SubSystems", 0},
        };

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
                Settings[setting] = value;
                ApplySettings(ai, playerId);

                if (session.MpActive) 
                    session.SendAiData(ai);
            }
            else if (session.IsClient)
            {
                session.SendOverRidesClientAi(ai, Name, setting, value);
            }
        }

        internal void RequestSetValue(WeaponComponent comp, string setting, int value, long playerId)
        {
            if (comp.Session.IsServer)
            {
                SetValue(comp, setting, value, playerId);
            }
            else if (comp.Session.IsClient)
            {
                comp.Session.SendOverRidesClientComp(comp, setting, value);
            }
        }

        internal void ApplySettings(GridAi ai, long playerId)
        {
            for (int i = 0; i < CompIds.Count; i++)
            {
                WeaponComponent comp;
                if (!ai.Session.IdToCompMap.TryGetValue(CompIds[i], out comp))
                    continue;

                var o = comp.Data.Repo.Base.Set.Overrides;
                var change = false;

                foreach (var setting in Settings)
                {

                    var v = setting.Value;
                    var enabled = v > 0;
                    switch (setting.Key)
                    {
                        case "MaxSize":
                            var maxSize = v;
                            if (!change && o.MaxSize != maxSize) change = true;
                            o.MaxSize = maxSize;
                            break;
                        case "MinSize":
                            var minSize = v;
                            if (!change && o.MinSize != minSize) change = true;
                            o.MinSize = minSize;
                            break;
                        case "SubSystems":
                            var blockType = (BlockTypes)v;
                            if (!change && o.SubSystem != blockType) change = true;
                            o.SubSystem = blockType;
                            break;
                        case "MovementModes":
                            var moveType = (GroupOverrides.MoveModes)v;
                            if (!change && o.MoveMode != moveType) change = true;
                            o.MoveMode = moveType;
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
                    ResetCompState(comp, playerId, false, Settings);
                    if (comp.Session.MpActive)
                        comp.Session.SendCompBaseData(comp);
                }
            }
        }

        internal static void SetValue(WeaponComponent comp, string setting, int v, long playerId)
        {
            var o = comp.Data.Repo.Base.Set.Overrides;
            var enabled = v > 0;
            var clearTargets = false;

            switch (setting)
            {
                case "MaxSize":
                    o.MaxSize = v;
                    break;
                case "MinSize":
                    o.MinSize = v;
                    break;
                case "SubSystems":
                    o.SubSystem = (BlockTypes)v;
                    break;
                case "MovementModes":
                    o.MoveMode = (GroupOverrides.MoveModes)v;
                    clearTargets = true;
                    break;
                case "FocusSubSystem":
                    o.FocusSubSystem = enabled;
                    break;
                case "FocusTargets":
                    o.FocusTargets = enabled;
                    clearTargets = true;
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
                    clearTargets = true;
                    break;
                case "Meteors":
                    o.Meteors = enabled;
                    break;
                case "Biologicals":
                    o.Biologicals = enabled;
                    break;
                case "Projectiles":
                    o.Projectiles = enabled;
                    clearTargets = true;
                    break;
                case "Neutrals":
                    o.Neutrals = enabled;
                    clearTargets = true;
                    break;
            }

            ResetCompState(comp, playerId, clearTargets);

            if (comp.Session.MpActive)
                comp.Session.SendCompBaseData(comp);
        }


        internal static void ResetCompState(WeaponComponent comp, long playerId, bool resetTarget, Dictionary<string, int> settings = null)
        {
            var o = comp.Data.Repo.Base.Set.Overrides;
            var userControl = o.ManualControl || o.TargetPainter;
            
            if (userControl)
            {
                comp.Data.Repo.Base.State.PlayerId = playerId;
                comp.Data.Repo.Base.State.Control = CompStateValues.ControlMode.Ui;
                if (o.ManualControl)
                {
                    o.TargetPainter = false;
                    if (settings != null) settings["TargetPainter"] = 0;
                }
                else
                {
                    o.ManualControl = false;
                    if (settings != null) settings["ManualControl"] = 0;
                }
                comp.Data.Repo.Base.State.TerminalActionSetter(comp, ShootActions.ShootOff);
            }
            else
            {
                comp.Data.Repo.Base.State.PlayerId = -1;
                comp.Data.Repo.Base.State.Control = CompStateValues.ControlMode.None;
            }

            if (resetTarget)
                ClearTargets(comp);
        }

        private static void ClearTargets(WeaponComponent comp)
        {
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var weapon = comp.Platform.Weapons[i];
                if (weapon.Target.HasTarget)
                    comp.Platform.Weapons[i].Target.Reset(comp.Session.Tick, Target.States.ControlReset);
            }
        }

        internal void Clean()
        {
            Name = null;
            ChangeState = ChangeStates.None;
            CompIds.Clear();
            foreach (var s in DefaultSettings)
                Settings[s.Key] = s.Value;
        }
    }

    [ProtoContract]
    public class FocusData
    {
        public enum LockModes
        {
            None,
            Locked,
            ExclusiveLock,
        }

        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public long[] Target;
        [ProtoMember(3)] public int ActiveId;
        [ProtoMember(4)] public bool HasFocus;
        [ProtoMember(5)] public float DistToNearestFocusSqr;
        [ProtoMember(6)] public LockModes[] Locked;


        public void Sync(GridAi ai, FocusData sync, bool localCall = false)
        {
            if (ai.Session.IsServer || sync.Revision > Revision)
            {
                Revision = sync.Revision;
                ActiveId = sync.ActiveId;
                HasFocus = sync.HasFocus;
                DistToNearestFocusSqr = sync.DistToNearestFocusSqr;

                for (int i = 0; i < Target.Length; i++) {
                    Target[i] = sync.Target[i];
                    Locked[i] = sync.Locked[i];
                }

                if (ai == ai.Construct.RootAi && localCall)
                    ai.Construct.UpdateLeafFoci();
            }
            //else Log.Line($"FocusData older revision:  {sync.Revision}  > {Revision}");
        }
    }
}
