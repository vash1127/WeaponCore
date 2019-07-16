using System.Collections.Generic;
using Sandbox.ModAPI;
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
                var p = drawList[i];
                if (p.Entity != null)
                {
                    if (!p.Entity.InScene)
                    {
                        p.Entity.InScene = true;
                        p.Entity.Render.UpdateRenderObject(true, false);
                    }

                    p.Entity.PositionComp.SetWorldMatrix(p.EntityMatrix, null, false, false, false);
                    if (p.Last)
                    {
                        p.Entity.InScene = false;
                        p.Entity.Render.RemoveRenderObjects();
                    }
                    if (!p.System.Values.Graphics.Line.Trail) continue;
                }

                var trajectile = p.Trajectile;
                var width = p.LineWidth;

                if (p.Shrink && p.HitEntity != null)
                {
                    sFound = true;
                    var shrink = _shrinkPool.Get();
                    shrink.Init(trajectile, ref p);
                    _shrinking.Add(shrink);
                }
                var color = p.Color;

                var newWidth = width;

                if (p.System.Values.Ammo.Trajectory.DesiredSpeed <= 0)
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
                    TransparentRenderExt.DrawTransparentCylinder(ref matrix, newWidth, newWidth, (float)trajectile.Length, 12, color, color, p.System.ProjectileMaterial, p.System.ProjectileMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                }
                else
                    MyTransparentGeometry.AddLocalLineBillboard(p.System.ProjectileMaterial, color, trajectile.PrevPosition, 0, trajectile.Direction, (float)trajectile.Length, newWidth);
                if (p.System.IsBeamWeapon)
                {
                    var c = p.FiringCube;
                    if (p.FiringCube == null || p.FiringCube.MarkedForClose) continue;
                    var weapon = GridTargetingAIs[c.CubeGrid].WeaponBase[c].Platform.Weapons[p.WeaponId];
                    if (weapon != null)
                    {
                        var effect = weapon.HitEffects[p.MuzzleId];
                        if (p.HitEntity?.HitPos != null && p.OnScreen)
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
                            var hitPos = p.HitEntity.HitPos.Value;
                            MatrixD matrix;
                            MatrixD.CreateTranslation(ref hitPos, out matrix);
                            if (effect == null)
                            {
                                MyParticlesManager.TryCreateParticleEffect(p.System.Values.Graphics.Particles.Hit.Name, ref matrix, ref hitPos, uint.MaxValue, out effect);
                                if (effect == null)
                                {
                                    weapon.HitEffects[p.MuzzleId] = null;
                                    continue;
                                }

                                effect.DistanceMax = p.System.Values.Graphics.Particles.Hit.Extras.MaxDistance;
                                effect.DurationMax = p.System.Values.Graphics.Particles.Hit.Extras.MaxDuration;
                                effect.UserColorMultiplier = p.System.Values.Graphics.Particles.Hit.Color;
                                //var reScale = (float)Math.Log(195312.5, MyAPIGateway.); // wtf is up with particles and camera distance
                                //var scaler = reScale < 1 ? reScale : 1;
                                var scaler = 1;
                                effect.Loop = p.System.Values.Graphics.Particles.Hit.Extras.Loop;

                                effect.UserRadiusMultiplier = p.System.Values.Graphics.Particles.Hit.Extras.Scale * scaler;
                                effect.UserEmitterScale = 1 * scaler;
                            }
                            else if (effect.IsEmittingStopped)
                                effect.Play();

                            effect.WorldMatrix = matrix;
                            if (p.HitEntity.Entity.Physics != null)
                                effect.Velocity = p.HitEntity.Entity.Physics.LinearVelocity;
                            weapon.HitEffects[p.MuzzleId] = effect;
                        }
                        else if (effect != null)
                        {
                            effect.Stop(false);
                            weapon.HitEffects[p.MuzzleId] = null;
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
