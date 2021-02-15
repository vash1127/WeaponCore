using System;
using System.Collections.Generic;
using CoreSystems.Support;
using VRageMath;

namespace CoreSystems.Platform
{
    public partial class Part
    {
        internal void DrawPower(float assignedPower)
        {
            AssignedPower = MathHelper.Clamp(assignedPower, 0, DesiredPower);
            BaseComp.SinkPower += AssignedPower;
            BaseComp.Ai.GridAssignedPower += AssignedPower;
            BaseComp.Cube.ResourceSink.Update();
            Charging = true;
        }

        internal void AdjustPower(float assignedPower)
        {
            BaseComp.SinkPower -= AssignedPower;
            BaseComp.Ai.GridAssignedPower -= AssignedPower;

            AssignedPower = MathHelper.Clamp(assignedPower, 0, DesiredPower); ;

            BaseComp.SinkPower += AssignedPower;
            BaseComp.Ai.GridAssignedPower += AssignedPower;

            BaseComp.Cube.ResourceSink.Update();
            NewPowerNeeds = false;
        }

        internal void StopPowerDraw(bool hardStop)
        {
            if (!Charging) {
                if (!hardStop) Log.Line($"wasnt drawing power");
                return;
            }

            BaseComp.SinkPower -= AssignedPower;
            BaseComp.Ai.GridAssignedPower -= AssignedPower;
            AssignedPower = 0;

            if (BaseComp.SinkPower < BaseComp.IdlePower) BaseComp.SinkPower = BaseComp.IdlePower;
            BaseComp.Cube.ResourceSink.Update();
            Charging = false;
        }

    }
}
