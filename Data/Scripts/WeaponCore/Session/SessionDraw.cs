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
        private void DrawLists(List<Projectiles.Projectiles.DrawProjectile> drawList)
        {
            var sFound = false;
            for (int i = 0; i < drawList.Count; i++)
            {
                var d = drawList[i];
                if (d.Projectile.Entity != null)
                {
                    if (!d.Last && !d.Projectile.Entity.InScene)
                    {
                        d.Projectile.Entity.InScene = true;
                        d.Projectile.Entity.Render.UpdateRenderObject(true, false);
                    }

                    d.Projectile.Entity.PositionComp.SetWorldMatrix(d.Projectile.EntityMatrix, null, false, false, false);
                    if (d.Last)
                    {
                        d.Projectile.Entity.InScene = false;
                        d.Projectile.Entity.Render.RemoveRenderObjects();
                    }
                    if (!d.Projectile.System.Values.Graphics.Line.Trail) continue;
                }

                var trajectile = d.Projectile.Trajectile;
                var width = d.LineWidth;
                if (d.Projectile.Shrink && d.HitEntity != null)
                {
                    sFound = true;
                    var shrink = _shrinkPool.Get();
                    shrink.Init(trajectile, ref d);
                    _shrinking.Add(shrink);
                }
                var color = d.Color;
                var newWidth = width;

                if (d.Projectile.System.Values.Ammo.Trajectory.DesiredSpeed <= 0)
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
                    var matrix = MatrixD.CreateFromDir(trajectile.Direction);
                    matrix.Translation = trajectile.PrevPosition;
                    TransparentRenderExt.DrawTransparentCylinder(ref matrix, newWidth, newWidth, (float)trajectile.Length, 12, color, color, d.Projectile.System.ProjectileMaterial, d.Projectile.System.ProjectileMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                }
                else
                    MyTransparentGeometry.AddLocalLineBillboard(d.Projectile.System.ProjectileMaterial, color, trajectile.PrevPosition, 0, trajectile.Direction, (float)trajectile.Length, newWidth);

                var combine = d.Projectile.System.CombineBarrels;
                if (d.Projectile.System.IsBeamWeapon && d.Projectile.System.HitParticle && (!combine || d.Projectile.MuzzleId == 0 && d.Projectile.System.Values.HardPoint.Loading.FakeBarrels.Converge))
                {
                    var c = d.Projectile.FiringCube;
                    if (d.Projectile.FiringCube == null || d.Projectile.FiringCube.MarkedForClose) continue;
                    var weapon = GridTargetingAIs[c.CubeGrid].WeaponBase[c].Platform.Weapons[d.Projectile.WeaponId];
                    if (weapon != null)
                    {
                        var effect = weapon.HitEffects[d.Projectile.MuzzleId];
                        if (d.HitEntity?.HitPos != null && d.Projectile.OnScreen)
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
                            var hitPos = d.HitEntity.HitPos.Value;
                            MatrixD matrix;
                            MatrixD.CreateTranslation(ref hitPos, out matrix);
                            if (effect == null)
                            {
                                MyParticlesManager.TryCreateParticleEffect(d.Projectile.System.Values.Graphics.Particles.Hit.Name, ref matrix, ref hitPos, UInt32.MaxValue, out effect);
                                if (effect == null)
                                {
                                    weapon.HitEffects[d.Projectile.MuzzleId] = null;
                                    continue;
                                }

                                effect.DistanceMax = d.Projectile.System.Values.Graphics.Particles.Hit.Extras.MaxDistance;
                                effect.DurationMax = d.Projectile.System.Values.Graphics.Particles.Hit.Extras.MaxDuration;
                                effect.UserColorMultiplier = d.Projectile.System.Values.Graphics.Particles.Hit.Color;
                                //var reScale = (float)Math.Log(195312.5, MyAPIGateway.); // wtf is up with particles and camera distance
                                //var scaler = reScale < 1 ? reScale : 1;
                                var scaler = 1;
                                effect.Loop = d.Projectile.System.Values.Graphics.Particles.Hit.Extras.Loop;

                                effect.UserRadiusMultiplier = d.Projectile.System.Values.Graphics.Particles.Hit.Extras.Scale * scaler;
                                effect.UserEmitterScale = 1 * scaler;
                            }
                            else if (effect.IsEmittingStopped)
                                effect.Play();

                            effect.WorldMatrix = matrix;
                            if (d.HitEntity.Entity.Physics != null)
                                effect.Velocity = d.HitEntity.Entity.Physics.LinearVelocity;
                            weapon.HitEffects[d.Projectile.MuzzleId] = effect;
                        }
                        else if (effect != null)
                        {
                            effect.Stop(false);
                            weapon.HitEffects[d.Projectile.MuzzleId] = null;
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
