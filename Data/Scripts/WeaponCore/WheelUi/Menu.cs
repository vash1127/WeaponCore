using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using Math = System.Math;

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
            internal List<Projectile> Projectiles;
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
            internal MyEntity Target;
            internal IMyGps Gps;
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
                                Target = target;
                                message = FormatMessage();
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
                            Target = target;
                            message = FormatMessage();
                        }
                        break;
                }

                return message;
            }

            internal string FormatMessage()
            {
                if (Target == null || Target.MarkedForClose) return string.Empty;

                var targetDir = Vector3D.Normalize(Target.Physics?.LinearVelocity ?? Vector3.Zero);
                var targetPos = Target.PositionComp.WorldAABB.Center;

                var myPos = Wheel.Ai.MyGrid.PositionComp.WorldAABB.Center;
                var myHeading = Vector3D.Normalize(myPos - targetPos);
                var degrees = Math.Cos(MathHelper.ToRadians(25));
                var name = Target.DisplayName;
                var speed = Math.Round(Target.Physics?.Speed ?? 0, 2);
                var nameLen = 30;
                var armed = Session.Instance.GridTargetingAIs.ContainsKey((MyCubeGrid)Target);
                var intercept = Weapon.IsDotProductWithinTolerance(ref targetDir, ref myHeading, degrees);
                var armedStr = armed ? "Yes" : "No";
                var interceptStr = intercept ? "Yes" : "No";
                name = name.Replace("[", "(");
                name = name.Replace("]", ")");
                if (name.Length > nameLen) name = name.Substring(0, nameLen);
                var message = $"[Target:  {name}\n"
                          + $"Speed:  {speed} m/s\n"
                          + $"Armed:  {armedStr}\n" 
                          + $"Intercept:  {interceptStr}]";
                Gps.Coords = targetPos;
                Gps.Name = $"Speed:  {speed} m/s\n Armed:  {armedStr}\n Intercept:  {interceptStr}";
                return message;
            }

            internal void StatusUpdate(Wheel wheel)
            {
                if (wheel.ResetMenu)
                {
                    var ai = wheel.Ai;
                    if (Target != null) MyAPIGateway.Session.SetCameraController(MyCameraControllerEnum.SpectatorDelta, Target);
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
                    case "Ordinance":
                        item.Projectiles = Wheel.Projectiles;
                        item.SubSlotCount = item.Projectiles.Count;
                        break;
                }

                if (Gps == null)
                {
                    Gps = MyAPIGateway.Session.GPS.Create("", "", Vector3D.MaxValue, true, true);
                    MyAPIGateway.Session.GPS.AddLocalGps(Gps);
                    MyVisualScriptLogicProvider.SetGPSColor(Gps.Name, Color.Yellow);
                }
                var target = item.Targets.Count > 0 ? item.Targets[item.SubSlot].Target  : null;
                if (target != null)
                {
                    Target = target;
                    item.Message = FormatMessage();
                }
            }
        }
    }
}
