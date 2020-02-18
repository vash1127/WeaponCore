using VRage.Game.Entity;
using static WeaponCore.Support.TargetingDefinition;

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

        internal void AddFocus(MyEntity target, GridAi ai)
        {
            var session = ai.Session;
            Target[ActiveId] = target;
            ai.TargetResetTick = session.Tick + 1;
            foreach (var sub in ai.SubGrids)
            {
                GridAi gridAi;
                if (session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                {
                    gridAi.Focus.Target[ActiveId] = target;
                    gridAi.TargetResetTick = session.Tick + 1;
                }
            }

            if(!session.DedicatedServer && !session.IsServer)
                session.SendPacketToServer(new TargetPacket { EntityId = ai.MyGrid.EntityId, SenderId = session.MultiplayerId, PType = PacketType.TargetUpdate, Data = new TransferTarget { EntityId = target.EntityId } });
        }

        internal bool ReassignTarget(MyEntity target, int focusId, GridAi ai)
        {
            if (focusId >= Target.Length) return false;
            Target[focusId] = target;
            foreach (var sub in ai.SubGrids)
            {
                GridAi gridAi;
                if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                    gridAi.Focus.Target[focusId] = Target[ActiveId];
            }
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
                foreach (var sub in ai.SubGrids)
                {
                    GridAi gridAi;
                    if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                        gridAi.Focus.Target[newActiveId] = Target[ActiveId];
                }
            }
            else if (!addSecondary && Target[newActiveId] != null)
                ActiveId = newActiveId;
        }

        internal bool IsFocused(GridAi ai)
        {
            HasFocus = false;
            for (int i = 0; i < Target.Length; i++)
            {
                if (Target[i] != null)
                {
                    if (!Target[i].MarkedForClose) HasFocus = true;
                    else
                    {
                        Target[i] = null;
                        foreach (var sub in ai.SubGrids)
                        {
                            GridAi gridAi;
                            if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                                gridAi.Focus.Target[i] = null;
                        }
                    }
                }

                if (Target[0] == null && HasFocus)
                {
                    Target[0] = Target[i];
                    Target[i] = null;
                    ActiveId = 0;

                    foreach (var sub in ai.SubGrids)
                    {
                        GridAi gridAi;
                        if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                        {
                            gridAi.Focus.Target[0] = Target[i];
                            gridAi.Focus.Target[i] = null;
                        }
                    }
                }
            }
            return HasFocus;
        }

        internal void ReleaseActive(GridAi ai)
        {
            Target[ActiveId] = null;

            foreach (var sub in ai.SubGrids)
            {
                GridAi gridAi;
                if (ai.Session.GridTargetingAIs.TryGetValue(sub, out gridAi))
                    gridAi.Focus.Target[ActiveId] = null;
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
