using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Collections;
using VRage.Game;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Support
{
    class RunAv
    {
        internal readonly MyConcurrentPool<AvShot> AvShotPool = new MyConcurrentPool<AvShot>(128, shot => shot.Close());
        internal readonly List<AvShot> AvShots = new List<AvShot>(128);
        internal readonly Stack<AfterGlow> Glows = new Stack<AfterGlow>();

        internal Session Session;

        internal int ExplosionCounter;

        internal bool ExplosionReady
        {
            get
            {
                if (++ExplosionCounter <= 5)
                {
                    return true;
                }
                return false;
            }
        }

        internal RunAv(Session session)
        {
            Session = session;
        }

        internal void Run()
        {
            //if (Session.Tick300) Log.Line($"{AvShots.Count}");
            for (int i = AvShots.Count - 1; i >= 0; i--)
            {
                var av = AvShots[i];
                var refreshed = av.LastTick == Session.Tick;

                if ((refreshed  || av.TracerShrinks.Count > 0) && av.Tracer != AvShot.TracerState.Off && av.OnScreen != AvShot.Screen.None)
                {
                    if (!av.System.OffsetEffect)
                    {
                        if (av.Tracer != AvShot.TracerState.Shrink)
                            MyTransparentGeometry.AddLineBillboard(av.System.TracerMaterial, av.Color, av.Position, -av.PointDir, (float)av.TracerLength, (float)av.Thickness);
                        else
                        {
                            var s = av.TracerShrinks.Dequeue();
                            Log.Line($"drawining non-offset shrink: Color:{s.Color} - Start:{s.Start} - Len:{s.Length} - Thickness:{s.Thickness}");

                            MyTransparentGeometry.AddLineBillboard(av.System.TracerMaterial, s.Color, s.Start, -av.PointDir, s.Length, s.Thickness);
                        }
                    }
                    else
                    {
                        Shrinks s = new Shrinks();
                        List<Vector3D> list;
                        if (av.Tracer == AvShot.TracerState.Shrink)
                        {
                            s = av.TracerShrinks.Dequeue();
                            list = av.ShrinkOffsets.Dequeue();
                        }
                        else list = av.Offsets;
                        for (int x = 0; x < list.Count; x++)
                        {
                            Vector3D fromBeam;
                            Vector3D toBeam;

                            if (x == 0)
                            {
                                fromBeam = av.OffsetMatrix.Translation;
                                toBeam = Vector3D.Transform(list[x], av.OffsetMatrix);
                            }
                            else
                            {
                                fromBeam = Vector3D.Transform(list[x - 1], av.OffsetMatrix);
                                toBeam = Vector3D.Transform(list[x], av.OffsetMatrix);
                            }

                            Vector3 dir = (toBeam - fromBeam);
                            var length = dir.Length();
                            var normDir = dir / length;
                            if (av.Tracer != AvShot.TracerState.Shrink)
                                MyTransparentGeometry.AddLineBillboard(av.System.TracerMaterial, av.Color, fromBeam, normDir, length, (float)av.Thickness);
                            else
                                MyTransparentGeometry.AddLineBillboard(av.System.TracerMaterial, s.Color, s.Start, -av.PointDir, s.Length, s.Thickness);

                            if (Vector3D.DistanceSquared(av.OffsetMatrix.Translation, toBeam) > av.TracerLengthSqr) break;
                        }
                        list.Clear();
                    }
                }

                var glowCnt = av.GlowSteps.Count;
                if (av.Trail != AvShot.TrailState.Off)
                {
                    var steps = av.System.Values.Graphics.Line.Trail.DecayTime;
                    var remove = false;
                    for (int j = glowCnt - 1; j >= 0; j--)
                    {
                        var glow = av.GlowSteps[j];

                        if (av.OnScreen != AvShot.Screen.None)
                        {
                            var reduction = (av.GlowShrinkSize * glow.Step);
                            var width = (av.System.Values.Graphics.Line.Tracer.Width - reduction) * av.LineScaler;
                            MyTransparentGeometry.AddLineBillboard(av.System.TrailMaterial, av.System.Values.Graphics.Line.Trail.Color, glow.Line.To, glow.Line.Direction, (float)glow.Line.Length, width);
                        }
                        if (++glow.Step >= steps)
                        {
                            remove = true;
                            glowCnt--;
                            Glows.Push(glow);
                        }
                    }

                    if (remove) av.GlowSteps.Dequeue();
                }

                if (av.PrimeEntity != null)
                {
                    if (refreshed)
                    {
                        if (av.Model != AvShot.ModelState.Close && !av.PrimeEntity.InScene && !av.Cloaked)
                        {
                            av.PrimeEntity.InScene = true;
                            av.PrimeEntity.Render.UpdateRenderObject(true, false);
                        }

                        av.PrimeEntity.PositionComp.SetWorldMatrix(av.PrimeMatrix, null, false, false, false);
                    }

                    if (av.Model == AvShot.ModelState.Close || refreshed && av.Cloaked && av.PrimeEntity.InScene)
                    {
                        av.PrimeEntity.InScene = false;
                        av.PrimeEntity.Render.RemoveRenderObjects();
                        if (av.Model == AvShot.ModelState.Close) av.Model = AvShot.ModelState.None;
                    }
                }

                if (av.Triggered && av.TriggerEntity != null)
                {
                    if (refreshed)
                    {
                        if ((av.Model != AvShot.ModelState.Close && !av.TriggerEntity.InScene))
                        {
                            av.TriggerEntity.InScene = true;
                            av.TriggerEntity.Render.UpdateRenderObject(true, false);
                        }

                        av.TriggerEntity.PositionComp.SetWorldMatrix(av.TriggerMatrix, null, false, false, false);
                    }

                    if (av.Model == AvShot.ModelState.Close)
                    {
                        av.TriggerEntity.InScene = false;
                        av.TriggerEntity.Render.RemoveRenderObjects();
                        av.Model = AvShot.ModelState.None;
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
                            Vector3D.DistanceSquared(ref av.Position, ref Session.CameraPos, out dist);
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
                            SUtils.CreateFakeExplosion(Session, av.System.Values.Ammo.AreaEffect.AreaEffectRadius, av.Position, av.System);
                    }
                }


                var noNextStep = glowCnt == 0 && av.Model == AvShot.ModelState.None && av.TracerShrinks.Count == 0;
                if (noNextStep && (!refreshed || av.System.IsBeamWeapon))
                {
                    AvShotPool.Return(av);
                    AvShots.RemoveAtFast(i);
                }
            }
        }
    }
}
