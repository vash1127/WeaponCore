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
        internal bool Back;
        internal bool DetonateFakeExp;
        internal bool LastStep;
        internal double MaxTracerLength;
        internal double MaxGlowLength;
        internal double StepSize;
        internal double ShortStepSize;
        internal double TotalLength;
        internal double TracerWidth;
        internal double TrailWidth;
        internal double ScaleFov;
        internal double VisualLength;
        internal double MaxSpeed;
        internal double MaxStepSize;
        internal double TracerLengthSqr;
        internal double EstTravel;
        internal double ShortEstTravel;
        internal float TrailScaler;
        internal float GlowShrinkSize;
        internal float DistanceToLine;
        internal int LifeTime;
        internal int MuzzleId;
        internal int WeaponId;
        internal int TracerStep;
        internal int TracerSteps;
        internal uint LastTick;
        internal TracerState Tracer;
        internal TrailState Trail;
        internal ModelState Model;
        internal Screen OnScreen;
        internal MatrixD OffsetMatrix;
        internal Vector3D Origin;
        internal Vector3D Direction;
        internal Vector3D PointDir;
        internal Vector3D HitVelocity;
        internal Vector3D ShootVelStep;
        internal Vector3D TracerFront;
        internal Vector3D TracerBack;
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
            ShootVelStep = info.ShooterVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            info.Ai.WeaponBase.TryGetValue(info.Target.FiringCube, out FiringWeapon);
            ShrinkInited = false;

            if (System.DrawLine) Tracer = !System.IsBeamWeapon && firstStepSize < MaxTracerLength && !MyUtils.IsZero(firstStepSize - MaxTracerLength, 1E-01F) ? TracerState.Grow : TracerState.Full;
            else Tracer = TracerState.Off;

            if (System.Trail)
            {
                MaxGlowLength = MathHelperD.Clamp(System.Values.Graphics.Line.Trail.DecayTime * MaxStepSize, 0.1f, System.MaxTrajectory);
                Trail = System.Values.Graphics.Line.Trail.Back ? TrailState.Back : Trail = TrailState.Front;
                GlowShrinkSize = !System.Values.Graphics.Line.Trail.UseColorFade ? System.TrailWidth / System.Values.Graphics.Line.Trail.DecayTime : 1f / System.Values.Graphics.Line.Trail.DecayTime;
                Back = Trail == TrailState.Back;
            }
            else Trail = TrailState.Off;
            TotalLength = MathHelperD.Clamp(MaxTracerLength + MaxGlowLength, 0.1f, info.System.MaxTrajectory);

        }

        internal void Update(double stepSize, double visualLength, ref Vector3D tracerFront, ref Vector3D direction, ref Vector3D pointDir, double? shortStepSize = null)
        {
            ++LifeTime;
            LastTick = Ai.Session.Tick;
            StepSize = stepSize;
            EstTravel = StepSize * LifeTime;
            ShortStepSize = shortStepSize ?? stepSize;
            ShortEstTravel = MathHelperD.Clamp((EstTravel - StepSize) + ShortStepSize, 0, double.MaxValue);
            
            Direction = direction;
            VisualLength = visualLength;
            TracerFront = tracerFront;
            TracerBack = TracerFront + (-Direction * VisualLength);
            PointDir = pointDir;
            if (Tracer == TracerState.Grow && LastStep || MyUtils.IsZero(MaxTracerLength - VisualLength, 1E-01F))
                Tracer = TracerState.Full;

            if (OnScreen == Screen.None && (System.DrawLine || Model == ModelState.None && System.AmmoParticle))
            {
                var rayTracer = new RayD(TracerBack, PointDir);
                var rayTrail = new RayD(TracerFront + (-Direction * ShortEstTravel), Direction);

                //DsDebugDraw.DrawRay(rayTracer, VRageMath.Color.White, 0.25f, (float) VisualLength);
                //DsDebugDraw.DrawRay(rayTrail, VRageMath.Color.Orange, 0.25f, (float)ShortEstTravel);
                
                double? dist;
                Ai.Session.CameraFrustrum.Intersects(ref rayTracer, out dist);

                if (dist != null && dist <= VisualLength)
                    OnScreen = Screen.Tracer;
                else if (System.Trail)
                {
                    Ai.Session.CameraFrustrum.Intersects(ref rayTrail, out dist);
                    if (dist != null && dist <= ShortEstTravel + ShortStepSize)
                        OnScreen = Screen.Trail;
                }

                if (OnScreen != Screen.None && !TrailActivated && System.Trail) TrailActivated = true;
            }
            
            if (OnScreen == Screen.None && TrailActivated) OnScreen = Screen.Trail;
        }

        internal void Complete(Projectile p, bool saveHit = false, bool closeModel = false, bool detonateFakeExp = false)
        {
            if (!Active) {

                Active = true;
                Ai.Session.Av.AvShots.Add(this);
            }

            if (closeModel)
                Model = ModelState.Close;

            DetonateFakeExp = detonateFakeExp;
            if (Model != ModelState.Close && !DetonateFakeExp)
            {
                if (Hit.HitPos != Vector3D.Zero)
                {
                    if (saveHit)
                    {
                        if (Hit.Entity != null)
                            HitVelocity = Hit.Entity.GetTopMostParent()?.Physics?.LinearVelocity ?? Vector3D.Zero;
                        else if (Hit.Projectile != null)
                            HitVelocity = Hit.Projectile.Velocity;

                        Hitting = !ShrinkInited;
                    }
                }

                if (System.IsBeamWeapon)
                    Tracer = TracerState.Full;
                else if (Tracer != TracerState.Off && VisualLength <= 0)
                    Tracer = TracerState.Off;
                else if (VisualLength / StepSize > 1 && !MyUtils.IsZero(EstTravel - ShortEstTravel, 1E-01F) && Hitting)
                {
                    if (!ShrinkInited) Tracer = TracerState.Shrink;
                    TotalLength = MathHelperD.Clamp(VisualLength + MaxGlowLength, 0.1f, Vector3D.Distance(Origin, TracerFront));
                }

                LastStep = Hitting || MyUtils.IsZero(System.MaxTrajectory - ShortEstTravel, 1E-01F);

                if (System.DrawLine)
                    LineVariableEffects();

                if (Tracer != TracerState.Off && OnScreen != Screen.None)
                {
                    if (Tracer == TracerState.Shrink && !ShrinkInited)
                        Shrink();
                    else if (System.IsBeamWeapon)
                        RunBeam();

                    if (System.OffsetEffect)
                        PrepOffsetEffect(TracerFront, PointDir, VisualLength);
                }

                var backAndGrowing = Back && Tracer == TracerState.Grow;
                if (OnScreen != Screen.None && Trail != TrailState.Off && !backAndGrowing)
                    RunGlow(ref EmptyShrink);
            }

            Hitting = false;
        }

        internal void RunGlow(ref Shrinks shrink, bool shrinking = false)
        {
            var glowCount = GlowSteps.Count;
            var firstStep = glowCount == 0;
            var onlyStep = firstStep && LastStep;
            var extEnd = !Back && Hitting;
            var extStart = Back && firstStep && VisualLength < ShortStepSize;
            Vector3D frontPos;
            Vector3D backPos;
            if (shrinking)
            {
                frontPos = shrink.NewFront;
                backPos = !shrink.Last ? shrink.NewFront : TracerFront;
            }
            else
            {
                var futureStep = Direction * ShortStepSize;
                frontPos = Back && !onlyStep ? TracerBack + futureStep : TracerFront;
                backPos = Back && !extStart ? TracerBack : TracerFront + -futureStep;
            }

            if (glowCount <= System.Values.Graphics.Line.Trail.DecayTime)
            {
                var glow = Ai.Session.Av.Glows.Count > 0 ? Ai.Session.Av.Glows.Pop() : new AfterGlow();
                
                glow.TailPos = backPos;
                GlowSteps.Enqueue(glow);
                ++glowCount;
            }

            var endIdx = glowCount - 1;
            for (int i = endIdx; i >= 0; i--)
            {
                var g = GlowSteps[i];

                if (i != 0)
                {
                    var extend = extEnd && i == endIdx;
                    g.Parent = GlowSteps[i - 1];
                    g.Line = new LineD(extend ? TracerFront + ShootVelStep: g.Parent.TailPos += ShootVelStep, extend ? g.Parent.TailPos : g.TailPos);
                }
                else if (i != endIdx)
                    g.Line = new LineD(g.Line.From + ShootVelStep, g.TailPos);
                else
                    g.Line = new LineD(frontPos, backPos);
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
                    TracerShrinks.Enqueue(new Shrinks { NewFront = shrunk.Value.NewTracerFront, Color = color, Length = shrunk.Value.Reduced, Thickness = width, Last = last});
                }
            }
        }

        private void ShrinkInit()
        {
            ShrinkInited = true;

            var fractualSteps = VisualLength  / StepSize;
            TracerSteps = (int)Math.Floor(fractualSteps);
            TracerStep = TracerSteps;
            if (TracerSteps <= 0 || fractualSteps < StepSize && !MyUtils.IsZero(fractualSteps - StepSize, 1E-01F))
                Tracer = TracerState.Off;

        }

        internal Shrunk? GetLine()
        {
            if (TracerStep > 0)
            {
                Hit.HitPos += ShootVelStep;
                var newTracerFront = Hit.HitPos + -(Direction * (TracerStep * StepSize));
                var reduced = TracerStep-- * StepSize;
                return new Shrunk(ref newTracerFront, (float) reduced);
            }
            return null;
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
            var tracerWidth = System.Values.Graphics.Line.Tracer.Width;
            var trailWidth = System.TrailWidth;
            if (System.LineWidthVariance)
            {
                var wv = System.Values.Graphics.Line.WidthVariance;
                var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                tracerWidth += randomValue;
                if (System.Values.Graphics.Line.Trail.UseWidthVariance)
                    trailWidth += randomValue;
            }

            var target = TracerFront + (-Direction * TotalLength);
            ClosestPointOnLine = MyUtils.GetClosestPointOnLine(ref TracerFront, ref target, ref Ai.Session.CameraPos);
            DistanceToLine = (float)Vector3D.Distance(ClosestPointOnLine, Ai.Session.CameraMatrix.Translation);

            if (System.IsBeamWeapon && Vector3D.DistanceSquared(TracerFront, TracerBack) > 640000)
            {
                target = TracerFront + (-Direction * (TotalLength - MathHelperD.Clamp(DistanceToLine * 6, DistanceToLine, System.MaxTrajectory * 0.5)));
                ClosestPointOnLine = MyUtils.GetClosestPointOnLine(ref TracerFront, ref target, ref Ai.Session.CameraPos);
                DistanceToLine = (float)Vector3D.Distance(ClosestPointOnLine, Ai.Session.CameraMatrix.Translation);
            }

            double scale = 0.1f;
            ScaleFov = Math.Tan(MyAPIGateway.Session.Camera.FovWithZoom * 0.5);
            TracerWidth = Math.Max(tracerWidth, scale * ScaleFov * (DistanceToLine / 100));
            TrailWidth = Math.Max(trailWidth, scale * ScaleFov * (DistanceToLine / 100));
            TrailScaler = ((float)TrailWidth / trailWidth);
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

        internal void PrepOffsetEffect(Vector3D tracerStart, Vector3D direction, double tracerLength)
        {
            var up = MatrixD.Identity.Up;
            var startPos = tracerStart + -(direction * tracerLength);
            MatrixD.CreateWorld(ref startPos, ref direction, ref up, out OffsetMatrix);
            TracerLengthSqr = tracerLength * tracerLength;
            var maxOffset = System.Values.Graphics.Line.OffsetEffect.MaxOffset;
            var minLength = System.Values.Graphics.Line.OffsetEffect.MinLength;
            var maxLength = MathHelperD.Clamp(System.Values.Graphics.Line.OffsetEffect.MaxLength, 0, tracerLength);

            double currentForwardDistance = 0;
            while (currentForwardDistance <= tracerLength)
            {
                currentForwardDistance += MyUtils.GetRandomDouble(minLength, maxLength);
                var lateralXDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                var lateralYDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                Offsets.Add(new Vector3D(lateralXDistance, lateralYDistance, currentForwardDistance * -1));
            }
        }


        internal void DrawLineOffsetEffect(Vector3D pos, Vector3D direction, double tracerLength, float beamRadius, Vector4 color)
        {
            MatrixD matrix;
            var up = MatrixD.Identity.Up;
            var startPos = pos + -(direction * tracerLength);
            MatrixD.CreateWorld(ref startPos, ref direction, ref up, out matrix);
            var offsetMaterial = System.TracerMaterial;
            var tracerLengthSqr = tracerLength * tracerLength;
            var maxOffset = System.Values.Graphics.Line.OffsetEffect.MaxOffset;
            var minLength = System.Values.Graphics.Line.OffsetEffect.MinLength;
            var maxLength = MathHelperD.Clamp(System.Values.Graphics.Line.OffsetEffect.MaxLength, 0, tracerLength);

            double currentForwardDistance = 0;

            while (currentForwardDistance < tracerLength)
            {
                currentForwardDistance += MyUtils.GetRandomDouble(minLength, maxLength);
                var lateralXDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                var lateralYDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                Offsets.Add(new Vector3D(lateralXDistance, lateralYDistance, currentForwardDistance * -1));
            }

            for (int i = 0; i < Offsets.Count; i++)
            {
                Vector3D fromBeam;
                Vector3D toBeam;

                if (i == 0)
                {
                    fromBeam = matrix.Translation;
                    toBeam = Vector3D.Transform(Offsets[i], matrix);
                }
                else
                {
                    fromBeam = Vector3D.Transform(Offsets[i - 1], matrix);
                    toBeam = Vector3D.Transform(Offsets[i], matrix);
                }

                Vector3 dir = (toBeam - fromBeam);
                var length = dir.Length();
                var normDir = dir / length;
                MyTransparentGeometry.AddLineBillboard(offsetMaterial, color, fromBeam, normDir, length, beamRadius);

                if (Vector3D.DistanceSquared(matrix.Translation, toBeam) > tracerLengthSqr) break;
            }
            Offsets.Clear();
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
            TravelEmitter.SetPosition(TracerFront);
            TravelEmitter.Entity = PrimeEntity;
            TravelEmitter.PlaySound(TravelSound, true);
            AmmoSound = true;
        }

        internal void ResetHit()
        {
            ShrinkInited = false;
            TotalLength = MathHelperD.Clamp(MaxTracerLength + MaxGlowLength, 0.1f, System.MaxTrajectory);
        }

        internal void Close()
        {
            // Reset only vars that are not always set
            Hit = new Hit();
            HitVelocity = Vector3D.Zero;
            TracerBack = Vector3D.Zero;
            OnScreen = Screen.None;
            Tracer = TracerState.Off;
            Trail = TrailState.Off;
            LifeTime = 0;
            TracerSteps = 0;
            TracerStep = 0;

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
            Back = false;
            LastStep = false;
            DetonateFakeExp = false;
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
        internal LineD Line;
        internal int Step;
    }

    internal struct Shrinks
    {
        internal Vector3D NewFront;
        internal Vector4 Color;
        internal float Length;
        internal float Thickness;
        internal bool Last;

    }

    internal struct Shrunk
    {
        internal readonly Vector3D NewTracerFront;
        internal readonly float Reduced;

        internal Shrunk(ref Vector3D newTracerFront, float reduced)
        {
            NewTracerFront = newTracerFront;
            Reduced = reduced;
        }
    }
}
