using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Support;

namespace WeaponCore
{
    internal partial class Wheel
    {

        internal struct GroupInfo
        {
            internal string Title;
            internal WeaponComponent Comps;
        }

        internal class Item
        {
            internal MyStringId Texture;
            internal GroupInfo GroupInfo;
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
            internal List<List<GroupInfo>> BlockGroups;

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
                                switch (Name)
                                {
                                    case "WeaponGroups":
                                        GetInfo(item);
                                        break;
                                    default:
                                        GetInfo(item);
                                        break;
                                }
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
                            switch (Name)
                            {
                                case "WeaponGroups":
                                    GetInfo(item);
                                    break;
                                default:
                                    GetInfo(item);
                                    break;
                            }
                        }
                        break;
                }
            }

            internal void GetInfo(Item item)
            {
                switch (Name)
                {
                    case "WeaponGroups":
                        if (GroupNames.Count > 0)
                        {
                            var groupName = GroupNames[item.SubSlot];

                            HashSet<WeaponComponent> weaponGroup;
                            if (!Wheel.Ai.BlockGroups.TryGetValue(groupName, out weaponGroup)) break;
                            FormatGroupMessage(groupName);
                        }
                        break;
                    case "Weapons":
                        if (BlockGroups.Count > 0)
                        {
                            var groupInfo = BlockGroups[Wheel.ActiveGroupId][item.SubSlot];
                            HashSet<WeaponComponent> weaponGroup;
                            if (!Wheel.Ai.BlockGroups.TryGetValue(groupInfo.Title, out weaponGroup)) break;
                            FormatWeaponMessage(groupInfo);
                        }
                        break;
                }
            }

            internal void FormatWeaponMessage(GroupInfo groupInfo)
            {
                var message = groupInfo.Title;
                var gpsName = groupInfo.Comps.MyCube.DisplayNameText;
                Wheel.Session.SetGpsInfo(groupInfo.Comps.MyCube.PositionComp.GetPosition(), gpsName);
                Message = message;
            }

            internal void FormatGroupMessage(string groupName)
            {
                var message = groupName;
                //Wheel.Session.SetGpsInfo(Wheel.Ai.GridCenter, groupName);
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
                    case "Weapons":
                        BlockGroups = Wheel.BlockGroups;
                        item.SubSlotCount = BlockGroups.Count;
                        break;
                }

                Wheel.Session.ResetGps();
                GetInfo(item);
            }

            internal void CleanUp()
            {
                Log.Line("CleanUp");
                Wheel.Session.RemoveGps();
                GroupNames?.Clear();
                BlockGroups?.Clear();
            }
        }
    }
}
