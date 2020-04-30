using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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
        internal bool HitSoundInitted;
        internal bool StartSoundActived;
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
        internal bool IsShrapnel;
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
        internal double MaxTrajectory;
        internal float ShotFade;
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
        internal ParticleState HitParticle;
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

        internal enum ParticleState
        {
            None,
            Explosion,
            Custom,
            Dirty,
        }

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
            InProximity,
            Tracer,
            Trail,
        }

        internal void Init(ProInfo info, double firstStepSize, double maxSpeed)
        {
            System = info.System;
            AmmoDef = info.AmmoDef;
            Ai = info.Ai;
            IsShrapnel = info.IsShrapnel;
            if (ParentId != ulong.MaxValue) Log.Line($"invalid avshot, parentId:{ParentId}");
            ParentId = info.Id;
            Model = (info.AmmoDef.Const.PrimeModel || info.AmmoDef.Const.TriggerModel) ? Model = ModelState.Exists : Model = ModelState.None;
            PrimeEntity = info.PrimeEntity;
            TriggerEntity = info.TriggerEntity;
            Origin = info.Origin;
            Offset = AmmoDef.Const.OffsetEffect;
            MaxTracerLength = info.TracerLength;
            MuzzleId = info.MuzzleId;
            WeaponId = info.WeaponId;
            MaxSpeed = maxSpeed;
            MaxStepSize = MaxSpeed * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            ShootVelStep = info.ShooterVel * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            info.Ai.WeaponBase.TryGetValue(info.Target.FiringCube, out FiringWeapon);
            MaxTrajectory = info.MaxTrajectory;
            ShotFade = info.ShotFade;
            ShrinkInited = false;
            HitEmitter.CanPlayLoopSounds = false;
            if (AmmoDef.Const.DrawLine) Tracer = !AmmoDef.Const.IsBeamWeapon && firstStepSize < MaxTracerLength && !MyUtils.IsZero(firstStepSize - MaxTracerLength, 1E-01F) ? TracerState.Grow : TracerState.Full;
            else Tracer = TracerState.Off;

            if (AmmoDef.Const.Trail)
            {
                MaxGlowLength = MathHelperD.Clamp(AmmoDef.AmmoGraphics.Lines.Trail.DecayTime * MaxStepSize, 0.1f, MaxTrajectory);
                Trail = AmmoDef.AmmoGraphics.Lines.Trail.Back ? TrailState.Back : Trail = TrailState.Front;
                GlowShrinkSize = !AmmoDef.AmmoGraphics.Lines.Trail.UseColorFade ? AmmoDef.Const.TrailWidth / AmmoDef.AmmoGraphics.Lines.Trail.DecayTime : 1f / AmmoDef.AmmoGraphics.Lines.Trail.DecayTime;
                Back = Trail == TrailState.Back;
            }
            else Trail = TrailState.Off;
            TotalLength = MathHelperD.Clamp(MaxTracerLength + MaxGlowLength, 0.1f, MaxTrajectory);
        }

        internal static void DeferedAvStateUpdates(Session s)
        {
            for (int x = 0; x < s.Projectiles.DeferedAvDraw.Count; x++)
            {
                var d = s.Projectiles.DeferedAvDraw[x];
                var i = d.Info;
                var a = i.AvShot;
                var lineEffect = a.AmmoDef.Const.Trail || a.AmmoDef.Const.DrawLine;
                var saveHit = d.Hit;
                ++a.LifeTime;
                a.LastTick = s.Tick;
                a.StepSize = d.StepSize;
                a.EstTravel = a.StepSize * a.LifeTime;
                a.ShortStepSize = d.ShortStepSize ?? d.StepSize;
                a.ShortEstTravel = MathHelperD.Clamp((a.EstTravel - a.StepSize) + a.ShortStepSize, 0, double.MaxValue);

                a.VisualLength = d.VisualLength;
                a.TracerFront = d.TracerFront;
                a.Direction = i.Direction;
                a.PointDir = !saveHit && a.GlowSteps.Count > 1 ? a.GlowSteps[a.GlowSteps.Count - 1].Line.Direction : i.VisualDir;
                a.TracerBack = a.TracerFront + (-a.Direction * a.VisualLength);
                a.OnScreen = Screen.None; // clear OnScreen
                if (i.ModelOnly)
                {
                    a.ModelSphereCurrent.Center = a.TracerFront;
                    if (a.Triggered)
                        a.ModelSphereCurrent.Radius = i.TriggerGrowthSteps < a.AmmoDef.Const.AreaEffectSize ? a.TriggerMatrix.Scale.AbsMax() : a.AmmoDef.Const.AreaEffectSize;

                    if (s.Camera.IsInFrustum(ref a.ModelSphereCurrent))
                        a.OnScreen = Screen.ModelOnly;
                }
                else if (lineEffect || a.Model == ModelState.None && a.AmmoDef.Const.AmmoParticle)
                {


                    var rayTracer = new RayD(a.TracerBack, a.PointDir);
                    var rayTrail = new RayD(a.TracerFront + (-a.Direction * a.ShortEstTravel), a.Direction);

                    //DsDebugDraw.DrawRay(rayTracer, VRageMath.Color.White, 0.25f, (float) VisualLength);
                    //DsDebugDraw.DrawRay(rayTrail, VRageMath.Color.Orange, 0.25f, (float)ShortEstTravel);

                   double? dist;
                   s.CameraFrustrum.Intersects(ref rayTracer, out dist);

                    if (dist != null && dist <= a.VisualLength)
                        a.OnScreen = Screen.Tracer;
                    else if (a.AmmoDef.Const.Trail)
                    {
                        s.CameraFrustrum.Intersects(ref rayTrail, out dist);
                        if (dist != null && dist <= a.ShortEstTravel + a.ShortStepSize)
                            a.OnScreen = Screen.Trail;
                    }

                    if (a.OnScreen != Screen.None && !a.TrailActivated && a.AmmoDef.Const.Trail) a.TrailActivated = true;

                    if (a.OnScreen == Screen.None && a.TrailActivated) a.OnScreen = Screen.Trail;

                    if (a.Model != ModelState.None)
                    {
                        a.ModelSphereCurrent.Center = a.TracerFront;
                        if (a.Triggered)
                            a.ModelSphereCurrent.Radius = i.TriggerGrowthSteps < a.AmmoDef.Const.AreaEffectSize ? a.TriggerMatrix.Scale.AbsMax() : a.AmmoDef.Const.AreaEffectSize;

                        if (a.OnScreen == Screen.None && s.Camera.IsInFrustum(ref a.ModelSphereCurrent))
                            a.OnScreen = Screen.ModelOnly;
                    }
                }

                if (a.OnScreen == Screen.None && Vector3D.DistanceSquared(a.TracerFront, a.Ai.Session.CameraPos) <= 225) 
                    a.OnScreen = Screen.InProximity;

                if (i.MuzzleId == -1)
                    return;

                if (saveHit) {
                    a.HitVelocity = a.Hit.HitVelocity;
                    a.Hitting = !a.ShrinkInited;
                    if (!a.HitSoundInitted && a.HitSoundActive)
                        a.HitSoundStart();
                }
                a.LastStep = a.Hitting || MyUtils.IsZero(a.MaxTrajectory - a.ShortEstTravel, 1E-01F);

                if (a.AmmoDef.Const.DrawLine) {
                    if (a.AmmoDef.Const.IsBeamWeapon || !saveHit && MyUtils.IsZero(a.MaxTracerLength - a.VisualLength, 1E-01F)) {
                        a.Tracer = TracerState.Full;
                    }
                    else if (a.Tracer != TracerState.Off && a.VisualLength <= 0) {
                        a.Tracer = TracerState.Off;
                    }
                    else if (a.Hitting && !i.ModelOnly && lineEffect && a.VisualLength / a.StepSize > 1 && !MyUtils.IsZero(a.EstTravel - a.ShortEstTravel, 1E-01F)) {
                        a.Tracer = TracerState.Shrink;
                        a.TotalLength = MathHelperD.Clamp(a.VisualLength + a.MaxGlowLength, 0.1f, Vector3D.Distance(a.Origin, a.TracerFront));
                    }
                    else if (a.Tracer == TracerState.Grow && a.LastStep) {
                        a.Tracer = TracerState.Full;
                    }
                }

                var lineOnScreen = a.OnScreen > (Screen)2;

                if (lineEffect && (a.Active || lineOnScreen))
                    a.LineVariableEffects();

                if (a.Tracer != TracerState.Off && lineOnScreen)
                {
                    if (a.Tracer == TracerState.Shrink && !a.ShrinkInited)
                        a.Shrink();
                    else if (a.AmmoDef.Const.IsBeamWeapon && a.Hitting && a.AmmoDef.Const.HitParticle && !(a.MuzzleId != 0 && (a.AmmoDef.Const.ConvergeBeams || a.AmmoDef.Const.OneHitParticle)))
                    {
                        ContainmentType containment;
                        s.CameraFrustrum.Contains(ref a.Hit.HitPos, out containment);
                        if (containment != ContainmentType.Disjoint) a.RunBeam();
                    }

                    if (a.AmmoDef.Const.OffsetEffect)
                        a.PrepOffsetEffect(a.TracerFront, a.PointDir, a.VisualLength);
                }

                var backAndGrowing = a.Back && a.Tracer == TracerState.Grow;
                if (a.Trail != TrailState.Off && !backAndGrowing && lineOnScreen)
                    a.RunGlow(ref a.EmptyShrink);

                if (!a.Active && a.OnScreen != Screen.None)
                {
                    a.Active = true;
                    s.Av.AvShots.Add(a);
                }
                a.Hitting = false;
            }
            s.Projectiles.DeferedAvDraw.Clear();
        }

        internal void AvClose(Vector3D endPos, bool detonateFakeExp = false)
        {
            if (Vector3D.IsZero(TracerFront)) TracerFront = endPos;
            DetonateFakeExp = detonateFakeExp;
            Dirty = true;

            if (DetonateFakeExp)
            {
                HitParticle = ParticleState.Dirty;
                if (Ai.Session.Av.ExplosionReady)
                {

                    if (OnScreen != Screen.None)
                    {
                        if (DetonateFakeExp) SUtils.CreateFakeExplosion(Ai.Session, AmmoDef.AreaEffect.Detonation.DetonationRadius, TracerFront, AmmoDef);
                        else SUtils.CreateFakeExplosion(Ai.Session, AmmoDef.AreaEffect.AreaEffectRadius, TracerFront, AmmoDef);
                    }
                }
            }

            if (!Active)
                Ai.Session.Av.AvShotPool.Return(this);
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
            var velStep = ShootVelStep;
            var hit = !MyUtils.IsZero(Hit.HitPos);
            if (hit)
                velStep = Vector3D.Zero;

            if (shrinking)
            {
                frontPos = shrink.NewFront;
                backPos = !shrink.Last ? shrink.NewFront : TracerFront;
            }
            else
            {
                var futureStep = (Direction * ShortStepSize);
                if (!Back) futureStep -= velStep;
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
                    g.Line = new LineD(extend ? TracerFront + velStep : g.Parent.TailPos += velStep, extend ? g.Parent.TailPos : g.TailPos);
                }
                else if (i != endIdx)
                    g.Line = new LineD(g.Line.From + velStep, g.TailPos);
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

                    if (ShotFade > 0)
                        color *= MathHelper.Clamp(1f - ShotFade, 0.005f, 1f);

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
                var newTracerFront = Hit.HitPos + -(PointDir * (TracerStep * StepSize));
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
                target = TracerFront + (-Direction * (TotalLength - MathHelperD.Clamp(DistanceToLine * 6, DistanceToLine, MaxTrajectory * 0.5)));
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
                if (OnScreen != Screen.None)
                {
                    MatrixD matrix;
                    MatrixD.CreateTranslation(ref Hit.HitPos, out matrix);
                    if (weapon.HitEffects[MuzzleId] == null || weapon.HitEffects[MuzzleId].IsEmittingStopped || !Ai.Session.Av.RipMap.ContainsKey(weapon.HitEffects[MuzzleId]))
                    {
                        if (!MyParticlesManager.TryCreateParticleEffect(AmmoDef.AmmoGraphics.Particles.Hit.Name, ref matrix, ref Hit.HitPos, uint.MaxValue, out weapon.HitEffects[MuzzleId]))
                        {
                            if (weapon.HitEffects[MuzzleId] != null)
                            {
                                weapon.HitEffects[MuzzleId].Stop();
                                weapon.HitEffects[MuzzleId] = null;
                            }
                            return;
                        }

                        weapon.HitEffects[MuzzleId].UserRadiusMultiplier = AmmoDef.AmmoGraphics.Particles.Hit.Extras.Scale;
                        weapon.HitEffects[MuzzleId].UserColorMultiplier = AmmoDef.AmmoGraphics.Particles.Hit.Color;
                        var scale = MathHelper.Lerp(1, 0, (DistanceToLine * 2) / AmmoDef.AmmoGraphics.Particles.Hit.Extras.MaxDistance);
                        weapon.HitEffects[MuzzleId].WorldMatrix = matrix;
                        weapon.HitEffects[MuzzleId].UserScale = scale;
                        Vector3D.ClampToSphere(ref HitVelocity, (float)MaxSpeed);

                        var mess = Ai.Session.Av.KeenMessPool.Get();
                        mess.Effect = weapon.HitEffects[MuzzleId];
                        mess.AmmoDef = AmmoDef;
                        mess.Velocity = HitVelocity;
                        mess.LastTick = Ai.Session.Tick;
                        mess.Looping = mess.Effect.Loop;
                        Ai.Session.Av.KeensBrokenParticles.Add(mess);
                        Ai.Session.Av.RipMap.Add(mess.Effect, mess);
                    }
                    else if (weapon.HitEffects[MuzzleId] != null)
                    {
                        Ai.Session.Av.RipMap[weapon.HitEffects[MuzzleId]].LastTick = Ai.Session.Tick;
                        Ai.Session.Av.RipMap[weapon.HitEffects[MuzzleId]].Velocity = HitVelocity;

                        var scale = MathHelper.Lerp(1, 0, (DistanceToLine * 2) / AmmoDef.AmmoGraphics.Particles.Hit.Extras.MaxDistance);
                        weapon.HitEffects[MuzzleId].UserScale = scale;
                        weapon.HitEffects[MuzzleId].WorldMatrix = matrix;
                    }

                }
                else if (weapon.HitEffects[MuzzleId] != null)
                {
                    weapon.HitEffects[MuzzleId].Stop(weapon.HitEffects[MuzzleId].Loop);
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
            }

            if (!IsShrapnel && FiringSoundState == WeaponSystem.FiringSoundState.PerShot && distanceFromCameraSqr < System.FiringSoundDistSqr)
            {
                StartSoundActived = true;
                FireSound.Init(System.Values.HardPoint.Audio.FiringSound, false);
                FireEmitter.SetPosition(Origin);
                FireEmitter.Entity = FiringWeapon.MyCube;
            }
                        
            if (StartSoundActived) {
                StartSoundActived = false;
                FireEmitter.PlaySound(FireSound, true);
            }
        }

        internal void HitSoundStart()
        {
            HitSoundInitted = true;
            if (!AmmoDef.Const.AltHitSounds)
                HitSound.Init(AmmoDef.AmmoAudio.HitSound, false);
            else
            {
                var ent = HitEmitter.Entity;
                if (ent is MyCubeGrid)
                    HitSound.Init(AmmoDef.AmmoAudio.ShieldHitSound, false);
                else if (ent is IMyUpgradeModule && AmmoDef.AmmoAudio.ShieldHitSound != string.Empty)
                    HitSound.Init(AmmoDef.AmmoAudio.HitSound, false);
                else if (ent is MyVoxelBase && AmmoDef.AmmoAudio.VoxelHitSound != string.Empty)
                    HitSound.Init(AmmoDef.AmmoAudio.VoxelHitSound, false);
                else if (ent is IMyCharacter && AmmoDef.AmmoAudio.PlayerHitSound != string.Empty)
                    HitSound.Init(AmmoDef.AmmoAudio.PlayerHitSound, false);
                else if (ent is MyFloatingObject && AmmoDef.AmmoAudio.FloatingHitSound != string.Empty)
                    HitSound.Init(AmmoDef.AmmoAudio.FloatingHitSound, false);
                else HitSound.Init(AmmoDef.AmmoAudio.HitSound, false);
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
            TotalLength = MathHelperD.Clamp(MaxTracerLength + MaxGlowLength, 0.1f, MaxTrajectory);
        }

        internal void Close()
        {
            // Reset only vars that are not always set
            Hit = new Hit();
            if (AmmoSound)
            {
                TravelEmitter.StopSound(true);
                AmmoSound = false;
            }
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
            MaxTrajectory = 0;
            ShotFade = 0;
            ParentId = ulong.MaxValue;
            Dirty = false;
            AmmoSound = false;
            HitSoundActive = false;
            HitSoundInitted = false;
            StartSoundActived = false;
            IsShrapnel = false;
            HasTravelSound = false;
            HitParticle = ParticleState.None;
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
    internal struct DeferedAv
    {
        internal ProInfo Info;
        internal bool Hit;
        internal double StepSize;
        internal double VisualLength;
        internal double? ShortStepSize;
        internal Vector3D TracerFront;
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
