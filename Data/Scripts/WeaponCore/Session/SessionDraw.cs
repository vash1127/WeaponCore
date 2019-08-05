using System;
using System.Collections.Generic;
using VRage.Game;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
namespace WeaponCore
{
    public partial class Session
    {
        private void DrawLists(List<Projectiles.Projectiles.Trajectile> drawList)
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

                if (InTurret)
                {
                    var matrix = MatrixD.CreateFromDir(t.Direction);
                    matrix.Translation = t.PrevPosition;
                    TransparentRenderExt.DrawTransparentCylinder(ref matrix, newWidth, newWidth, (float)t.Length, 12, color, color, t.System.ProjectileMaterial, t.System.ProjectileMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                }
                else
                    MyTransparentGeometry.AddLocalLineBillboard(t.System.ProjectileMaterial, color, t.PrevPosition, 0, t.Direction, (float)t.Length, newWidth);

                var combine = t.System.CombineBarrels;
                if (t.System.IsBeamWeapon && t.System.HitParticle && (!combine || t.MuzzleId == 0 && t.System.Values.HardPoint.Loading.FakeBarrels.Converge))
                {
                    var c = t.FiringCube;
                    if (c == null || c.MarkedForClose) continue;
                    var weapon = GridTargetingAIs[c.CubeGrid].WeaponBase[c].Platform.Weapons[t.WeaponId];
                    if (weapon != null)
                    {
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
                            var hitPos = t.HitEntity.HitPos.Value;
                            MatrixD matrix;
                            MatrixD.CreateTranslation(ref hitPos, out matrix);
                            if (effect == null)
                            {
                                MyParticlesManager.TryCreateParticleEffect(t.System.Values.Graphics.Particles.Hit.Name, ref matrix, ref hitPos, UInt32.MaxValue, out effect);
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
                            if (t.HitEntity.Entity.Physics != null)
                                effect.Velocity = t.HitEntity.Entity.Physics.LinearVelocity;
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
                    if (InTurret)
                    {
                        var matrix = MatrixD.CreateFromDir(trajectile.Value.Direction);
                        matrix.Translation = trajectile.Value.PrevPosition;

                        TransparentRenderExt.DrawTransparentCylinder(ref matrix, s.System.Values.Graphics.Line.Width, s.System.Values.Graphics.Line.Width, (float)trajectile.Value.Length, 6, s.System.Values.Graphics.Line.Color, s.System.Values.Graphics.Line.Color, s.System.ProjectileMaterial, s.System.ProjectileMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                    }
                    else MyTransparentGeometry.AddLocalLineBillboard(s.System.ProjectileMaterial, s.System.Values.Graphics.Line.Color, trajectile.Value.PrevPosition, 0, trajectile.Value.Direction, (float)trajectile.Value.Length, s.System.Values.Graphics.Line.Width);
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
