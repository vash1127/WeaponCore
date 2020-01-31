using System.Collections.Generic;
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
        internal readonly List<AvShot> AvStart = new List<AvShot>(128);
        internal readonly List<AvShot> AvEnd = new List<AvShot>(128);

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

        private int _onScreens = 0;
        private int _shrinks = 0;
        private int _glows = 0;
        private int _models = 0;

        internal void Start()
        {
            for (int i = AvStart.Count - 1; i >= 0; i--)
            {
                var av = AvStart[i];
                if (av.StartSoundActived)
                {
                    av.StartSoundActived = false;
                    av.FireEmitter.PlaySound(av.FireSound, true);
                }
            }
            AvStart.Clear();
        }

        internal void End()
        {
            for (int i = AvEnd.Count - 1; i >= 0; i--)
            {
                var av = AvEnd[i];
                if (av.FakeExplosion && av.DetonateFakeExp)
                {
                    av.FakeExplosion = false;
                    if (ExplosionReady)
                    {
                        if (av.DetonateFakeExp) SUtils.CreateFakeExplosion(Session, av.System.Values.Ammo.AreaEffect.Detonation.DetonationRadius, av.TracerFront, av.System);
                        else SUtils.CreateFakeExplosion(Session, av.System.Values.Ammo.AreaEffect.AreaEffectRadius, av.TracerFront, av.System);
                    }
                }

                if (!av.Active)
                    AvShotPool.Return(av);
            }
            AvEnd.Clear();
        }

        internal void Run()
        {

            if (Session.Tick600)
            {
                Log.LineShortDate($"-= [AvShots] {AvShots.Count} [OnScreen] {_onScreens} [Shrinks] {_shrinks} [Glows] {_glows} [Models] {_models} [P] {Session.Projectiles.ActiveProjetiles.Count} [P-Pool] {Session.Projectiles.ProjectilePool.Count} [AvPool] {AvShotPool.Count} =-");
                _glows = 0;
                _shrinks = 0;
            }

            _onScreens = 0;
            _models = 0;

            Start();
            if (AvEnd.Count > 0) End();
            if (AvStart.Count > 0) Start();

            for (int i = AvShots.Count - 1; i >= 0; i--)
            {
                var av = AvShots[i];
                if (av.OnScreen != AvShot.Screen.None) _onScreens++;
                var refreshed = av.LastTick == Session.Tick;

                if (refreshed && av.Tracer != AvShot.TracerState.Off && av.OnScreen != AvShot.Screen.None)
                {
                    if (!av.System.OffsetEffect)
                    {
                        if (av.Tracer != AvShot.TracerState.Shrink)
                            MyTransparentGeometry.AddLineBillboard(av.System.TracerMaterial, av.Color, av.TracerBack, av.PointDir, (float)av.VisualLength, (float)av.TracerWidth);
                    }
                    else
                    {
                        var list = av.Offsets;
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
                            MyTransparentGeometry.AddLineBillboard(av.System.TracerMaterial, av.Color, fromBeam, normDir, length, (float)av.TracerWidth);

                            if (Vector3D.DistanceSquared(av.OffsetMatrix.Translation, toBeam) > av.TracerLengthSqr) break;
                        }
                        list.Clear();
                    }
                }

                var shrinkCnt = av.TracerShrinks.Count;
                if (shrinkCnt > _shrinks) _shrinks = shrinkCnt;

                if (shrinkCnt > 0)
                    RunShrinks(av);

                var glowCnt = av.GlowSteps.Count;

                if (glowCnt > _glows)
                    _glows = glowCnt;

                if (av.Trail != AvShot.TrailState.Off)
                {
                    var steps = av.System.Values.Graphics.Line.Trail.DecayTime;
                    var widthScaler = !av.System.Values.Graphics.Line.Trail.UseColorFade;
                    var remove = false;
                    for (int j = glowCnt - 1; j >= 0; j--)
                    {
                        var glow = av.GlowSteps[j];

                        if (!refreshed)
                            glow.Line = new LineD(glow.Line.From + av.ShootVelStep, glow.Line.To + av.ShootVelStep, glow.Line.Length);

                        if (av.OnScreen != AvShot.Screen.None)
                        {
                            var reduction = (av.GlowShrinkSize * glow.Step);
                            var width = widthScaler ? (av.System.TrailWidth - reduction) * av.TrailScaler : av.System.TrailWidth * av.TrailScaler;
                            var color = av.System.Values.Graphics.Line.Trail.Color;

                            if (!widthScaler)
                                color *= MathHelper.Clamp(1f - reduction, 0.01f, 1f);
                            
                            MyTransparentGeometry.AddLineBillboard(av.System.TrailMaterial, color, glow.Line.From, glow.Line.Direction, (float) glow.Line.Length, width);
                        }

                        if (++glow.Step >= steps)
                        {
                            glow.Parent = null;
                            glow.Step = 0;
                            remove = true;
                            glowCnt--;
                            Glows.Push(glow);
                        }
                    }

                    if (remove) av.GlowSteps.Dequeue();
                }

                if (refreshed)
                {
                    if (av.PrimeEntity != null)
                    {
                        _models++;
                        if (av.OnScreen != AvShot.Screen.None)
                        {
                            if (!av.PrimeEntity.InScene && !av.Cloaked)
                            {
                                av.PrimeEntity.InScene = true;
                                av.PrimeEntity.Render.UpdateRenderObject(true, false);
                            }
                            av.PrimeEntity.PositionComp.SetWorldMatrix(av.PrimeMatrix, null, false, false, false);

                        }

                        if ((av.Cloaked || av.OnScreen == AvShot.Screen.None) && av.PrimeEntity.InScene)
                        {
                            av.PrimeEntity.InScene = false;
                            av.PrimeEntity.Render.RemoveRenderObjects();
                        }
                    }

                    if (av.Triggered && av.TriggerEntity != null)
                    {
                        if ((!av.TriggerEntity.InScene))
                        {
                            av.TriggerEntity.InScene = true;
                            av.TriggerEntity.Render.UpdateRenderObject(true, false);
                        }

                        av.TriggerEntity.PositionComp.SetWorldMatrix(av.TriggerMatrix, null, false, false, false);
                    }

                    if (av.HasTravelSound)
                    {
                        if (!av.AmmoSound)
                        {
                            double distSqr;
                            Vector3D.DistanceSquared(ref av.TracerFront, ref Session.CameraPos, out distSqr);
                            if (distSqr <= av.System.AmmoTravelSoundDistSqr)
                            {
                                av.AmmoSoundStart();
                            }
                        }
                        else av.TravelEmitter.SetPosition(av.TracerFront);
                    }

                    if (av.HitSoundActived)
                    {
                        av.HitSoundActived = false;
                        av.HitEmitter.SetPosition(av.TracerFront);
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

                    if (av.FakeExplosion)
                    {
                        av.FakeExplosion = false;
                        if (ExplosionReady)
                        {
                            if (av.DetonateFakeExp) SUtils.CreateFakeExplosion(Session, av.System.Values.Ammo.AreaEffect.Detonation.DetonationRadius, av.TracerFront, av.System);
                            else SUtils.CreateFakeExplosion(Session, av.System.Values.Ammo.AreaEffect.AreaEffectRadius, av.TracerFront, av.System);
                        }
                    }
                }   

                var noNextStep = glowCnt == 0 && shrinkCnt == 0 && av.Dirty;
                if (noNextStep)
                {
                    AvShotPool.Return(av);
                    AvShots.RemoveAtFast(i);
                }
            }

        }

        private void RunShrinks(AvShot av)
        {
            var s = av.TracerShrinks.Dequeue();
            if (av.LastTick != Session.Tick)
            {
                if (!av.System.OffsetEffect)
                {
                    if (av.OnScreen != AvShot.Screen.None)
                        MyTransparentGeometry.AddLineBillboard(av.System.TracerMaterial, s.Color, s.NewFront, av.PointDir, s.Length, s.Thickness);
                }
                else if (av.OnScreen != AvShot.Screen.None)
                    av.DrawLineOffsetEffect(s.NewFront, -av.PointDir, s.Length, s.Thickness, s.Color);

                if (av.Trail != AvShot.TrailState.Off && av.Back)
                    av.RunGlow(ref s, true);
            }

            if (av.TracerShrinks.Count == 0) av.ResetHit();
        }
    }
}
