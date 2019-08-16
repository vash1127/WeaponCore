using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using VRageRender;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{
    class Wheel
    {
        private readonly Vector2D _wheelPosition = new Vector2D(0, 0);

        internal bool WheelActive;
        internal int PreviousWheel;
        internal int CurrentWheel;
        internal int WheelOptCount;
        internal int WheelOptSlot;
        internal bool MouseButtonPressed;
        internal bool MouseButtonLeft;
        internal bool MouseButtonMiddle;
        internal bool MouseButtonRight;

        internal enum State
        {
            Close,
            Open,
            NoChange,
        }

        internal State ChangeState
        {
            get
            {
                if (MyAPIGateway.Input.WasMiddleMouseReleased() && !WheelActive) return State.Open;
                if (MyAPIGateway.Input.WasMiddleMouseReleased() && WheelActive) return State.Close;
                return State.NoChange;
            }
            set { }
        }

        internal readonly MyStringId[] WheelTargetIds =
        {
            MyStringId.NullOrEmpty,
            MyStringId.GetOrCompute("DS_TargetWheel_NoSelect"),
            MyStringId.GetOrCompute("DS_TargetWheel_Comms"),
            MyStringId.GetOrCompute("DS_TargetWheel_Engines"),
            MyStringId.GetOrCompute("DS_TargetWheel_JumpDrive"),
            MyStringId.GetOrCompute("DS_TargetWheel_Ordinance"),
            MyStringId.GetOrCompute("DS_TargetWheel_Power"),
            MyStringId.GetOrCompute("DS_TargetWheel_Weapons"),
        };

        internal readonly MyStringId[] WheelMainIds =
        {
            MyStringId.GetOrCompute("DS_MainWheel_NoSelect"),
            MyStringId.GetOrCompute("DS_MainWheel_Grids"),
            MyStringId.GetOrCompute("DS_MainWheel_Players"),
            MyStringId.GetOrCompute("DS_MainWheel_WeaponGroups"),
            MyStringId.GetOrCompute("DS_MainWheel_Ordinance"),
        };

        internal void UpdateInput()
        {
            MouseButtonPressed = MyAPIGateway.Input.IsAnyMousePressed();
            if (MouseButtonPressed)
            {
                MouseButtonLeft = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Left);
                MouseButtonMiddle = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Middle);
                MouseButtonRight = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Right);

                if (MouseButtonMiddle && ChangeState == State.Open) OpenWheel();
                else if (MouseButtonMiddle && ChangeState == State.Close) CloseWheel();
            }
            else
            {
                MouseButtonLeft = false;
                MouseButtonMiddle = false;
                MouseButtonRight = false;
                if (WheelActive && !(Session.Instance.Session.ControlledObject is MyCockpit)) CloseWheel();
            }

            UpdatePosition();
        }

        internal void UpdatePosition()
        {
            if (WheelActive)
            {
                PreviousWheel = MyAPIGateway.Input.PreviousMouseScrollWheelValue();
                CurrentWheel = MyAPIGateway.Input.MouseScrollWheelValue();

                if (CurrentWheel != PreviousWheel && CurrentWheel > PreviousWheel)
                {
                    WheelOptCount = WheelMainIds.Length;
                    if (WheelOptSlot < WheelOptCount - 1) WheelOptSlot++;
                    else WheelOptSlot = 0;
                }
                else if (CurrentWheel != PreviousWheel)
                {
                    WheelOptCount = WheelMainIds.Length;
                    if (WheelOptSlot - 1 >= 0) WheelOptSlot--;
                    else WheelOptSlot = WheelOptCount - 1;
                }
            }
        }

        internal void DrawWheel()
        {
            var position = new Vector3D(_wheelPosition.X, _wheelPosition.Y, 0);
            var fov = MyAPIGateway.Session.Camera.FovWithZoom;
            double aspectratio = MyAPIGateway.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);
            position.X *= scale * aspectratio;
            position.Y *= scale;
            var cameraWorldMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            position = Vector3D.Transform(new Vector3D(position.X, position.Y, -.1), cameraWorldMatrix);

            var origin = position;
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            scale = 1 * scale;

            MyTransparentGeometry.AddBillboardOriented(WheelMainIds[WheelOptSlot], Color.White, origin, left, up, (float)scale, BlendTypeEnum.PostPP);
        }

        internal void OpenWheel()
        {
            Log.Line("Lock mouse buttons and activate wheel");
            WheelActive = true;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, false);
        }

        internal void CloseWheel()
        {
            Log.Line("Unlock mouse buttons and deactive wheel");
            WheelActive = false;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, true);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, true);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, true);
        }
    }
}
