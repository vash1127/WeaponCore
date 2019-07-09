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
                var p = drawList[i];
                var drawLine = p.Fired.System.Values.Graphics.Line.Trail;
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
