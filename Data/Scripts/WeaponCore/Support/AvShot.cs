using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace WeaponCore.Support
{
    internal class AvShot
    {
        internal WeaponSystem System;
        internal GridAi Ai;
        internal MyQueue<AfterGlow> GlowSteps = new MyQueue<AfterGlow>();
        internal Stack<Shrinking> ShrinkSteps = new Stack<Shrinking>();
        internal WeaponComponent FiringWeapon;
        internal bool Offset;
        internal bool Accelerates;
        internal bool Active;
        internal TracerState Tracer;
        internal TrailState Trail;
        internal Screen OnScreen;
        internal double MaxTracerLength;
        internal double TracerLength;
        internal double MaxGlowLength;
        internal double FirstStepSize;
        internal double StepSize;
        internal double TotalLength;
        internal double Thickness;
        internal double LineScaler;
        internal double DistanceToLine;
        internal double ScaleFov;
        internal double VisualLength;
        internal double MaxSpeed;
        internal double MaxStepSize;
        internal int LifeTime;
        internal int MuzzleId;
        internal int WeaponId;
        internal int LastGlowIdx = 0;
        internal uint EndTick;
        internal Vector3D Position;
        internal Vector3D Direction;
        internal Vector3D HitVelocity;
        internal Vector3D HitPosition;
        internal Vector3D ShooterVelocity;
        internal Vector3D TracerStart;
        internal Vector4 Color;
        internal Vector3 ClosestPointOnLine;
        internal DrawHit DrawHit;

        internal enum TracerState
        {
            Full,
            Grow,
            Shrink,
            Off,
        }

        internal enum TrailState
        {
            Front,
            Back,
            Off,
        }

        internal enum Screen
        {
            Tracer,
            Tail,
            None,
        }

        internal void Init(ProInfo info,  double firstStepSize, double maxSpeed)
        {
            System = info.System;
            Ai = info.Ai;
            Offset = System.OffsetEffect;
            MaxTracerLength = System.TracerLength;
            FirstStepSize = firstStepSize;
            Accelerates = System.Values.Ammo.Trajectory.AccelPerSec > 0;
            MuzzleId = info.MuzzleId;
            WeaponId = info.WeaponId;
            MaxSpeed = maxSpeed;
            MaxStepSize = MaxSpeed * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;

            FiringWeapon = null;
            info.Ai.WeaponBase.TryGetValue(info.Target.FiringCube, out FiringWeapon);
            // Reset to zero
            DrawHit = new DrawHit();
            HitVelocity = Vector3D.Zero;
            HitPosition = Vector3D.Zero;
            Position = Vector3D.Zero;
            Direction = Vector3D.Zero;
            TracerStart = Vector3D.Zero;
            OnScreen = Screen.None;
            Tracer = TracerState.Off;
            Trail = TrailState.Off;
            Active = false;
            EndTick = uint.MaxValue;
            LifeTime = 0;
            LastGlowIdx = 0;
            //

            if (System.DrawLine) Tracer = firstStepSize < MaxTracerLength ? TracerState.Grow : TracerState.Full;
            else Tracer = TracerState.Off;

            if (System.Trail)
            {
                MaxGlowLength = System.Values.Graphics.Line.Trail.DecayTime * MaxStepSize;
                Trail = System.Values.Graphics.Line.Trail.Back ? TrailState.Back : Trail = TrailState.Front;
            }
            else Trail = TrailState.Off;
            TotalLength = MaxTracerLength + MaxTracerLength;
        }

        internal void Update(double stepSize, double visualLength, ref Vector3D shooterVelocity, ref Vector3D position, ref Vector3D direction)
        {
            Position = position;
            Direction = direction;
            StepSize = stepSize;
            ShooterVelocity = shooterVelocity;
            VisualLength = visualLength;
            TracerStart = Position + (-Direction * VisualLength);
            LifeTime++;
        }

        internal void Complete(ProInfo info, bool saveHit = false, bool end = false)
        {
            Active = true;
            if (DrawHit.HitPos != Vector3D.Zero)
            {
                if (saveHit)
                {
                    if (DrawHit.Entity == null)
                        HitVelocity = DrawHit.Projectile.Velocity;
                    else HitVelocity = DrawHit.Entity?.GetTopMostParent()?.Physics?.LinearVelocity ?? Vector3D.Zero;
                    HitPosition = DrawHit.HitPos;
                }

                var totalDiff = TotalLength - StepSize;
                var tracerDiff = TracerLength - StepSize;
                var visable = totalDiff > 0 && !MyUtils.IsZero(totalDiff);
                var tracerVisable = tracerDiff > 0 && !MyUtils.IsZero(tracerDiff);
                if (!visable || !tracerVisable)
                {
                    Tracer = TracerState.Off;
                }
                else
                {
                    Tracer = TracerState.Shrink;
                    TracerLength = tracerDiff;
                }
            }
            else if (Tracer != TracerState.Off && VisualLength >= MaxTracerLength)
            {
                Tracer = TracerState.Full;
                TracerLength = MaxTracerLength;
            }

            if (end)
            {
                EndTick = info.System.Session.Tick;
                if (Trail == TrailState.Off) Close();
                Tracer = TracerState.Off;
            }

            if (Tracer != TracerState.Off)
                RunTracer();

            if (Trail != TrailState.Off && Tracer != TracerState.Grow)
                RunGlow();

            var color = System.Values.Graphics.Line.Tracer.Color;
            if (System.LineColorVariance)
            {
                var cv = System.Values.Graphics.Line.ColorVariance;
                var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                color.X *= randomValue;
                color.Y *= randomValue;
                color.Z *= randomValue;
            }
            Color = color;
            var width = System.Values.Graphics.Line.Tracer.Width;
            if (System.LineWidthVariance)
            {
                var wv = System.Values.Graphics.Line.WidthVariance;
                var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                width += randomValue;
            }

            var target = Position + (-Direction * MaxTracerLength);
            ClosestPointOnLine = MyUtils.GetClosestPointOnLine(ref Position, ref target, ref Ai.Session.CameraPos);
            DistanceToLine = (float)Vector3D.Distance(ClosestPointOnLine, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            ScaleFov = Math.Tan(MyAPIGateway.Session.Camera.FovWithZoom * 0.5);
            Thickness = Math.Max(width, 0.10f * ScaleFov * (DistanceToLine / 100));
            LineScaler = (Thickness / width);

            if (Tracer == TracerState.Grow)
            {
                TracerLength = VisualLength;
            }

            if (OnScreen == Screen.Tail)
            {
                var totalLen = TracerStart + (-Direction * MaxGlowLength);
                var bb = new BoundingBoxD(Vector3D.Min(totalLen, Position), Vector3D.Max(totalLen, Position));
                if (!Ai.Session.Camera.IsInFrustum(ref bb)) OnScreen = Screen.None;
            }
        }

        internal void RunTracer()
        {
            if (DrawHit.HitPos != Vector3D.Zero && System.IsBeamWeapon && System.HitParticle && !(MuzzleId != 0 && (System.ConvergeBeams || System.OneHitParticle)))
            {
                if (FiringWeapon != null)
                {
                    var weapon = FiringWeapon.Platform.Weapons[WeaponId];
                    var effect = weapon.HitEffects[MuzzleId];
                    if (DrawHit.HitPos != Vector3D.Zero && OnScreen == Screen.Tail)
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
                        MatrixD matrix;
                        MatrixD.CreateTranslation(ref HitPosition, out matrix);
                        if (effect == null)
                        {
                            MyParticlesManager.TryCreateParticleEffect(System.Values.Graphics.Particles.Hit.Name, ref matrix, ref HitPosition, uint.MaxValue, out effect);
                            if (effect == null)
                            {
                                weapon.HitEffects[MuzzleId] = null;
                                return;
                            }

                            effect.DistanceMax = System.Values.Graphics.Particles.Hit.Extras.MaxDistance;
                            effect.DurationMax = System.Values.Graphics.Particles.Hit.Extras.MaxDuration;
                            effect.UserColorMultiplier = System.Values.Graphics.Particles.Hit.Color;
                            effect.Loop = System.Values.Graphics.Particles.Hit.Extras.Loop;
                            effect.UserRadiusMultiplier = System.Values.Graphics.Particles.Hit.Extras.Scale * 1;
                            var scale = MathHelper.Lerp(1, 0, (DistanceToLine * 2) / System.Values.Graphics.Particles.Hit.Extras.MaxDistance);
                            effect.UserEmitterScale = (float)scale;
                        }
                        else if (effect.IsEmittingStopped)
                            effect.Play();

                        effect.WorldMatrix = matrix;
                        effect.Velocity = HitVelocity;
                        weapon.HitEffects[MuzzleId] = effect;
                    }
                    else if (effect != null)
                    {
                        effect.Stop(false);
                        weapon.HitEffects[MuzzleId] = null;
                    }
                }
            }
        }

        internal void RunGlow()
        {
            var glowCount = GlowSteps.Count;
            if (glowCount <= System.Values.Graphics.Line.Trail.DecayTime + 1)
            {
                var glow = Ai.Session.GlowPool.Get();
                LastGlowIdx = glowCount - 1;

                glow.Parent = glowCount > 0 ? GlowSteps[LastGlowIdx] : null;
                if (glow.Parent == null)
                {
                    glow.TracerStart = TracerStart;
                    glow.TailPos = TracerStart;
                }
                else glow.TailPos = glow.Parent.TailPos + (Direction * StepSize);

                glow.FirstTick = Ai.Session.Tick;
                glow.ShooterVel = ShooterVelocity;
                glow.System = System;
                GlowSteps.Enqueue(glow);
                ++glowCount;
            }

            float scale = 1;
            
            var distanceFromPointSqr = Vector3D.DistanceSquared(Ai.Session.CameraPos, ClosestPointOnLine);

            if (distanceFromPointSqr > 1500 * 1500) scale = 1.0f;
            if (distanceFromPointSqr > 1250 * 1250) scale = 1.1f;
            else if (distanceFromPointSqr > 1000 * 1000) scale = 1.2f;
            else if (distanceFromPointSqr > 750 * 750) scale = 1.3f;
            else if (distanceFromPointSqr > 500 * 500) scale = 1.4f;
            else if (distanceFromPointSqr > 250 * 250) scale = 1.3f;
            else if (distanceFromPointSqr > 125 * 125) scale = 1.2f;
            else if (distanceFromPointSqr > 75 * 75) scale = 1.1f;

            var sliderScale = ((float)LineScaler * scale);

            for (int i = 0; i < glowCount; i++)
            {
                var glow = GlowSteps[i];
                var thisStep = (Ai.Session.Tick - glow.FirstTick);
                //if (thisStep != 0) glow.Back += (glow.ShooterVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);
                var steps = glow.System.Values.Graphics.Line.Trail.DecayTime;
                var fullSize = glow.System.Values.Graphics.Line.Tracer.Width;
                var shrinkAmount = fullSize / steps;
                glow.Line = new LineD(glow.Parent?.TailPos ?? glow.TracerStart, glow.TailPos);
                /*
                if (glow.Parent == null)
                {
                    DsDebugDraw.DrawSingleVec(glow.TracerStart, 0.125f, VRageMath.Color.Orange);
                    DsDebugDraw.DrawSingleVec(glow.TailPos, 0.125f, VRageMath.Color.Purple);
                }
                else if (i == 0)
                {
                    DsDebugDraw.DrawSingleVec(glow.Parent.TailPos, 0.125f, VRageMath.Color.Orange);
                    DsDebugDraw.DrawSingleVec(glow.TailPos, 0.125f, VRageMath.Color.Purple);
                }
                else if (i == 1)
                {
                    DsDebugDraw.DrawSingleVec(glow.Parent.TailPos, 0.125f, VRageMath.Color.Red);
                    DsDebugDraw.DrawSingleVec(glow.TailPos, 0.125f, VRageMath.Color.Green);
                }
                else if (i == 2)
                {
                    DsDebugDraw.DrawSingleVec(glow.Parent.TailPos, 0.125f, VRageMath.Color.Red);
                    DsDebugDraw.DrawSingleVec(glow.TailPos, 0.125f, VRageMath.Color.Green);
                }
                */

                var reduction = (shrinkAmount * thisStep);
                glow.Thickness = (fullSize - reduction) * sliderScale;
            }
        }

        private void Close()
        {
            //Log.Line($"VsShot Closed");
            Active = false;
            Ai.Session.VisualShotPool.Return(this);
        }
    }

    internal class AfterGlow
    {
        internal WeaponSystem System;
        internal AfterGlow Parent;
        internal Vector3D TracerStart;
        internal Vector3D TailPos;
        internal Vector3D ShooterVel;
        internal LineD Line;
        internal int LifeTime;
        internal uint FirstTick;
        internal float WidthScaler;
        internal float Length;
        internal float Thickness;

        internal void Clean()
        {
            System = null;
            Parent = null;
            TracerStart = Vector3D.Zero;
            TailPos = Vector3D.Zero;
            FirstTick = 0;
            WidthScaler = 0;
            Length = 0;
            Thickness = 0;
            LifeTime = 0;
        }
    }
}
