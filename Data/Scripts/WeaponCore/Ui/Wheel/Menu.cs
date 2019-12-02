using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Support.TargetingDefinition;

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
            internal MyStringId Texture;
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

                            GroupInfo groupInfo;
                            if (!Wheel.Ai.BlockGroups.TryGetValue(groupName, out groupInfo)) break;
                            FormatGroupMessage(groupName);
                        }
                        break;
                    case "Weapons":
                        if (BlockGroups.Count > 0)
                        {
                            var groupMember = BlockGroups[Wheel.ActiveGroupId][item.SubSlot];
                            GroupInfo groupInfo;
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
                        item.SubSlotCount = BlockGroups[Wheel.ActiveGroupId].Count;
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
