using CoreSystems.Support;
using VRageMath;

namespace CoreSystems.Platform
{
    public partial class Part
    {
        internal void DrawPower(float assignedPower, Ai ai)
        {
            var wasSink = BaseComp.SinkPower;
            var wasGrid = ai.GridAssignedPower;

            AssignedPower = MathHelper.Clamp(assignedPower, 0, DesiredPower);
            BaseComp.SinkPower += AssignedPower;
            ai.GridAssignedPower += AssignedPower;
            //Log.Line($"[Add] Id:{PartId} - Sink:{wasSink}({BaseComp.SinkPower}) - grid:{wasGrid}({BaseComp.Ai.GridAssignedPower}) - assigned:{assignedPower} - requested:{AssignedPower} - desired:{DesiredPower}");
            Charging = true;

            if (BaseComp.Cube.MarkedForClose)
                return;

            BaseComp.Cube.ResourceSink.Update();
        }

        internal void AdjustPower(float assignedPower, Ai ai)
        {
            var wasSink = BaseComp.SinkPower;
            var wasGrid = ai.GridAssignedPower;
            BaseComp.SinkPower -= AssignedPower;
            ai.GridAssignedPower -= AssignedPower;

            AssignedPower = MathHelper.Clamp(assignedPower, 0, DesiredPower); ;

            BaseComp.SinkPower += AssignedPower;
            ai.GridAssignedPower += AssignedPower;

            //Log.Line($"[Reb] Id:{PartId} - Sink{wasSink}({BaseComp.SinkPower}) - grid:{wasGrid}({BaseComp.Ai.GridAssignedPower}) - assigned:{assignedPower}");

            NewPowerNeeds = false;

            if (BaseComp.Cube.MarkedForClose)
                return;

            BaseComp.Cube.ResourceSink.Update();

        }

        internal void StopPowerDraw(bool hardStop, Ai ai)
        {
            if (!Charging) {
                if (!hardStop) Log.Line($"wasnt drawing power");
                return;
            }
            var wasSink = BaseComp.SinkPower;
            var wasGrid = ai.GridAssignedPower;

            BaseComp.SinkPower -= AssignedPower;
            ai.GridAssignedPower -= AssignedPower;
            //Log.Line($"[Rem] Id:{PartId} - Sink:{wasSink}({BaseComp.SinkPower}) - grid:{wasGrid}({BaseComp.Ai.GridAssignedPower}) - assigned:{AssignedPower} - desired:{DesiredPower}");
            AssignedPower = 0;

            if (BaseComp.SinkPower < BaseComp.IdlePower) BaseComp.SinkPower = BaseComp.IdlePower;
            Charging = false;

            if (BaseComp.Cube.MarkedForClose)
                return;

            BaseComp.Cube.ResourceSink.Update();
        }

    }
}
