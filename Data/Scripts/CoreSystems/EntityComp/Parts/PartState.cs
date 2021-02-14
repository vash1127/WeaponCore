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
            AssignedPower = assignedPower;
            DrawingPower = true;
            BaseComp.SinkPower += AssignedPower;
            BaseComp.Ai.GridAssignedPower += AssignedPower;
            BaseComp.Cube.ResourceSink.Update();
        }

        internal void AdjustPower(float newValue)
        {
            BaseComp.SinkPower -= AssignedPower;
            BaseComp.Ai.GridAssignedPower -= AssignedPower;

            AssignedPower = newValue;

            BaseComp.SinkPower += AssignedPower;
            BaseComp.Ai.GridAssignedPower += AssignedPower;

            BaseComp.Cube.ResourceSink.Update();
        }

        internal void StopPowerDraw()
        {
            if (!DrawingPower) {
                Log.Line($"wasnt drawing power");
                return;
            }

            DrawingPower = false;
            BaseComp.SinkPower -= AssignedPower;
            BaseComp.Ai.GridAssignedPower -= AssignedPower;

            ChargeDelayTicks = 0;
            if (BaseComp.SinkPower < BaseComp.IdlePower) BaseComp.SinkPower = BaseComp.IdlePower;
            BaseComp.Cube.ResourceSink.Update();
        }

    }
}
