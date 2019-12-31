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
        private void RunAv()
        {
            //if (Tick180) Log.Line($"ShotCnt:{AvShots.Count}");
            //var watch = (Tick % 300 == 0 || Tick % 301 == 0 || Tick % 302 == 0 || Tick % 303 == 0 || Tick % 304 == 0 || Tick % 305 == 0 || Tick % 306 == 0 || Tick % 307 == 0 || Tick % 308 == 0 || Tick % 309 == 0);
            var watch = (Tick % 300 == 0 || Tick % 301 == 0 || Tick % 302 == 0 || Tick % 303 == 0 || Tick % 304 == 0);
            if (Tick % 300 == 0) Log.Line($"[Start watch]");
            for (int i = AvShots.Count - 1; i >= 0; i--)
            {
                var av = AvShots[i];
                var refreshed = av.LastTick == Tick;

                if (refreshed && av.Tracer != TracerState.Off && av.OnScreen != Screen.None)
                {
                    if (!av.System.OffsetEffect)
                        MyTransparentGeometry.AddLineBillboard(av.System.TracerMaterial, av.Color, av.Position, -av.Direction, (float)av.TracerLength, (float)av.Thickness);
                    else
                    {
                        for (int x = 0; x < av.Offsets.Count; x++)
                        {
                            Vector3D fromBeam;
                            Vector3D toBeam;

                            if (x == 0)
                            {
                                fromBeam = av.OffsetMatrix.Translation;
                                toBeam = Vector3D.Transform(av.Offsets[x], av.OffsetMatrix);
                            }
                            else
                            {
                                fromBeam = Vector3D.Transform(av.Offsets[x - 1], av.OffsetMatrix);
                                toBeam = Vector3D.Transform(av.Offsets[x], av.OffsetMatrix);
                            }

                            Vector3 dir = (toBeam - fromBeam);
                            var length = dir.Length();
                            var normDir = dir / length;
                            MyTransparentGeometry.AddLineBillboard(av.System.TracerMaterial, av.Color, fromBeam, normDir, length, (float)av.Thickness);

                            if (Vector3D.DistanceSquared(av.OffsetMatrix.Translation, toBeam) > av.TracerLengthSqr) break;
                        }
                        av.Offsets.Clear();
                    }
                }

                var glowCnt = av.GlowSteps.Count;
                if (av.Trail != TrailState.Off)
                {
                    var steps = av.System.Values.Graphics.Line.Trail.DecayTime;
                    for (int j = 0; j < glowCnt; j++)
                    {
                        var glow = av.GlowSteps[j];

                        if (av.OnScreen != Screen.None)
                            MyTransparentGeometry.AddLineBillboard(av.System.TrailMaterial, glow.Color, glow.Line.From, glow.Line.Direction, (float)glow.Line.Length, glow.Thickness);
                        if (++glow.Step >= steps)
                        {
                            glowCnt--;
                            if (watch) Log.Line($"[removing] step:{glow.Step}({steps}) - remaining:{glowCnt} - index:{j}");
                            av.GlowSteps.Dequeue();
                            glow.Clean();
                            GlowPool.Return(glow);
                        }
                        else if (watch) Log.Line($"[continue] step:{glow.Step}({steps}) - remaining:{glowCnt} - index:{j}");
                    }
                }


                if (av.PrimeEntity != null)
                {
                    if (refreshed)
                    {
                        if (av.Model != ModelState.Close && !av.PrimeEntity.InScene && !av.Cloaked)
                        {
                            av.PrimeEntity.InScene = true;
                            av.PrimeEntity.Render.UpdateRenderObject(true, false);
                        }

                        av.PrimeEntity.PositionComp.SetWorldMatrix(av.PrimeMatrix, null, false, false, false);
                    }

                    if (av.Model == ModelState.Close || refreshed && av.Cloaked && av.PrimeEntity.InScene)
                    {
                        av.PrimeEntity.InScene = false;
                        av.PrimeEntity.Render.RemoveRenderObjects();
                        if (av.Model == ModelState.Close) av.Model = ModelState.None;
                    }
                }

                if (av.Triggered && av.TriggerEntity != null)
                {
                    if (refreshed)
                    {
                        if ((av.Model != ModelState.Close && !av.TriggerEntity.InScene))
                        {
                            av.TriggerEntity.InScene = true;
                            av.TriggerEntity.Render.UpdateRenderObject(true, false);
                        }

                        av.TriggerEntity.PositionComp.SetWorldMatrix(av.TriggerMatrix, null, false, false, false);
                    }

                    if (av.Model == ModelState.Close)
                    {
                        av.TriggerEntity.InScene = false;
                        av.TriggerEntity.Render.RemoveRenderObjects();
                        av.Model = ModelState.None;
                    }
                }

                if (refreshed)
                {
                    if (av.StartSoundActived)
                    {
                        av.StartSoundActived = false;
                        av.FireEmitter.PlaySound(av.FireSound, true);
                    }

                    if (av.HasTravelSound)
                    {
                        if (!av.AmmoSound)
                        {
                            double dist;
                            Vector3D.DistanceSquared(ref av.Position, ref CameraPos, out dist);
                            if (dist <= av.System.AmmoTravelSoundDistSqr) av.AmmoSoundStart();
                        }
                        else av.TravelEmitter.SetPosition(av.Position);
                    }

                    if (av.HitSoundActived)
                    {
                        av.HitSoundActived = false;
                        av.HitEmitter.SetPosition(av.Position);
                        av.HitEmitter.CanPlayLoopSounds = false;
                        av.HitEmitter.PlaySound(av.HitSound, true);
                        /*
                        var prevPos = t.Position + (-t.Direction * t.Length);
                        IHitInfo hitInfo;
                        Physics.CastRay(prevPos, t.Position, out hitInfo, 15, false);
                        if (hitInfo?.HitEntity != null)
                        {
                            Log.Line("hit");
                            var myHitInfo = new MyHitInfo { Position = hitInfo.Position, Normal = hitInfo.Normal };
                            MyDecals.HandleAddDecal(hitInfo.HitEntity, myHitInfo, new MyStringHash(), new MyStringHash(), null, -1f);
                        }
                        */
                    }

                    if (av.FakeExplosion && refreshed)
                    {
                        av.FakeExplosion = false;
                        if (ExplosionReady)
                            SUtils.CreateFakeExplosion(this, av.System.Values.Ammo.AreaEffect.AreaEffectRadius, av.Position, av.System);
                    }
                }


                var noNextStep = glowCnt == 0 && av.Model == ModelState.None;
                if (noNextStep && (!refreshed || av.System.IsBeamWeapon))
                {
                    av.Close();
                    AvShots.RemoveAtFast(i);
                }
            }
        }

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
