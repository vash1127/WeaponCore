using VRage.Game.Entity;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;

namespace WeaponCore.Support
{
    public class Focus
    {
        internal Focus(int count)
        {
            Target = new MyEntity[count];
            SubSystem = new BlockTypes[count];
            TargetState = new TargetStatus[count];
            PrevTargetId = new long[count];
            for (int i = 0; i < TargetState.Length; i++)
                TargetState[i] = new TargetStatus();
        }

        internal readonly BlockTypes[] SubSystem;
        internal TargetStatus[] TargetState;
        internal readonly long[] PrevTargetId;
        internal MyEntity[] Target;
        internal int ActiveId;
        internal bool HasFocus;

        internal void AddFocus(MyEntity target, GridAi ai, bool alreadySynced = false)
        {
            var session = ai.Session;
            Target[ActiveId] = target;
            ai.TargetResetTick = session.Tick + 1;
            UpdateSubGrids(ai, true);
            if (session.MpActive && session.HandlesInput && !alreadySynced)
                session.SendFocusTargetUpdate(ai, target.EntityId);
        }

        internal bool GetPriorityTarget(out MyEntity target)
        {
            if (Target[ActiveId] != null)
            {
                target = Target[ActiveId];
                return true;
            }

            for (int i = 0; i < Target.Length; i++)
            {
                target = Target[i];
                if (target != null) return true;
            }

            target = null;
            return false;
        }

        internal bool ReassignTarget(MyEntity target, int focusId, GridAi ai)
        {
            if (focusId >= Target.Length || target == null || target.MarkedForClose) return false;
            Target[focusId] = target;
            HasFocus = true;
            UpdateSubGrids(ai);
            return true;
        }

        internal void NextActive(bool addSecondary, GridAi ai)
        {
            var prevId = ActiveId;
            var newActiveId = prevId;
            if (newActiveId + 1 > Target.Length - 1) newActiveId -= 1;
            else newActiveId += 1;

            if (addSecondary && Target[newActiveId] == null)
            {
                Target[newActiveId] = Target[prevId];
                ActiveId = newActiveId;
            }
            else if (!addSecondary && Target[newActiveId] != null)
                ActiveId = newActiveId;

            UpdateSubGrids(ai);
        }

        internal bool IsFocused(GridAi ai)
        {
            HasFocus = false;
            for (int i = 0; i < Target.Length; i++) {

                if (Target[i] != null) {

                    if (!Target[i].MarkedForClose) 
                        HasFocus = true;
                    else
                        Target[i] = null;
                }

                if (Target[0] == null && HasFocus) {

                    Target[0] = Target[i];
                    Target[i] = null;
                    ActiveId = 0;
                }
            }

            UpdateSubGrids(ai);

            return HasFocus;
        }

        internal void ReleaseActive(GridAi ai)
        {
            Target[ActiveId] = null;

            IsFocused(ai);
            UpdateSubGrids(ai);
        }

        internal void UpdateSubGrids(GridAi ai, bool resetTick = false)
        {
            foreach (var sub in ai.SubGrids)
            {
                if (ai.MyGrid == sub) continue;

                GridAi gridAi;
                if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                {
                    if (resetTick) gridAi.TargetResetTick = gridAi.Session.Tick + 1;
                    for (int i = 0; i < gridAi.Focus.Target.Length; i++)
                    {
                        gridAi.Focus.Target[i] = Target[i];
                        gridAi.Focus.HasFocus = HasFocus;
                        gridAi.Focus.ActiveId = ActiveId;
                    }
                }
            }
        }

        internal void Clean()
        {
            for (int i = 0; i < Target.Length; i++)
            {
                Target[i] = null;
            }
        }
    }

}
