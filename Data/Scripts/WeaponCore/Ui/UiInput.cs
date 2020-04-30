using System.Diagnostics;
using Sandbox.ModAPI;
using VRage.Input;
using WeaponCore.Support;
using static WeaponCore.Session;

namespace WeaponCore
{
    internal class UiInput
    {
        internal int PreviousWheel;
        internal int CurrentWheel;
        internal int ShiftTime;
        internal int MouseXMove;
        internal int MouseYMove;
        internal bool LeftMouseReleased;
        internal bool RightMouseReleased;
        internal bool MouseButtonPressed;
        internal bool InputChanged;
        internal bool MouseButtonLeftWasPressed;
        internal bool MouseButtonMiddleWasPressed;
        internal bool MouseButtonRightWasPressed;
        internal bool WasInMenu;
        internal bool WheelForward;
        internal bool WheelBackward;
        internal bool ShiftReleased;
        internal bool ShiftPressed;
        internal bool LongShift;
        internal bool AltPressed;
        internal bool CtrlPressed;
        internal bool AnyKeyPressed;
        internal bool KeyPrevPressed;
        internal bool UiKeyPressed;
        internal bool UiKeyWasPressed;
        internal bool PlayerCamera;
        internal bool FPressed;
        internal bool FirstPersonView;
        private readonly Session _session;
        internal readonly InputStateData ClientInputState;

        internal UiInput(Session session)
        {
            _session = session;
            ClientInputState = new InputStateData();
        }

        internal void UpdateInputState()
        {
            var s = _session;
            WheelForward = false;
            WheelBackward = false;

            if (!s.InGridAiBlock) s.UpdateLocalAiAndCockpit();

            if (s.InGridAiBlock && !s.InMenu)
            {
                MouseButtonPressed = MyAPIGateway.Input.IsAnyMousePressed();

                MouseXMove = MyAPIGateway.Input.GetMouseXForGamePlay();
                MouseYMove = MyAPIGateway.Input.GetMouseYForGamePlay();

                MouseButtonLeftWasPressed = ClientInputState.MouseButtonLeft;
                MouseButtonMiddleWasPressed = ClientInputState.MouseButtonMiddle;
                MouseButtonRightWasPressed = ClientInputState.MouseButtonRight;

                WasInMenu = ClientInputState.InMenu;
                ClientInputState.InMenu = _session.InMenu;

                if (MouseButtonPressed)
                {

                    ClientInputState.MouseButtonLeft = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Left);
                    ClientInputState.MouseButtonMiddle = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Middle);
                    ClientInputState.MouseButtonRight = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Right);
                }
                else
                {
                    ClientInputState.MouseButtonLeft = false;
                    ClientInputState.MouseButtonMiddle = false;
                    ClientInputState.MouseButtonRight = false;
                }

                InputChanged = MouseButtonLeftWasPressed != ClientInputState.MouseButtonLeft || MouseButtonMiddleWasPressed != ClientInputState.MouseButtonMiddle || MouseButtonRightWasPressed != ClientInputState.MouseButtonRight || WasInMenu != ClientInputState.InMenu;

                ShiftReleased = MyAPIGateway.Input.IsNewKeyReleased(MyKeys.LeftShift);
                ShiftPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.LeftShift);
                if (ShiftPressed)
                {
                    ShiftTime++;
                    LongShift = ShiftTime > 59;
                }
                else
                {
                    if (LongShift) ShiftReleased = false;
                    ShiftTime = 0;
                    LongShift = false;
                }

                AltPressed = MyAPIGateway.Input.IsAnyAltKeyPressed();
                CtrlPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Control);
                FPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.F);
                KeyPrevPressed = AnyKeyPressed;
                AnyKeyPressed = MyAPIGateway.Input.IsAnyKeyPress();
                UiKeyWasPressed = UiKeyPressed;
                UiKeyPressed = CtrlPressed || AltPressed || ShiftPressed;
                PlayerCamera = MyAPIGateway.Session.IsCameraControlledObject;
                FirstPersonView = PlayerCamera && MyAPIGateway.Session.CameraController.IsInFirstPersonView;
                if ((!UiKeyPressed && !UiKeyWasPressed) || !AltPressed && CtrlPressed && !FirstPersonView)
                {
                    PreviousWheel = MyAPIGateway.Input.PreviousMouseScrollWheelValue();
                    CurrentWheel = MyAPIGateway.Input.MouseScrollWheelValue();
                }
            }

            if (CurrentWheel != PreviousWheel && CurrentWheel > PreviousWheel)
                WheelForward = true;
            else if (s.UiInput.CurrentWheel != s.UiInput.PreviousWheel)
                WheelBackward = true;

            if (s.WheelUi.WheelActive)
            {
                LeftMouseReleased = MyAPIGateway.Input.IsNewLeftMouseReleased();
                RightMouseReleased = MyAPIGateway.Input.IsNewRightMouseReleased();
            }
            else
            {
                LeftMouseReleased = false;
                RightMouseReleased = false;
            }
        }
    }
}
