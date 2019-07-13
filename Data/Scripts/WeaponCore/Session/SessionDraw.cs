using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
namespace WeaponCore
{
    public partial class Session
    {
        private void DrawLists(List<Projectiles.Projectiles.DrawProjectile> drawList)
        {
            var sFound = false;
            for (int i = 0; i < drawList.Count; i++)
            {
                var p = drawList[i];
                if (p.Entity != null)
                {
                    var drawLine = p.Fired.System.Values.Graphics.Line.Trail;
                    p.Entity.PositionComp.SetWorldMatrix(p.EntityMatrix, null, false, false, false);
                    if (p.Last)
                    {
                        p.Entity.InScene = false;
                        p.Entity.Render.RemoveRenderObjects();
                    }
                    if (!drawLine) continue;
                }

                var line = p.Projectile;
                var width = p.LineWidth;

                if (p.Shrink)
                {
                    sFound = true;
                    var shrink = _shrinkPool.Get();
                    shrink.Init(line, ref p);
                    _shrinking.Add(shrink);
                }
                var color = p.Color;

                var newWidth = width;

                if (p.Fired.System.Values.Ammo.Trajectory.DesiredSpeed <= 0)
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
                    var matrix = MatrixD.CreateFromDir(line.Direction);
                    matrix.Translation = line.From;
                    TransparentRenderExt.DrawTransparentCylinder(ref matrix, newWidth, newWidth, (float)line.Length, 12, color, color, p.Fired.System.ProjectileMaterial, p.Fired.System.ProjectileMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                }
                else
                    MyTransparentGeometry.AddLocalLineBillboard(p.Fired.System.ProjectileMaterial, color, line.From, 0, line.Direction, (float)line.Length, newWidth);

                var f = p.Fired;
                if (f.IsBeam)
                {
                    var c = f.FiringCube;
                    if (f.FiringCube == null || f.FiringCube.MarkedForClose) continue;
                    var weapon = GridTargetingAIs[c.CubeGrid].WeaponBase[c].Platform.Weapons[f.WeaponId];
                    if (weapon != null)
                    {
                        var effect = weapon.HitEffects[f.MuzzleId];
                        if (p.HitPos.HasValue && p.OnScreen)
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
                            var hitPos = p.HitPos.Value;
                            MatrixD matrix;
                            MatrixD.CreateTranslation(ref hitPos, out matrix);
                            if (effect == null)
                            {
                                MyParticlesManager.TryCreateParticleEffect(f.System.Values.Graphics.Particles.HitParticle, ref matrix, ref hitPos, uint.MaxValue, out effect);
                                if (effect == null)
                                {
                                    weapon.HitEffects[f.MuzzleId] = null;
                                    continue;
                                }

                                effect.DistanceMax = 5000;
                                effect.DurationMax = 1f;
                                effect.UserColorMultiplier = f.System.Values.Graphics.Particles.HitColor;
                                //var reScale = (float)Math.Log(195312.5, MyAPIGateway.); // wtf is up with particles and camera distance
                                //var scaler = reScale < 1 ? reScale : 1;
                                var scaler = 1;
                                effect.Loop = false;

                                effect.UserRadiusMultiplier = f.System.Values.Graphics.Particles.HitScale * scaler;
                                effect.UserEmitterScale = 1 * scaler;
                            }
                            else if (effect.IsEmittingStopped)
                                effect.Play();

                            effect.WorldMatrix = matrix;
                            if (p.HitEntity?.Physics != null)
                                effect.Velocity = p.HitEntity.Physics.LinearVelocity;
                            weapon.HitEffects[f.MuzzleId] = effect;
                        }
                        else if (effect != null)
                        {
                            effect.Stop(false);
                            weapon.HitEffects[f.MuzzleId] = null;
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
                var line = s.GetLine();
                if (line.HasValue)
                {
                    if (InTurret)
                    {
                        var matrix = MatrixD.CreateFromDir(line.Value.Direction);
                        matrix.Translation = line.Value.From;

                        TransparentRenderExt.DrawTransparentCylinder(ref matrix, s.DrawProjectile.Fired.System.Values.Graphics.Line.Width, s.DrawProjectile.Fired.System.Values.Graphics.Line.Width, (float)line.Value.Length, 6, s.DrawProjectile.Fired.System.Values.Graphics.Line.Color, s.DrawProjectile.Fired.System.Values.Graphics.Line.Color, s.DrawProjectile.Fired.System.ProjectileMaterial, s.DrawProjectile.Fired.System.ProjectileMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                    }
                    else MyTransparentGeometry.AddLocalLineBillboard(s.DrawProjectile.Fired.System.ProjectileMaterial, s.DrawProjectile.Fired.System.Values.Graphics.Line.Color, line.Value.From, 0, line.Value.Direction, (float)line.Value.Length, s.DrawProjectile.Fired.System.Values.Graphics.Line.Width);
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
