using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;

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
        internal Queue<Shrinks> TracerShrinks = new Queue<Shrinks>(64);
        internal List<Vector3D> Offsets = new List<Vector3D>(64);

        internal WeaponComponent FiringWeapon;
        internal WeaponSystem.FiringSoundState FiringSoundState;

        internal bool Offset;
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
        internal bool TrailActivated;
        internal bool Hitting;
        internal double MaxTracerLength;
        internal double MaxGlowLength;
        internal double StepSize;
        internal double ShortStepSize;
        internal double TotalLength;
        internal double Thickness;
        internal double ScaleFov;
        internal double VisualLength;
        internal double MaxSpeed;
        internal double MaxStepSize;
        internal double TracerLengthSqr;
        internal double EstimatedTravel;
        internal float LineScaler;
        internal float GlowShrinkSize;
        internal float DistanceToLine;
        internal int LifeTime;
        internal int MuzzleId;
        internal int WeaponId;
        internal int TracerStep;
        internal int TracerSteps;
        internal int TrailSteps;
        internal uint LastTick;
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
        internal Vector3D BackOfTracer;
        internal Vector3D ClosestPointOnLine;
        internal Vector4 Color;

        internal Hit Hit;
        internal MatrixD PrimeMatrix = MatrixD.Identity;
        internal MatrixD TriggerMatrix = MatrixD.Identity;
        internal Shrinks EmptyShrink;

        internal enum TracerState
        {
            Full,
            Grow,
            Shrink,
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

        internal enum Screen // Tracer includes Tail;
        {
            Tracer,
            Trail,
            None,
        }

        internal void Init(ProInfo info, double firstStepSize, double maxSpeed)
        {
            System = info.System;
            Ai = info.Ai;
            Model = (info.System.PrimeModelId != -1 || info.System.TriggerModelId != -1) ? Model = ModelState.Exists : Model = ModelState.None;
            PrimeEntity = info.PrimeEntity;
            TriggerEntity = info.TriggerEntity;
            Origin = info.Origin;
            Offset = System.OffsetEffect;
            MaxTracerLength = System.TracerLength;
            MuzzleId = info.MuzzleId;
            WeaponId = info.WeaponId;
            MaxSpeed = maxSpeed;
            MaxStepSize = MaxSpeed * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
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

        internal void Update(double stepSize, double visualLength, ref Vector3D position, ref Vector3D direction, ref Vector3D pointDir, double? shortStepSize = null)
        {
            LastTick = Ai.Session.Tick;
            ++LifeTime;

            StepSize = stepSize;
            ShortStepSize = shortStepSize ?? stepSize;

            Position = position;
            Direction = direction;

            VisualLength = visualLength;
            BackOfTracer = Position + (-Direction * VisualLength);
            PointDir = pointDir;
            EstimatedTravel = LifeTime > 1 ? (StepSize * (LifeTime - 1)) + ShortStepSize : ShortStepSize;

            if (Tracer == TracerState.Grow && MyUtils.IsZero(MaxTracerLength - VisualLength))
                Tracer = TracerState.Full;

            if (OnScreen == Screen.None && (System.DrawLine || Model == ModelState.None && System.AmmoParticle))
            {
                var distBack = MathHelperD.Clamp(EstimatedTravel, ShortStepSize, MaxGlowLength);
                var rayTracer = new RayD(BackOfTracer, PointDir);
                var rayTrail = new RayD(Position + (-Direction * distBack), Direction);

                //DsDebugDraw.DrawRay(rayTracer, VRageMath.Color.White, 0.5f, (float) VisualLength);
                //DsDebugDraw.DrawRay(rayTrail, VRageMath.Color.Yellow, 0.5f, (float)EstimatedTravel);
                double? dist;
                Ai.Session.CameraFrustrum.Intersects(ref rayTracer, out dist);
                if (dist != null && dist <= VisualLength)
                    OnScreen = Screen.Tracer;
                else if (OnScreen == Screen.None && System.Trail)
                {
                    Ai.Session.CameraFrustrum.Intersects(ref rayTrail, out dist);
                    if (dist != null && dist <= System.MaxTrajectory - EstimatedTravel)
                        OnScreen = Screen.Trail;
                }
                if (OnScreen != Screen.None && System.Trail) TrailActivated = true;
            }
            else if (TrailActivated) OnScreen = Screen.Trail;
        }

        internal void Complete(Projectile p, bool saveHit = false, bool closeModel = false)
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

                    Hitting = true;
                }

                if (System.IsBeamWeapon) Tracer = TracerState.Full;
                else if (Tracer != TracerState.Off && VisualLength <= 0) {
                    Tracer = TracerState.Off;
                    
                }
                else if (VisualLength / StepSize > 1)
                {
                    if (!ShrinkInited) Tracer = TracerState.Shrink;
                    TotalLength = MathHelperD.Clamp(VisualLength + MaxGlowLength, 0.1f, Vector3D.Distance(Origin, Position));
                }
                else Tracer = TracerState.Full;
            }
            else if (Tracer != TracerState.Grow) Tracer = TracerState.Full;

            if (closeModel)
                Model = ModelState.Close;

            if (System.DrawLine)
                LineVariableEffects();

            if (Tracer != TracerState.Off && OnScreen != Screen.None)
            {
                if (Tracer == TracerState.Shrink && !ShrinkInited)
                    Shrink();
                else if (System.IsBeamWeapon)
                    RunBeam();

                if (System.OffsetEffect)
                    LineOffsetEffect(Position, PointDir, VisualLength);
            }
            var backAndGrowing = Trail == TrailState.Back && Tracer == TracerState.Grow;
            if (OnScreen != Screen.None && Trail != TrailState.Off && !backAndGrowing && Tracer != TracerState.Shrink)
                RunGlow(ref EmptyShrink); 
        }

        internal void LineVariableEffects()
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
            Color = color;
            var width = System.Values.Graphics.Line.Tracer.Width;
            if (System.LineWidthVariance)
            {
                var wv = System.Values.Graphics.Line.WidthVariance;
                var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                width += randomValue;
            }

            var target = Position + (-Direction * TotalLength);
            ClosestPointOnLine = MyUtils.GetClosestPointOnLine(ref Position, ref target, ref Ai.Session.CameraPos);
            DistanceToLine = (float)Vector3D.Distance(ClosestPointOnLine, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            
            double scale = 0.1f;
            ScaleFov = Math.Tan(MyAPIGateway.Session.Camera.FovWithZoom * 0.5);
            Thickness = Math.Max(width, scale * ScaleFov * (DistanceToLine / 100));
            LineScaler = ((float)Thickness / width);
        }

        internal void RunGlow(ref Shrinks shrink)
        {
            var glowCount = GlowSteps.Count;
            var parentPos = Trail == TrailState.Back ? Position : BackOfTracer;
            if (glowCount <= System.Values.Graphics.Line.Trail.DecayTime)
            {
                var glow = Ai.Session.Av.Glows.Count > 0 ? Ai.Session.Av.Glows.Pop() : new AfterGlow();

                Vector3D backPos;
                if (shrink.Thickness > 0 && !shrink.Last) backPos = shrink.Back;
                else if (shrink.Thickness > 0) backPos = Hit.HitPos;
                else backPos = BackOfTracer;

                var startPos = Trail == TrailState.Back ? backPos : Position;

                var trailTravel = EstimatedTravel - VisualLength;
                var expanding = trailTravel < ShortStepSize;
                var back = Trail == TrailState.Back;
                var backExpanding = back && expanding;
                var earlyEnd = LifeTime <= 1;

                if (Hitting && !earlyEnd || !expanding && back && VisualLength > 0)
                    glow.VelStep = Vector3D.Zero;
                else if (backExpanding)
                {
                    glow.VelStep = Direction * (EstimatedTravel - VisualLength);
                    parentPos = BackOfTracer;
                }
                else
                    glow.VelStep = Direction * ShortStepSize;

                glow.TailPos = startPos + -glow.VelStep;

                GlowSteps.Enqueue(glow);
                ++glowCount;
                glow.Step = 0;
            }
            var endIdx = glowCount - 1;
            for (int i = endIdx; i >= 0; i--)
            {
                var glow = GlowSteps[i];

                if (i != 0) glow.Parent = GlowSteps[i - 1];
                if (i == endIdx)
                    glow.Line = i != 0 ? new LineD(glow.Parent.TailPos, glow.TailPos) : new LineD(parentPos, glow.TailPos);
            }
        }

        internal void Shrink()
        {
            ShrinkInit();
            for (int i = 0; i < TracerSteps; i++)
            {
                var last = (i == TracerSteps - 1);
                var shrunk = GetLine();
                if (shrunk.HasValue)
                {
                    if (shrunk.Value.Reduced < 0.1) continue;

                    var color = System.Values.Graphics.Line.Tracer.Color;
                    if (System.LineColorVariance)
                    {
                        var cv = System.Values.Graphics.Line.ColorVariance;
                        var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                        color.X *= randomValue;
                        color.Y *= randomValue;
                        color.Z *= randomValue;
                    }

                    var width = System.Values.Graphics.Line.Tracer.Width;
                    if (System.LineWidthVariance)
                    {
                        var wv = System.Values.Graphics.Line.WidthVariance;
                        var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                        width += randomValue;
                    }

                    width = (float)Math.Max(width, 0.10f * ScaleFov * (DistanceToLine / 100));

                    if (System.OffsetEffect)
                    {
                        var offsets = LineOffsetEffect(shrunk.Value.NewTracerBack, -PointDir, shrunk.Value.Reduced, true);
                        TracerShrinks.Enqueue(new Shrinks { Back = shrunk.Value.NewTracerBack, Color = color, Length = shrunk.Value.Reduced, Thickness = width, Offsets = offsets, OffsetMatrix = OffsetMatrix, LengthSqr = TracerLengthSqr, Last = last });
                    }
                    else
                        TracerShrinks.Enqueue(new Shrinks { Back = shrunk.Value.NewTracerBack, Color = color, Length = shrunk.Value.Reduced, Thickness = width, Last = last });
                }
            }
        }

        private void ShrinkInit()
        {
            ShrinkInited = true;

            var fractualSteps = VisualLength / StepSize;
            TracerSteps = (int)Math.Floor(fractualSteps);
            TracerStep = TracerSteps;
            if (fractualSteps < StepSize || TracerSteps <= 0)
                Tracer = TracerState.Off;
        }

        internal Shrunk? GetLine()
        {
            if (TracerStep > 0)
            {
                Hit.HitPos += HitVelocity;
                var newTracerBack = Hit.HitPos + -(Direction * (TracerStep * StepSize));
                var reduced = TracerStep-- * StepSize;
                return new Shrunk(ref newTracerBack, (float) reduced);
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
                        MatrixD.CreateTranslation(ref Hit.HitPos, out matrix);
                        if (effect == null)
                        {
                            MyParticlesManager.TryCreateParticleEffect(System.Values.Graphics.Particles.Hit.Name, ref matrix, ref Hit.HitPos, uint.MaxValue, out effect);
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

        internal List<Vector3D> LineOffsetEffect(Vector3D tracerStart, Vector3D direction, double tracerLength, bool addToShrinks = false)
        {
            var up = MatrixD.Identity.Up;
            var startPos = tracerStart + (-direction * tracerLength);
            MatrixD.CreateWorld(ref startPos, ref direction, ref up, out OffsetMatrix);
            TracerLengthSqr = tracerLength * tracerLength;
            var maxOffset = System.Values.Graphics.Line.OffsetEffect.MaxOffset;
            var minLength = System.Values.Graphics.Line.OffsetEffect.MinLength;
            var maxLength = MathHelperD.Clamp(System.Values.Graphics.Line.OffsetEffect.MaxLength, 0, tracerLength);

            double currentForwardDistance = 0;
            var first = true;
            List<Vector3D> shrinkList = null;
            while (currentForwardDistance <= tracerLength)
            {
                currentForwardDistance += MyUtils.GetRandomDouble(minLength, maxLength);
                var lateralXDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                var lateralYDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                if (!addToShrinks) Offsets.Add(new Vector3D(lateralXDistance, lateralYDistance, currentForwardDistance * -1));
                else
                {
                    if (first)
                    {
                        first = false;
                        shrinkList = Ai.Session.ListOfVectorsPool.Get();
                    }
                    shrinkList.Add(new Vector3D(lateralXDistance, lateralYDistance, currentForwardDistance * -1));
                }
            }

            if (addToShrinks && shrinkList != null)
                return shrinkList;

            return null;
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
            // Reset only vars that are not always set
            Hit = new Hit();
            HitVelocity = Vector3D.Zero;
            BackOfTracer = Vector3D.Zero;
            OnScreen = Screen.None;
            Tracer = TracerState.Off;
            Trail = TrailState.Off;
            LifeTime = 0;
            TracerSteps = 0;
            TracerStep = 0;
            TrailSteps = 0;
            AmmoSound = false;
            HitSoundActive = false;
            HitSoundActived = false;
            StartSoundActived = false;
            HasTravelSound = false;
            FakeExplosion = false;
            Triggered = false;
            Cloaked = false;
            Active = false;
            TrailActivated = false;
            ShrinkInited = false;
            Hitting = false;
            GlowSteps.Clear();
            Offsets.Clear();
            //
            FiringWeapon = null;
            PrimeEntity = null;
            TriggerEntity = null;
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
        internal List<Vector3D> Offsets;
        internal Vector3D Back;
        internal Vector4 Color;
        internal MatrixD OffsetMatrix;
        internal float Length;
        internal float Thickness;
        internal double LengthSqr;
        internal bool Last;

    }

    internal struct Shrunk
    {
        internal readonly Vector3D NewTracerBack;
        internal readonly float Reduced;

        internal Shrunk(ref Vector3D newTracerBack, float reduced)
        {
            NewTracerBack = newTracerBack;
            Reduced = reduced;
        }
    }
}
