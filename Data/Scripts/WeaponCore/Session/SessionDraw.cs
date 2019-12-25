using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
namespace WeaponCore
{
    public partial class Session
    {
        private void DrawLists()
        {
            var sFound = false;
            for (int i = 0; i < Projectiles.DrawProjectiles.Count; i++)
            {
                var info = Projectiles.DrawProjectiles[i];
                if (info.StartSoundActived)
                {
                    info.StartSoundActived = false;
                    info.FireEmitter.PlaySound(info.FireSound, true);
                }

                if (info.HasTravelSound)
                {
                    if (!info.AmmoSound)
                    {
                        double dist;
                        Vector3D.DistanceSquared(ref info.Position, ref CameraPos, out dist);
                        if (dist <= info.System.AmmoTravelSoundDistSqr) info.AmmoSoundStart();
                    }
                    else info.TravelEmitter.SetPosition(info.Position);
                }

                if (info.HitSoundActived)
                {
                    info.HitSoundActived = false;
                    info.HitEmitter.SetPosition(info.Position);
                    info.HitEmitter.CanPlayLoopSounds = false;
                    info.HitEmitter.PlaySound(info.HitSound, true);
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

                if (info.FakeExplosion)
                {
                    info.FakeExplosion = false;
                    if (ExplosionReady)
                        SUtils.CreateFakeExplosion(this, info.System.Values.Ammo.AreaEffect.AreaEffectRadius, info.Position, info.System);
                }

                if (info.PrimeEntity != null)
                {
                    if (info.Draw != ProInfo.DrawState.Last && !info.PrimeEntity.InScene && !info.Cloaked)
                    {
                        info.PrimeEntity.InScene = true;
                        info.PrimeEntity.Render.UpdateRenderObject(true, false);
                    }
                    info.PrimeEntity.PositionComp.SetWorldMatrix(info.PrimeMatrix, null, false, false, false);
                    if (info.Draw == ProInfo.DrawState.Last || info.Cloaked && info.PrimeEntity.InScene)
                    {
                        info.PrimeEntity.InScene = false;
                        info.PrimeEntity.Render.RemoveRenderObjects();
                    }
                    if (!info.System.Values.Graphics.Line.Tracer.Enable && info.TriggerEntity == null) continue;
                }

                if (info.Triggered && info.TriggerEntity != null)
                {
                    if ((info.Draw != ProInfo.DrawState.Last && !info.TriggerEntity.InScene))
                    {
                        info.TriggerEntity.InScene = true;
                        info.TriggerEntity.Render.UpdateRenderObject(true, false);
                    }

                    info.TriggerEntity.PositionComp.SetWorldMatrix(info.TriggerMatrix, null, false, false, false);
                    if (info.Draw == ProInfo.DrawState.Last)
                    {
                        info.TriggerEntity.InScene = false;
                        info.TriggerEntity.Render.RemoveRenderObjects();
                    }
                }

                if (!info.System.Values.Graphics.Line.Tracer.Enable || info.Shrinking) continue;

                var color = info.Color;
                var thickness = info.LineWidth;
                if (info.System.IsBeamWeapon)
                {
                    var changeValue = 0.01f;
                    if (info.System.IsBeamWeapon && info.BaseDamagePool > info.System.Values.Ammo.BaseDamage)
                    {
                        thickness *= info.BaseDamagePool / info.System.Values.Ammo.BaseDamage;
                        changeValue = 0.02f;
                    }
                    if (_lCount < 60)
                    {
                        var adder = (_lCount + 1);
                        var adder2 = adder * changeValue;
                        var adder3 = adder2 + 1;
                        thickness = adder3 * thickness;
                        color *= adder3;
                    }
                    else
                    {
                        var shrinkFrom = ((60) * changeValue) + 1;

                        var adder = (_lCount - 59);
                        var adder2 = adder * changeValue;
                        var scaler = (shrinkFrom - adder2);
                        thickness = scaler * thickness;
                        color *= (shrinkFrom - adder2);
                    }
                }
                else
                {
                    if (info.ReSizing == ProInfo.ReSize.Shrink && info.DrawHit?.HitPos != null && info.OnScreen != ProInfo.Screen.None)
                    {
                        info.Shrinking = true;
                        sFound = true;
                        var shrink = _shrinkPool.Get();
                        shrink.Init(info, thickness);
                        _shrinking.Add(shrink);
                    }
                    else if (info.System.Trail && info.ReSizing != ProInfo.ReSize.Grow)
                    {
                        var glow = _glowPool.Get();
                        glow.Parent = info.Glowers.Count > 0 ? info.Glowers.Peek() : null;
                        glow.Back = info.LineStart;
                        glow.FirstTick = Tick;
                        glow.System = info.System;
                        glow.ShooterVel = info.ShooterVel;
                        glow.WidthScaler = info.LineScaler;
                        info.Glowers.Push(glow);
                        _afterGlow.Add(glow);
                    }
                }

                if (info.System.OffsetEffect && info.OnScreen == ProInfo.Screen.Tracer)
                    LineOffsetEffect(info.System, info.Position, info.Direction, info.DistanceTraveled, info.Length, thickness, color);
                else if (info.OnScreen == ProInfo.Screen.Tracer)
                    MyTransparentGeometry.AddLineBillboard(info.System.TracerMaterial, color, info.Position, -info.Direction, (float)info.Length, thickness);

                if (info.System.IsBeamWeapon && info.System.HitParticle && !(info.MuzzleId != 0 && (info.System.ConvergeBeams || info.System.OneHitParticle)))
                {
                    var c = info.Target.FiringCube;
                    if (c == null || c.MarkedForClose)
                        continue;

                    WeaponComponent weaponComp;
                    if (info.Ai.WeaponBase.TryGetValue(c, out weaponComp))
                    {
                        var weapon = weaponComp.Platform.Weapons[info.WeaponId];
                        var effect = weapon.HitEffects[info.MuzzleId];
                        if (info.DrawHit?.HitPos != null && info.OnScreen == ProInfo.Screen.Tail)
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
                            var hit = info.DrawHit.Value.HitPos.Value;
                            MatrixD matrix;
                            MatrixD.CreateTranslation(ref hit, out matrix);
                            if (effect == null)
                            {
                                MyParticlesManager.TryCreateParticleEffect(info.System.Values.Graphics.Particles.Hit.Name, ref matrix, ref hit, uint.MaxValue, out effect);
                                if (effect == null)
                                {
                                    weapon.HitEffects[info.MuzzleId] = null;
                                    continue;
                                }

                                effect.DistanceMax = info.System.Values.Graphics.Particles.Hit.Extras.MaxDistance;
                                effect.DurationMax = info.System.Values.Graphics.Particles.Hit.Extras.MaxDuration;
                                effect.UserColorMultiplier = info.System.Values.Graphics.Particles.Hit.Color;
                                effect.Loop = info.System.Values.Graphics.Particles.Hit.Extras.Loop;
                                effect.UserRadiusMultiplier = info.System.Values.Graphics.Particles.Hit.Extras.Scale * 1;
                                var scale = MathHelper.Lerp(1, 0, (info.DistanceToLine * 2) / info.System.Values.Graphics.Particles.Hit.Extras.MaxDistance);
                                effect.UserEmitterScale = scale;
                            }
                            else if (effect.IsEmittingStopped)
                                effect.Play();

                            effect.WorldMatrix = matrix;
                            if (info.DrawHit.Value.Projectile != null) effect.Velocity = info.DrawHit.Value.Projectile.Velocity;
                            else if (info.DrawHit.Value.Entity?.GetTopMostParent()?.Physics != null) effect.Velocity = info.DrawHit.Value.Entity.GetTopMostParent().Physics.LinearVelocity;
                            weapon.HitEffects[info.MuzzleId] = effect;
                        }
                        else if (effect != null)
                        {
                            effect.Stop(false);
                            weapon.HitEffects[info.MuzzleId] = null;
                        }
                    }
                }
            }
            if (sFound) _shrinking.ApplyAdditions();
            Projectiles.DrawProjectiles.Clear();
        }

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
                        var glow = _glowPool.Get();
                        glow.Parent = s.Glowers.Count > 0 ? s.Glowers.Peek() : null;
                        glow.Back = shrunk.Value.BackOfTail;
                        glow.FirstTick = Tick;
                        glow.System = s.System;
                        glow.ShooterVel = s.ShooterVel;
                        glow.WidthScaler = s.LineScaler;
                        s.Glowers.Push(glow);
                        _afterGlow.Add(glow);
                    }
                }
                else
                {
                    s.Clean();
                    _shrinkPool.Return(s);
                    _shrinking.Remove(s);
                    sRemove = true;
                }
            }
            if (sRemove) _shrinking.ApplyRemovals();
        }

        private void AfterGlow()
        {
            for (int i = 0; i < _afterGlow.Count; i++)
            {
                var glow = _afterGlow[i];
                var thisStep = (Tick - glow.FirstTick);
                if (thisStep != 0) glow.Back += (glow.ShooterVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);
                if (glow.Parent == null) continue;
                var steps = glow.System.Values.Graphics.Line.Trail.DecayTime;
                var fullSize = glow.System.Values.Graphics.Line.Tracer.Width;
                var shrinkAmount = fullSize / steps;

                var line = new LineD(glow.Back, glow.Parent.Back);

                var distanceFromPointSqr = Vector3D.DistanceSquared(CameraPos, (MyUtils.GetClosestPointOnLine(ref line.From, ref line.To, ref CameraPos)));
                int scale = 1;
                if (distanceFromPointSqr > 8000 * 8000) scale = 8;
                else if (distanceFromPointSqr > 4000 * 4000) scale = 7;
                else if (distanceFromPointSqr > 2000 * 2000) scale = 6;
                else if (distanceFromPointSqr > 1000 * 1000) scale = 5;
                else if (distanceFromPointSqr > 500 * 500) scale = 4;
                else if (distanceFromPointSqr > 250 * 250) scale = 3;
                else if (distanceFromPointSqr > 100 * 100) scale = 2;
                var sliderScale = (glow.WidthScaler * scale);
                var reduction = (shrinkAmount * thisStep);
                var thickness = (fullSize - reduction) * sliderScale;

                if (thisStep < steps)
                    MyTransparentGeometry.AddLineBillboard(glow.System.TrailMaterial, glow.System.Values.Graphics.Line.Trail.Color, line.From, line.Direction, (float)line.Length, thickness);
                else
                    _glowRemove.Add(glow);
            }

            for (int i = 0; i < _glowRemove.Count; i++)
            {
                var remove = _glowRemove[i];

                remove.Clean();
                _afterGlow.Remove(remove);
                _glowPool.Return(remove);
            }
            _glowRemove.Clear();
        }

        internal void LineOffsetEffect(WeaponSystem system, Vector3D pos, Vector3D direction, double distanceTraveled, double tracerLength, float beamRadius, Vector4 color)
        {
            MatrixD matrix;
            var up = MatrixD.Identity.Up;
            var startPos = pos + -(direction * tracerLength);
            MatrixD.CreateWorld(ref startPos, ref direction, ref up, out matrix);
            var offsetMaterial = system.TracerMaterial;
            var tracerLengthSqr = tracerLength * tracerLength;
            var maxOffset = system.Values.Graphics.Line.OffsetEffect.MaxOffset;
            var minLength = system.Values.Graphics.Line.OffsetEffect.MinLength;
            var maxLength = system.Values.Graphics.Line.OffsetEffect.MaxLength;

            double currentForwardDistance = 0;

            while (currentForwardDistance < tracerLength)
            {
                currentForwardDistance += MyUtils.GetRandomDouble(minLength, maxLength);
                var lateralXDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                var lateralYDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                _offsetList.Add(new Vector3D(lateralXDistance, lateralYDistance, currentForwardDistance * -1));
            }

            for (int i = 0; i < _offsetList.Count; i++)
            {
                Vector3D fromBeam;
                Vector3D toBeam;

                if (i == 0)
                {
                    fromBeam = matrix.Translation;
                    toBeam = Vector3D.Transform(_offsetList[i], matrix);
                }
                else
                {
                    fromBeam = Vector3D.Transform(_offsetList[i - 1], matrix);
                    toBeam = Vector3D.Transform(_offsetList[i], matrix);
                }

                Vector3 dir = (toBeam - fromBeam);
                var length = dir.Length();
                var normDir = dir / length;
                MyTransparentGeometry.AddLineBillboard(offsetMaterial, color, fromBeam, normDir, length, beamRadius);

                if (Vector3D.DistanceSquared(matrix.Translation, toBeam) > tracerLengthSqr) break;
            }
            _offsetList.Clear();
        }
    }
}
