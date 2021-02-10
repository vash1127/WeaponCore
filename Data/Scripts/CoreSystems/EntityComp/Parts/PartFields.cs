using System;
using System.Collections.Generic;
using CoreSystems.Support;
using VRageMath;

namespace CoreSystems.Platform
{
    public class Part
    {
        internal readonly List<Action<long, int, ulong, long, Vector3D, bool>> Monitors = new List<Action<long, int, ulong, long, Vector3D, bool>>();
        internal readonly uint[] MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];

        internal CoreComponent BaseComp;
        internal CoreSystem CoreSystem;
        internal PartAcquire Acquire;
        internal uint PartCreatedTick;
        internal int ShortLoadId;
        internal uint PartReadyTick;
        internal int UniqueId;
        internal int PartId;
        internal bool IsPrime;

        internal void Init(CoreComponent comp, CoreSystem system, int partId)
        {
            CoreSystem = system;
            BaseComp = comp;
            PartCreatedTick = CoreSystem.Session.Tick;
            PartId = partId;
            IsPrime = partId == comp.Platform.Structure.PrimaryPart;
            Acquire = new PartAcquire(this);
            UniqueId = comp.Session.UniquePartId;
            ShortLoadId = comp.Session.ShortLoadAssigner();
            for (int i = 0; i < BaseComp.Monitors[PartId].Count; i++)
                Monitors.Add(BaseComp.Monitors[PartId][i]);
        }


        internal class PartAcquire
        {
            internal readonly Part Part;
            internal uint CreatedTick;
            internal int SlotId;
            internal bool IsSleeping;
            internal bool Monitoring;

            internal PartAcquire(Part part)
            {
                Part = part;
            }
        }

    }
}
