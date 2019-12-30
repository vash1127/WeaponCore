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

        internal MyQueue<AfterGlow> GlowSteps = new MyQueue<AfterGlow>();
        internal List<Vector3D> Offsets = new List<Vector3D>();

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
        internal double MaxTracerLength;
        internal double TracerLength;
        internal double MaxGlowLength;
        internal double FirstStepSize;
        internal double StepSize;
        internal double TotalLength;
        internal double Thickness;
        internal double LineScaler;
        internal double ScaleFov;
        internal double VisualLength;
        internal double MaxSpeed;
        internal double MaxStepSize;
        internal double TracerLengthSqr;
        internal float DistanceToLine;
        internal int LifeTime;
        internal int MuzzleId;
        internal int WeaponId;
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
        internal Vector3D HitVelocity;
        internal Vector3D HitPosition;
        internal Vector3D ShooterVelocity;
        internal Vector3D TracerStart;
        internal Vector3D ShooterVelStep;
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


            if (System.DrawLine) Tracer = !System.IsBeamWeapon && firstStepSize < MaxTracerLength ? TracerState.Grow : TracerState.Full;
            else Tracer = TracerState.Off;

            if (System.Trail)
            {
                MaxGlowLength = System.Values.Graphics.Line.Trail.DecayTime * MaxStepSize;
                Trail = System.Values.Graphics.Line.Trail.Back ? TrailState.Back : Trail = TrailState.Front;
            }
            else Trail = TrailState.Off;
            TotalLength = MaxTracerLength + MaxGlowLength;
        }

        internal void Update(double stepSize, double visualLength, ref Vector3D position, ref Vector3D direction)
        {
            LastTick = Ai.Session.Tick;
            Position = position;
            Direction = direction;
            StepSize = stepSize;
            VisualLength = visualLength;
            TracerStart = Position + -(Direction * VisualLength);
            LifeTime++;
        }

        internal void Complete(bool saveHit = false, bool closeModel = false)
        {
            if (!Active)
            {
                Active = true;
                Ai.Session.AvShots.Add(this);
            }

            if (Hit.HitPos != Vector3D.Zero)
            {
                if (saveHit)
                {
                    if (Hit.Entity != null)
                        HitVelocity = Hit.Entity.GetTopMostParent()?.Physics?.LinearVelocity ?? Vector3D.Zero;
                    else if (Hit.Projectile != null) 
                        HitVelocity = Hit.Projectile.Velocity;

                    HitPosition = Hit.HitPos;
                }

                var totalDiff = TotalLength - StepSize;
                var tracerDiff = TracerLength - StepSize;
                var visable = totalDiff > 0 && !MyUtils.IsZero(totalDiff);
                var tracerVisable = tracerDiff > 0 && !MyUtils.IsZero(tracerDiff);
                if (System.IsBeamWeapon) Tracer = TracerState.Full;
                else if (!visable || !tracerVisable)
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

            var target = System.IsBeamWeapon ? Position + -Direction * TracerLength : Position + -Direction * MaxTracerLength;
            ClosestPointOnLine = MyUtils.GetClosestPointOnLine(ref Position, ref target, ref Ai.Session.CameraPos);
            DistanceToLine = (float)Vector3D.Distance(ClosestPointOnLine, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            if (System.IsBeamWeapon && DistanceToLine < 1000) DistanceToLine = 1000;
            else if (DistanceToLine < 350) DistanceToLine = 350;
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

            if (closeModel)
                Model = ModelState.Close;

            if (Tracer != TracerState.Off && System.IsBeamWeapon && Hit.HitPos != Vector3D.Zero)
                RunBeam();
            else if (Tracer != TracerState.Off && OnScreen == Screen.Tracer && System.OffsetEffect)
                LineOffsetEffect(TracerStart, -Direction, TracerLength);

            if (Trail != TrailState.Off && Tracer != TracerState.Grow)
                RunGlow();

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
                else
                    glow.TailPos = glow.Parent.TailPos + (Direction * StepSize);

                glow.FirstTick = Ai.Session.Tick;
                glow.Direction = Direction;
                glow.VelStep = Direction * StepSize;
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
                var steps = System.Values.Graphics.Line.Trail.DecayTime;
                var fullSize = System.Values.Graphics.Line.Tracer.Width;
                var shrinkAmount = fullSize / steps;
                glow.TailPos += (ShooterVelStep);
                //glow.TracerStart += (ShooterVelStep);
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
        internal void RunBeam()
        {
            TracerLength = VisualLength;

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

        internal void LineOffsetEffect(Vector3D pos, Vector3D direction, double tracerLength)
        {
            var up = MatrixD.Identity.Up;
            var startPos = pos + -(direction * tracerLength);
            MatrixD.CreateWorld(ref startPos, ref direction, ref up, out OffsetMatrix);
            TracerLengthSqr = tracerLength * tracerLength;
            var maxOffset = System.Values.Graphics.Line.OffsetEffect.MaxOffset;
            var minLength = System.Values.Graphics.Line.OffsetEffect.MinLength;
            var maxLength = MathHelperD.Clamp(System.Values.Graphics.Line.OffsetEffect.MaxLength, 0, TracerLength);

            double currentForwardDistance = 0;

            while (currentForwardDistance < tracerLength)
            {
                currentForwardDistance += MyUtils.GetRandomDouble(minLength, maxLength);
                var lateralXDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                var lateralYDistance = MyUtils.GetRandomDouble(maxOffset * -1, maxOffset);
                Offsets.Add(new Vector3D(lateralXDistance, lateralYDistance, currentForwardDistance * -1));
            }
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
            OnScreen = Screen.None;
            Tracer = TracerState.Off;
            Trail = TrailState.Off;
            Model = ModelState.None;
            LastTick = 0;
            LifeTime = 0;
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
            //
            Ai.Session.AvShotPool.Return(this);
            Ai = null;
            System = null;
        }
    }

    internal class AfterGlow
    {
        internal AfterGlow Parent;
        internal Vector3D TracerStart;
        internal Vector3D TailPos;
        internal Vector3D Direction;
        internal LineD Line;
        internal Vector3D VelStep;
        internal uint FirstTick;
        internal float WidthScaler;
        internal float Length;
        internal float Thickness;

        internal void Clean()
        {
            Parent = null;
            TracerStart = Vector3D.Zero;
            TailPos = Vector3D.Zero;
            FirstTick = 0;
            WidthScaler = 0;
            Length = 0;
            Thickness = 0;
        }
    }
}
