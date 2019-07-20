using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectiles;
namespace WeaponCore.Projectiles
{
    internal class Projectile
    {
        internal const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal MatrixD EntityMatrix = MatrixD.Identity;
        internal const int EndSteps = 2;
        internal volatile float DamagePool;
        internal volatile bool Colliding;
        internal WeaponSystem System;
        internal MyCubeBlock FiringCube;
        internal MyCubeGrid FiringGrid;
        internal GridTargetingAi Ai;
        internal ProjectileState State;
        internal EntityState ModelState;
        internal Trajectile Trajectile;
        internal Vector3D Direction;
        internal Vector3D OriginUp;
        internal Vector3D AccelDir;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Vector3D Origin;
        internal Vector3D StartSpeed;
        internal Vector3D Velocity;
        internal Vector3D AccelVelocity;
        internal Vector3D MaxVelocity;
        internal Vector3D TravelMagnitude;
        internal Vector3D LastEntityPos;
        internal Vector3D OriginTargetPos;
        internal Vector3D PredictedTargetPos;
        internal Vector3D PrevTargetPos;
        internal Vector3 PrevTargetVel;
        internal Vector3D? LastHitPos;
        internal Vector3? LastHitEntVel;
        internal double AccelLength;
        internal double MaxTrajectorySqr;
        internal double DistanceTraveled;
        internal double DistanceToTravelSqr;
        internal double LineLength;
        internal double SmartsDelayDistSqr;
        internal double DistanceFromCameraSqr;
        internal double AccelPerSec;
        internal double MaxSpeedSqr;
        internal double MaxSpeed;
        internal double MaxSpeedLength;
        internal float DesiredSpeed;
        internal float AmmoTravelSoundRangeSqr;
        internal float MaxTrajectory;
        internal int PoolId;
        internal int Age;
        internal int ChaseAge;
        internal int MaxChaseAge;
        internal int ObjectsHit;
        internal int ReSizeSteps;
        internal int GrowStep = 1;
        internal int EndStep;
        internal int ModelId;
        internal int WeaponId;
        internal int MuzzleId;
        internal int ZombieLifeTime;
        internal bool Grow;
        internal bool Shrink;
        internal bool EnableAv;
        internal bool DrawLine;
        internal bool AmmoSound;
        internal bool FirstOffScreen;
        internal bool HasTravelSound;
        internal bool ConstantSpeed;
        internal bool PositionChecked;
        internal bool MoveToAndActivate;
        internal bool LockedTarget;
        internal bool FoundTarget;
        internal bool SeekTarget;
        internal bool DynamicGuidance;
        internal bool IsBeamWeapon;
        internal bool OnScreen;
        internal bool ParticleStopped;
        internal bool ParticleLateStart;
        internal WeaponSystem.FiringSoundState FiringSoundState;
        internal AmmoTrajectory.GuidanceType Guidance;
        internal BoundingSphereD TestSphere = new BoundingSphereD(Vector3D.Zero, 200f);
        internal BoundingSphereD ModelSphereCurrent;
        internal BoundingSphereD ModelSphereLast;
        internal readonly MyTimedItemCache VoxelRayCache = new MyTimedItemCache(4000);
        internal List<MyLineSegmentOverlapResult<MyEntity>> EntityRaycastResult = null;
        internal MyEntity Entity;
        internal MyEntity Target;
        internal MyParticleEffect AmmoEffect;
        internal MyParticleEffect HitEffect;
        internal readonly MyEntity3DSoundEmitter FireEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyEntity3DSoundEmitter TravelEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly MyEntity3DSoundEmitter HitEmitter = new MyEntity3DSoundEmitter(null, false, 1f);
        internal readonly List<HitEntity> HitList = new List<HitEntity>();
        internal readonly List<MyLineSegmentOverlapResult<MyEntity>> SegmentList = new List<MyLineSegmentOverlapResult<MyEntity>>();
        internal readonly int[] TargetShuffle = new int[0];
        internal readonly int[] BlockSuffle = new int[0];
        internal int TargetShuffleLen;
        internal int BlockShuffleLen;
        internal MySoundPair FireSound = new MySoundPair();
        internal MySoundPair TravelSound = new MySoundPair();
        internal MySoundPair HitSound = new MySoundPair();

