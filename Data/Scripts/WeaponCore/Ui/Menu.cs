using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
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
            internal bool OtherArms;
            internal string Threat;
            internal Projectile Projectile;
            internal List<MenuTarget> Targets;

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
                                GetTargetInfo(item);
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
                            var target = Targets[item.SubSlot];
                            if (target.MyEntity == null) break;
                            OtherArms = target.OtherArms;
                            Threat = target.Threat;
                            Session.Instance.SetTarget(target.MyEntity, Wheel.Ai);
                            FormatGridMessage();
                        }
                        break;
                    case "Ordinance":
                        if (Targets.Count > 0)
                        {
                            var target = Targets[item.SubSlot];
                            if (target.Projectile == null) break;
                            OtherArms = target.OtherArms;
                            Threat = target.Threat;
                            Projectile = target.Projectile;
                            FormatProjectileMessage();
                        }
                        break;
                }
            }

            internal void FormatGridMessage()
            {
                if (!Session.Instance.CheckTarget(Wheel.Ai))
                {
                    Message = string.Empty;
                    return;
                }

                var target = Wheel.Ai.PrimeTarget;
                var targetDir = Vector3D.Normalize(target.Physics?.LinearVelocity ?? Vector3.Zero);
                var targetPos = target.PositionComp.WorldAABB.Center;

                var myPos = Wheel.Ai.MyGrid.PositionComp.WorldAABB.Center;
                var myHeading = Vector3D.Normalize(myPos - targetPos);
                var degrees = Math.Cos(MathHelper.ToRadians(25));
                var name = target.DisplayName;
                var speed = Math.Round(target.Physics?.Speed ?? 0, 2);
                var nameLen = 30;
                var armed = OtherArms || Session.Instance.GridTargetingAIs.ContainsKey((MyCubeGrid)target);
                var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, degrees);
                var armedStr = armed ? "Yes" : "No";
                var interceptStr = intercept ? "Yes" : "No";
                name = name.Replace("[", "(");
                name = name.Replace("]", ")");
                if (name.Length > nameLen) name = name.Substring(0, nameLen);
                var message = $"[Target:  {name}\n"
                              + $"Speed:  {speed} m/s\n"
                              + $"Armed:  {armedStr}\n"
                              + $"Threat:  {Threat}\n"
                              + $"Intercept:  {interceptStr}]";
                var gpsName = $"Speed:  {speed} m/s\n Armed:  {armedStr}\n Threat:  {Threat}\n Intercept:  {interceptStr}";
                Session.Instance.SetGpsInfo(targetPos, gpsName);
                Message = message;
            }
            internal void FormatProjectileMessage()
            {
                if (Projectile == null || Projectile.State == Projectile.ProjectileState.Dead)
                {
                    Message = string.Empty;
                    return;
                }

                var target = Wheel.Ai.PrimeTarget;
                var targetDir = Vector3D.Normalize(target.Physics?.LinearVelocity ?? Vector3.Zero);
                var targetPos = target.PositionComp.WorldAABB.Center;

                var myPos = Wheel.Ai.MyGrid.PositionComp.WorldAABB.Center;
                var myHeading = Vector3D.Normalize(myPos - targetPos);
                var degrees = Math.Cos(MathHelper.ToRadians(25));
                var name = target.DisplayName;
                var speed = Math.Round(target.Physics?.Speed ?? 0, 1);
                var nameLen = 30;
                var armed = Session.Instance.GridTargetingAIs.ContainsKey((MyCubeGrid)target);
                var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, degrees);
                var armedStr = armed ? "Yes" : "No";
                var interceptStr = intercept ? "Yes" : "No";
                name = name.Replace("[", "(");
                name = name.Replace("]", ")");
                if (name.Length > nameLen) name = name.Substring(0, nameLen);
                var message = $"[Target:  {name}\n"
                              + $"Speed:  {speed} m/s\n"
                              + $"Armed:  {armedStr}\n"
                              + $"Threat:  {Threat}\n"  
                              + $"Intercept:  {interceptStr}]";
                var gpsName = $"Speed:  {speed} m/s\n Armed:  {armedStr}\n Threat:  {Threat}\n Intercept:  {interceptStr}";
                Session.Instance.SetGpsInfo(targetPos, gpsName);
                Message = message;
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
                        Targets = Wheel.Projectiles;
                        item.SubSlotCount = Targets.Count;
                        break;
                }

                Session.Instance.ResetGps();
                GetTargetInfo(item);
            }

            internal void CleanUp()
            {
                Session.Instance.RemoveGps();
                Targets?.Clear();
            }
        }
    }
}
