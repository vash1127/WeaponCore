using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
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
        internal bool WheelActive;
        internal readonly MenuGroup[] Groups = new MenuGroup[10];
        internal readonly Dictionary<string, Menu> Menus = new Dictionary<string, Menu>();
        internal readonly List<CompInfo> Comps = new List<CompInfo>();
        internal readonly List<string> GroupNames = new List<string>();
        internal readonly List<HashSet<WeaponComponent>> BlockGroups = new List<HashSet<WeaponComponent>>();

        internal readonly Session Session;
        internal GridAi Ai;
        internal IMyHudNotification HudNotify;

        internal readonly Item[] GroupItems =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "Assign Subsystem]", ParentName = "WeaponGroups", SubName = "SubSystems"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "Remove Weapons]", ParentName = "WeaponGroups", SubName = "Remove"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "Add Weapons]", ParentName = "WeaponGroups", SubName = "Add"},
        };

        internal readonly Item[] AddItems =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "Add]", ParentName = "Group"},
        };

        internal readonly Item[] RemoveItems =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "Remove]", ParentName = "Group"},
        };

        internal readonly Item[] SubSystemItems =
        {
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Comms"), ItemMessage = "Production]", ParentName = "Group"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_JumpDrive"), ItemMessage = "Navigation]", ParentName = "Group"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Engines"), ItemMessage = "Engines]", ParentName = "Group"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Weapons"), ItemMessage = "Weapons]", ParentName = "Group"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Power"), ItemMessage = "Power]", ParentName = "Group"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Ordinance"), ItemMessage = "Ordinance]", ParentName = "Group"},
        };

        internal readonly Item[] WeaponGroupItems =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "Weapon Groups]", SubName = "Group"},
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
            var add = new Menu(this, "Add", AddItems, AddItems.Length);
            var remove = new Menu(this, "Remove", RemoveItems, RemoveItems.Length);
            var group = new Menu(this, "Group", GroupItems, GroupItems.Length);
            var subSystems = new Menu(this, "SubSystems", SubSystemItems, SubSystemItems.Length);

            var weaponGroups = new Menu(this, "WeaponGroups", WeaponGroupItems, WeaponGroupItems.Length);

            Menus.Add(add.Name, add);
            Menus.Add(remove.Name, remove);
            Menus.Add(weaponGroups.Name, weaponGroups);
            Menus.Add(group.Name, group);
            Menus.Add(subSystems.Name, subSystems);
            for (int i = 0; i < Groups.Length; i++) Groups[i] = new MenuGroup();
        }
    }
}