        internal void Start(bool noAv, int poolId)
        {
            PoolId = poolId;

            Position = Origin;
            AccelDir = Direction;
            var cameraStart = MyAPIGateway.Session.Camera.Position;
            Vector3D.DistanceSquared(ref cameraStart, ref Origin, out DistanceFromCameraSqr);
            var probability = System.Values.Graphics.VisualProbability;
            EnableAv = !noAv && DistanceFromCameraSqr <= Session.Instance.SyncDistSqr && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));

            EntityMatrix = MatrixD.Identity;
            ModelState = EntityState.None;
            LastEntityPos = Position;
            PrevTargetPos = PredictedTargetPos;
            PrevTargetVel = Vector3D.Zero;
            LastHitPos = null;
            LastHitEntVel = null;
            Age = 0;
            ChaseAge = 0;
            ZombieLifeTime = 0;
            ObjectsHit = 0;
            Colliding = false;
            ParticleStopped = false;
            ParticleLateStart = false;
            OnScreen = true;
            FirstOffScreen = true;
            AmmoSound = false;
            PositionChecked = false;
            EndStep = 0;
            GrowStep = 1;
            DistanceTraveled = 0;

            FiringGrid = FiringCube.CubeGrid;
            DamagePool = System.Values.Ammo.DefaultDamage;
            Guidance = System.Values.Ammo.Trajectory.Guidance;
            DynamicGuidance = Guidance != AmmoTrajectory.GuidanceType.None;

            if (Guidance == AmmoTrajectory.GuidanceType.Smart && !IsBeamWeapon)
            {
                Session.Instance.GridTargetingAIs.TryGetValue(FiringGrid, out Ai);
                MaxChaseAge = System.Values.Ammo.Trajectory.Smarts.MaxChaseTime;
            }
            else MaxChaseAge = int.MaxValue;

            LockedTarget = Target != null && !Target.MarkedForClose;
            if (Target != null && LockedTarget) OriginTargetPos = Target.PositionComp.WorldAABB.Center;

            DrawLine = System.Values.Graphics.Line.Trail;
            if (System.RangeVariance)
            {
                var min = System.Values.Ammo.Trajectory.RangeVariance.Start;
                var max = System.Values.Ammo.Trajectory.RangeVariance.End;
                MaxTrajectory = System.Values.Ammo.Trajectory.MaxTrajectory - MyUtils.GetRandomFloat(min, max);
            }
            else MaxTrajectory = System.Values.Ammo.Trajectory.MaxTrajectory;

            MaxTrajectorySqr = MaxTrajectory * MaxTrajectory;
            LineLength = System.Values.Graphics.Line.Length;

            var smartsDelayDist = LineLength * System.Values.Ammo.Trajectory.Smarts.TrackingDelay;
            SmartsDelayDistSqr = smartsDelayDist * smartsDelayDist;

            StartSpeed = FiringGrid.Physics.LinearVelocity;

            if (System.SpeedVariance && !IsBeamWeapon)
            {
                var min = System.Values.Ammo.Trajectory.SpeedVariance.Start;
                var max = System.Values.Ammo.Trajectory.SpeedVariance.End;
                DesiredSpeed = System.Values.Ammo.Trajectory.DesiredSpeed - MyUtils.GetRandomFloat(min, max);
            }
            else DesiredSpeed = System.Values.Ammo.Trajectory.DesiredSpeed;


            if (LockedTarget) FoundTarget = true;
            else if (DynamicGuidance) SeekTarget = true;
            MoveToAndActivate = FoundTarget && !IsBeamWeapon &&Guidance == AmmoTrajectory.GuidanceType.TravelTo;

