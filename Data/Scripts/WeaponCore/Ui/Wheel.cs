using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Input;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using static WeaponCore.Wheel.Menu;
using static WeaponCore.Support.TargetingDefinition.BlockTypes;
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
                    else if (menu.Name == "Main") CloseWheel();
                }

                if (_currentWheel != _previousWheel && _currentWheel > _previousWheel)
                    GetCurrentMenu().Move(Movement.Forward);
                else if (_currentWheel != _previousWheel)
                    GetCurrentMenu().Move(Movement.Backward);

                if (previousMenu != _currentMenu) SetCurrentMessage();
            }
        }

        internal void DrawWheel()
        {
            var position = new Vector3D(_wheelPosition.X, _wheelPosition.Y, 0);
            var fov = Session.Instance.Camera.FovWithZoom;
            double aspectratio = Session.Instance.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);
            position.X *= scale * aspectratio;
            position.Y *= scale;
            var cameraWorldMatrix = Session.Instance.Camera.WorldMatrix;
            position = Vector3D.Transform(new Vector3D(position.X, position.Y, -.1), cameraWorldMatrix);

            var origin = position;
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            scale = 1 * scale;
            if (Session.Instance.Tick10)
                SetCurrentMessage();
            
            MyTransparentGeometry.AddBillboardOriented(GetCurrentMenuItem().Texture, Color.White, origin, left, up, (float)scale, BlendTypeEnum.PostPP);
        }

        internal void OpenWheel()
        {
            WheelActive = true;
            if (HudNotify == null) HudNotify = MyAPIGateway.Utilities.CreateNotification("[Grids]", 160, "UrlHighlight");
            if (_currentMenu == string.Empty) _currentMenu = "Main";

            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, false);
        }

        internal void CloseWheel()
        {
            GetCurrentMenu().CleanUp();

            _currentMenu = "Main";
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
            if (currentMessage == string.Empty)
                currentMessage = GetCurrentMenu().CurrentItemMessage();
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
            oldMenu.CleanUp();

            Grids.Clear();
            Characters.Clear();
            Projectiles.Clear();
            foreach (var target in Ai.SortedTargets)
            {
                if (target.IsGrid)
                {
                    var menuTarget = new MenuTarget { MyEntity = target.Target, OtherArms = target.TypeDict[Weapons].Count > 0, Projectile = null, Threat = "High"};
                    Grids.Add(menuTarget);
                }
                else
                {
                    var menuTarget = new MenuTarget { MyEntity = target.Target, OtherArms = false, Projectile = null, Threat = "Low" };
                    Characters.Add(menuTarget);
                }
            }

            foreach (var lp in Ai.LiveProjectile)
            {
                var menuTarget = new MenuTarget { MyEntity = null, OtherArms = false, Projectile = lp, Threat = "Medium" };
                Projectiles.Add(menuTarget);
            }

            Projectiles.Sort((a, b) => Vector3D.DistanceSquared(a.Projectile.Position, Ai.MyGrid.PositionComp.WorldAABB.Center).CompareTo(Vector3D.DistanceSquared(b.Projectile.Position, Ai.MyGrid.PositionComp.WorldAABB.Center)));

            var menu = Menus[_currentMenu];
            if (menu.ItemCount <= 1) menu.LoadInfo();
        }

    }
}
