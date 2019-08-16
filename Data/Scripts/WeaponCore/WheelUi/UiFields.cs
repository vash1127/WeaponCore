using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

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
        internal readonly Dictionary<string, Menu> Menus = new Dictionary<string, Menu>();

        internal readonly MyStringId[] TargetItems =
        {
           // MyStringId.GetOrCompute("DS_TargetWheel_NoSelect"),
           MyStringId.GetOrCompute("DS_TargetWheel_Weapons"),
           MyStringId.GetOrCompute("DS_TargetWheel_JumpDrive"),
           MyStringId.GetOrCompute("DS_TargetWheel_Engines"),
           MyStringId.GetOrCompute("DS_TargetWheel_Ordinance"),
           MyStringId.GetOrCompute("DS_TargetWheel_Power"),
           MyStringId.GetOrCompute("DS_TargetWheel_Comms"),
        };

        internal readonly MyStringId[] MainItems =
        {
            //MyStringId.GetOrCompute("DS_MainWheel_NoSelect"),
            MyStringId.GetOrCompute("DS_MainWheel_Grids"),
            MyStringId.GetOrCompute("DS_MainWheel_Players"),
            MyStringId.GetOrCompute("DS_MainWheel_WeaponGroups"),
            MyStringId.GetOrCompute("DS_MainWheel_Ordinance"),
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
                if (MyAPIGateway.Input.WasMiddleMouseReleased() && !WheelActive) return State.Open;
                if (MyAPIGateway.Input.WasMiddleMouseReleased() && WheelActive) return State.Close;
                return State.NoChange;
            }
            set { }
        }

        internal Wheel()
        {
            var main = new Menu("Main", MainItems, MainItems.Length);
            var targets = new Menu("Targets", TargetItems, TargetItems.Length);

            Menus.Add(main.Name, main);
            Menus.Add(targets.Name, targets);
        }

        internal class Menu
        {
            internal readonly string Name;
            internal readonly MyStringId[] Items;
            internal readonly int ItemCount;
            internal int CurrentSlot;

            internal Menu(string name, MyStringId[] items, int itemCount)
            {
                Name = name;
                Items = items;
                ItemCount = itemCount;
            }
        }
    }
}