            if (MoveToAndActivate)
            {
                var distancePos = PredictedTargetPos != Vector3D.Zero ? PredictedTargetPos : OriginTargetPos;
                Vector3D.DistanceSquared(ref Origin, ref distancePos, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = MaxTrajectorySqr;

            FiringSoundState = System.FiringSound;
            AmmoTravelSoundRangeSqr = System.AmmoTravelSoundDistSqr;

            if (EnableAv)
            {
                if (!IsBeamWeapon && System.AmmoTravelSound)
                {
                    HasTravelSound = true;
                    TravelSound.Init(System.Values.Audio.Ammo.TravelSound, false);
                }
                else HasTravelSound = false;

                if (System.HitSound)
                    HitSound.Init(System.Values.Audio.Ammo.HitSound, false);

                if (FiringSoundState == WeaponSystem.FiringSoundState.PerShot)
                {
                    FireSound.Init(System.Values.Audio.HardPoint.FiringSound, false);
                    FireSoundStart();
                }
            }

            ModelId = System.ModelId;
            if (ModelId == -1 || IsBeamWeapon) ModelState = EntityState.None;
            else
            {
                if (EnableAv)
                {
                    ModelState = EntityState.Exists;
                    ModelSphereCurrent.Radius = Entity.PositionComp.WorldVolume.Radius * 2;
                    ModelSphereLast.Radius = Entity.PositionComp.WorldVolume.Radius * 2;
                }
                else ModelState = EntityState.NoDraw;
            }

            var accelPerSec = System.Values.Ammo.Trajectory.AccelPerSec;
            ConstantSpeed = accelPerSec <= 0;
            AccelPerSec = accelPerSec > 0 ? accelPerSec : DesiredSpeed; 
            MaxVelocity = StartSpeed + (Direction * DesiredSpeed);
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            MaxSpeedLength = MaxSpeed * StepConst;
            AccelLength = System.Values.Ammo.Trajectory.AccelPerSec * StepConst;
            AccelVelocity = (Direction * AccelLength);
            Velocity = ConstantSpeed ? MaxVelocity : StartSpeed + AccelVelocity;
            TravelMagnitude = Velocity * StepConst;
            if (!IsBeamWeapon)
            {
                var reSizeSteps = (int) (LineLength / MaxSpeedLength);
                ReSizeSteps = ModelState == EntityState.None && reSizeSteps > 0 ? reSizeSteps : 1;
                Grow = ReSizeSteps > 1 || AccelLength > 0 && AccelLength < LineLength;
                Shrink = Grow;
                State = ProjectileState.Alive;
            }
            else State = ProjectileState.OneAndDone;

            if (System.AmmoParticle && EnableAv && !IsBeamWeapon) PlayAmmoParticle();
        }

        internal void FireSoundStart()
        {
            FireEmitter.SetPosition(Origin);
            FireEmitter.PlaySoundWithDistance(FireSound.SoundId, false, false, false, true, false, false, false);
        }

        internal void AmmoSoundStart()
        {
            TravelEmitter.SetPosition(Position);
            TravelEmitter.PlaySoundWithDistance(TravelSound.SoundId, false, false, false, true, false, false, false);
            AmmoSound = true;
        }

        internal void ProjectileClose(Projectiles manager, int poolId)
        {
            if (!EnableAv && ModelId == -1)
            {
                HitList.Clear();
                manager.ProjectilePool[poolId].MarkForDeallocate(this);
                State = ProjectileState.Dead;
            }
            else State = ProjectileState.Ending;
        }

        internal void Stop(Projectiles manager, int poolId)
        {
            if (EndStep++ >= EndSteps)
            {
                if (EnableAv)
                {
                    if (System.AmmoParticle) DisposeAmmoEffect();
                    HitEffects();
                    if (AmmoSound) TravelEmitter.StopSound(false, true);
                }

                HitList.Clear();
                manager.ProjectilePool[poolId].MarkForDeallocate(this);
                State = ProjectileState.Dead;
            }
        }

        internal bool CloseModel(Projectiles manager, int poolId)
        {
            EntityMatrix = MatrixD.Identity;
            manager.DrawProjectiles[poolId].Add(new DrawProjectile(this, null, true));
            manager.EntityPool[poolId][ModelId].MarkForDeallocate(Entity);
            ModelState = EntityState.None;
            return true;
        }

        internal bool EndChase()
        {
            Log.Line("end chase");
            ChaseAge = Age;
            var reaquire = GridTargetingAi.ReacquireTarget(this);
            if (!reaquire) Target = null;
            return reaquire;
        }


        internal void UpdateZombie(bool reset = false)
        {
            if (reset) ZombieLifeTime = 0;
            else
            {
                PrevTargetPos = PredictedTargetPos;
                if (ZombieLifeTime++ > System.TargetLossTime) DistanceToTravelSqr = DistanceTraveled * DistanceTraveled;
            }
        }

        internal void HitEffects()
        {
            if (Colliding)
            {
                if (System.HitParticle && !IsBeamWeapon) PlayHitParticle();
                if (System.HitSound)
                {
                    HitEmitter.SetPosition(Position);
                    HitEmitter.CanPlayLoopSounds = false;
                    HitEmitter.PlaySoundWithDistance(HitSound.SoundId, true, false, false, true, true, false, false);
                }
            }
            Colliding = false;
        }

        internal void PlayAmmoParticle()
        {
            //Log.Line($"Particle Start:{Age} - EntMat!=Id:{EntityMatrix != MatrixD.Identity} - Ent!=Id:{Entity.WorldMatrix != MatrixD.Identity}");

            if (Age == 0 && !ParticleLateStart)
            {
                TestSphere.Center = Position;
                if (!MyAPIGateway.Session.Camera.IsInFrustum(ref TestSphere))
                {
                    ParticleLateStart = true;
                    return;
                }
            }
            var parentId = uint.MaxValue;
            MatrixD matrix;
            if (ModelState == EntityState.Exists)
            {
                matrix = MatrixD.CreateWorld(Position, AccelDir, Entity.PositionComp.WorldMatrix.Up);
                var offVec = Position + Vector3D.Rotate(System.Values.Graphics.Particles.Ammo.Offset, matrix);
                matrix.Translation = offVec;
                EntityMatrix = matrix;
            }
            else
            {
                matrix = MatrixD.CreateWorld(Position, AccelDir, OriginUp);
                var offVec = Position + Vector3D.Rotate(System.Values.Graphics.Particles.Ammo.Offset, matrix);
                matrix.Translation = offVec;
            }

            MyParticlesManager.TryCreateParticleEffect(System.Values.Graphics.Particles.Ammo.Name, ref matrix, ref Position, parentId, out AmmoEffect); // 15, 16, 24, 25, 28, (31, 32) 211 215 53
            if (AmmoEffect == null) return;
            //Log.Line($"create particle: p:{Position} - v:{Velocity} - c:{ConstantSpeed} - dSpeed:{DesiredSpeed} - sSpeed:{StartSpeed.Length()} - age:{Age}");
            AmmoEffect.DistanceMax = System.Values.Graphics.Particles.Ammo.Extras.MaxDistance;
            AmmoEffect.UserColorMultiplier = System.Values.Graphics.Particles.Ammo.Color;
            //var reScale = (float)Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
            var reScale = 1;
            var scaler = reScale < 1 ? reScale : 1;

            AmmoEffect.UserRadiusMultiplier = System.Values.Graphics.Particles.Ammo.Extras.Scale * scaler;
            AmmoEffect.UserEmitterScale = 1 * scaler;
            if (ConstantSpeed) AmmoEffect.Velocity = Velocity;
            ParticleStopped = false;
            ParticleLateStart = false;
        }


        private void PlayHitParticle()
        {
            if (HitEffect != null) DisposeHitEffect();
            if (LastHitPos.HasValue)
            {
                var pos = LastHitPos.Value;
                var matrix = MatrixD.CreateTranslation(pos);
                var parentId = uint.MaxValue;
                MyParticlesManager.TryCreateParticleEffect(System.Values.Graphics.Particles.Hit.Name, ref matrix, ref pos, parentId, out HitEffect);
                if (HitEffect == null) return;
                HitEffect.Loop = false;
                HitEffect.DurationMax = System.Values.Graphics.Particles.Hit.Extras.MaxDuration;
                HitEffect.DistanceMax = System.Values.Graphics.Particles.Hit.Extras.MaxDistance;
                HitEffect.UserColorMultiplier = System.Values.Graphics.Particles.Hit.Color;
                //var reScale = (float)Math.Log(195312.5, DistanceFromCameraSqr); // wtf is up with particles and camera distance
                var reScale = 1;
                var scaler = reScale < 1 ? reScale : 1;

                HitEffect.UserRadiusMultiplier = System.Values.Graphics.Particles.Hit.Extras.Scale * scaler;
                HitEffect.UserEmitterScale = 1 * scaler;
                var hitVel = LastHitEntVel ?? Vector3.Zero;
                Vector3.ClampToSphere(ref hitVel, (float)MaxSpeed);
                HitEffect.Velocity = hitVel;
            }
        }

        internal void DisposeAmmoEffect(bool pause = false)
        {
            if (AmmoEffect != null)
            {
                AmmoEffect.Stop(false);
                AmmoEffect = null;
            }
            if (pause) ParticleStopped = true;
        }

        private void DisposeHitEffect()
        {
            if (HitEffect != null)
            {
                HitEffect.Stop(false);
                HitEffect = null;
            }
        }

        internal enum ProjectileState
        {
            Start,
            Alive,
            Ending,
            Dead,
            OneAndDone,
            Depleted,
        }

        internal enum EntityState
        {
            Exists,
            NoDraw,
            None
        }
    }
}