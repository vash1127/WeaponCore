using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    internal partial class Wheel
    {
        internal class Item
        {
            internal MyStringId Texture;
            internal string Message;
            internal string SubName;
            internal string ParentName;
            internal int SubSlot;
            internal int SubSlotCount;
            internal List<GridAi.TargetInfo> Targets;
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

            internal Menu(Wheel wheel, string name, Item[] items, int itemCount)
            {
                Wheel = wheel;
                Name = name;
                Items = items;
                ItemCount = itemCount;
            }

            internal string Move(Movement move)
            {
                var message = string.Empty;
                switch (move)
                {
                    case Movement.Forward:
                        {
                            if (ItemCount > 1)
                            {
                                if (CurrentSlot < ItemCount - 1) CurrentSlot++;
                                else CurrentSlot = 0;
                                message = Items[CurrentSlot].Message;
                            }
                            else
                            {
                                var item = Items[0];
                                if (item.SubSlot < item.SubSlotCount - 1) item.SubSlot++;
                                else item.SubSlot = 0;
                                var target = item.Targets[item.SubSlot].Target as MyCubeGrid;
                                if (target == null) break;
                                var name = target.DisplayName;
                                name = name.Replace("[", "(");
                                name = name.Replace("]", ")");
                                message = $"[Target:  {name}\n"
                                          + $"Speed:  {target.Physics?.Speed}\n"
                                          + $"Armed:  {Session.Instance.GridTargetingAIs.ContainsKey(target)}\n]";
                            }

                            break;
                        }
                    case Movement.Backward:
                        if (ItemCount > 1)
                        {
                            if (CurrentSlot - 1 >= 0) CurrentSlot--;
                            else CurrentSlot = ItemCount - 1;
                            message = Items[CurrentSlot].Message;
                        }
                        else
                        {
                            var item = Items[0];
                            if (item.SubSlot - 1 >= 0) item.SubSlot--;
                            else item.SubSlot = item.SubSlotCount - 1;
                            var target = item.Targets[item.SubSlot].Target as MyCubeGrid;
                            if (target == null) break;
                            var name = target.DisplayName;
                            name = name.Replace("[", "(");
                            name = name.Replace("]", ")");
                            message = $"Target:  {name}\n"
                                      + $"Speed:  {target.Physics?.Speed}\n"
                                      + $"Armed:  {Session.Instance.GridTargetingAIs.ContainsKey(target)}\n";
                        }
                        break;
                }
                return message;
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

            internal void Refresh()
            {
                var item = Items[0];
                item.SubSlot = 0;
                switch (Name)
                {
                    case "Grids":
                        item.Targets = Wheel.Grids;
                        item.SubSlotCount = item.Targets.Count;
                        break;
                    case "Characters":
                        item.Targets = Wheel.Characters;
                        item.SubSlotCount = item.Targets.Count;
                        break;
                }
            }
        }
    }
}
