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
        internal class Item
        {
            internal MyStringId Texture;
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
            internal List<CompInfo> Comps;
            internal List<string> GroupNames;

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
                Log.Line($"GetCompInfo: name: {Name}");
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
                    case "Add":
                        if (Comps.Count > 0)
                        {
                            var compInfo = Comps[item.SubSlot];
                            if (!Wheel.Ai.WeaponBase.ContainsKey(compInfo.Comp.MyCube)) break;
                            FormatCompMessage(compInfo);
                        }
                        break;
                }
            }

            internal void FormatCompMessage(CompInfo compInfo)
            {
                Log.Line("format message");
                var message = $"testMessage";
                var gpsName = compInfo.Name;
                Wheel.Session.SetGpsInfo(compInfo.Comp.MyCube.PositionComp.GetPosition(), gpsName);
                Message = message;
            }

            internal void FormatGroupMessage(string groupName)
            {
                Log.Line("format message");
                var message = $"testMessage";
                Wheel.Session.SetGpsInfo(Wheel.Ai.GridCenter, groupName);
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
                    case "Add":
                        Comps = Wheel.Comps;
                        item.SubSlotCount = Comps.Count;
                        break;
                }

                Wheel.Session.ResetGps();
                GetInfo(item);
            }

            internal void CleanUp()
            {
                Log.Line("CleanUp");
                Wheel.Session.RemoveGps();
                Comps?.Clear();
            }
        }

        internal class MenuGroup
        {
            internal List<WeaponComponent> GroupComps = new List<WeaponComponent>();
            internal Dictionary<string, GroupState> GroupSettings = new Dictionary<string, GroupState>()
            {
                ["Group Active"] = GroupState.Enabled,
                ["Target Unarmed"] = GroupState.Enabled,
                ["Target Neutrals"] = GroupState.Enabled,
                ["Friendly Fire"] = GroupState.Enabled,
                ["Manual Aim"] = GroupState.Enabled,
                ["Manual Fire"] = GroupState.Enabled,
            };

            internal enum GroupState
            {
                Enabled,
                Disabled,
            }
        }

        internal struct CompInfo
        {
            internal WeaponComponent Comp;
            internal string Name;
        }
    }
}
