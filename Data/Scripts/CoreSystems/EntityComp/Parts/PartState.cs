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
            BaseComp.SinkPower += AssignedPower;
            BaseComp.Ai.GridAssignedPower += AssignedPower;
            BaseComp.Cube.ResourceSink.Update();
            Charging = true;
        }

        internal void AdjustPower(float newValue)
        {
            BaseComp.SinkPower -= AssignedPower;
            BaseComp.Ai.GridAssignedPower -= AssignedPower;

            AssignedPower = newValue;

            BaseComp.SinkPower += AssignedPower;
            BaseComp.Ai.GridAssignedPower += AssignedPower;

            BaseComp.Cube.ResourceSink.Update();
            NewPowerNeeds = false;
        }

        internal void StopPowerDraw()
        {
            if (!ExitCharger) {
                Log.Line($"wasnt drawing power");
                return;
            }

            BaseComp.SinkPower -= AssignedPower;
            BaseComp.Ai.GridAssignedPower -= AssignedPower;

            if (BaseComp.SinkPower < BaseComp.IdlePower) BaseComp.SinkPower = BaseComp.IdlePower;
            BaseComp.Cube.ResourceSink.Update();
            Charging = false;
        }

    }
}
