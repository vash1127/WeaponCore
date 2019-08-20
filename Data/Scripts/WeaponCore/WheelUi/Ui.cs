using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using static WeaponCore.Wheel.Menu;
namespace WeaponCore
{
    internal partial class Wheel
    {
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
                _previousWheel = MyAPIGateway.Input.PreviousMouseScrollWheelValue();
                _currentWheel = MyAPIGateway.Input.MouseScrollWheelValue();

                var previousMenu = _currentMenu;
                if (MyAPIGateway.Input.IsNewLeftMouseReleased())
                {
                    var menu = Menus[_currentMenu];
                    var menuItem = menu.Items[menu.CurrentSlot];

                    if (menuItem.SubName != null)
                    {
                        _currentMenu = menuItem.SubName;
                        UpdateTargets();
                    }
                }
                else if (MyAPIGateway.Input.IsNewRightMouseReleased())
                {
                    var menu = Menus[_currentMenu];
                    var menuItem = menu.Items[menu.CurrentSlot];

                    if (menuItem.ParentName != null)
                    {
                        _currentMenu = menuItem.ParentName;
                        UpdateTargets();
                    }
                    else if (menu.Name == "Main") CloseWheel();
                }

                if (_currentWheel != _previousWheel && _currentWheel > _previousWheel)
                    HudNotify.Text = Menus[_currentMenu].Move(Movement.Forward);
                else if (_currentWheel != _previousWheel)
                    HudNotify.Text = Menus[_currentMenu].Move(Movement.Backward);

                if (previousMenu != _currentMenu)
                {
                    var menu = Menus[_currentMenu];
                    var menuItem = menu.Items[menu.CurrentSlot];
                    HudNotify.Text = menuItem.Message;
                }
            }
        }

        internal void DrawWheel()
        {
            var position = new Vector3D(_wheelPosition.X, _wheelPosition.Y, 0);
            var fov = Session.Instance.Session.Camera.FovWithZoom;
            double aspectratio = Session.Instance.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);
            position.X *= scale * aspectratio;
            position.Y *= scale;
            var cameraWorldMatrix = Session.Instance.Session.Camera.WorldMatrix;
            position = Vector3D.Transform(new Vector3D(position.X, position.Y, -.1), cameraWorldMatrix);

            var origin = position;
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            scale = 1 * scale;
            var menu = Menus[_currentMenu];

            if (Session.Instance.Tick20 && menu.ItemCount <= 1)
                HudNotify.Text = menu.FormatMessage();

            HudNotify.Show();

            MyTransparentGeometry.AddBillboardOriented(menu.Items[menu.CurrentSlot].Texture, Color.White, origin, left, up, (float)scale, BlendTypeEnum.PostPP);
        }

        internal void OpenWheel()
        {
            WheelActive = true;
            if (HudNotify == null) HudNotify = MyAPIGateway.Utilities.CreateNotification("[Grids]", 320, "UrlHighlight");
            if (_currentMenu == string.Empty) _currentMenu = "Main";
            var menu = Menus[_currentMenu];
            var menuItem = menu.Items[menu.CurrentSlot];
            HudNotify.Text = menuItem.Message;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, false);
        }

        internal void CloseWheel()
        {
            _currentMenu = string.Empty;
            WheelActive = false;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, true);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, true);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, true);
        }

        internal void UpdateTargets()
        {
            Grids.Clear();
            Characters.Clear();
            Projectiles.Clear();
            foreach (var target in Ai.SortedTargets)
            {
                if (target.IsGrid) Grids.Add(target);
                else Characters.Add(target);
            }
            foreach (var lp in Ai.LiveProjectile) Projectiles.Add(lp);
            Projectiles.Sort((a, b) => Vector3D.DistanceSquared(a.Position, Ai.MyGrid.PositionComp.WorldAABB.Center).CompareTo(Vector3D.DistanceSquared(b.Position, Ai.MyGrid.PositionComp.WorldAABB.Center)));

            var menu = Menus[_currentMenu];
            if (menu.ItemCount <= 1) menu.Refresh();
        }
    }
}
