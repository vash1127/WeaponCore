using System;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using static WeaponCore.Wheel.Menu;
namespace WeaponCore
{
    internal partial class Wheel
    {
        internal void UpdatePosition()
        {
            var s = Session;
            if (s.UiInput.MouseButtonPressed)
            {
                if (s.UiInput.ClientInputState.MouseButtonMenu && ChangeState == State.Open)
                    OpenWheel();
                else if (s.UiInput.ClientInputState.MouseButtonMenu && ChangeState == State.Close) 
                    CloseWheel();
            }

            if (!WheelActive && _currentMenu != string.Empty)
                _currentMenu = string.Empty;

            if (WheelActive)
            {
                var previousMenu = _currentMenu;
                if (s.UiInput.LeftMouseReleased)
                {
                    var menu = GetCurrentMenu();
                    var item = GetCurrentMenuItem();
                    if (item.SubName != null)
                    {
                        SaveMenuInfo(menu, item);
                        UpdateState(menu, item, Update.Sub);
                    }
                    else if (item.Dynamic)
                        menu.SetInfo();
                }
                else if (s.UiInput.RightMouseReleased)
                {
                    var menu = GetCurrentMenu();
                    var item = GetCurrentMenuItem();
                    if (item.ParentName != null)
                    {
                        UpdateState(menu, item, Update.Parent, item.SubName != null);
                    }
                    else if (menu.Name == "CompGroups") CloseWheel();
                }

                if (!s.UiInput.UiKeyPressed && s.UiInput.WheelForward)
                {
                    GetCurrentMenu().Move(Movement.Forward);
                }
                else if (!s.UiInput.UiKeyPressed && s.UiInput.WheelBackward)
                {
                    GetCurrentMenu().Move(Movement.Backward);
                }

                if (previousMenu != _currentMenu) {
                    Dirty = false;
                    SetCurrentMessage();
                }
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
            var currentItem = GetCurrentMenuItem();
            var foreTexture = currentItem.ForeTexture;
            var backTexture = currentItem.BackTexture;
            var foreColor = Color.White * Session.UiOpacity;
            var backColor = Color.White * Session.UiBkOpacity;

            SetCurrentMessage();
            if (backTexture != MyStringId.NullOrEmpty) MyTransparentGeometry.AddBillboardOriented(backTexture, backColor, origin, left, up, (float)scale, BlendTypeEnum.PostPP);
            if (foreTexture != MyStringId.NullOrEmpty) MyTransparentGeometry.AddBillboardOriented(foreTexture, foreColor, origin, left, up, (float)scale, BlendTypeEnum.PostPP);
        }

        internal void OpenWheel()
        {
            WheelActive = true;
            if (Session.IsClient)
                Session.SendGroupUpdate(Ai);

            if (HudNotify == null) HudNotify = MyAPIGateway.Utilities.CreateNotification("[Grids]", 160, "UrlHighlight");
            if (string.IsNullOrEmpty(_currentMenu))
            {
                _currentMenu = "CompGroups";
                var menu = GetCurrentMenu();
                var item = GetCurrentMenuItem();
                UpdateState(menu, item, Update.None);
            }
            Ai.SupressMouseShoot = true;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, Session.PlayerId, false);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, Session.PlayerId, false);
            var controlStringMenuButton = MyAPIGateway.Input.GetControl(Session.UiInput.MouseButtonMenu)?.GetGameControlEnum().String;
            if (!string.IsNullOrEmpty(controlStringMenuButton))
            {
                MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMenuButton, Session.PlayerId, false);
            }
            else Log.Line($"OpenWheel mouseButtonControl null or empty: {Session.UiInput.MouseButtonMenu}");
        }

        internal void CloseWheel()
        {
            WheelActive = false;
            Ai.SupressMouseShoot = false;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, Session.PlayerId, true);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, Session.PlayerId, true);
            var controlStringMenuButton = MyAPIGateway.Input.GetControl(Session.UiInput.MouseButtonMenu)?.GetGameControlEnum().String;
            if (!string.IsNullOrEmpty(controlStringMenuButton))
            {
                MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMenuButton, Session.PlayerId, true);
            }
            else Log.Line($"CloseWheel mouseButtonControl null or empty: {Session.UiInput.MouseButtonMenu}");
            Session.RemoveGps();
        }
        
        internal void SetCurrentMessage()
        {
            var currentMenu = GetCurrentMenu();
            var currentMessage = currentMenu.Message;

            if (_currentMenu == "Group")
                currentMessage = currentMenu.CurrentItemMessage();
            
            if (currentMenu.GpsEntity != null)
            {
                var gpsName = currentMenu.GpsEntity.DisplayNameText;
                Session.AddGps(Color.Yellow);
                Session.SetGpsInfo(currentMenu.GpsEntity.PositionComp.GetPosition(), gpsName);
            }
            else Session.RemoveGps();
            string font;
            switch (currentMenu.Font)
            {
                case "Red":
                    font = currentMenu.Font;
                    break;
                case "Blue":
                    font = currentMenu.Font;
                    break;
                case "Green":
                    font = currentMenu.Font;
                    break;
                case "Yellow":
                    font = "White";
                    currentMessage = $"[{currentMessage}]";
                    break;
                default:
                    font = "White";
                    break;
            }

            if (Dirty)
                currentMenu.ReportInfo(GetCurrentMenuItem());

            HudNotify.Font = font; // BuildInfoHighlight, Red, Blue, Green, White, DarkBlue, 
            var oldText = HudNotify.Text;
            if (oldText != currentMessage)
                HudNotify.Hide();
            HudNotify.Text = currentMessage;
            HudNotify.Show();
        }

        internal Menu GetCurrentMenu()
        {
            var menu = Menus[_currentMenu];

            return menu;
        }

        internal Item GetCurrentMenuItem()
        {
            var menu = Menus[_currentMenu];
            return menu.Items[menu.CurrentSlot];
        }

        internal void SaveMenuInfo(Menu menu, Item item)
        {
            switch (menu.Name)
            {
                case "CompGroups":
                    ActiveGroupId = item.SubSlot;
                    break;
                case "Comps":
                    ActiveWeaponId = item.SubSlot;
                    break;
            }
        }

        internal void UpdateState(Menu oldMenu, Item item, Update update, bool reset = true)
        {
            Dirty = false;

            if (Ai.Construct.MenuBlockGroups.Count > 0)
            {
                switch (update)
                {
                    case Update.Parent:
                        _currentMenu = item.ParentName;
                        break;
                    case Update.Sub:
                        _currentMenu = item.SubName;
                        break;
                    default:
                        break;
                }
                var menu = Menus[_currentMenu];
                if (menu.ItemCount <= 1) 
                    menu.LoadInfo(reset);
            }
        }

        internal void ForceUpdate()
        {
            if (_currentMenu == "CompGroups")
            {
                UpdateState(GetCurrentMenu(), GetCurrentMenuItem(), Update.None);
            }
        }
    }
}
