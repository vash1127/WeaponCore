using System.Diagnostics;
using Sandbox.ModAPI;
using VRage.Input;
using WeaponCore.Support;
namespace WeaponCore
{
    internal class UiInput
    {
        internal int PreviousWheel;
        internal int CurrentWheel;
        internal int ShiftTime;
        internal bool LeftMouseReleased;
        internal bool RightMouseReleased;
        internal bool MouseButtonPressed;
        internal bool MouseButtonLeftWasPressed;
        internal bool MouseButtonMiddleWasPressed;
        internal bool MouseButtonRightWasPressed;
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
        internal bool FirstPersonView;
        internal bool InSpyCam;
        private readonly Session _session;
        internal readonly MouseState ClientMouseState;

        internal UiInput(Session session)
        {
            _session = session;
            ClientMouseState = new MouseState();
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

                MouseButtonLeftWasPressed = ClientMouseState.MouseButtonLeft;
                MouseButtonMiddleWasPressed = ClientMouseState.MouseButtonMiddle;
                MouseButtonRightWasPressed = ClientMouseState.MouseButtonRight;

                if (MouseButtonPressed)
                {
                    ClientMouseState.MouseButtonLeft = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Left);
                    ClientMouseState.MouseButtonMiddle = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Middle);
                    ClientMouseState.MouseButtonRight = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Right);
                }
                else
                {
                    ClientMouseState.MouseButtonLeft = false;
                    ClientMouseState.MouseButtonMiddle = false;
                    ClientMouseState.MouseButtonRight = false;
                }

                if (_session.MpActive && (MouseButtonLeftWasPressed != ClientMouseState.MouseButtonLeft ||
                MouseButtonMiddleWasPressed != ClientMouseState.MouseButtonMiddle ||
                MouseButtonRightWasPressed != ClientMouseState.MouseButtonRight))
                {
                    if(_session.IsClient)
                    _session.SendPacketToServer(new MouseInputPacket {EntityId = -1, SenderId = _session.MultiplayerId, PType = PacketType.ClientMouseEvent, Data = ClientMouseState });
                    else if(_session.MpActive && _session.IsServer)
                        _session.PacketizeToClientsInRange(null, new MouseInputPacket { EntityId = -1, SenderId = _session.MultiplayerId, PType = PacketType.ClientMouseEvent, Data = ClientMouseState });
                }

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
                KeyPrevPressed = AnyKeyPressed;
                AnyKeyPressed = MyAPIGateway.Input.IsAnyKeyPress();
                UiKeyWasPressed = UiKeyPressed;
                UiKeyPressed = CtrlPressed || AltPressed || ShiftPressed;
                PlayerCamera = MyAPIGateway.Session.IsCameraControlledObject;
                FirstPersonView = PlayerCamera && MyAPIGateway.Session.CameraController.IsInFirstPersonView;
                //InSpyCam = _session.Session.CameraController == _session.SpyCam;
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
