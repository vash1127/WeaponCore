using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    internal partial class Wheel
    {
        internal struct GroupMember
        {
            internal string Name;
            internal WeaponComponent Comp;
        }

        internal class Item
        {
            internal string Title;
            internal string ItemMessage;
            internal string SubName;
            internal string ParentName;
            internal MyStringId ForeTexture;
            internal MyStringId BackTexture;
            internal int SubSlot;
            internal int SubSlotCount;
            internal bool Dynamic;
        }

        internal class Menu
        {
            internal enum Movement
            {
                Forward,
                Backward,
            }

            internal readonly Wheel Wheel;
            internal readonly string Name;
            internal readonly Item[] Items;
            internal readonly int ItemCount;
            internal int CurrentSlot;
            internal List<string> GroupNames;
            internal List<List<GroupMember>> BlockGroups;
            internal MyEntity GpsEntity;
            internal string Font;

            private string _message = "You have no weapons assigned to groups!";
            public string Message
            {
                get { return _message ?? string.Empty; }
                set { _message = value ?? string.Empty; }
            }

            internal Menu(Wheel wheel, string name, Item[] items, int itemCount, string font = "White")
            {
                Wheel = wheel;
                Name = name;
                Items = items;
                ItemCount = itemCount;
                Font = font;
            }

            internal string CurrentItemMessage()
            {
                var currentItemMessage = Items[CurrentSlot].ItemMessage;
                switch (Name)
                {
                    case "Group":
                        if (Wheel.GroupNames.Count > 0)
                        {
                            currentItemMessage = $"# {Wheel.ActiveGroupName} #";
                        }
                        break;
                    default:
                        break;
                }
                return currentItemMessage;
            }

            internal void Move(Movement move)
            {
                switch (move)
                {
                    case Movement.Forward:
                        {
                            if (ItemCount > 1)
                            {
                                if (CurrentSlot < ItemCount - 1) CurrentSlot++;
                                else CurrentSlot = 0;
                                var item = Items[CurrentSlot];
                                PickMessage(item);
                            }
                            else
                            {
                                var item = Items[0];
                                if (item.SubSlot < item.SubSlotCount - 1) item.SubSlot++;
                                else item.SubSlot = 0;
                                PickMessage(item);
                            }

                            break;
                        }
                    case Movement.Backward:
                        if (ItemCount > 1)
                        {
                            if (CurrentSlot - 1 >= 0) CurrentSlot--;
                            else CurrentSlot = ItemCount - 1;
                            var item = Items[CurrentSlot];
                            PickMessage(item);
                        }
                        else
                        {
                            var item = Items[0];
                            if (item.SubSlot - 1 >= 0) item.SubSlot--;
                            else item.SubSlot = item.SubSlotCount - 1;
                            PickMessage(item);
                        }
                        break;
                }
            }

            internal void PickMessage(Item item)
            {
                if (Wheel.BlockGroups == null || item == null || Wheel.BlockGroups.Count == 0) return;
                Wheel.Dirty = false;
                GroupInfo groupInfo;
                switch (Name)
                {
                    case "Group":
                        if (Wheel.GroupNames.Count > 0 && Wheel.ActiveGroupName != null)
                        {
                            Message = $"# {Wheel.ActiveGroupName} #\n{item.ItemMessage}";
                        }
                        break;
                    case "CompGroups":
                        if (GroupNames.Count > 0)
                        {
                            var groupName = GroupNames[item.SubSlot];

                            if (!Wheel.Ai.BlockGroups.TryGetValue(groupName, out groupInfo)) break;
                            Wheel.ActiveGroupName = groupName;
                            FormatGroupMessage(groupInfo);
                        }
                        break;
                    case "GroupSettings":
                        if (Wheel.ActiveGroupName != null)
                        {
                            if (!Wheel.Ai.BlockGroups.TryGetValue(Wheel.ActiveGroupName, out groupInfo)) break;
                            ReportGroupSettings(groupInfo, item);
                        }
                        break;
                    case "Comps":
                        if (BlockGroups.Count > 0)
                        {
                            var groupMember = BlockGroups[Wheel.ActiveGroupId][item.SubSlot];
                            if (!Wheel.Ai.BlockGroups.TryGetValue(groupMember.Name, out groupInfo)) break;
                            FormatCompMessage(groupMember, Color.Yellow);
                        }
                        break;
                    case "CompSettings":
                        if (Wheel.BlockGroups.Count > 0)
                        {
                            var groupMember = Wheel.BlockGroups[Wheel.ActiveGroupId][Wheel.ActiveWeaponId];
                            if (!Wheel.Ai.BlockGroups.TryGetValue(groupMember.Name, out groupInfo)) break;
                            FormatCompMessage(groupMember, Color.DarkOrange);
                            ReportMemberSettings(groupInfo, groupMember, item);
                        }
                        break;
                }
            }

            internal void SetInfo()
            {
                Log.Line("set info");
                GroupInfo groupInfo;
                switch (Name)
                {
                    case "GroupSettings":
                        if (!Wheel.Ai.BlockGroups.TryGetValue(Wheel.ActiveGroupName, out groupInfo)) break;
                        SetGroupSettings(groupInfo);
                        break;
                }
                switch (Name)
                {
                    case "CompSettings":
                        if (Wheel.BlockGroups.Count > 0)
                        {
                            var groupMember = Wheel.BlockGroups[Wheel.ActiveGroupId][Wheel.ActiveWeaponId];
                            if (!Wheel.Ai.BlockGroups.TryGetValue(groupMember.Name, out groupInfo)) break;
                            SetMemberSettings(groupInfo, groupMember);
                        }
                        break;
                }
            }

            internal void ReportInfo(Item item)
            {
                Log.Line("report info");
                GroupInfo groupInfo;
                switch (Name)
                {
                    case "GroupSettings":
                        if (!Wheel.Ai.BlockGroups.TryGetValue(Wheel.ActiveGroupName, out groupInfo)) break;
                        ReportGroupSettings(groupInfo, item);
                        break;
                }
                switch (Name)
                {
                    case "CompSettings":
                        if (Wheel.BlockGroups.Count > 0)
                        {
                            var groupMember = Wheel.BlockGroups[Wheel.ActiveGroupId][Wheel.ActiveWeaponId];
                            if (!Wheel.Ai.BlockGroups.TryGetValue(groupMember.Name, out groupInfo)) break;
                            ReportMemberSettings(groupInfo, groupMember, item);
                        }
                        break;
                }
                Wheel.Dirty = false;
            }

            internal void ReportGroupSettings(GroupInfo groupInfo, Item item)
            {
                var settingName = Wheel.SettingNames[Items[CurrentSlot].SubSlot];
                var setting = Wheel.SettingCycleStrMap[settingName];
                var current = setting[groupInfo.Settings[settingName]].CurrentValue;
                item.ForeTexture = Wheel.SettingStrToTextures[settingName][current][0];
                item.BackTexture = Wheel.SettingStrToTextures[settingName][current][1];
                var message = $"# {groupInfo.Name} #";
                Message = message;
            }

            internal void SetGroupSettings(GroupInfo groupInfo)
            {
                var s = Wheel.Session;
                var currentSettingName = Wheel.SettingNames[Items[CurrentSlot].SubSlot];
                var currentValue = groupInfo.Settings[currentSettingName];
                var map = Wheel.SettingCycleStrMap[currentSettingName];
                var nextValueStr = map[currentValue].NextValue;
                var nextValue = Wheel.SettingStrToValues[currentSettingName][nextValueStr];
                groupInfo.RequestApplySettings(Wheel.Ai, currentSettingName, nextValue, s);
                if (Wheel.Session.IsServer) Wheel.Dirty = true;
            }

            internal void SetMemberSettings(GroupInfo groupInfo, GroupMember groupMember)
            {
                var settingName = Wheel.SettingNames[Items[CurrentSlot].SubSlot];
                var settingMap = Wheel.SettingCycleStrMap[settingName];
                var currentValue = groupInfo.GetCompSetting(settingName, groupMember.Comp);
                var nextValueToStr = settingMap[currentValue].NextValue;
                var nextValue = Wheel.SettingStrToValues[settingName][nextValueToStr];
                groupInfo.RequestSetValue(groupMember.Comp, settingName, nextValue);
                if (Wheel.Session.IsServer) Wheel.Dirty = true;
            }

            internal void FormatCompMessage(GroupMember groupMember, Color color)
            {
                var message = groupMember.Name;
                GpsEntity = groupMember.Comp.MyCube;
                var gpsName = GpsEntity.DisplayNameText;
                Wheel.Session.SetGpsInfo(GpsEntity.PositionComp.GetPosition(), gpsName, 0, color);
                Message = $"# {message} #";
            }

            internal void FormatGroupMessage(GroupInfo groupInfo)
            {
                var message = $"# {groupInfo.Name} #";
                Message = message;
            }

            internal void ReportMemberSettings(GroupInfo groupInfo, GroupMember groupMember, Item item)
            {
                var settingName = Wheel.SettingNames[Items[CurrentSlot].SubSlot];
                var setting = Wheel.SettingCycleStrMap[settingName];
                var current = setting[groupInfo.GetCompSetting(settingName, groupMember.Comp)].CurrentValue;
                item.ForeTexture = Wheel.SettingStrToTextures[settingName][current][0];
                item.BackTexture = Wheel.SettingStrToTextures[settingName][current][1];
                var message = $"# {groupInfo.Name} #";
                Message = message;
            }

            internal void LoadInfo(bool reset = true)
            {
                var item = Items[0];
                if (reset)
                {
                    item.SubSlot = 0;
                    switch (Name)
                    {
                        case "CompGroups":
                            GroupNames = Wheel.GroupNames;
                            item.SubSlotCount = GroupNames.Count;
                            break;
                        case "GroupSettings":
                            item.SubSlotCount = Wheel.SettingStrToValues.Count;
                            break;
                        case "Comps":
                            BlockGroups = Wheel.BlockGroups;
                            item.SubSlotCount = BlockGroups[Wheel.ActiveGroupId].Count;
                            break;
                        case "CompSettings":
                            item.SubSlotCount = Wheel.SettingStrToValues.Count;
                            break;
                    }
                }

                PickMessage(item);
            }

            internal void CleanUp()
            {
                GroupNames?.Clear();
                BlockGroups?.Clear();
            }
        }
    }
}
