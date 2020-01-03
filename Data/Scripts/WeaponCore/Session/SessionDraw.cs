using System.Collections.Generic;
using VRage.Game;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using static WeaponCore.Support.AvShot;
namespace WeaponCore
{
    public partial class Session
    {


        /*
            {
                if (info.ReSizing == ProInfo.ReSize.Shrink && info.AvShot.DrawHit.HitPos != Vector3D.Zero && info.AvShot.OnScreen != AvShot.Screen.None)
                {
                    info.Shrinking = true;
                    sFound = true;
                    VisualShots.Add(info.AvShot);
                    continue;
                    var shrink = ShrinkPool.Get();
                    shrink.Init(info, thickness);
                    _shrinking.Add(shrink);
                }
                else if (info.System.Trail && info.ReSizing != ProInfo.ReSize.Grow)
                {
                    var glow = GlowPool.Get();
                    glow.Parent = info.Glowers.Count > 0 ? info.Glowers.Peek() : null;
                    glow.TailPos = info.LineStart;
                    glow.FirstTick = Tick;
                    glow.System = info.System;
                    glow.ShooterVel = info.ShooterVel;
                    glow.WidthScaler = info.LineScaler;
                    info.Glowers.Push(glow);
                    _afterGlow.Add(glow);

                }
            }
        */

        /*
        private void Shrink()
        {
            var sRemove = false;
            foreach (var s in _shrinking)
            {
                var shrunk = s.GetLine();

                if (shrunk.HasValue)
                {
                    var color = s.System.Values.Graphics.Line.Tracer.Color;
                    if (s.System.LineColorVariance)
                    {
                        var cv = s.System.Values.Graphics.Line.ColorVariance;
                        var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                        color.X *= randomValue;
                        color.Y *= randomValue;
                        color.Z *= randomValue;
                    }

                    var width = s.Thickness;
                    if (s.System.LineWidthVariance)
                    {
                        var wv = s.System.Values.Graphics.Line.WidthVariance;
                        var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                        width += randomValue;
                    }

                    if (s.System.OffsetEffect)
                        LineOffsetEffect(s.System, s.HitPos, s.Direction, shrunk.Value.Reduced, shrunk.Value.Reduced, width, color);
                    else MyTransparentGeometry.AddLineBillboard(s.System.TracerMaterial, color, shrunk.Value.BackOfTail, s.Direction, (float)(shrunk.Value.Reduced + shrunk.Value.StepLength), width);
                    if (s.System.Trail)
                    {
                        var glow = GlowPool.Get();
                        glow.Parent = s.Glowers.Count > 0 ? s.Glowers.Peek() : null;
                        glow.TailPos = shrunk.Value.BackOfTail;
                        glow.FirstTick = Tick;
                        //glow.System = s.System;
                        glow.ShooterVel = s.ShooterVel;
                        glow.WidthScaler = s.LineScaler;
                        s.Glowers.Push(glow);
                        _afterGlow.Add(glow);
                    }
                }
                else
                {
                    s.Clean();
                    ShrinkPool.Return(s);
                    _shrinking.Remove(s);
                    sRemove = true;
                }
            }
            if (sRemove) _shrinking.ApplyRemovals();
        }
        */
    }
}
