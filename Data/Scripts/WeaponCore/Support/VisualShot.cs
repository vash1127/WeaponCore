using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Support
{
    internal class VisualShot
    {
        internal WeaponSystem System;
        internal GridAi Ai;
        internal bool Offset;
        internal bool Accelerates;
        internal bool Hit;
        internal TracerState Tracer;
        internal TrailState Trail;
        internal DrawState Draw;
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
        internal int MaxGlowSteps;
        internal int LifeTime;
        internal Vector3D Position;
        internal Vector3D Direction;
        internal Vector3D HitVelocity;
        internal Vector3D HitPosition;
        internal Vector3D ShooterVelocity;
        internal Vector3D TracerStart;
        internal Vector4 Color;
        internal Vector3 ClosestPointOnLine;
        internal DrawHit? DrawHit;
        internal MyQueue<AfterGlow> GlowSteps = new MyQueue<AfterGlow>();
        internal Stack<Shrinking> ShrinkSteps = new Stack<Shrinking>();
        internal int LastGlowIdx = 0;

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
        internal enum DrawState
        {
            Last,
            Hit,
            Default
        }

        internal enum Screen
        {
            Tracer,
            Tail,
            None,
        }

        internal void Init(WeaponSystem system, GridAi ai, double firstStepSize, double maxSpeed)
        {
            System = system;
            Ai = ai;
            Offset = system.OffsetEffect;
            MaxTracerLength = system.TracerLength;
            FirstStepSize = firstStepSize;
            Accelerates = system.Values.Ammo.Trajectory.AccelPerSec > 0;
            if (system.DrawLine) Tracer = firstStepSize < MaxTracerLength ? TracerState.Grow : TracerState.Full;
            else Tracer = TracerState.Off;

            if (system.Trail)
            {
                MaxGlowLength = system.Values.Graphics.Line.Trail.DecayTime * (maxSpeed / MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);
                Trail = system.Values.Graphics.Line.Trail.Back ? TrailState.Back : Trail = TrailState.Front;
                MaxGlowSteps = (int)Math.Ceiling(MaxGlowLength);
            }
            else Trail = TrailState.Off;
            OnScreen = Screen.None;
        }

        internal void Update(double stepSize, double visualLength, Vector3D shooterVelocity, Vector3D position, Vector3D direction)
        {
            Position = position;
            Direction = direction;
            TracerStart = position + (-direction * visualLength);
            StepSize = stepSize;
            ShooterVelocity = shooterVelocity;
            VisualLength = visualLength;

            LifeTime++;
        }

        internal void Complete(ProInfo info, DrawHit? drawHit = null, DrawState drawState = DrawState.Default, bool saveHit = false)
        {
            if (!saveHit)
            {
                Draw = drawState;
                DrawHit = drawHit;
            }

            var hit = info.BaseDamagePool <= 0;
            if (hit || DrawHit.HasValue) Log.Line($"{Ai.Session.Tick} - hit:{hit} - DrawHit:{DrawHit.HasValue}");
            if (DrawHit.HasValue)
            {
                Log.Line("hit");
                Hit = hit;
                if (!hit) HitVelocity = DrawHit.Value.Entity?.GetTopMostParent()?.Physics?.LinearVelocity ?? Vector3D.Zero;
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
                    HitPosition = DrawHit.Value.HitPos ?? Vector3D.Zero;
                }
            }
            else if (Tracer != TracerState.Off && VisualLength >= MaxTracerLength)
            {
                Tracer = TracerState.Full;
                TracerLength = MaxTracerLength;
            }

            if (Tracer != TracerState.Off)
                RunTracer();

            if (Trail != TrailState.Off)
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

        }

        internal void RunGlow()
        {
            var glowCount = GlowSteps.Count;
            if (glowCount < MaxGlowSteps)
            {
                var glow = Ai.Session.GlowPool.Get();
                LastGlowIdx = glowCount - 1;

                glow.Parent = glowCount > 0 ? GlowSteps[LastGlowIdx] : null;
                glow.Back = glow.Parent == null ? Position + (-Direction * TracerLength) : glow.Parent.Back * (-Direction * StepSize);
                glow.FirstTick = Ai.Session.Tick;
                glow.ShooterVel = ShooterVelocity;
                glow.System = System;
                GlowSteps.Enqueue(glow);
                ++glowCount;
            }

            var removeGlowStep = false;
            for (int i = 0; i < glowCount; i++)
            {
                var glow = GlowSteps[i];
                var thisStep = (Ai.Session.Tick - glow.FirstTick);
                if (thisStep != 0) glow.Back += (glow.ShooterVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);
                if (glow.Parent == null) continue;
                var steps = glow.System.Values.Graphics.Line.Trail.DecayTime;
                var fullSize = glow.System.Values.Graphics.Line.Tracer.Width;
                var shrinkAmount = fullSize / steps;
                glow.Line = new LineD(glow.Back, glow.Parent.Back);

                var distanceFromPointSqr = Vector3D.DistanceSquared(Ai.Session.CameraPos, (MyUtils.GetClosestPointOnLine(ref glow.Line.From, ref glow.Line.To, ref Ai.Session.CameraPos)));
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
                glow.Thickness = (fullSize - reduction) * sliderScale;

                if (thisStep >= steps) removeGlowStep = true;
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

        internal void DrawTracer()
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
        }

        internal void DrawTrail()
        {
            if (Trail != TrailState.Off)
            {
                for (int i = 0; i < GlowSteps.Count; i++)
                {
                    var glow = GlowSteps[i];
                    MyTransparentGeometry.AddLineBillboard(System.TrailMaterial, Color, glow.Back, glow.Line.Direction, glow.Length, glow.Thickness);
                }
            }
        }
    }
}
