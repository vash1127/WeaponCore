using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
namespace WeaponCore.Support
{
    internal class AvShot
    {
        internal WeaponSystem System;
        internal GridAi Ai;
        internal MyEntity PrimeEntity;
        internal MyEntity TriggerEntity;
        internal readonly MySoundPair FireSound = new MySoundPair();
        internal readonly MySoundPair TravelSound = new MySoundPair();
        internal readonly MySoundPair HitSound = new MySoundPair();
        internal readonly MyEntity3DSoundEmitter FireEmitter = new MyEntity3DSoundEmitter(null, true, 1f);
        internal readonly MyEntity3DSoundEmitter TravelEmitter = new MyEntity3DSoundEmitter(null, true, 1f);
        internal readonly MyEntity3DSoundEmitter HitEmitter = new MyEntity3DSoundEmitter(null, true, 1f);

        internal MyQueue<AfterGlow> GlowSteps = new MyQueue<AfterGlow>(64);
        internal Queue<List<Vector3D>> ShrinkOffsets = new Queue<List<Vector3D>>(64);
        internal Queue<Shrinks> TracerShrinks = new Queue<Shrinks>(64);
        internal List<Vector3D> Offsets = new List<Vector3D>(64);

        //internal Stack<Shrinking> ShrinkSteps = new Stack<Shrinking>();
        internal WeaponComponent FiringWeapon;
        internal WeaponSystem.FiringSoundState FiringSoundState;

        internal bool Offset;
        internal bool Accelerates;
        internal bool AmmoSound;
        internal bool HasTravelSound;
        internal bool HitSoundActive;
        internal bool HitSoundActived;
        internal bool StartSoundActived;
        internal bool FakeExplosion;
        internal bool Triggered;
        internal bool Cloaked;
        internal bool Active;
        internal bool ShrinkInited;
        internal bool Growing;
        internal double MaxTracerLength;
        internal double TracerLength;
        internal double MaxGlowLength;
        internal double FirstStepSize;
        internal double StepSize;
        internal double TotalLength;
        internal double Thickness;
        internal double ScaleFov;
        internal double VisualLength;
        internal double MaxSpeed;
        internal double MaxStepSize;
        internal double TracerLengthSqr;
        internal float LineScaler;
        internal float GlowShrinkSize;
        internal float DistanceToLine;
        internal int LifeTime;
        internal int MuzzleId;
        internal int WeaponId;
        internal int TracerStep;
        internal int TracerSteps;
        internal int TailSteps;
        internal int LastGlowIdx;
        internal uint LastTick;
        internal uint InitTick;
        internal TracerState Tracer;
        internal TrailState Trail;
        internal ModelState Model;
        internal Screen OnScreen;
        internal MatrixD OffsetMatrix;
        internal Vector3D Origin;
        internal Vector3D Position;
        internal Vector3D Direction;
        internal Vector3D PointDir;
        internal Vector3D HitVelocity;
        internal Vector3D HitPosition;
        internal Vector3D ShooterVelocity;
        internal Vector3D TracerStart;
        internal Vector3D ShooterVelStep;
        internal Vector3D BackOfTracer;
        internal Vector4 Color;
        internal Vector3 ClosestPointOnLine;
        internal Hit Hit;
        internal MatrixD PrimeMatrix = MatrixD.Identity;
        internal MatrixD TriggerMatrix = MatrixD.Identity;

        internal enum TracerState
        {
            Full,
            Grow,
            Shrink,
            OverShoot,
            Off,
        }

        internal enum ModelState
        {
            None,
            Exists,
            Close,
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

