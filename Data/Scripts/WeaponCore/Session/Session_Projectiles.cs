using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using VRageRender;
using WeaponCore.Platform;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using static WeaponCore.Projectiles.Projectiles;
namespace WeaponCore
{
    public partial class Session
    {
        private void DrawLists(List<DrawProjectile> drawList)
        {
            var sFound = false;
            for (int i = 0; i < drawList.Count; i++)
            {
                var p = drawList[i];
                var wDef = p.WeaponSystem.WeaponType;
                var drawLine = wDef.GraphicDef.ProjectileTrail;
                if (!drawLine)
                {
                    if (p.Entity != null)
                    {
                        p.Entity.PositionComp.SetWorldMatrix(p.EntityMatrix, null, false, false, false);
                        if (p.Last)
                        {
                            p.Entity.InScene = false;
                            p.Entity.Render.RemoveRenderObjects();
                        }
                    }
                    continue;
                }

                var line = p.Projectile;

                if (p.Shrink)
                {
                    sFound = true;
                    var shrink = _shrinkPool.Get();
                    shrink.Init(wDef, line, p.ReSizeSteps, p.LineReSizeLen);
                    _shrinking.Add(shrink);
                }
                var color = wDef.GraphicDef.ProjectileColor;
                var width = wDef.GraphicDef.ProjectileWidth;

                var newWidth = width;

                if (wDef.AmmoDef.DesiredSpeed <= 0)
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

                if (!InTurret) 
                {
                    var matrix = MatrixD.CreateFromDir(line.Direction);
                    matrix.Translation = line.From;
                    TransparentRenderExt.DrawTransparentCylinder(ref matrix, newWidth, wDef.GraphicDef.ProjectileWidth, (float)line.Length, 12, color, color, wDef.GraphicDef.ProjectileMaterial, wDef.GraphicDef.ProjectileMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                }
                else MyTransparentGeometry.AddLocalLineBillboard(wDef.GraphicDef.ProjectileMaterial, color, line.From, 0, line.Direction, (float)line.Length, newWidth);
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

                        TransparentRenderExt.DrawTransparentCylinder(ref matrix, s.WepDef.GraphicDef.ProjectileWidth, s.WepDef.GraphicDef.ProjectileWidth, (float)line.Value.Length, 6, s.WepDef.GraphicDef.ProjectileColor, s.WepDef.GraphicDef.ProjectileColor, s.WepDef.GraphicDef.ProjectileMaterial, s.WepDef.GraphicDef.ProjectileMaterial, 0f, BlendTypeEnum.Standard, BlendTypeEnum.Standard, false);
                    }
                    else MyTransparentGeometry.AddLocalLineBillboard(s.WepDef.GraphicDef.ProjectileMaterial, s.WepDef.GraphicDef.ProjectileColor, line.Value.From, 0, line.Value.Direction, (float)line.Value.Length, s.WepDef.GraphicDef.ProjectileWidth);
                }
                else
                {
                    _shrinking.Remove(s);
                    sRemove = true;
                }
            }
            if (sRemove) _shrinking.ApplyRemovals();
        }

        private void UpdateWeaponPlatforms()
        {
            _dsUtil.Sw.Restart();
            if (!GameLoaded) return;
            foreach (var aiPair in GridTargetingAIs)
            {
                //var grid = aiPair.Key;
                var gridAi = aiPair.Value;
                if (!gridAi.Ready) continue;
                foreach (var basePair in gridAi.WeaponBase)
                {
                    //var myCube = basePair.Key;
                    var comp = basePair.Value;
                    if (!comp.MainInit || !comp.State.Value.Online) continue;

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        if (w.SeekTarget && w.TrackTarget) gridAi.SelectTarget(ref w.Target, w);

                        if (w.AiReady || w.Gunner && (j == 0 && MouseButtonLeft || j == 1 && MouseButtonRight)) w.Shoot();
                    }
                }
                gridAi.Ready = false;
            }
            _dsUtil.StopWatchReport("test", -1);
        }

        private void AiLoop()
        {
            if (!GameLoaded) return;
            foreach (var aiPair in GridTargetingAIs)
            {
                //var grid = aiPair.Key;
                var ai = aiPair.Value;
                foreach (var basePair in ai.WeaponBase)
                {
                    //var myCube = basePair.Key;
                    var comp = basePair.Value;
                    if (!comp.MainInit || !comp.State.Value.Online) continue;

                    for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                    {
                        var w = comp.Platform.Weapons[j];
                        w.Gunner = ControlledEntity == comp.MyCube;
                        if (!w.Gunner)
                        {
                            if (w.TrackingAi)
                            {
                                if (w.Target != null && !Weapon.TrackingTarget(w, w.Target, true))
                                    w.Target = null;
                            }
                            else
                            {
                                if (!w.TrackTarget) w.Target = comp.TrackingWeapon.Target;
                                if (w.Target != null && !Weapon.CheckTarget(w, w.Target)) w.Target = null;
                            }

                            if (w != comp.TrackingWeapon && comp.TrackingWeapon.Target == null) w.Target = null;
                        }
                        else
                        {
                            InTurret = true;
                            if (MouseButtonPressed)
                            {
                                var currentAmmo = comp.Gun.GunBase.CurrentAmmo;
                                if (currentAmmo <= 1) comp.Gun.GunBase.CurrentAmmo += 1;
                            }
                        }
                        w.AiReady = w.Target != null && !w.Gunner && w.Comp.TurretTargetLock && !w.Target.MarkedForClose;
                        w.SeekTarget = Tick20 && !w.Gunner && (w.Target == null || w.Target != null && w.Target.MarkedForClose) && w.TrackTarget;
                        if (w.AiReady || w.SeekTarget || w.Gunner) ai.Ready = true;
                    }
                }
            }
        }
    }
}