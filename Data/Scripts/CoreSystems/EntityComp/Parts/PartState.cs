using CoreSystems.Support;
using VRageMath;

namespace CoreSystems.Platform
{
    public partial class Part
    {
        internal void DrawPower(float assignedPower)
        {
            var wasSink = BaseComp.SinkPower;
            var wasGrid = BaseComp.Ai.GridAssignedPower;

            AssignedPower = MathHelper.Clamp(assignedPower, 0, DesiredPower);
            BaseComp.SinkPower += AssignedPower;
            BaseComp.Ai.GridAssignedPower += AssignedPower;
            //Log.Line($"[Add] Id:{PartId} - Sink:{wasSink}({BaseComp.SinkPower}) - grid:{wasGrid}({BaseComp.Ai.GridAssignedPower}) - assigned:{assignedPower} - requested:{AssignedPower} - desired:{DesiredPower}");

            BaseComp.Cube.ResourceSink.Update();
            Charging = true;
        }

        internal void AdjustPower(float assignedPower)
        {
            var wasSink = BaseComp.SinkPower;
            var wasGrid = BaseComp.Ai.GridAssignedPower;
            BaseComp.SinkPower -= AssignedPower;
            BaseComp.Ai.GridAssignedPower -= AssignedPower;

            AssignedPower = MathHelper.Clamp(assignedPower, 0, DesiredPower); ;

            BaseComp.SinkPower += AssignedPower;
            BaseComp.Ai.GridAssignedPower += AssignedPower;

            //Log.Line($"[Reb] Id:{PartId} - Sink{wasSink}({BaseComp.SinkPower}) - grid:{wasGrid}({BaseComp.Ai.GridAssignedPower}) - assigned:{assignedPower}");

            BaseComp.Cube.ResourceSink.Update();
            NewPowerNeeds = false;
        }

        internal void StopPowerDraw(bool hardStop)
        {
            if (!Charging) {
                if (!hardStop) Log.Line($"wasnt drawing power");
                return;
            }
            var wasSink = BaseComp.SinkPower;
            var wasGrid = BaseComp.Ai.GridAssignedPower;

            BaseComp.SinkPower -= AssignedPower;
            BaseComp.Ai.GridAssignedPower -= AssignedPower;
            //Log.Line($"[Rem] Id:{PartId} - Sink:{wasSink}({BaseComp.SinkPower}) - grid:{wasGrid}({BaseComp.Ai.GridAssignedPower}) - assigned:{AssignedPower} - desired:{DesiredPower}");
            AssignedPower = 0;

            if (BaseComp.SinkPower < BaseComp.IdlePower) BaseComp.SinkPower = BaseComp.IdlePower;
            BaseComp.Cube.ResourceSink.Update();
            Charging = false;
        }

    }
}
