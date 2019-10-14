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

        private string _currentMenu = "Main";
        private int _previousWheel;
        private int _currentWheel;
        internal bool WheelActive;
        internal bool MouseButtonPressed;
        internal bool MouseButtonLeft;
        internal bool MouseButtonMiddle;
        internal bool MouseButtonRight;
        internal readonly List<MenuTarget> Grids = new List<MenuTarget>();
        internal readonly List<MenuTarget> Characters = new List<MenuTarget>();
        internal readonly List<MenuTarget> Projectiles = new List<MenuTarget>();
        internal readonly Dictionary<string, Menu> Menus = new Dictionary<string, Menu>();
        internal readonly Session Session;
        internal GridAi Ai;
        internal IMyHudNotification HudNotify;

        internal readonly Item[] GridItems =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "[Grids]", ParentName = "Main", SubName = "SubSystems"},
        };

        internal readonly Item[] CharacterItems =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "[Characters]", ParentName = "Main", SubName = "WeaponGroups"},
        };

        internal readonly Item[] OrdinanceItems =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Empty_Wheel"), ItemMessage = "[Ordinance]", ParentName = "Main", SubName = "WeaponGroups"},
        };

        internal readonly Item[] SubSystemItems =
        {
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Comms"), ItemMessage = "[Production]", ParentName = "Grids"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_JumpDrive"), ItemMessage = "[Navigation]", ParentName = "Grids"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Engines"), ItemMessage = "[Engines]", ParentName = "Grids"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Weapons"), ItemMessage = "[Weapons]", ParentName = "Grids"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Power"), ItemMessage = "[Power]", ParentName = "Grids"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Ordinance"), ItemMessage = "[Ordinance]", ParentName = "Grids"},
        };

        internal readonly Item[] MainItems =
        {
            //new Item { Texture = MyStringId.GetOrCompute("DS_MainWheel_NoSelect"), Message = "Main" },
            new Item { Texture = MyStringId.GetOrCompute("DS_MainWheel_Grids"), ItemMessage = "[Grids]", SubName = "Grids"},
            new Item { Texture = MyStringId.GetOrCompute("DS_MainWheel_Players"), ItemMessage = "[Characters]", SubName = "Characters"},
            new Item { Texture = MyStringId.GetOrCompute("DS_MainWheel_WeaponGroups"), ItemMessage = "[Weapon Groups]", SubName = "WeaponGroups"},
            new Item { Texture = MyStringId.GetOrCompute("DS_MainWheel_Ordinance"), ItemMessage = "[Ordinance]", SubName = "Ordinance"},
        };

        internal readonly Item[] WeaponGroupItems =
        {
            new Item { Texture = MyStringId.GetOrCompute("DS_Group_Wheel_0"), ItemMessage = "[Group 0]", ParentName = "Main"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Group_Wheel_1"), ItemMessage = "[Group 1]", ParentName = "Main"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Group_Wheel_2"), ItemMessage = "[Group 2]", ParentName = "Main"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Group_Wheel_3"), ItemMessage = "[Group 3]", ParentName = "Main"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Group_Wheel_4"), ItemMessage = "[Group 4]", ParentName = "Main"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Group_Wheel_5"), ItemMessage = "[Group 5]", ParentName = "Main"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Group_Wheel_6"), ItemMessage = "[Group 6]", ParentName = "Main"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Group_Wheel_7"), ItemMessage = "[Group 7]", ParentName = "Main"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Group_Wheel_8"), ItemMessage = "[Group 8]", ParentName = "Main"},
            new Item { Texture = MyStringId.GetOrCompute("DS_Group_Wheel_9"), ItemMessage = "[Group 9]", ParentName = "Main"},
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
            var main = new Menu(this, "Main", MainItems, MainItems.Length);
            var subSystems = new Menu(this, "SubSystems", SubSystemItems, SubSystemItems.Length);
            var grids = new Menu(this, "Grids", GridItems, GridItems.Length);
            var characters = new Menu(this, "Characters", CharacterItems, CharacterItems.Length);
            var ordinance = new Menu(this, "Ordinance", OrdinanceItems, OrdinanceItems.Length);
            var weaponGroups = new Menu(this, "WeaponGroups", WeaponGroupItems, WeaponGroupItems.Length);

            Menus.Add(main.Name, main);
            Menus.Add(subSystems.Name, subSystems);
            Menus.Add(grids.Name, grids);
            Menus.Add(characters.Name, characters);
            Menus.Add(ordinance.Name, ordinance);
            Menus.Add(weaponGroups.Name, weaponGroups);
        }

        internal struct MenuTarget
        {
            internal Projectile Projectile;
            internal MyEntity MyEntity;
            internal bool OtherArms;
            internal string Threat;
        }
    }
}
