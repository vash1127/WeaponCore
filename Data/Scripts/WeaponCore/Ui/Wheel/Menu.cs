using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Utils;
using WeaponCore.Support;

namespace WeaponCore
{
    internal partial class Wheel
    {

        internal struct GroupMember
        {
            internal string Name;
            internal WeaponComponent Comps;
        }

        internal class Item
        {
            internal string Title;
            internal string ItemMessage;
            internal string SubName;
            internal string ParentName;
            internal int SubSlot;
            internal int SubSlotCount;
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

            private string _message;
            public string Message
            {
                get { return _message ?? string.Empty; }
                set { _message = value ?? string.Empty; }
            }

            internal Menu(Wheel wheel, string name, Item[] items, int itemCount)
            {
                Wheel = wheel;
                Name = name;
                Items = items;
                ItemCount = itemCount;
            }

            internal string CurrentItemMessage()
            {
                return Items[CurrentSlot].ItemMessage;
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
                                Message = Items[CurrentSlot].ItemMessage;
                            }
                            else
                            {
                                var item = Items[0];
                                if (item.SubSlot < item.SubSlotCount - 1) item.SubSlot++;
                                else item.SubSlot = 0;
                                GetInfo(item);
                            }

                            break;
                        }
                    case Movement.Backward:
                        if (ItemCount > 1)
                        {
                            if (CurrentSlot - 1 >= 0) CurrentSlot--;
                            else CurrentSlot = ItemCount - 1;
                            Message = Items[CurrentSlot].ItemMessage;
                        }
                        else
                        {
                            var item = Items[0];
                            if (item.SubSlot - 1 >= 0) item.SubSlot--;
                            else item.SubSlot = item.SubSlotCount - 1;
                            GetInfo(item);
                        }
                        break;
                }
            }

            internal void GetInfo(Item item)
            {
                GroupInfo groupInfo;
                switch (Name)
                {
                    case "WeaponGroups":
                        if (GroupNames.Count > 0)
                        {
                            var groupName = GroupNames[item.SubSlot];

                            if (!Wheel.Ai.BlockGroups.TryGetValue(groupName, out groupInfo)) break;
                            Wheel.ActiveGroupName = groupName;
                            FormatGroupMessage(groupInfo);
                        }
                        break;
                    case "Settings":
                        if (!Wheel.Ai.BlockGroups.TryGetValue(Wheel.ActiveGroupName, out groupInfo)) break;
                        FormatSettingsMessage(groupInfo);
                        break;
                    case "Weapons":
                        if (BlockGroups.Count > 0)
                        {
                            var groupMember = BlockGroups[Wheel.ActiveGroupId][item.SubSlot];
                            if (!Wheel.Ai.BlockGroups.TryGetValue(groupMember.Name, out groupInfo)) break;
                            FormatWeaponMessage(groupMember);
                        }
                        break;
                }
            }

            internal void FormatWeaponMessage(GroupMember groupMember)
            {
                var message = groupMember.Name;
                GpsEntity = groupMember.Comps.MyCube;
                var gpsName = GpsEntity.DisplayNameText;
                Wheel.Session.SetGpsInfo(GpsEntity.PositionComp.GetPosition(), gpsName);
                Message = $"[{message}]";
            }

            internal void FormatGroupMessage(GroupInfo groupInfo)
            {
                var enabledValueString = Wheel.SettingStrings["Active"][groupInfo.Settings["Active"]].Value;
                var message = $"[Weapon Group:\n{groupInfo.Name} ({enabledValueString})]";
                Message = message;
            }

            internal void FormatSettingsMessage(GroupInfo groupInfo)
            {
                var settingName = Wheel.SettingNames[Items[CurrentSlot].SubSlot];
                var setting = Wheel.SettingStrings[settingName];
                var currentState = setting[groupInfo.Settings[settingName]].Value;
                var message = $"[{settingName} ({currentState})]";
                Message = message;
            }

            internal void LoadInfo()
            {
                var item = Items[0];
                item.SubSlot = 0;
                Log.Line($"LoadInfo");
                switch (Name)
                {
                    case "WeaponGroups":
                        GroupNames = Wheel.GroupNames;
                        item.SubSlotCount = GroupNames.Count;
                        break;
                    case "Settings":
                        item.SubSlotCount = Wheel.SettingStrings.Count;
                        break;
                    case "Weapons":
                        BlockGroups = Wheel.BlockGroups;
                        item.SubSlotCount = BlockGroups[Wheel.ActiveGroupId].Count;
                        break;
                }

                GetInfo(item);
            }

            internal void CleanUp()
            {
                GroupNames?.Clear();
                BlockGroups?.Clear();
            }
        }
    }
}
