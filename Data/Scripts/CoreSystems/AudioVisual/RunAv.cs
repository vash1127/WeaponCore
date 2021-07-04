﻿using System.Collections.Generic;
using CoreSystems.Platform;
using Sandbox.Game.Entities;
using VRage.Collections;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace CoreSystems.Support
{
    class RunAv
    {
        internal readonly MyConcurrentPool<AvShot> AvShotPool = new MyConcurrentPool<AvShot>(128, shot => shot.Close());
        internal readonly MyConcurrentPool<AvEffect> AvEffectPool = new MyConcurrentPool<AvEffect>(128, barrel => barrel.Clean());
        internal readonly List<AvEffect> Effects1 = new List<AvEffect>(128);
        internal readonly List<AvEffect> Effects2 = new List<AvEffect>(128);
        internal readonly List<ParticleEvent> ParticlesToProcess = new List<ParticleEvent>(128);
        internal readonly Dictionary<ulong, MyParticleEffect> BeamEffects = new Dictionary<ulong, MyParticleEffect>();

        internal readonly List<AvShot> AvShots = new List<AvShot>(128);
        internal readonly List<HitSound> HitSounds = new List<HitSound>(128);
        internal readonly Stack<AfterGlow> Glows = new Stack<AfterGlow>();
        internal readonly Stack<MyEntity3DSoundEmitter> FireEmitters = new Stack<MyEntity3DSoundEmitter>();
        internal readonly Stack<MyEntity3DSoundEmitter> TravelEmitters = new Stack<MyEntity3DSoundEmitter>();
        internal readonly Stack<MyEntity3DSoundEmitter> HitEmitters = new Stack<MyEntity3DSoundEmitter>();

        internal Session Session;

        internal int ExplosionCounter;
        internal int MaxExplosions = 100;

        internal bool ExplosionReady
        {
            get {
                if (ExplosionCounter + 1 <= MaxExplosions)
                {
                    ExplosionCounter++;
                    return true;
                }
                return false;
            }
        }

        internal RunAv(Session session)
        {
            Session = session;
        }
        

        private int _onScreens;
        private int _shrinks;
        private int _glows;
        private int _models;

        internal void End()
        {
            if (Effects1.Count > 0) RunAvEffects1();
            if (Effects2.Count > 0) RunAvEffects2();
            if (HitSounds.Count > 0) RunHitSounds();
            if (ParticlesToProcess.Count > 0) Session.ProcessParticles();

            for (int i = AvShots.Count - 1; i >= 0; i--)
            {
                var av = AvShots[i];

                var refreshed = av.LastTick == Session.Tick;
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
                            av.PrimeEntity.PositionComp.SetWorldMatrix(ref av.PrimeMatrix, null, false, false, false);
                        }

                        if ((av.Cloaked || av.OnScreen == AvShot.Screen.None) && av.PrimeEntity.InScene)
                        {
                            av.PrimeEntity.InScene = false;
                            av.PrimeEntity.Render.RemoveRenderObjects();
                        }
                    }
                    if (av.Triggered && av.TriggerEntity != null)
                    {
                        if (!av.AmmoDef.AreaEffect.Pulse.HideModel && (!av.TriggerEntity.InScene))
                        {
                            av.TriggerEntity.InScene = true;
                            av.TriggerEntity.Render.UpdateRenderObject(true, false);
                        }
                        av.TriggerEntity.PositionComp.SetWorldMatrix(ref av.TriggerMatrix, null, false, false, false);

                        if (av.OnScreen != AvShot.Screen.None && av.AmmoDef.Const.FieldParticle && av.FieldEffect != null)
                            av.FieldEffect.WorldMatrix = av.PrimeMatrix;
                    }

                    if (av.HasTravelSound)
                    {
                        if (!av.AmmoSound)
                        {
                            double distSqr;
                            Vector3D.DistanceSquared(ref av.TracerFront, ref Session.CameraPos, out distSqr);
                            if (distSqr <= av.AmmoDef.Const.AmmoTravelSoundDistSqr)
                                av.AmmoSoundStart();
                        }
                        else av.TravelEmitter.SetPosition(av.TracerFront);
                    }

                    if (av.HitParticle == AvShot.ParticleState.Custom) 
                    {
                        av.HitParticle = AvShot.ParticleState.Dirty;
                        if (av.OnScreen != AvShot.Screen.None) {
                            var pos = av.Hit.HitTick == Session.Tick && !MyUtils.IsZero(av.Hit.SurfaceHit) ? av.Hit.SurfaceHit : av.TracerFront;
                            var matrix = MatrixD.CreateTranslation(pos);

                            MyParticleEffect hitEffect;
                            if (MyParticlesManager.TryCreateParticleEffect(av.AmmoDef.AmmoGraphics.Particles.Hit.Name, ref matrix, ref pos, uint.MaxValue, out hitEffect)) {

                                //hitEffect.UserColorMultiplier = av.AmmoDef.AmmoGraphics.Particles.Hit.Color;
                                var scaler = 1;
                                hitEffect.UserRadiusMultiplier = av.AmmoDef.AmmoGraphics.Particles.Hit.Extras.Scale * scaler;
                                //var scale = av.AmmoDef.Const.HitParticleShrinks ? MathHelper.Clamp(MathHelper.Lerp(1, 0, av.DistanceToLine / av.AmmoDef.AmmoGraphics.Particles.Hit.Extras.MaxDistance), 0.05f, 1) : 1;
                                //hitEffect.UserScale = scale * scaler;
                                hitEffect.Velocity = av.Hit.HitVelocity;
                            }
                        }
                    }
                    else if (av.HitParticle == AvShot.ParticleState.Explosion)
                    {
                        av.HitParticle = AvShot.ParticleState.Dirty;
                        if (ExplosionReady && av.OnScreen != AvShot.Screen.None)
                        {
                            var pos = !MyUtils.IsZero(av.Hit.SurfaceHit) ? av.Hit.SurfaceHit : av.TracerFront;
                            if (av.DetonateFakeExp) SUtils.CreateFakeExplosion(Session, av.AmmoDef.Const.DetonationRadius, pos, av.Direction, av.Hit.Entity, av.AmmoDef, av.Hit.HitVelocity);
                            else SUtils.CreateFakeExplosion(Session, av.AmmoDef.Const.AreaEffectSize, pos, av.Direction, av.Hit.Entity, av.AmmoDef, av.Hit.HitVelocity);
                        }
                    }

                    if (av.Model != AvShot.ModelState.None)
                    {
                        if (av.AmmoEffect != null && av.AmmoDef.Const.AmmoParticle && av.AmmoDef.Const.PrimeModel)
                        {
                            var offVec = av.TracerFront + Vector3D.Rotate(av.AmmoDef.AmmoGraphics.Particles.Ammo.Offset, av.PrimeMatrix);
                            av.AmmoEffect.WorldMatrix = av.PrimeMatrix;
                            av.AmmoEffect.SetTranslation(ref offVec);
                        }
                    }
                    else if (av.AmmoEffect != null && av.AmmoDef.Const.AmmoParticle)
                    {
                        av.AmmoEffect.SetTranslation(ref av.TracerFront);
                    }
                }

                if (av.EndState.Dirty)
                    av.AvClose();
            }
        }


        internal void Run()
        {
            if (Session.Tick180) {

                Log.LineShortDate($"(DRAWS) --------------- AvShots:[{AvShots.Count}] OnScreen:[{_onScreens}] Shrinks:[{_shrinks}] Glows:[{_glows}] Models:[{_models}] P:[{Session.Projectiles.ActiveProjetiles.Count}] P-Pool:[{Session.Projectiles.ProjectilePool.Count}] AvPool:[{AvShotPool.Count}] (AvBarrels 1:[{Effects1.Count}] 2:[{Effects2.Count}])", "stats");
                _glows = 0;
                _shrinks = 0;
            }

            _onScreens = 0;
            _models = 0;
            for (int i = AvShots.Count - 1; i >= 0; i--)
            {
                var av = AvShots[i];
                if (av.OnScreen != AvShot.Screen.None) _onScreens++;
                var refreshed = av.LastTick == Session.Tick;

                if (refreshed && av.Tracer != AvShot.TracerState.Off && av.OnScreen != AvShot.Screen.None)
                {
                    var color = av.Color;
                    var segColor = av.SegmentColor;

                    if (av.ShotFade > 0)
                    {
                        var fade = MathHelper.Clamp(1f - av.ShotFade, 0.005f, 1f);
                        color *= fade;
                        segColor *= fade;
                    }

                    if (!av.AmmoDef.Const.OffsetEffect)
                    {
                        if (av.Tracer != AvShot.TracerState.Shrink)
                        {
                            if (av.AmmoDef.Const.TracerMode == AmmoConstants.Texture.Normal)
                                MyTransparentGeometry.AddLineBillboard(av.AmmoDef.Const.TracerTextures[0], color, av.TracerBack, av.VisualDir, (float)av.VisualLength, (float)av.TracerWidth);
                            else if (av.AmmoDef.Const.TracerMode != AmmoConstants.Texture.Resize)
                                MyTransparentGeometry.AddLineBillboard(av.AmmoDef.Const.TracerTextures[av.TextureIdx], color, av.TracerBack, av.VisualDir, (float)av.VisualLength, (float)av.TracerWidth);
                            else {
                                
                                var seg = av.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation;
                                var stepPos = av.TracerBack;
                                var segTextureCnt = av.AmmoDef.Const.SegmentTextures.Length;
                                var gapTextureCnt = av.AmmoDef.Const.TracerTextures.Length;
                                var segStepLen = seg.SegmentLength / segTextureCnt;
                                var gapStepLen = seg.SegmentGap / gapTextureCnt;
                                var gapEnabled = gapStepLen > 0;
                                int j = 0;
                                double travel = 0;
                                while (travel < av.VisualLength) {

                                    var mod = j++ % 2;
                                    var gap = gapEnabled && (av.SegmentGaped && mod == 0 || !av.SegmentGaped && mod == 1);
                                    var first = travel <= 0;

                                    double width;
                                    double rawLen;
                                    Vector4 dyncColor;
                                    if (!gap) {
                                        rawLen = first ? av.SegmentLenTranserved : seg.SegmentLength;
                                        if (rawLen <= 0)
                                            break;
                                        width = av.SegmentWidth;
                                        dyncColor = segColor;
                                    }
                                    else {
                                        rawLen = first ? av.SegmentLenTranserved : seg.SegmentGap;
                                        if (rawLen <= 0)
                                            break;
                                        width = av.TracerWidth;
                                        dyncColor = color;
                                    }

                                    var notLast = travel + rawLen < av.VisualLength;
                                    var len = notLast ? rawLen : av.VisualLength - travel;
                                    var clampStep = !gap ? MathHelperD.Clamp((int)((len / segStepLen) + 0.5) - 1, 0, segTextureCnt - 1) : MathHelperD.Clamp((int)((len / gapStepLen) + 0.5) - 1, 0, gapTextureCnt - 1);
                                    var material = !gap ? av.AmmoDef.Const.SegmentTextures[(int)clampStep] : av.AmmoDef.Const.TracerTextures[(int)clampStep];

                                    MyTransparentGeometry.AddLineBillboard(material, dyncColor, stepPos, av.VisualDir, (float)len, (float)width);
                                    if (!notLast)
                                        travel = av.VisualLength;
                                    else
                                        travel += len;
                                    stepPos += (av.VisualDir * len);
                                }
                            }
                        }
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
                            MyTransparentGeometry.AddLineBillboard(av.AmmoDef.Const.TracerTextures[0], color, fromBeam, normDir, length, (float)av.TracerWidth);

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
                    var steps = av.AmmoDef.AmmoGraphics.Lines.Trail.DecayTime;
                    var widthScaler = !av.AmmoDef.AmmoGraphics.Lines.Trail.UseColorFade;
                    var remove = false;
                    for (int j = glowCnt - 1; j >= 0; j--)
                    {
                        var glow = av.GlowSteps[j];

                        if (!refreshed)
                            glow.Line = new LineD(glow.Line.From + av.ShootVelStep, glow.Line.To + av.ShootVelStep, glow.Line.Length);

                        if (av.OnScreen != AvShot.Screen.None)
                        {
                            var reduction = (av.GlowShrinkSize * glow.Step);
                            var width = widthScaler ? (av.AmmoDef.Const.TrailWidth - reduction) * av.TrailScaler : av.AmmoDef.Const.TrailWidth * av.TrailScaler;
                            var color = av.AmmoDef.AmmoGraphics.Lines.Trail.Color;

                            if (!widthScaler)
                                color *= MathHelper.Clamp(1f - reduction, 0.01f, 1f);
                            
                            MyTransparentGeometry.AddLineBillboard(av.AmmoDef.Const.TrailTextures[0], color, glow.Line.From, glow.Line.Direction, (float) glow.Line.Length, width);
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
                
                if (glowCnt == 0 && shrinkCnt == 0 && av.MarkForClose) {
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
                if (!av.AmmoDef.Const.OffsetEffect) {

                    if (av.OnScreen != AvShot.Screen.None)
                        MyTransparentGeometry.AddLineBillboard(av.AmmoDef.Const.TracerTextures[0], s.Color, s.NewFront, av.VisualDir, s.Length, s.Thickness);
                }
                else if (av.OnScreen != AvShot.Screen.None)
                    av.DrawLineOffsetEffect(s.NewFront, -av.Direction, s.Length, s.Thickness, s.Color);

                if (av.Trail != AvShot.TrailState.Off && av.Back)
                    av.RunGlow(ref s, true);
            }

            if (av.TracerShrinks.Count == 0) av.ResetHit();
        }

        internal void RunHitSounds()
        {
            for (int i = 0; i < HitSounds.Count; i++)
            {
                var av = HitSounds[i];

                av.Emitter.SetPosition(av.Position);
                av.Emitter.PlaySound(av.SoundPair);
                Session.SoundsToClean.Add(new Session.CleanSound { Hit = av.Hit, Emitter = av.Emitter, EmitterPool = HitEmitters, SoundPair = av.SoundPair, SoundPairPool = av.Pool, SpawnTick = Session.Tick });
            }
            HitSounds.Clear();
        }

        internal void RunAvEffects1()
        {
            for (int i = Effects1.Count - 1; i >= 0; i--) {

                var avEffect = Effects1[i];
                var weapon = avEffect.Weapon;
                var muzzle = avEffect.Muzzle;
                var ticksAgo = weapon.Comp.Session.Tick - avEffect.StartTick;
                var bAv = weapon.System.Values.HardPoint.Graphics.Effect1;
                var effect = weapon.Effects1[muzzle.MuzzleId];

                var effectExists = effect != null;
                if (effectExists && avEffect.EndTick == 0 && weapon.StopBarrelAvTick >= Session.Tick)
                    avEffect.EndTick = (uint)bAv.Extras.MaxDuration + Session.Tick;

                var info = weapon.Dummies[muzzle.MuzzleId].Info;
                var somethingEnded = avEffect.EndTick != 0 && avEffect.EndTick <= Session.Tick || !weapon.PlayTurretAv || info.Entity == null || info.Entity.MarkedForClose || weapon.Comp.Ai == null || weapon.MuzzlePart.Entity?.Parent == null || weapon.Comp.CoreEntity.MarkedForClose || weapon.MuzzlePart.Entity.MarkedForClose;

                var effectStale = effectExists && (effect.IsEmittingStopped || effect.IsStopped || effect.GetElapsedTime() >= effect.DurationMax) || !effectExists && ticksAgo > 0;
                if (effectStale || somethingEnded || !weapon.Comp.IsWorking) {
                    if (effectExists) {
                        if (effect.Loop) effect.Stop(bAv.Extras.Restart);
                        weapon.Effects1[muzzle.MuzzleId] = null;
                    }
                    muzzle.Av1Looping = false;
                    muzzle.LastAv1Tick = 0;
                    Effects1.RemoveAtFast(i);
                    AvEffectPool.Return(avEffect);
                    continue;
                }

                if (weapon.Comp.Ai.VelocityUpdateTick != weapon.Comp.Session.Tick) {

                    weapon.Comp.Ai.GridVel = weapon.Comp.Ai.TopEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                    weapon.Comp.Ai.IsStatic = weapon.Comp.Ai.TopEntity.Physics?.IsStatic ?? false;
                    weapon.Comp.Ai.VelocityUpdateTick = weapon.Comp.Session.Tick;
                }


                var particles = weapon.System.Values.HardPoint.Graphics.Effect1;
                var renderId = info.Entity.Render.GetRenderObjectID();
                var matrix = info.DummyMatrix;
                var pos = info.Position;
                matrix.Translation = info.LocalPosition + particles.Offset;

                if (!effectExists && ticksAgo <= 0) {

                    if (MyParticlesManager.TryCreateParticleEffect(particles.Name, ref matrix, ref pos, renderId, out weapon.Effects1[muzzle.MuzzleId])) {

                        effect = weapon.Effects1[muzzle.MuzzleId];
                        //effect.UserColorMultiplier = particles.Color;
                        effect.UserRadiusMultiplier = particles.Extras.Scale;
                        muzzle.Av1Looping = effect.Loop || effect.DurationMax <= 0;
                    }
                }
                else if (particles.Extras.Restart && effectExists && effect.IsEmittingStopped) {
                    effect.WorldMatrix = matrix;
                    //effect.SetTranslation(ref pos);
                    effect.Play();
                }
                else if (effectExists) {
                    effect.WorldMatrix = matrix;
                    //effect.SetTranslation(ref pos);
                }
            }
        }

        internal void RunAvEffects2()
        {
            for (int i = Effects2.Count - 1; i >= 0; i--) {
                var av = Effects2[i];
                var weapon = av.Weapon;
                var muzzle = av.Muzzle;
                var ticksAgo = weapon.Comp.Session.Tick - av.StartTick;
                var bAv = weapon.System.Values.HardPoint.Graphics.Effect2;

                var effect = weapon.Effects2[muzzle.MuzzleId];
                var effectExists = effect != null;
                if (effectExists && av.EndTick == 0 && weapon.StopBarrelAvTick >= Session.Tick)
                    av.EndTick = (uint)bAv.Extras.MaxDuration + Session.Tick;
                
                var info = weapon.Dummies[muzzle.MuzzleId].Info;
                var somethingEnded = av.EndTick != 0 && av.EndTick <= Session.Tick || !weapon.PlayTurretAv || info.Entity == null || info.Entity.MarkedForClose || weapon.Comp.Ai == null || weapon.MuzzlePart.Entity?.Parent == null || weapon.Comp.CoreEntity.MarkedForClose || weapon.MuzzlePart.Entity.MarkedForClose;
                
                var effectStale = effectExists && (effect.IsEmittingStopped || effect.IsStopped || effect.GetElapsedTime() >= effect.DurationMax) || !effectExists && ticksAgo > 0;

                if (effectStale || somethingEnded || !weapon.Comp.IsWorking)
                {
                    if (effectExists) {
                        if (effect.Loop) effect.Stop(bAv.Extras.Restart);
                        weapon.Effects2[muzzle.MuzzleId] = null;
                    }
                    muzzle.Av2Looping = false;
                    muzzle.LastAv2Tick = 0;
                    Effects2.RemoveAtFast(i);
                    AvEffectPool.Return(av);
                    continue;
                }

                if (weapon.Comp.Ai.VelocityUpdateTick != weapon.Comp.Session.Tick)  {

                    weapon.Comp.Ai.GridVel = weapon.Comp.Ai.TopEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                    weapon.Comp.Ai.IsStatic = weapon.Comp.Ai.TopEntity.Physics?.IsStatic ?? false;
                    weapon.Comp.Ai.VelocityUpdateTick = weapon.Comp.Session.Tick;
                }

                var particles = weapon.System.Values.HardPoint.Graphics.Effect2;
                var renderId = info.Entity.Render.GetRenderObjectID();
                var matrix = info.DummyMatrix;
                var pos = info.Position;
                matrix.Translation = info.LocalPosition + particles.Offset;

                if (!effectExists && ticksAgo <= 0)  {

                    if (MyParticlesManager.TryCreateParticleEffect(particles.Name, ref matrix, ref pos, renderId, out weapon.Effects2[muzzle.MuzzleId]))  {
                        effect = weapon.Effects2[muzzle.MuzzleId];
                        //effect.UserColorMultiplier = particles.Color;
                        effect.UserRadiusMultiplier = particles.Extras.Scale;
                        muzzle.Av2Looping = effect.Loop || effect.DurationMax <= 0;
                    }
                }
                else if (particles.Extras.Restart && effectExists && effect.IsEmittingStopped)  {

                    effect.WorldMatrix = matrix;
                    //effect.SetTranslation(ref pos);
                    effect.Play();
                }
                else if (effectExists)  {

                    effect.WorldMatrix = matrix;
                    //effect.SetTranslation(ref pos);

                }
            }
        }
    }

    internal class AvEffect
    {
        internal Weapon Weapon;
        internal Weapon.Muzzle Muzzle;
        internal uint StartTick;
        internal uint EndTick;

        internal void Clean()
        {
            Weapon = null;
            Muzzle = null;
            StartTick = 0;
            EndTick = 0;
        }
    }

    internal struct HitSound
    {
        internal MyEntity3DSoundEmitter Emitter;
        internal MySoundPair SoundPair;
        internal Stack<MySoundPair> Pool;
        internal Vector3D Position;
        internal bool Hit;
    }
}
