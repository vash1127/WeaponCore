using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Input;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using static WeaponCore.Wheel.Menu;
using static WeaponCore.Support.TargetingDefinition.BlockTypes;
namespace WeaponCore
{
    internal partial class Wheel
    {
        internal void UpdatePosition()
        {
            var s = Session;
            if (s.UiInput.MouseButtonPressed)
            {
                if (s.UiInput.MouseButtonMiddle && ChangeState == State.Open) OpenWheel();
                else if (s.UiInput.MouseButtonMiddle && ChangeState == State.Close) CloseWheel();
            }
            else
            {
                if (WheelActive && !(Session.Session.ControlledObject is MyCockpit)) CloseWheel();
            }
            if (WheelActive)
            {
                var previousMenu = _currentMenu;
                if (MyAPIGateway.Input.IsNewLeftMouseReleased())
                {
                    var menu = GetCurrentMenu();
                    var item = GetCurrentMenuItem();
                    if (item.SubName != null)
                    {
                        _currentMenu = item.SubName;
                        UpdateState(menu);
                    }
                }
                else if (MyAPIGateway.Input.IsNewRightMouseReleased())
                {
                    var menu = GetCurrentMenu();
                    var item = GetCurrentMenuItem();
                    if (item.ParentName != null)
                    {
                        _currentMenu = item.ParentName;
                        UpdateState(menu);
                    }
                    else if (menu.Name == "WeaponGroups") CloseWheel();
                }

                if (s.UiInput.WheelForward)
                    GetCurrentMenu().Move(Movement.Forward);
                else if (s.UiInput.WheelBackward)
                    GetCurrentMenu().Move(Movement.Backward);

                if (previousMenu != _currentMenu) SetCurrentMessage();
            }
        }

        internal void DrawWheel()
        {
            var position = new Vector3D(_wheelPosition.X, _wheelPosition.Y, 0);
            var fov = Session.Camera.FovWithZoom;
            double aspectratio = Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);
            position.X *= scale * aspectratio;
            position.Y *= scale;
            var cameraWorldMatrix = Session.Camera.WorldMatrix;
            position = Vector3D.Transform(new Vector3D(position.X, position.Y, -.1), cameraWorldMatrix);

            var origin = position;
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            scale = 1 * scale;
            if (Session.Tick10)
                SetCurrentMessage();
            
            MyTransparentGeometry.AddBillboardOriented(GetCurrentMenuItem().Texture, Color.White, origin, left, up, (float)scale, BlendTypeEnum.PostPP);
        }

        internal void OpenWheel()
        {
            WheelActive = true;
            if (HudNotify == null) HudNotify = MyAPIGateway.Utilities.CreateNotification("[Grids]", 160, "UrlHighlight");
            if (_currentMenu == string.Empty) _currentMenu = "WeaponGroups";
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, false);
        }

        internal void CloseWheel()
        {
            _currentMenu = "WeaponGroups";
            WheelActive = false;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, true);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, true);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, true);
        }
        
        internal void SetCurrentMessage()
        {
            var currentMessage = GetCurrentMenu().Message;
            string name = string.Empty;
            var ai = Session.TrackingAi;
            if (ai?.Focus.Target[ai.Focus.ActiveId] != null)
            {
                name = Session.TrackingAi.Focus.Target[ai.Focus.ActiveId].DisplayName;
                var nameLen = 30;
                name = name.Replace("[", "(");
                name = name.Replace("]", ")");
                if (name.Length > nameLen) name = name.Substring(0, nameLen);
                name = $"[{name}\n";
            }


            if (currentMessage == string.Empty)
                currentMessage = GetCurrentMenu().CurrentItemMessage();

            currentMessage = name + currentMessage;

            HudNotify.Text = currentMessage;
            HudNotify.Show();
        }

        internal Menu GetCurrentMenu()
        {
            return Menus[_currentMenu];
        }

        internal Item GetCurrentMenuItem()
        {
            var menu = Menus[_currentMenu];
            return menu.Items[menu.CurrentSlot];
        }

        internal void UpdateState(Menu oldMenu)
        {
            var menu = Menus[_currentMenu];
            if (menu.ItemCount <= 1) menu.LoadInfo();
        }
    }
}
