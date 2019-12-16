using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
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

        internal readonly Session Session;
        internal GridAi Ai;
        internal IMyHudNotification HudNotify;
        internal bool WheelActive;
        internal string ActiveGroupName;
        internal int ActiveGroupId;
        internal int ActiveWeaponId;
        internal int CurrentTextureId;

        internal enum Update
        {
            Parent,
            Sub,
            None
        }

        internal struct Names
        {
            internal string Value;
            internal string CurrentValue;
            internal string NextValue;
            internal string PreviousValue;
        }

        internal readonly Item[] CompGroups =
        {
            new Item {Title = "Select Group", SubName = "Group"},
        };

        internal readonly Item[] Group =
        {
            new Item {ItemMessage = "Group Settings", Texture = MyStringId.GetOrCompute("DS_GroupSettings"), ParentName = "CompGroups", SubName = "GroupSettings"},
            new Item {ItemMessage = "Modify Weapons",  Texture = MyStringId.GetOrCompute("DS_ModifyWeapon"), ParentName = "CompGroups", SubName = "Comps"},
        };

        internal readonly Item[] Settings =
        {
            new Item {Title = "Settings Menu", Dynamic = true, ParentName = "Group"},
        };

        internal readonly Item[] Comps =
        {
            new Item {Title = "Comps", ParentName = "Group", SubName = "CompSettings"},
        };

        internal readonly Item[] CompSet =
        {
            new Item {Title = "Comp Settings", Dynamic = true, ParentName = "Comps"},
        };

        internal readonly Dictionary<string, Dictionary<int, Names>> SettingCycleStrMap = new Dictionary<string, Dictionary<int, Names>>()
        {
            {
                "Active", new Dictionary<int, Names>
                {
                    [0] = new Names {Value = "Deactivated", CurrentValue = "Deactivate", NextValue = "Activate"},
                    [1] = new Names {Value = "Activated", CurrentValue = "Activate", NextValue = "Deactivate"},
                }
            },
            {
                "Neutrals", new Dictionary<int, Names>
                {
                    [0] = new Names {Value = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Value = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Friends", new Dictionary<int, Names>
                {
                    [0] = new Names {Value = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Value = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Unowned", new Dictionary<int, Names>
                {
                    [0] = new Names {Value = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Value = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Manual Aim", new Dictionary<int, Names>
                {
                    [0] = new Names {Value = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Value = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Manual Fire", new Dictionary<int, Names>
                {
                    [0] = new Names {Value = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Value = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Focus Targets", new Dictionary<int, Names>
                {
                    [0] = new Names {Value = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Value = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Focus SubSystem", new Dictionary<int, Names>
                {
                    [0] = new Names {Value = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Value = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Sub Systems", new Dictionary<int, Names>
                {
                    [0] = new Names {Value = "Any", CurrentValue = "Any", NextValue = "Offense", PreviousValue = "Steering"},
                    [1] = new Names {Value = "Offense", CurrentValue = "Offense", NextValue = "Utility", PreviousValue = "Any"},
                    [2] = new Names {Value = "Utility", CurrentValue = "Utility", NextValue = "Power", PreviousValue = "Offense"},
                    [3] = new Names {Value = "Power", CurrentValue = "Power", NextValue = "Production", PreviousValue = "Utility"},
                    [4] = new Names {Value = "Production", CurrentValue = "Production", NextValue = "Thrust", PreviousValue = "Power"},
                    [5] = new Names {Value = "Thrust", CurrentValue = "Thrust", NextValue = "Jumping", PreviousValue = "Production"},
                    [6] = new Names {Value = "Jumping", CurrentValue = "Jumping", NextValue = "Steering", PreviousValue = "Thrust"},
                    [7] = new Names {Value = "Steering", CurrentValue = "Steering", NextValue = "Any", PreviousValue = "Jumping"},
                }
            },
        };

        internal readonly Dictionary<string, Dictionary<string, int>> SettingStrToValues = new Dictionary<string, Dictionary<string, int>>()
        {
            {
                "Active", new Dictionary<string, int>
                {
                    ["Activate"] = 1,
                    ["Deactivate"] = 0,
                }
            },
            {
                "Neutrals", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Unowned", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Friends", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Manual Aim", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Manual Fire", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Focus Targets", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Focus SubSystem", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Sub Systems", new Dictionary<string, int>
                {
                    ["Any"] = 0,
                    ["Offense"] = 1,
                    ["Utility"] = 2,
                    ["Power"] = 3,
                    ["Production"] = 4,
                    ["Thrust"] = 5,
                    ["Jumping"] = 6,
                    ["Steering"] = 7,
                }
            },
        };

        internal readonly List<string> SettingNames = new List<string>();

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
            var compGroups = new Menu(this, "CompGroups", CompGroups, CompGroups.Length, "Green");
            var group = new Menu(this, "Group", Group, Group.Length, "Green");
            var groupSettings = new Menu(this, "GroupSettings", Settings, Settings.Length, "Red");
            var comps = new Menu(this, "Comps", Comps, Comps.Length, "Yellow");
            var compSet = new Menu(this, "CompSettings", CompSet, CompSet.Length, "Yellow");

            var tmpGroupInfo = new GroupInfo();
            foreach (var groupInfo in tmpGroupInfo.Settings.Keys)
                SettingNames.Add(groupInfo);

            Menus.Add(compGroups.Name, compGroups);
            Menus.Add(group.Name, group);
            Menus.Add(groupSettings.Name, groupSettings);
            Menus.Add(comps.Name, comps);
            Menus.Add(compSet.Name, compSet);
        }
    }
}
