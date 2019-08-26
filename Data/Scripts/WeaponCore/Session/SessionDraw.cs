using System;
using System.Collections.Generic;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
namespace WeaponCore
{
    public partial class Session
    {
        private void DrawLists(List<Trajectile> drawList)
        {
            var sFound = false;
            for (int i = 0; i < drawList.Count; i++)
            {
                var t = drawList[i];
                if (t.Entity != null)
                {
                    if (!t.Last && !t.Entity.InScene)
                    {
                        t.Entity.InScene = true;
                        t.Entity.Render.UpdateRenderObject(true, false);
                    }

                    t.Entity.PositionComp.SetWorldMatrix(t.EntityMatrix, null, false, false, false);
                    if (t.Last)
                    {
                        t.Entity.InScene = false;
                        t.Entity.Render.RemoveRenderObjects();
                    }
                    if (!t.System.Values.Graphics.Line.Trail) continue;
                }

                var width = t.LineWidth;
                if (t.Shrink && t.HitEntity != null)
                {
                    sFound = true;
                    var shrink = _shrinkPool.Get();
                    shrink.Init(t);
                    _shrinking.Add(shrink);
                }
                var color = t.Color;
                var newWidth = width;

                if (t.System.Values.Ammo.Trajectory.DesiredSpeed <= 0)
                {
                    var changeValue = 0.01f;
                    if (_lCount < 60)
                    {
                        var adder = (_lCount + 1);
                        var adder2 = adder * changeValue;
                        var adder3 = adder2 + 1;
                        newWidth = adder3 * width;
                        color *= adder3;
                    }
                    else
                    {
                        var shrinkFrom = ((60) * changeValue) + 1;

                        var adder = (_lCount - 59);
                        var adder2 = adder * changeValue;
                        var scaler = (shrinkFrom - adder2);
                        newWidth = scaler * width;
                        color *= (shrinkFrom - adder2);
                    }
                }

                var hitPos = t.PrevPosition + (t.Direction * t.Length);
                var distanceFromPoint = (float)Vector3D.Distance(CameraPos, (MyUtils.GetClosestPointOnLine(ref t.PrevPosition, ref hitPos, ref CameraPos)));
                var thickness = newWidth;
                if (distanceFromPoint < 10) thickness *= 0.25f;
                else if (distanceFromPoint < 20) thickness *= 0.5f;
                else if (distanceFromPoint > 400) thickness *= 8f;
                else if (distanceFromPoint > 200) thickness *= 4f;
                else if (distanceFromPoint > 100) thickness *= 2f;

                if (!t.System.IsBeamWeapon) MyTransparentGeometry.AddLocalLineBillboard(t.System.ProjectileMaterial, color, t.PrevPosition, uint.MaxValue, t.Direction, (float)t.Length, thickness);
                else MyTransparentGeometry.AddLineBillboard(t.System.ProjectileMaterial, color, t.PrevPosition, t.Direction, (float)t.Length, thickness);

                if (t.System.IsBeamWeapon && t.System.HitParticle && !(t.MuzzleId != 0 && (t.System.ConvergeBeams || t.System.OneHitParticle)))
                {
                    var c = t.Target.FiringCube;
                    if (c == null || c.MarkedForClose)
                    {
                        Log.Line($"FiringCube marked for close");
                        continue;
                    }
                    WeaponComponent weaponComp;
                    if (t.Ai.WeaponBase.TryGetValue(c, out weaponComp))
                    {
                        var weapon = weaponComp.Platform.Weapons[t.WeaponId];
                        var effect = weapon.HitEffects[t.MuzzleId];
                        if (t.HitEntity?.HitPos != null && t.OnScreen)
                        {
                            if (effect != null)
                            {
                                var elapsedTime = effect.GetElapsedTime();
                                if (elapsedTime <= 0 || elapsedTime >= 1)
                                {
                                    effect.Stop(true);
                                    effect = null;
                                }
                            }
                            var hit = t.HitEntity.HitPos.Value;
                            MatrixD matrix;
                            MatrixD.CreateTranslation(ref hit, out matrix);
                            if (effect == null)
                            {
                                MyParticlesManager.TryCreateParticleEffect(t.System.Values.Graphics.Particles.Hit.Name, ref matrix, ref hit, uint.MaxValue, out effect);
                                if (effect == null)
                                {
                                    weapon.HitEffects[t.MuzzleId] = null;
                                    continue;
                                }

                                effect.DistanceMax = t.System.Values.Graphics.Particles.Hit.Extras.MaxDistance;
                                effect.DurationMax = t.System.Values.Graphics.Particles.Hit.Extras.MaxDuration;
                                effect.UserColorMultiplier = t.System.Values.Graphics.Particles.Hit.Color;
                                var scaler = 1;
                                effect.Loop = t.System.Values.Graphics.Particles.Hit.Extras.Loop;

                                effect.UserRadiusMultiplier = t.System.Values.Graphics.Particles.Hit.Extras.Scale * scaler;
                                effect.UserEmitterScale = 1 * scaler;
                            }
                            else if (effect.IsEmittingStopped)
                                effect.Play();

                            effect.WorldMatrix = matrix;
                            if (t.HitEntity.Projectile != null) effect.Velocity = t.HitEntity.Projectile.Velocity;
                            else if (t.HitEntity.Entity?.GetTopMostParent()?.Physics != null) effect.Velocity = t.HitEntity.Entity.GetTopMostParent().Physics.LinearVelocity;
                            weapon.HitEffects[t.MuzzleId] = effect;
                        }
                        else if (effect != null)
                        {
                            effect.Stop(false);
                            weapon.HitEffects[t.MuzzleId] = null;
                        }
                    }
                }
            }
            drawList.Clear();
            if (sFound) _shrinking.ApplyAdditions();
        }

        private void Shrink()
        {
            var sRemove = false;
            foreach (var s in _shrinking)
            {
                var trajectile = s.GetLine();
                if (trajectile.HasValue)
                {
                    MyTransparentGeometry.AddLocalLineBillboard(s.System.ProjectileMaterial, s.System.Values.Graphics.Line.Color, trajectile.Value.PrevPosition, 0, trajectile.Value.Direction, (float)trajectile.Value.Length, s.System.Values.Graphics.Line.Width);
                }
                else
                {
                    _shrinking.Remove(s);
                    sRemove = true;
                }
            }
            if (sRemove) _shrinking.ApplyRemovals();
        }
    }
}
