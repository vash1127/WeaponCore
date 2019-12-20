using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
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
                if (s.UiInput.MouseButtonMiddle && ChangeState == State.Open)
                    OpenWheel();
                else if (s.UiInput.MouseButtonMiddle && ChangeState == State.Close) 
                    CloseWheel();


            }
            else if (WheelActive && !(Session.Session.ControlledObject is MyCockpit)) CloseWheel();
            
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
                        menu.SetInfo(item);
                }
                else if (s.UiInput.RightMouseReleased)
                {
                    Log.Line("RightMouseReleased");
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
            var currentItem = GetCurrentMenuItem();
            var foreTexture = currentItem.ForeTexture;
            var backTexture = currentItem.BackTexture;
            var foreColor = Color.White * Session.UIOpacity;
            var backColor = Color.White * Session.UIBkOpacity;

            SetCurrentMessage();
            if (backTexture != MyStringId.NullOrEmpty) MyTransparentGeometry.AddBillboardOriented(backTexture, backColor, origin, left, up, (float)scale, BlendTypeEnum.PostPP);
            if (foreTexture != MyStringId.NullOrEmpty) MyTransparentGeometry.AddBillboardOriented(foreTexture, foreColor, origin, left, up, (float)scale, BlendTypeEnum.PostPP);
        }

        internal void OpenWheel()
        {
            Log.Line($"open wheel");
            WheelActive = true;
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
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, false);
        }

        internal void CloseWheel()
        {
            WheelActive = false;
            Ai.SupressMouseShoot = false;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, true);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, true);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, true);
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
            HudNotify.Font = font; // BuildInfoHighlight, Red, Blue, Green, White, DarkBlue,  
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

        internal bool UpdateState(Menu oldMenu, Item item, Update update, bool reset = true)
        {
            if (reset)
            {
                oldMenu.CleanUp();
                GroupNames.Clear();

                foreach (var group in BlockGroups)
                {
                    group.Clear();
                    MembersPool.Return(group);
                }

                BlockGroups.Clear();

                foreach (var group in Ai.BlockGroups)
                {
                    var groupName = group.Key;
                    GroupNames.Add(groupName);
                    var membersList = MembersPool.Get();

                    foreach (var comp in group.Value.Comps)
                    {
                        var groupMember = new GroupMember { Comp = comp, Name = groupName };
                        membersList.Add(groupMember);
                    }
                    BlockGroups.Add(membersList);
                }
            }

            var groupReady = BlockGroups.Count > 0;
            if (groupReady)
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
                if (menu.ItemCount <= 1) menu.LoadInfo(reset);
            }

            return groupReady;
        }
    }
}