        internal void Init(ProInfo info, double firstStepSize, double maxSpeed)
        {
            System = info.System;
            Ai = info.Ai;
            Model = (info.System.PrimeModelId != -1 || info.System.TriggerModelId != -1) ? Model = ModelState.Exists : Model = ModelState.None;
            PrimeEntity = info.PrimeEntity;
            TriggerEntity = info.TriggerEntity;
            InitTick = Ai.Session.Tick;
            Origin = info.Origin;
            Offset = System.OffsetEffect;
            MaxTracerLength = System.TracerLength;
            FirstStepSize = firstStepSize;
            Accelerates = System.Values.Ammo.Trajectory.AccelPerSec > 0;
            MuzzleId = info.MuzzleId;
            WeaponId = info.WeaponId;
            MaxSpeed = maxSpeed;
            MaxStepSize = MaxSpeed * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            ShooterVelocity = info.ShooterVel;
            ShooterVelStep = info.ShooterVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            info.Ai.WeaponBase.TryGetValue(info.Target.FiringCube, out FiringWeapon);
            ShrinkInited = false;

            if (System.DrawLine) Tracer = !System.IsBeamWeapon && firstStepSize < MaxTracerLength ? TracerState.Grow : TracerState.Full;
            else Tracer = TracerState.Off;

            if (System.Trail)
            {
                MaxGlowLength = MathHelperD.Clamp(System.Values.Graphics.Line.Trail.DecayTime * MaxStepSize, 0.1f, info.System.MaxTrajectory);
                Trail = System.Values.Graphics.Line.Trail.Back ? TrailState.Back : Trail = TrailState.Front;
                GlowShrinkSize = System.Values.Graphics.Line.Tracer.Width / System.Values.Graphics.Line.Trail.DecayTime;
            }
            else Trail = TrailState.Off;
            TotalLength = MathHelperD.Clamp(MaxTracerLength + MaxGlowLength, 0.1f, info.System.MaxTrajectory);
        }

        internal void Update(double stepSize, double visualLength, ref Vector3D position, ref Vector3D direction, ref Vector3D pointDir, bool growing = false)
        {
            LastTick = Ai.Session.Tick;
            Position = position;
            Direction = direction;
            StepSize = stepSize;
            VisualLength = visualLength;
            var flip = StepSize > VisualLength && !MyUtils.IsZero(StepSize - VisualLength);
            TracerStart = !flip ? Position + (-Direction * VisualLength) : Position;
            //TracerStart = Position + -(Direction * VisualLength);
            PointDir = pointDir;
            Growing = growing;
            LifeTime++;
        }

