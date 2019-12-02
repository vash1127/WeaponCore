using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Support;

namespace WeaponCore
{
    internal partial class Wheel
    {
        private readonly Vector2D _wheelPosition = new Vector2D(0, 0);

        private string _currentMenu;
        internal readonly Dictionary<string, Menu> Menus = new Dictionary<string, Menu>();
        internal readonly List<string> GroupNames = new List<string>();
        internal readonly List<List<GroupMember>> BlockGroups = new List<List<GroupMember>>();
        internal readonly MyConcurrentPool<List<GroupMember>> MembersPool = new MyConcurrentPool<List<GroupMember>>();
        internal readonly MyConcurrentPool<GroupInfo> GroupInfoPool = new MyConcurrentPool<GroupInfo>();

        internal readonly Session Session;
        internal GridAi Ai;
        internal IMyHudNotification HudNotify;
        internal bool WheelActive;
        internal int ActiveGroupId;
        internal int ActiveWeaponId;

        internal readonly Item[] WeaponGroups =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "Weapon Groups]", SubName = "Group"},
        };

        internal readonly Item[] Group =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "Group Settings]", ParentName = "WeaponGroups", SubName = "Settings"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "Modify Weapons]", ParentName = "WeaponGroups", SubName = "Weapons"},
        };

        internal readonly Item[] Settings =
        {
            new Item { Title = "Group Enabled", Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "", ParentName = "Group"},
            new Item { Title = "Attack Neutrals", Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "", ParentName = "Group"},
            new Item { Title = "Attack Friends", Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "", ParentName = "Group"},
            new Item { Title = "Manual Aim", Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "", ParentName = "Group"},
            new Item { Title = "Manual Fire", Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "", ParentName = "Group"},
            new Item { Title = "Target Subsystem", Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "", ParentName = "Group"},
        };

        internal readonly Item[] Weapons =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "Change Weapon]", ParentName = "Group"},
        };


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
                var cockPit = Session.Session.ControlledObject as MyCockpit;
                var isGridAi = cockPit != null && Session.GridTargetingAIs.TryGetValue(cockPit.CubeGrid, out Ai);
                if (MyAPIGateway.Input.WasMiddleMouseReleased() && !WheelActive && isGridAi) return State.Open;
                if (MyAPIGateway.Input.WasMiddleMouseReleased() && WheelActive) return State.Close;
                return State.NoChange;
            }
        }

        internal Wheel(Session session)
        {
            Session = session;
            var weaponGroups = new Menu(this, "WeaponGroups", WeaponGroups, WeaponGroups.Length);

            var group = new Menu(this, "Group", Group, Group.Length);
            var settings = new Menu(this, "Settings", Settings, Settings.Length);
            var weapons = new Menu(this, "Weapons", Weapons, Weapons.Length);


            Menus.Add(weaponGroups.Name, weaponGroups);
            Menus.Add(group.Name, group);
            Menus.Add(settings.Name, settings);
            Menus.Add(weapons.Name, weapons);
        }
    }
}
