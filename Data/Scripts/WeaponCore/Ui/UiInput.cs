using System.Diagnostics;
using Sandbox.ModAPI;
using VRage.Input;

namespace WeaponCore
{
    class UiInput
    {
        internal int PreviousWheel;
        internal int CurrentWheel;
        internal bool MouseButtonPressed;
        internal bool MouseButtonLeft;
        internal bool MouseButtonMiddle;
        internal bool MouseButtonRight;
        internal bool WheelForward;
        internal bool WheelBackward;
        internal bool ShiftReleased;

        private readonly Session _session;

        internal UiInput(Session session)
        {
            _session = session;
        }

        internal void UpdateInputState()
        {
            var s = _session;
            MouseButtonPressed = MyAPIGateway.Input.IsAnyMousePressed();
            WheelForward = false;
            WheelBackward = false;
            if (MouseButtonPressed)
            {
                MouseButtonLeft = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Left);
                MouseButtonMiddle = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Middle);
                MouseButtonRight = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Right);
            }
            else
            {
                MouseButtonLeft = false;
                MouseButtonMiddle = false;
                MouseButtonRight = false;
            }

            if (s.WheelUi.WheelActive || s.TargetUi.DrawReticle)
            {
                PreviousWheel = MyAPIGateway.Input.PreviousMouseScrollWheelValue();
                CurrentWheel = MyAPIGateway.Input.MouseScrollWheelValue();
                ShiftReleased = MyAPIGateway.Input.IsNewKeyReleased(MyKeys.LeftShift);
            }
            if (CurrentWheel != PreviousWheel && CurrentWheel > PreviousWheel)
                WheelForward = true;
            else if (s.UiInput.CurrentWheel != s.UiInput.PreviousWheel)
                WheelBackward = true;
        }
    }
}