        internal void Complete(ProInfo info, bool saveHit = false, bool closeModel = false)
        {
            if (!Active) {
                Active = true;
                Ai.Session.Av.AvShots.Add(this);
            }

            if (Hit.HitPos != Vector3D.Zero) {
                if (saveHit) {
                    if (Hit.Entity != null)
                        HitVelocity = Hit.Entity.GetTopMostParent()?.Physics?.LinearVelocity ?? Vector3D.Zero;
                    else if (Hit.Projectile != null) 
                        HitVelocity = Hit.Projectile.Velocity;
                    HitPosition = Hit.HitPos;
                }
                if (System.IsBeamWeapon) Tracer = TracerState.Full;
                else if (Tracer != TracerState.Off && Math.Abs(TotalLength - (VisualLength + MaxTracerLength)) < 0.1f) {
                    //Log.Line($"Hit TotalOverShot: {TotalLength - (VisualLength + MaxTracerLength)} - {Math.Abs(VisualLength - TotalLength)} - {VisualLength - TotalLength} - VisLen:{VisualLength} - TotLen:{TotalLength} - Result:{VisualLength + MaxGlowLength}");
                    //Tracer = TracerState.Off;

                    VisualLength = MaxTracerLength;
                    TracerStart = HitPosition + (-Direction * MaxTracerLength);
                    Tracer = TracerState.OverShoot;
                    if (OnScreen != Screen.Tracer)
                    {
                        var bb1 = new BoundingBoxD(Vector3D.Min(TracerStart, Position), Vector3D.Max(TracerStart, Position));
                        if (Ai.Session.Camera.IsInFrustum(ref bb1)) OnScreen = Screen.Tracer;
                        else
                        {
                            var fakeTracerHead = Origin + (Direction * MaxTracerLength);
                            var bb2 = new BoundingBoxD(Vector3D.Min(Origin, fakeTracerHead), Vector3D.Max(Origin, fakeTracerHead));
                            if (Ai.Session.Camera.IsInFrustum(ref bb2)) OnScreen = Screen.Tracer;
                        }
                    }
                }
                else {
                    Tracer = TracerState.Shrink;
                    TracerLength = VisualLength;
                    TotalLength = MathHelperD.Clamp(VisualLength + MaxGlowLength, 0.1f, Vector3D.Distance(Origin, Position));
                }
            }
            else if (Tracer != TracerState.Off) {

                //Log.Line($"[NonHit] State:{Tracer} - visVsTotal:{Math.Abs(VisualLength - TotalLength)} - vis-Total:{VisualLength - TotalLength} - VisLen:{VisualLength} - TotLen:{TotalLength} - Result:{VisualLength + MaxGlowLength}");

                if (Tracer == TracerState.Grow && Growing) {

                    TracerLength = VisualLength;
                }
                else {
                    
                    Tracer = TracerState.Full;
                    TracerLength = MaxTracerLength;
                }
            }

            if (closeModel)
                Model = ModelState.Close;

            if (OnScreen == Screen.Tail) {
                var totalLen = Position + (-Direction * TotalLength);
                var bb = new BoundingBoxD(Vector3D.Min(totalLen, Position), Vector3D.Max(totalLen, Position));
                if (!Ai.Session.Camera.IsInFrustum(ref bb)) OnScreen = Screen.None;
            }

            var color = System.Values.Graphics.Line.Tracer.Color;
            if (System.LineColorVariance) {
                var cv = System.Values.Graphics.Line.ColorVariance;
                var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                color.X *= randomValue;
                color.Y *= randomValue;
                color.Z *= randomValue;
            }
            Color = color;
            var width = System.Values.Graphics.Line.Tracer.Width;
            if (System.LineWidthVariance) {
                var wv = System.Values.Graphics.Line.WidthVariance;
                var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                width += randomValue;
            }

            var target = System.IsBeamWeapon ? Position + -Direction * TracerLength : Position + (-Direction * TotalLength);
            ClosestPointOnLine = MyUtils.GetClosestPointOnLine(ref Position, ref target, ref Ai.Session.CameraPos);
            DistanceToLine = (float)Vector3D.Distance(ClosestPointOnLine, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            if (System.IsBeamWeapon && DistanceToLine < 1000) DistanceToLine = 1000;
            else if (System.IsBeamWeapon && DistanceToLine < 350) DistanceToLine = 350;
            ScaleFov = Math.Tan(MyAPIGateway.Session.Camera.FovWithZoom * 0.5);
            Thickness = Math.Max(width, 0.10f * ScaleFov * (DistanceToLine / 100));
            LineScaler = ((float)Thickness / width);

           // var test = TracerStart = StepSize <= VisualLength && VisualLength > 0 ? Position + -(Direction * VisualLength) : Position;
            //Log.Line($"Tracer:{Tracer} - notOverSize:{StepSize <= VisualLength && VisualLength > 0} - Trial:{Trail} - OnSreen:{OnScreen} - Hit:{Hit.HitPos != Vector3D.Zero} - VisualLen:{VisualLength} - TracerLen:{TracerLength}({MaxTracerLength}) - TotalLen:{TotalLength}({MaxTracerLength}-{MaxGlowLength}) - dToLine:{DistanceToLine}({target})");

            if (Tracer != TracerState.Off && Hit.HitPos != Vector3D.Zero) {
                
                if (System.IsBeamWeapon)
                    RunBeam();
                //else if (Tracer == TracerState.Shrink && OnScreen != Screen.None) 
                    //Shrink();
            }
            else
            {
                if (Tracer != TracerState.Off && OnScreen == Screen.Tracer && System.OffsetEffect)
                    LineOffsetEffect(TracerStart, -PointDir, TracerLength);

                if (Trail != TrailState.Off && Tracer != TracerState.Shrink)
                    RunGlow();
            }
        }

        internal void RunGlow()
        {
            var glowCount = GlowSteps.Count;
            if (glowCount <= System.Values.Graphics.Line.Trail.DecayTime)
            {
                var glow = Ai.Session.Av.Glows.Count > 0 ? Ai.Session.Av.Glows.Pop() : new AfterGlow();
                glow.Step = 0;
                glow.VelStep = Direction * StepSize;
                glow.TailPos = Position + -glow.VelStep;
                GlowSteps.Enqueue(glow);
                ++glowCount;
            }

            var endIdx = glowCount - 1;
            for (int i = endIdx; i >= 0; i--)
            {
                var glow = GlowSteps[i];

                if (i != 0) glow.Parent = GlowSteps[i - 1];
                //var glowPos = glow.TailPos + ShooterVelocity * glow.Step;
                if (i == endIdx)
                    glow.Line = i != 0 ? new LineD(glow.Parent.TailPos, glow.TailPos) : new LineD(glow.TailPos - glow.VelStep, glow.TailPos);
            }
        }

        internal void Shrink()
        {
            Log.Line("shrink");
            if (!ShrinkInited) ShrinkInit();
            for (int i = 0; i < TracerSteps; i++)
            {
                var shrunk = GetLine();

                if (shrunk.HasValue)
                {
                    var color = System.Values.Graphics.Line.Tracer.Color;
                    if (System.LineColorVariance)
                    {
                        var cv = System.Values.Graphics.Line.ColorVariance;
                        var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                        color.X *= randomValue;
                        color.Y *= randomValue;
                        color.Z *= randomValue;
                    }

                    var width = Thickness;
                    if (System.LineWidthVariance)
                    {
                        var wv = System.Values.Graphics.Line.WidthVariance;
                        var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                        width += randomValue;
                    }

                    if (System.OffsetEffect)
                        LineOffsetEffect(Hit.HitPos, Direction, shrunk.Value.Reduced, true);
                    else
                        TracerShrinks.Enqueue(new Shrinks {Start = Hit.HitPos, Color = color, Length = (float)(shrunk.Value.Reduced + shrunk.Value.StepLength), Thickness = (float) width});
                    
                    /*
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
                    */
                }
            }
            if (TracerSteps == 0) RunGlow();
            Log.Line($"TracerSteps: {TracerSteps} - Shrinks:{TracerShrinks.Count}");

        }

        private void ShrinkInit()
        {
            var fractualSteps = VisualLength / StepSize;
            TracerSteps = (int)Math.Floor(fractualSteps);
            TracerStep = TracerSteps;
            Log.Line($"steps: {TracerSteps} -  fractualSteps:{fractualSteps}");
            var frontOfTracer = (TracerStart + (Direction * TracerLength));
            //var tracerLength = Info.System.TracerLength;
            //BackOfTracer = frontOfTracer + (-Direction * (tracerLength + ResizeLen));
            BackOfTracer = frontOfTracer + (-Direction * StepSize);
            if (fractualSteps < StepSize || TracerSteps <= 0)
            {
                Tracer = TracerState.Off;
            }
        }

        internal Shrunk? GetLine()
        {
            if (TracerStep-- > 0)
            {
                BackOfTracer += ShooterVelStep;
                Hit.HitPos += ShooterVelStep;
                var backOfTail = BackOfTracer + (Direction * (TailSteps++ * StepSize));
                var newTracerBack = Hit.HitPos + -(Direction * TracerStep * StepSize);
                var reduced = TracerStep * StepSize;
                //if (TracerSteps < 0) StepSize = Vector3D.Distance(backOfTail, Hit.HitPos);

                return new Shrunk(ref newTracerBack, ref backOfTail, reduced, StepSize);
            }
            return null;
        }

        internal void RunBeam()
        {
            if (System.HitParticle && !(MuzzleId != 0 && (System.ConvergeBeams || System.OneHitParticle)))
            {

                if (FiringWeapon != null)
                {
                    var weapon = FiringWeapon.Platform.Weapons[WeaponId];
                    var effect = weapon.HitEffects[MuzzleId];
                    if (OnScreen != Screen.None)
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

        internal void LineOffsetEffect(Vector3D pos, Vector3D direction, double tracerLength, bool addToShrinks = false)
        {
            var up = MatrixD.Identity.Up;
            var startPos = pos + -(direction * tracerLength);
            MatrixD.CreateWorld(ref startPos, ref direction, ref up, out OffsetMatrix);
            TracerLengthSqr = tracerLength * tracerLength;
            var maxOffset = System.Values.Graphics.Line.OffsetEffect.MaxOffset;
            var minLength = System.Values.Graphics.Line.OffsetEffect.MinLength;
            var maxLength = MathHelperD.Clamp(System.Values.Graphics.Line.OffsetEffect.MaxLength, 0, TracerLength);

            double currentForwardDistance = 0;

            var first = true;
            List<Vector3D> shrinkList = null;
            while (currentForwardDistance < tracerLength)
            {
                currentForwardDistance += MyUtils.GetRandomDouble(minLength, maxLength);
                var lateralXDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                var lateralYDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                if (!addToShrinks) Offsets.Add(new Vector3D(lateralXDistance, lateralYDistance, currentForwardDistance * -1));
                else
                {
                    if (first)
                    {
                        shrinkList = Ai.Session.ListOfVectorsPool.Get();
                        first = false;
                    }
                    shrinkList.Add(new Vector3D(lateralXDistance, lateralYDistance, currentForwardDistance * -1));
                }
            }
            if (addToShrinks)
                ShrinkOffsets.Enqueue(shrinkList);
        }


        internal void SetupSounds(double distanceFromCameraSqr)
        {
            FiringSoundState = System.FiringSound;

            if (!System.IsBeamWeapon && System.AmmoTravelSound)
            {
                HasTravelSound = true;
                TravelSound.Init(System.Values.Audio.Ammo.TravelSound, false);
            }
            else HasTravelSound = false;

            if (System.HitSound)
            {
                var hitSoundChance = System.Values.Audio.Ammo.HitPlayChance;
                HitSoundActive = (hitSoundChance >= 1 || hitSoundChance >= MyUtils.GetRandomDouble(0.0f, 1f));
                if (HitSoundActive)
                    HitSound.Init(System.Values.Audio.Ammo.HitSound, false);
            }

            if (FiringSoundState == WeaponSystem.FiringSoundState.PerShot && distanceFromCameraSqr < System.FiringSoundDistSqr)
            {
                StartSoundActived = true;
                FireSound.Init(System.Values.Audio.HardPoint.FiringSound, false);
                FireEmitter.SetPosition(Origin);
                FireEmitter.Entity = FiringWeapon.MyCube;
            }
        }

        internal void AmmoSoundStart()
        {
            TravelEmitter.SetPosition(Position);
            TravelEmitter.Entity = PrimeEntity;
            TravelEmitter.PlaySound(TravelSound, true);
            AmmoSound = true;
        }

        internal void Close()
        {
            // Reset to zero
            PrimeMatrix = MatrixD.Identity;
            TriggerMatrix = MatrixD.Identity;
            Hit = new Hit();
            HitVelocity = Vector3D.Zero;
            HitPosition = Vector3D.Zero;
            Position = Vector3D.Zero;
            Direction = Vector3D.Zero;
            TracerStart = Vector3D.Zero;
            BackOfTracer = Vector3D.Zero;
            OnScreen = Screen.None;
            Tracer = TracerState.Off;
            Trail = TrailState.Off;
            Model = ModelState.None;
            LastTick = 0;
            LifeTime = 0;
            TracerSteps = 0;
            TracerStep = 0;
            LastGlowIdx = 0;
            FiringWeapon = null;
            PrimeEntity = null;
            TriggerEntity = null;
            AmmoSound = false;
            HitSoundActive = false;
            HitSoundActived = false;
            StartSoundActived = false;
            HasTravelSound = false;
            FakeExplosion = false;
            Triggered = false;
            Cloaked = false;
            Active = false;
            Growing = false;
            ShrinkInited = false;
            GlowSteps.Clear();
            Offsets.Clear();
            //
            Ai = null;
            System = null;
        }
    }

    internal class AfterGlow
    {
        internal AfterGlow Parent;
        internal Vector3D TailPos;
        internal Vector3D VelStep;
        internal LineD Line;
        internal int Step;
    }

    internal struct Shrinks
    {
        internal Vector3D Start;
        internal Vector4 Color;
        internal float Length;
        internal float Thickness;
    }

    internal struct Shrunk
    {
        internal readonly Vector3D PrevPosition;
        internal readonly Vector3D BackOfTail;
        internal readonly double Reduced;
        internal readonly double StepLength;

        internal Shrunk(ref Vector3D prevPosition, ref Vector3D backOfTail, double reduced, double stepLength)
        {
            PrevPosition = prevPosition;
            BackOfTail = backOfTail;
            Reduced = reduced;
            StepLength = stepLength;
        }
    }
}
