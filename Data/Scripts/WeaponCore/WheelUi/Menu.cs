using System;
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
            internal Projectile Projectile;
            internal IMyGps Gps;
            internal List<GridAi.TargetInfo> Targets;
            internal List<Projectile> Projectiles;

            internal string Message;
            /*
            public string Message
            {
                get { return Message ?? string.Empty; }
                set { Message = value ?? string.Empty; }
            }
            */
            internal Menu(Wheel wheel, string name, Item[] items, int itemCount)
            {
                Wheel = wheel;
                Name = name;
                Items = items;
                ItemCount = itemCount;
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
                                Message = Items[CurrentSlot].Message;
                            }
                            else
                            {
                                var item = Items[0];
                                if (item.SubSlot < item.SubSlotCount - 1) item.SubSlot++;
                                else item.SubSlot = 0;
                                GetTargetInfo(item);
                            }

                            break;
                        }
                    case Movement.Backward:
                        if (ItemCount > 1)
                        {
                            if (CurrentSlot - 1 >= 0) CurrentSlot--;
                            else CurrentSlot = ItemCount - 1;
                            Message = Items[CurrentSlot].Message;
                        }
                        else
                        {
                            var item = Items[0];
                            if (item.SubSlot - 1 >= 0) item.SubSlot--;
                            else item.SubSlot = item.SubSlotCount - 1;
                            GetTargetInfo(item);
                        }
                        break;
                }
            }

            internal void GetTargetInfo(Item item)
            {
                switch (Name)
                {
                    case "Grids":
                    case "Characters":
                        if (Targets.Count > 0)
                        {
                            var target = Targets[item.SubSlot].Target;
                            if (target == null) break;
                            Target = target;
                            FormatGridMessage();
                        }
                        break;
                    case "Ordinance":
                        if (Projectiles.Count > 0)
                        {
                            var projectile = Projectiles[item.SubSlot];
                            if (projectile == null) break;
                            Projectile = projectile;
                            FormatProjectileMessage();
                        }
                        break;
                }
            }

            internal void FormatGridMessage()
            {
                if (Target == null || Target.MarkedForClose)
                {
                    Message = string.Empty;
                    return;
                }

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
                Message = message;
            }

            internal void FormatProjectileMessage()
            {
                if (Projectile == null || Projectile.State == Projectile.ProjectileState.Dead)
                {
                    Message = string.Empty;
                    return;
                }

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
                Message = message;
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

            internal void LoadInfo()
            {
                var item = Items[0];
                item.SubSlot = 0;
                switch (Name)
                {
                    case "Grids":
                        Targets = Wheel.Grids;
                        item.SubSlotCount = Targets.Count;
                        break;
                    case "Characters":
                        Targets = Wheel.Characters;
                        item.SubSlotCount = Targets.Count;
                        break;
                    case "Ordinance":
                        Projectiles = Wheel.Projectiles;
                        item.SubSlotCount = Projectiles.Count;
                        break;
                }

                if (Gps == null)
                {
                    Gps = MyAPIGateway.Session.GPS.Create("", "", Vector3D.MaxValue, true, true);
                    MyAPIGateway.Session.GPS.AddLocalGps(Gps);
                    MyVisualScriptLogicProvider.SetGPSColor(Gps.Name, Color.Yellow);
                }

                GetTargetInfo(item);
            }

            internal void CleanUp()
            {
                if (Gps != null)
                {
                    MyAPIGateway.Session.GPS.RemoveLocalGps(Gps);
                    Gps = null;
                }
                Targets?.Clear();
                Projectiles?.Clear();
            }
        }
    }
}
