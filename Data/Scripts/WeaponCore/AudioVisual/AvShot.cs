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
        internal WeaponDefinition.AmmoDef AmmoDef;
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
        internal bool Dirty;
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
        internal ulong ParentId = ulong.MaxValue;
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
        internal BoundingSphereD ModelSphereCurrent;
        internal MatrixD TriggerMatrix = MatrixD.Identity;
        internal Shrinks EmptyShrink;

        internal enum TracerState
        {
            Off,
            Full,
            Grow,
            Shrink,
        }

        internal enum ModelState
        {
            None,
            Exists,
        }

        internal enum TrailState
        {
            Off,
            Front,
            Back,
        }

        internal enum Screen // Tracer includes Tail;
        {
            None,
            ModelOnly,
            Tracer,
            Trail,
        }

        internal void Init(ProInfo info, double firstStepSize, double maxSpeed)
        {
            System = info.System;
            AmmoDef = info.AmmoDef;
            Ai = info.Ai;
            if (ParentId != ulong.MaxValue) Log.Line($"invalid avshot, parentId:{ParentId}");
            ParentId = info.Id;
            Model = (info.AmmoDef.Const.PrimeModel || info.AmmoDef.Const.TriggerModel) ? Model = ModelState.Exists : Model = ModelState.None;
            PrimeEntity = info.PrimeEntity;
            TriggerEntity = info.TriggerEntity;
            Origin = info.Origin;
            Offset = AmmoDef.Const.OffsetEffect;
            MaxTracerLength = AmmoDef.Const.TracerLength;
            MuzzleId = info.MuzzleId;
            WeaponId = info.WeaponId;
            MaxSpeed = maxSpeed;
            MaxStepSize = MaxSpeed * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            ShootVelStep = info.ShooterVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            info.Ai.WeaponBase.TryGetValue(info.Target.FiringCube, out FiringWeapon);
            ShrinkInited = false;
            if (AmmoDef.Const.DrawLine) Tracer = !AmmoDef.Const.IsBeamWeapon && firstStepSize < MaxTracerLength && !MyUtils.IsZero(firstStepSize - MaxTracerLength, 1E-01F) ? TracerState.Grow : TracerState.Full;
            else Tracer = TracerState.Off;

            if (AmmoDef.Const.Trail)
            {
                MaxGlowLength = MathHelperD.Clamp(AmmoDef.AmmoGraphics.Lines.Trail.DecayTime * MaxStepSize, 0.1f, AmmoDef.Const.MaxTrajectory);
                Trail = AmmoDef.AmmoGraphics.Lines.Trail.Back ? TrailState.Back : Trail = TrailState.Front;
                GlowShrinkSize = !AmmoDef.AmmoGraphics.Lines.Trail.UseColorFade ? AmmoDef.Const.TrailWidth / AmmoDef.AmmoGraphics.Lines.Trail.DecayTime : 1f / AmmoDef.AmmoGraphics.Lines.Trail.DecayTime;
                Back = Trail == TrailState.Back;
            }
            else Trail = TrailState.Off;
            TotalLength = MathHelperD.Clamp(MaxTracerLength + MaxGlowLength, 0.1f, info.AmmoDef.Const.MaxTrajectory);
            Ai.Session.Av.AvStart.Add(this);
        }

        internal void Update(ProInfo info, double stepSize, double visualLength, ref Vector3D tracerFront, ref Vector3D direction, ref Vector3D pointDir, double? shortStepSize = null, bool saveHit = false, bool modelOnly = false)
        {
            var lineEffect = AmmoDef.Const.Trail || AmmoDef.Const.DrawLine;

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
            if (modelOnly) {
                ModelSphereCurrent.Center = TracerFront;
                if (Triggered)
                    ModelSphereCurrent.Radius = info.TriggerGrowthSteps < AmmoDef.Const.AreaEffectSize ? TriggerMatrix.Scale.AbsMax() : AmmoDef.Const.AreaEffectSize;

                if (Ai.Session.Camera.IsInFrustum(ref ModelSphereCurrent))
                    OnScreen = Screen.ModelOnly;
            }
            else if (lineEffect || Model == ModelState.None && AmmoDef.Const.AmmoParticle) {


                var rayTracer = new RayD(TracerBack, PointDir);
                var rayTrail = new RayD(TracerFront + (-Direction * ShortEstTravel), Direction);

                //DsDebugDraw.DrawRay(rayTracer, VRageMath.Color.White, 0.25f, (float) VisualLength);
                //DsDebugDraw.DrawRay(rayTrail, VRageMath.Color.Orange, 0.25f, (float)ShortEstTravel);
                
                double? dist;
                Ai.Session.CameraFrustrum.Intersects(ref rayTracer, out dist);

                if (dist != null && dist <= VisualLength)
                    OnScreen = Screen.Tracer;
                else if (AmmoDef.Const.Trail) {
                    Ai.Session.CameraFrustrum.Intersects(ref rayTrail, out dist);
                    if (dist != null && dist <= ShortEstTravel + ShortStepSize)
                        OnScreen = Screen.Trail;
                }

                if (OnScreen != Screen.None && !TrailActivated && AmmoDef.Const.Trail) TrailActivated = true;
                
                if (OnScreen == Screen.None && TrailActivated) OnScreen = Screen.Trail;

                if (Model != ModelState.None)
                {
                    ModelSphereCurrent.Center = TracerFront;
                    if (Triggered)
                        ModelSphereCurrent.Radius = info.TriggerGrowthSteps < AmmoDef.Const.AreaEffectSize ? TriggerMatrix.Scale.AbsMax() : AmmoDef.Const.AreaEffectSize;

                    if (OnScreen == Screen.None && Ai.Session.Camera.IsInFrustum(ref ModelSphereCurrent))
                        OnScreen = Screen.ModelOnly;
                }
            }

            if (info.MuzzleId == -1)
                return;

            if (saveHit) {
                if (Hit.Entity != null)
                    HitVelocity = Hit.Entity.GetTopMostParent()?.Physics?.LinearVelocity ?? Vector3D.Zero;
                else if (Hit.Projectile != null)
                    HitVelocity = Hit.Projectile.Velocity;
                Hitting = !ShrinkInited;
            }
            LastStep = Hitting || MyUtils.IsZero(AmmoDef.Const.MaxTrajectory - ShortEstTravel, 1E-01F);

            if (AmmoDef.Const.IsBeamWeapon || !saveHit && MyUtils.IsZero(MaxTracerLength - VisualLength, 1E-01F)) {
                Tracer = TracerState.Full;
            }
            else if (Tracer != TracerState.Off && VisualLength <= 0) {
                Tracer = TracerState.Off;
            }
            else if (Hitting && !modelOnly && lineEffect && VisualLength / StepSize > 1 && !MyUtils.IsZero(EstTravel - ShortEstTravel, 1E-01F)) {
                Tracer = TracerState.Shrink;
                TotalLength = MathHelperD.Clamp(VisualLength + MaxGlowLength, 0.1f, Vector3D.Distance(Origin, TracerFront));
            }
            else if (Tracer == TracerState.Grow && LastStep) {
                Tracer = TracerState.Full;
            }

            var lineOnScreen = OnScreen > (Screen)1;

            if (lineEffect && (Active || lineOnScreen))
                LineVariableEffects();

            if (Tracer != TracerState.Off && lineOnScreen) {
                if (Tracer == TracerState.Shrink && !ShrinkInited)
                    Shrink();
                else if (AmmoDef.Const.IsBeamWeapon && Hitting && AmmoDef.Const.HitParticle && !(MuzzleId != 0 && (AmmoDef.Const.ConvergeBeams || AmmoDef.Const.OneHitParticle)))
                {
                    ContainmentType containment;
                    Ai.Session.CameraFrustrum.Contains(ref Hit.HitPos, out containment);
                    if (containment != ContainmentType.Disjoint) RunBeam();
                }

                if (AmmoDef.Const.OffsetEffect)
                    PrepOffsetEffect(TracerFront, PointDir, VisualLength);
            }

            var backAndGrowing = Back && Tracer == TracerState.Grow;
            if (Trail != TrailState.Off && !backAndGrowing && lineOnScreen)
                RunGlow(ref EmptyShrink);

            if (!Active && OnScreen != Screen.None) {
                Active = true;
                Ai.Session.Av.AvShots.Add(this);
            }
            Hitting = false;
        }

        internal void End(Vector3D endPos, bool detonateFakeExp = false)
        {
            if (Vector3D.IsZero(TracerFront)) TracerFront = endPos;
            Ai.Session.Av.AvEnd.Add(this);
            DetonateFakeExp = detonateFakeExp;
            Dirty = true;
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

            if (glowCount <= AmmoDef.AmmoGraphics.Lines.Trail.DecayTime)
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

                    var color = AmmoDef.AmmoGraphics.Lines.Tracer.Color;
                    if (AmmoDef.Const.LineColorVariance)
                    {
                        var cv = AmmoDef.AmmoGraphics.Lines.ColorVariance;
                        var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                        color.X *= randomValue;
                        color.Y *= randomValue;
                        color.Z *= randomValue;
                    }

                    var width = AmmoDef.AmmoGraphics.Lines.Tracer.Width;
                    if (AmmoDef.Const.LineWidthVariance)
                    {
                        var wv = AmmoDef.AmmoGraphics.Lines.WidthVariance;
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
            var color = AmmoDef.AmmoGraphics.Lines.Tracer.Color;
            if (AmmoDef.Const.LineColorVariance)
            {
                var cv = AmmoDef.AmmoGraphics.Lines.ColorVariance;
                var randomValue = MyUtils.GetRandomFloat(cv.Start, cv.End);
                color.X *= randomValue;
                color.Y *= randomValue;
                color.Z *= randomValue;
            }
            Color = color;
            var tracerWidth = AmmoDef.AmmoGraphics.Lines.Tracer.Width;
            var trailWidth = AmmoDef.Const.TrailWidth;
            if (AmmoDef.Const.LineWidthVariance)
            {
                var wv = AmmoDef.AmmoGraphics.Lines.WidthVariance;
                var randomValue = MyUtils.GetRandomFloat(wv.Start, wv.End);
                tracerWidth += randomValue;
                if (AmmoDef.AmmoGraphics.Lines.Trail.UseWidthVariance)
                    trailWidth += randomValue;
            }

            var target = TracerFront + (-Direction * TotalLength);
            ClosestPointOnLine = MyUtils.GetClosestPointOnLine(ref TracerFront, ref target, ref Ai.Session.CameraPos);
            DistanceToLine = (float)Vector3D.Distance(ClosestPointOnLine, Ai.Session.CameraMatrix.Translation);

            if (AmmoDef.Const.IsBeamWeapon && Vector3D.DistanceSquared(TracerFront, TracerBack) > 640000)
            {
                target = TracerFront + (-Direction * (TotalLength - MathHelperD.Clamp(DistanceToLine * 6, DistanceToLine, AmmoDef.Const.MaxTrajectory * 0.5)));
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
                        MyParticlesManager.TryCreateParticleEffect(AmmoDef.AmmoGraphics.Particles.Hit.Name, ref matrix, ref Hit.HitPos, uint.MaxValue, out effect);
                        if (effect == null)
                        {
                            weapon.HitEffects[MuzzleId] = null;
                            return;
                        }

                        effect.DistanceMax = AmmoDef.AmmoGraphics.Particles.Hit.Extras.MaxDistance;
                        effect.DurationMax = AmmoDef.AmmoGraphics.Particles.Hit.Extras.MaxDuration;
                        effect.UserColorMultiplier = AmmoDef.AmmoGraphics.Particles.Hit.Color;
                        effect.Loop = AmmoDef.AmmoGraphics.Particles.Hit.Extras.Loop;
                        effect.UserRadiusMultiplier = AmmoDef.AmmoGraphics.Particles.Hit.Extras.Scale * 1;
                        var scale = MathHelper.Lerp(1, 0, (DistanceToLine * 2) / AmmoDef.AmmoGraphics.Particles.Hit.Extras.MaxDistance);
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

        internal void PrepOffsetEffect(Vector3D tracerStart, Vector3D direction, double tracerLength)
        {
            var up = MatrixD.Identity.Up;
            var startPos = tracerStart + -(direction * tracerLength);
            MatrixD.CreateWorld(ref startPos, ref direction, ref up, out OffsetMatrix);
            TracerLengthSqr = tracerLength * tracerLength;
            var maxOffset = AmmoDef.AmmoGraphics.Lines.OffsetEffect.MaxOffset;
            var minLength = AmmoDef.AmmoGraphics.Lines.OffsetEffect.MinLength;
            var maxLength = MathHelperD.Clamp(AmmoDef.AmmoGraphics.Lines.OffsetEffect.MaxLength, 0, tracerLength);

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
            var offsetMaterial = AmmoDef.Const.TracerMaterial;
            var tracerLengthSqr = tracerLength * tracerLength;
            var maxOffset = AmmoDef.AmmoGraphics.Lines.OffsetEffect.MaxOffset;
            var minLength = AmmoDef.AmmoGraphics.Lines.OffsetEffect.MinLength;
            var maxLength = MathHelperD.Clamp(AmmoDef.AmmoGraphics.Lines.OffsetEffect.MaxLength, 0, tracerLength);

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

            if (!AmmoDef.Const.IsBeamWeapon && AmmoDef.Const.AmmoTravelSound)
            {
                HasTravelSound = true;
                TravelSound.Init(AmmoDef.AmmoAudio.TravelSound, false);
            }
            else HasTravelSound = false;

            if (AmmoDef.Const.HitSound)
            {
                var hitSoundChance = AmmoDef.AmmoAudio.HitPlayChance;
                HitSoundActive = (hitSoundChance >= 1 || hitSoundChance >= MyUtils.GetRandomDouble(0.0f, 1f));
                if (HitSoundActive)
                    HitSound.Init(AmmoDef.AmmoAudio.HitSound, false);
            }

            if (FiringSoundState == WeaponSystem.FiringSoundState.PerShot && distanceFromCameraSqr < System.FiringSoundDistSqr)
            {
                StartSoundActived = true;
                FireSound.Init(System.Values.HardPoint.Audio.FiringSound, false);
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
            TotalLength = MathHelperD.Clamp(MaxTracerLength + MaxGlowLength, 0.1f, AmmoDef.Const.MaxTrajectory);
        }

        internal void Close()
        {
            // Reset only vars that are not always set
            Hit = new Hit();
            HitVelocity = Vector3D.Zero;
            TracerBack = Vector3D.Zero;
            TracerFront = Vector3D.Zero;
            ClosestPointOnLine = Vector3D.Zero;
            Color = Vector4.Zero;
            OnScreen = Screen.None;
            Tracer = TracerState.Off;
            Trail = TrailState.Off;
            LifeTime = 0;
            TracerSteps = 0;
            TracerStep = 0;
            DistanceToLine = 0;
            TracerWidth = 0;
            TrailWidth = 0;
            TrailScaler = 0;
            ParentId = ulong.MaxValue;
            Dirty = false;
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
            TracerShrinks.Clear();
            GlowSteps.Clear();
            Offsets.Clear();
            //
            FiringWeapon = null;
            PrimeEntity = null;
            TriggerEntity = null;
            Ai = null;
            AmmoDef = null;
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
