using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
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
        internal bool ResetMenu;
        internal readonly List<MyCubeGrid> Grids = new List<MyCubeGrid>();
        internal readonly List<IMyCharacter> Characters = new List<IMyCharacter>();
        internal readonly Dictionary<string, Menu> Menus = new Dictionary<string, Menu>();

        internal GridAi Ai;
        internal IMyHudNotification HudNotify;

        internal readonly Item[] SubSystemItems =
        {
           //new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_NoSelect"), Message = "SubSystems" },
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Comms"), Message = "[Production]", ParentName = "Main"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_JumpDrive"), Message = "[Navigation]", ParentName = "Main"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Engines"), Message = "[Engines]", ParentName = "Main"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Weapons"), Message = "[Weapons]", ParentName = "Main"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Power"), Message = "[Power]", ParentName = "Main"},
           new Item { Texture = MyStringId.GetOrCompute("DS_TargetWheel_Ordinance"), Message = "[Ordinance]", ParentName = "Main"},
        };

        internal readonly Item[] MainItems =
        {
            //new Item { Texture = MyStringId.GetOrCompute("DS_MainWheel_NoSelect"), Message = "Main" },
            new Item { Texture = MyStringId.GetOrCompute("DS_MainWheel_Grids"), Message = "[Grids]", SubName = "SubSystems"},
            new Item { Texture = MyStringId.GetOrCompute("DS_MainWheel_Players"), Message = "[Characters]" },
            new Item { Texture = MyStringId.GetOrCompute("DS_MainWheel_WeaponGroups"), Message = "[Weapon Groups]" },
            new Item { Texture = MyStringId.GetOrCompute("DS_MainWheel_Ordinance"), Message = "[Ordinance]" },
        };

        internal struct Item
        {
            internal MyStringId Texture;
            internal string Message;
            internal string SubName;
            internal string ParentName;
        }

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
                var cockPit = Session.Instance.Session.ControlledObject as MyCockpit;
                var isGridAi = cockPit != null && Session.Instance.GridTargetingAIs.TryGetValue(cockPit.CubeGrid, out Ai);
                if (MyAPIGateway.Input.WasMiddleMouseReleased() && !WheelActive && isGridAi) return State.Open;
                if (MyAPIGateway.Input.WasMiddleMouseReleased() && WheelActive) return State.Close;
                return State.NoChange;
            }
        }

        internal Wheel()
        {
            var main = new Menu("Main", MainItems, MainItems.Length);
            var subSystems = new Menu("SubSystems", SubSystemItems, SubSystemItems.Length);

            Menus.Add(main.Name, main);
            Menus.Add(subSystems.Name, subSystems);
        }

        internal class Menu
        {
            internal readonly string Name;
            internal readonly Item[] Items;
            internal readonly int ItemCount;
            internal int CurrentSlot;

            internal Menu(string name, Item[] items, int itemCount)
            {
                Name = name;
                Items = items;
                ItemCount = itemCount;
            }

            internal void StatusUpdate(Wheel wheel)
            {
                if (wheel.ResetMenu)
                {
                    var ai = wheel.Ai;
                    MyEntity target = null;
                    if (ai.SortedTargets.Count > 0) target = ai.SortedTargets[0].Target;
                    if (target != null) MyAPIGateway.Session.SetCameraController(MyCameraControllerEnum.SpectatorDelta, target);
                    wheel.ResetMenu = false;
                }
            }
        }
    }
}
