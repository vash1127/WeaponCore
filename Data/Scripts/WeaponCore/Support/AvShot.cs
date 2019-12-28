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
        internal float Thickness;
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
            MaxStepSize = MaxSpeed / MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;

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
            Active = true;
            EndTick = 0;
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
            TotalLength = MaxTracerLength + MaxGlowLength;
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
            if (end) EndTick = info.System.Session.Tick;
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
                if (!visable)
                {
                    Tracer = TracerState.Off;
                    Trail = TrailState.Off;
                }
                else if (!tracerVisable)
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
            if (OnScreen != Screen.None)
            {
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
                LineScaler = Math.Max(1, 0.10f * ScaleFov * (DistanceToLine / 100));
                Thickness = (float)(width * LineScaler);
            }

            if (Tracer == TracerState.Grow)
            {
                TracerLength = VisualLength;
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

            for (int i = 0; i < glowCount; i++)
            {
                var glow = GlowSteps[i];
                var thisStep = (Ai.Session.Tick - glow.FirstTick);
                //if (thisStep != 0) glow.Back += (glow.ShooterVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);
                var steps = glow.System.Values.Graphics.Line.Trail.DecayTime;
                var fullSize = glow.System.Values.Graphics.Line.Tracer.Width;
                var shrinkAmount = fullSize / steps;
                glow.Line = new LineD(glow.Parent?.TailPos ?? glow.TracerStart, glow.TailPos);
                if (glow.Parent == null)
                {
                    DsDebugDraw.DrawSingleVec(glow.TracerStart, 0.125f, VRageMath.Color.Orange);
                    DsDebugDraw.DrawSingleVec(glow.TailPos, 0.125f, VRageMath.Color.Purple);
                    Log.Line("test");
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
                var distanceFromPointSqr = Vector3D.DistanceSquared(Ai.Session.CameraPos, (MyUtils.GetClosestPointOnLine(ref glow.Line.From, ref glow.Line.To, ref Ai.Session.CameraPos)));
                int scale = 1;
                if (distanceFromPointSqr > 8000 * 8000) scale = 8;
                else if (distanceFromPointSqr > 4000 * 4000) scale = 7;
                else if (distanceFromPointSqr > 2000 * 2000) scale = 6;
                else if (distanceFromPointSqr > 1000 * 1000) scale = 5;
                else if (distanceFromPointSqr > 500 * 500) scale = 4;
                else if (distanceFromPointSqr > 250 * 250) scale = 3;
                else if (distanceFromPointSqr > 100 * 100) scale = 2;
                var sliderScale = ((float)LineScaler * scale);
                var reduction = (shrinkAmount * thisStep);
                glow.Thickness = (fullSize - reduction) * sliderScale;
            }
        }


        internal bool DrawAll()
        {
            DrawTracer();
            var tailActive = DrawTrail();
            Active = DrawHit.HitPos != Vector3D.Zero && tailActive; 
            return Active;
        }

        internal bool DrawTracer()
        {
            if (Tracer != TracerState.Off)
            {
                var width = Thickness;
                var color = Color;
                if (Tracer == TracerState.Shrink)
                {
                    if (System.LineColorVariance)
                    {
                        var cv = System.Values.Graphics.Line.ColorVariance;
                        var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                        color.X *= randomValue;
                        color.Y *= randomValue;
                        color.Z *= randomValue;
                    }

                    if (System.LineWidthVariance)
                    {
                        var wv = System.Values.Graphics.Line.WidthVariance;
                        var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                        width += randomValue;
                    }

                }
                MyTransparentGeometry.AddLineBillboard(System.TracerMaterial, color, Position, -Direction, (float)TracerLength, width);
            }

            return false;
        }

        internal bool DrawTrail()
        {
            if (Trail != TrailState.Off)
            {
                var removeGlowStep = false;
                var steps = System.Values.Graphics.Line.Trail.DecayTime;
                for (int i = 0; i < GlowSteps.Count; i++)
                {
                    var glow = GlowSteps[i];

                    MyTransparentGeometry.AddLineBillboard(System.TrailMaterial, System.Values.Graphics.Line.Trail.Color, glow.Line.From, glow.Line.Direction, (float)glow.Line.Length, glow.Thickness);
                    var thisStep = (Ai.Session.Tick - glow.FirstTick);
                    if (thisStep >= steps)
                        removeGlowStep = true;
                }

                if (removeGlowStep)
                {
                    AfterGlow glow;
                    if (GlowSteps.TryDequeue(out glow))
                    {
                        glow.Clean();
                        Ai.Session.GlowPool.Return(glow);
                    }
                }
            }

            return GlowSteps.Count > 0;
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
            TailPos = Vector3D.Zero;
            FirstTick = 0;
            WidthScaler = 0;
            Length = 0;
            Thickness = 0;
            LifeTime = 0;
        }
    }
}
