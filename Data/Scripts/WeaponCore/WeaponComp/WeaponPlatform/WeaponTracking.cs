﻿using System;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.HardPointDef;
using static WeaponCore.Support.GridAi.FakeTarget;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.TrajectoryDef;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal static bool CanShootTarget(Weapon weapon, ref Vector3D targetCenter, Vector3D targetLinVel, Vector3D targetAccel, out Vector3D targetPos, bool checkSelfHit = false, MyEntity target = null)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretMode ? weapon : weapon.Comp.TrackingWeapon;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var validEstimate = true;
            if (prediction != Prediction.Off && !weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
                targetPos = TrajectoryEstimation(weapon, targetCenter, targetLinVel, targetAccel, out validEstimate);
            else
                targetPos = targetCenter;
            var targetDir = targetPos - weapon.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            var inRange = rangeToTarget <= weapon.MaxTargetDistanceSqr && rangeToTarget >= weapon.MinTargetDistanceSqr;

            bool canTrack;
            bool isTracking;

            if (weapon == trackingWeapon)
                canTrack = validEstimate && MathFuncs.WeaponLookAt(weapon, ref targetDir, rangeToTarget, false, true, out isTracking);
            else
                canTrack = validEstimate && MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);

            bool selfHit = false;
            weapon.LastHitInfo = null;
            if (checkSelfHit && target != null) {

                var testLine = new LineD(targetCenter, weapon.BarrelOrigin);
                var predictedMuzzlePos = testLine.To + (-testLine.Direction * weapon.MuzzleDistToBarrelCenter);
                var ai = weapon.Comp.Ai;
                var localPredictedPos = Vector3I.Round(Vector3D.Transform(predictedMuzzlePos, ai.MyGrid.PositionComp.WorldMatrixNormalizedInv) * ai.MyGrid.GridSizeR);

                MyCube cube;
                var noCubeAtPosition = !ai.MyGrid.TryGetCube(localPredictedPos, out cube);
                if (noCubeAtPosition || cube.CubeBlock == weapon.Comp.MyCube.SlimBlock) {

                    var noCubeInLine = !ai.MyGrid.GetIntersectionWithLine(ref testLine, ref ai.GridHitInfo);
                    var noCubesInLineOrHitSelf = noCubeInLine || ai.GridHitInfo.Position == weapon.Comp.MyCube.Position;

                    if (noCubesInLineOrHitSelf) {

                        weapon.System.Session.Physics.CastRay(predictedMuzzlePos, testLine.From, out weapon.LastHitInfo, CollisionLayers.DefaultCollisionLayer);
                        
                        if (weapon.LastHitInfo != null && weapon.LastHitInfo.HitEntity == ai.MyGrid)
                            selfHit = true;
                    }
                }
                else selfHit = true;
            }

            return !selfHit && (inRange && canTrack || weapon.Comp.Data.Repo.Base.State.TrackingReticle);
        }

        internal static bool CheckSelfHit(Weapon w, ref Vector3D targetPos, ref Vector3D testPos, out Vector3D predictedMuzzlePos)
        {

            var testLine = new LineD(targetPos, testPos);
            predictedMuzzlePos = testLine.To + (-testLine.Direction * w.MuzzleDistToBarrelCenter);
            var ai = w.Comp.Ai;
            var localPredictedPos = Vector3I.Round(Vector3D.Transform(predictedMuzzlePos, ai.MyGrid.PositionComp.WorldMatrixNormalizedInv) * ai.MyGrid.GridSizeR);

            MyCube cube;
            var noCubeAtPosition = !ai.MyGrid.TryGetCube(localPredictedPos, out cube);
            if (noCubeAtPosition || cube.CubeBlock == w.Comp.MyCube.SlimBlock) {

                var noCubeInLine = !ai.MyGrid.GetIntersectionWithLine(ref testLine, ref ai.GridHitInfo);
                var noCubesInLineOrHitSelf = noCubeInLine || ai.GridHitInfo.Position == w.Comp.MyCube.Position;

                if (noCubesInLineOrHitSelf) {

                    w.System.Session.Physics.CastRay(predictedMuzzlePos, testLine.From, out w.LastHitInfo, CollisionLayers.DefaultCollisionLayer);

                    if (w.LastHitInfo != null && w.LastHitInfo.HitEntity == ai.MyGrid)
                        return true;
                }
            }
            else return true;

            return false;
        }

        internal static void LeadTarget(Weapon weapon, MyEntity target, out Vector3D targetPos, out bool couldHit, out bool willHit)
        {
            var vel = target.Physics.LinearVelocity;
            var accel = target.Physics.LinearAcceleration;
            var trackingWeapon = weapon.TurretMode || weapon.Comp.TrackingWeapon == null ? weapon : weapon.Comp.TrackingWeapon;

            var box = target.PositionComp.LocalAABB;
            var obb = new MyOrientedBoundingBoxD(box, target.PositionComp.WorldMatrixRef);

            var validEstimate = true;
            var advancedMode = weapon.ActiveAmmoDef.AmmoDef.Trajectory.AccelPerSec > 0 || weapon.Comp.Ai.InPlanetGravity && weapon.ActiveAmmoDef.AmmoDef.Const.FeelsGravity;
            if (!weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
                targetPos = TrajectoryEstimation(weapon, obb.Center, vel, accel, out validEstimate, true, advancedMode);
            else
                targetPos = obb.Center;

            obb.Center = targetPos;
            weapon.TargetBox = obb;

            var obbAbsMax = obb.HalfExtent.AbsMax();
            var maxRangeSqr = obbAbsMax + weapon.MaxTargetDistance;
            var minRangeSqr = obbAbsMax + weapon.MinTargetDistance;

            maxRangeSqr *= maxRangeSqr;
            minRangeSqr *= minRangeSqr;
            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            couldHit = validEstimate && rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr;

            bool canTrack = false;
            if (validEstimate && rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr)
            {
                var targetDir = targetPos - weapon.MyPivotPos;
                if (weapon == trackingWeapon)
                {
                    double checkAzimuth;
                    double checkElevation;

                    MathFuncs.GetRotationAngles(ref targetDir, ref weapon.WeaponConstMatrix, out checkAzimuth, out checkElevation);

                    var azConstraint = Math.Min(weapon.MaxAzToleranceRadians, Math.Max(weapon.MinAzToleranceRadians, checkAzimuth));
                    var elConstraint = Math.Min(weapon.MaxElToleranceRadians, Math.Max(weapon.MinElToleranceRadians, checkElevation));

                    Vector3D constraintVector;
                    Vector3D.CreateFromAzimuthAndElevation(azConstraint, elConstraint, out constraintVector);
                    Vector3D.Rotate(ref constraintVector, ref weapon.WeaponConstMatrix, out constraintVector);

                    var testRay = new RayD(ref weapon.MyPivotPos, ref constraintVector);
                    if (obb.Intersects(ref testRay) != null) canTrack = true;

                    if (weapon.Comp.Debug)
                        weapon.LimitLine = new LineD(weapon.MyPivotPos, weapon.MyPivotPos + (constraintVector * weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory));
                }
                else
                    canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);
            }
            willHit =  canTrack;
        }

        internal static bool CanShootTargetObb(Weapon weapon, MyEntity entity, Vector3D targetLinVel, Vector3D targetAccel, out Vector3D targetPos)
        {
            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretMode ? weapon : weapon.Comp.TrackingWeapon;

            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var box = entity.PositionComp.LocalAABB;
            var obb = new MyOrientedBoundingBoxD(box, entity.PositionComp.WorldMatrixRef);

            var validEstimate = true;
            if (prediction != Prediction.Off && !weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
                targetPos = TrajectoryEstimation(weapon, obb.Center, targetLinVel, targetAccel, out validEstimate);
            else
                targetPos = obb.Center;

            obb.Center = targetPos;
            weapon.TargetBox = obb;

            var obbAbsMax = obb.HalfExtent.AbsMax();
            var maxRangeSqr = obbAbsMax + weapon.MaxTargetDistance;
            var minRangeSqr = obbAbsMax + weapon.MinTargetDistance;

            maxRangeSqr *= maxRangeSqr;
            minRangeSqr *= minRangeSqr;
            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            bool canTrack = false;
            if (validEstimate && rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr)
            {
                var targetDir = targetPos - weapon.MyPivotPos;
                if (weapon == trackingWeapon)
                {
                    double checkAzimuth;
                    double checkElevation;

                    MathFuncs.GetRotationAngles(ref targetDir, ref weapon.WeaponConstMatrix, out checkAzimuth, out checkElevation);

                    var azConstraint = Math.Min(weapon.MaxAzToleranceRadians, Math.Max(weapon.MinAzToleranceRadians, checkAzimuth));
                    var elConstraint = Math.Min(weapon.MaxElToleranceRadians, Math.Max(weapon.MinElToleranceRadians, checkElevation));

                    Vector3D constraintVector;
                    Vector3D.CreateFromAzimuthAndElevation(azConstraint, elConstraint, out constraintVector);
                    Vector3D.Rotate(ref constraintVector, ref weapon.WeaponConstMatrix, out constraintVector);

                    var testRay = new RayD(ref weapon.MyPivotPos, ref constraintVector);
                    if (obb.Intersects(ref testRay) != null) canTrack = true;

                    if (weapon.Comp.Debug)
                        weapon.LimitLine = new LineD(weapon.MyPivotPos, weapon.MyPivotPos + (constraintVector * weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory));
                }
                else
                    canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);
            }
            return canTrack;
        }

        internal static bool TargetAligned(Weapon weapon, Target target, out Vector3D targetPos)
        {
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            Vector3D targetCenter;

            FakeWorldTargetInfo fakeTargetInfo = null;
            if (weapon.Comp.Data.Repo.Base.Set.Overrides.Control != GroupOverrides.ControlModes.Auto && weapon.ValidFakeTargetInfo(weapon.Comp.Data.Repo.Base.State.PlayerId, out fakeTargetInfo)) {
                targetCenter = fakeTargetInfo.WorldPosition;
            }
            else if (target.IsProjectile)
                targetCenter = target.Projectile?.Position ?? Vector3D.Zero;
            else if (!target.IsFakeTarget)
                targetCenter = target.Entity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            else
                targetCenter = Vector3D.Zero;

            var validEstimate = true;
            if (weapon.System.Prediction != Prediction.Off && (!weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)) {

                if (fakeTargetInfo != null) {
                    targetLinVel = fakeTargetInfo.LinearVelocity;
                    targetAccel = fakeTargetInfo.Acceleration;
                }
                else {

                    var cube = target.Entity as MyCubeBlock;
                    var topMostEnt = cube != null ? cube.CubeGrid : target.Entity;

                    if (target.Projectile != null) {
                        targetLinVel = target.Projectile.Velocity;
                        targetAccel = target.Projectile.AccelVelocity;
                    }
                    else if (topMostEnt?.Physics != null) {
                        targetLinVel = topMostEnt.Physics.LinearVelocity;
                        targetAccel = topMostEnt.Physics.LinearAcceleration;
                    }
                }
                if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
                if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;
                targetPos = TrajectoryEstimation(weapon, targetCenter, targetLinVel, targetAccel, out validEstimate);
            }
            else
                targetPos = targetCenter;

            var targetDir = targetPos - weapon.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            var inRange = rangeToTarget <= weapon.MaxTargetDistanceSqr && rangeToTarget >= weapon.MinTargetDistanceSqr;

            var isAligned = validEstimate && (inRange || weapon.Comp.Data.Repo.Base.State.TrackingReticle) && MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);

            weapon.Target.TargetPos = targetPos;
            weapon.Target.IsAligned = isAligned;
            return isAligned;
        }

        internal static bool TrackingTarget(Weapon w, Target target, out bool targetLock)
        {
            Vector3D targetPos;
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            Vector3D targetCenter;
            targetLock = false;

            var baseData = w.Comp.Data.Repo.Base;
            var session = w.System.Session;
            var ai = w.Comp.Ai;

            FakeWorldTargetInfo fakeTargetInfo = null;
            if (baseData.Set.Overrides.Control != GroupOverrides.ControlModes.Auto && w.ValidFakeTargetInfo(baseData.State.PlayerId, out fakeTargetInfo)) 
                targetCenter = fakeTargetInfo.WorldPosition;
            else if (target.IsProjectile)
                targetCenter = target.Projectile?.Position ?? Vector3D.Zero;
            else if (!target.IsFakeTarget)
                targetCenter = target.Entity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            else
                targetCenter = Vector3D.Zero;

            var validEstimate = true;
            if (w.System.Prediction != Prediction.Off && !w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && w.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0) {

                if (fakeTargetInfo != null) {
                    targetLinVel = fakeTargetInfo.LinearVelocity;
                    targetAccel = fakeTargetInfo.Acceleration;
                }
                else {
                    var cube = target.Entity as MyCubeBlock;
                    var topMostEnt = cube != null ? cube.CubeGrid : target.Entity;
                    
                    if (target.Projectile != null) {
                        targetLinVel = target.Projectile.Velocity;
                        targetAccel = target.Projectile.AccelVelocity;
                    }
                    else if (topMostEnt?.Physics != null) {
                        targetLinVel = topMostEnt.Physics.LinearVelocity;
                        targetAccel = topMostEnt.Physics.LinearAcceleration;
                    }
                }
                if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
                if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;
                targetPos = TrajectoryEstimation(w, targetCenter, targetLinVel, targetAccel, out validEstimate);
            }
            else
                targetPos = targetCenter;

            w.Target.TargetPos = targetPos;

            double rangeToTargetSqr;
            Vector3D.DistanceSquared(ref targetPos, ref w.MyPivotPos, out rangeToTargetSqr);

            var targetDir = targetPos - w.MyPivotPos;
            var readyToTrack = validEstimate && !w.Comp.ResettingSubparts && (baseData.State.TrackingReticle || rangeToTargetSqr <= w.MaxTargetDistanceSqr && rangeToTargetSqr >= w.MinTargetDistanceSqr);
            
            var locked = true;
            var isTracking = false;
            if (readyToTrack && baseData.State.Control != CompStateValues.ControlMode.Camera) {

                if (MathFuncs.WeaponLookAt(w, ref targetDir, rangeToTargetSqr, true, false, out isTracking)) {

                    w.ReturingHome = false;
                    locked = false;
                    w.AimBarrel();
                }
            }
            
            w.Rotating = !locked;

            if (baseData.State.Control == CompStateValues.ControlMode.Camera)
                return isTracking;

            var isAligned = false;

            if (isTracking)
                isAligned = locked || MathFuncs.IsDotProductWithinTolerance(ref w.MyPivotFwd, ref targetDir, w.AimingTolerance);

            var wasAligned = w.Target.IsAligned;
            w.Target.IsAligned = isAligned;
            var alignedChange = wasAligned != isAligned;
            if (w.System.DesignatorWeapon && session.IsServer && alignedChange) { 
                for (int i = 0; i < w.Comp.Platform.Weapons.Length; i++) {
                    var weapon = w.Comp.Platform.Weapons[i];
                    if (isAligned && !weapon.System.DesignatorWeapon)
                        weapon.Target.Reset(session.Tick, Target.States.Designator);
                    else if (!isAligned && weapon.System.DesignatorWeapon)
                        weapon.Target.Reset(session.Tick, Target.States.Designator);
                }
            }

            targetLock = isTracking && w.Target.IsAligned;

            if (session.IsServer && baseData.Set.Overrides.Repel && ai.TargetingInfo.DroneInRange && !target.IsDrone && (session.AwakeCount == w.Acquire.SlotId || ai.Construct.RootAi.Construct.LastDroneTick == session.Tick) && GridAi.SwitchToDrone(w))
                return true;

            var rayCheckTest = !w.Comp.Session.IsClient && targetLock && (baseData.State.Control == CompStateValues.ControlMode.None || baseData.State.Control == CompStateValues.ControlMode.Ui) && w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance != GuidanceType.Smart && (!w.Casting && session.Tick - w.Comp.LastRayCastTick > 29 || w.System.Values.HardPoint.Other.MuzzleCheck && session.Tick - w.LastMuzzleCheck > 29);

            if (rayCheckTest && !w.RayCheckTest())
                return false;
            
            return isTracking;
        }

        private const int LosMax = 10;
        private int _losAngle = 11;
        private bool _increase;
        private int GetAngle()
        {
            if (_increase && _losAngle + 1 <= LosMax)
                ++_losAngle;
            else if (_increase) {
                _increase = false;
                _losAngle = 9;
            }
            else if (_losAngle - 1 > 0)
                --_losAngle;
            else {
                _increase = true;
                _losAngle = 2;
            }
            return _losAngle;
        }

        public bool SmartLos()
        {
            _losAngle = 11;
            Comp.Data.Repo.Base.Set.Overrides.Debug = false;
            PauseShoot = false;
            LastSmartLosCheck = Comp.Ai.Session.Tick;
            if (PosChangedTick != System.Session.Tick)
                UpdatePivotPos();
            var info = GetScope.Info;

            var checkLevel = Comp.Ai.IsStatic ? 1 : 5;
            bool losBlocekd = false;
            for (int j = 0; j < 10; j++) {

                if (losBlocekd)
                    break;

                var angle = GetAngle();
                int blockedDir = 0;
                for (int i = 0; i < checkLevel; i++) {

                    var source = GetSmartLosPosition(i, ref info, angle);

                    IHitInfo hitInfo;
                    Comp.Ai.Session.Physics.CastRay(source, info.Position, out hitInfo, 15, false);
                    var grid = hitInfo?.HitEntity?.GetTopMostParent() as MyCubeGrid;
                    if (grid != null && grid.IsInSameLogicalGroupAs(Comp.Ai.MyGrid) && grid.GetTargetedBlock(hitInfo.Position + (-info.Direction * 0.1f)) != Comp.MyCube.SlimBlock) {

                        if (i == 0)
                            blockedDir = 5;
                        else
                            ++blockedDir;

                        if (blockedDir >= 4 || i > 0 && i > blockedDir) 
                            break;
                    }
                }
                losBlocekd = blockedDir >= 4;
            }

            PauseShoot = losBlocekd;

            return !PauseShoot;
        }

        private Vector3D GetSmartLosPosition(int i, ref Dummy.DummyInfo info, int degrees)
        {
            double angle = MathHelperD.ToRadians(degrees);
            var perpDir = Vector3D.CalculatePerpendicularVector(info.Direction);
            Vector3D up;
            Vector3D.Normalize(ref perpDir, out up);
            Vector3D right;
            Vector3D.Cross(ref info.Direction, ref up, out right);
            var offset = Math.Tan(angle); // angle better be in radians

            var destPos = info.Position;

            switch (i)
            {
                case 0:
                    return destPos + (info.Direction * Comp.Ai.GridVolume.Radius);
                case 1:
                    return destPos + ((info.Direction + up * offset) * Comp.Ai.GridVolume.Radius);
                case 2:
                    return destPos + ((info.Direction - up * offset) * Comp.Ai.GridVolume.Radius);
                case 3:
                    return destPos + ((info.Direction + right * offset) * Comp.Ai.GridVolume.Radius);
                case 4:
                    return destPos + ((info.Direction - right * offset) * Comp.Ai.GridVolume.Radius);
            }

            return Vector3D.Zero;
        }

        internal void SmartLosDebug()
        {
            if (PosChangedTick != System.Session.Tick)
                UpdatePivotPos();
            var info = GetScope.Info;

            var checkLevel = Comp.Ai.IsStatic ? 1 : 5;
            var angle = Comp.Session.Tick20 ? GetAngle() : _losAngle;
            for (int i = 0; i < checkLevel; i++)
            {
                var source = GetSmartLosPosition(i, ref info, angle);
                IHitInfo hitInfo;
                Comp.Ai.Session.Physics.CastRay(source, info.Position, out hitInfo, 15, false);
                var grid = hitInfo?.HitEntity?.GetTopMostParent() as MyCubeGrid;
                var hit = grid != null && grid.IsInSameLogicalGroupAs(Comp.Ai.MyGrid) && grid.GetTargetedBlock(hitInfo.Position + (-info.Direction * 0.1f)) != Comp.MyCube.SlimBlock;

                var line = new LineD(source, info.Position);
                DsDebugDraw.DrawLine(line, hit ? Color.Red : Color.Blue, 0.05f);
            }
        }

        internal static Vector3D TrajectoryEstimation(Weapon weapon, Vector3D targetPos, Vector3D targetVel, Vector3D targetAcc, out bool valid, bool overrideMode = false, bool setAdvOverride = false)
        {
            valid = true;
            var ai = weapon.Comp.Ai;
            var session = ai.Session;
            var ammoDef = weapon.ActiveAmmoDef.AmmoDef;
            if (ai.VelocityUpdateTick != session.Tick) {
                ai.GridVel = ai.MyGrid.Physics?.LinearVelocity ?? Vector3D.Zero;
                ai.IsStatic = ai.MyGrid.Physics?.IsStatic ?? false;
                ai.VelocityUpdateTick = session.Tick;
            }

            if (ammoDef.Const.FeelsGravity && session.Tick - weapon.GravityTick > 119) {
                weapon.GravityTick = session.Tick;
                float interference;
                weapon.GravityPoint = session.Physics.CalculateNaturalGravityAt(weapon.MyPivotPos, out interference);
            }

            var gravityMultiplier = ammoDef.Const.FeelsGravity && !MyUtils.IsZero(weapon.GravityPoint) ? ammoDef.Trajectory.GravityMultiplier : 0f;
            var targetMaxSpeed = weapon.Comp.Session.MaxEntitySpeed;
            var shooterPos = weapon.MyPivotPos;

            var shooterVel = (Vector3D)weapon.Comp.Ai.GridVel;
            var projectileMaxSpeed = ammoDef.Const.DesiredProjectileSpeed;
            var projectileInitSpeed = ammoDef.Trajectory.AccelPerSec * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            var projectileAccMag = ammoDef.Trajectory.AccelPerSec;
            var gravity = weapon.GravityPoint;
            var basic = weapon.System.Prediction != Prediction.Advanced && !overrideMode || overrideMode && !setAdvOverride;
            Vector3D deltaPos = targetPos - shooterPos;
            Vector3D deltaVel = targetVel - shooterVel;
            Vector3D deltaPosNorm;
            if (Vector3D.IsZero(deltaPos)) deltaPosNorm = Vector3D.Zero;
            else if (Vector3D.IsUnit(ref deltaPos)) deltaPosNorm = deltaPos;
            else Vector3D.Normalize(ref deltaPos, out deltaPosNorm);

            double closingSpeed;
            Vector3D.Dot(ref deltaVel, ref deltaPosNorm, out closingSpeed);

            Vector3D closingVel = closingSpeed * deltaPosNorm;
            Vector3D lateralVel = deltaVel - closingVel;
            double projectileMaxSpeedSqr = projectileMaxSpeed * projectileMaxSpeed;
            double ttiDiff = projectileMaxSpeedSqr - lateralVel.LengthSquared();

            if (ttiDiff < 0) {
                valid = false;
                return targetPos;
            }

            double projectileClosingSpeed = Math.Sqrt(ttiDiff) - closingSpeed;

            double closingDistance;
            Vector3D.Dot(ref deltaPos, ref deltaPosNorm, out closingDistance);

            double timeToIntercept = ttiDiff < 0 ? 0 : closingDistance / projectileClosingSpeed;

            if (timeToIntercept < 0) {
                valid = false;
                return targetPos;
            }

            double maxSpeedSqr = targetMaxSpeed * targetMaxSpeed;
            double shooterVelScaleFactor = 1;
            bool projectileAccelerates = projectileAccMag > 1e-6;
            bool hasGravity = gravityMultiplier > 1e-6 && !MyUtils.IsZero(weapon.GravityPoint);

            if (!basic && projectileAccelerates)
                shooterVelScaleFactor = Math.Min(1, (projectileMaxSpeed - projectileInitSpeed) / projectileAccMag);

            Vector3D estimatedImpactPoint = targetPos + timeToIntercept * (targetVel - shooterVel * shooterVelScaleFactor);
            if (basic) return estimatedImpactPoint;
            Vector3D aimDirection = estimatedImpactPoint - shooterPos;

            Vector3D projectileVel = shooterVel;
            Vector3D projectilePos = shooterPos;

            Vector3D aimDirectionNorm;
            if (projectileAccelerates) {
                
                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else aimDirectionNorm = Vector3D.Normalize(aimDirection);
                projectileVel += aimDirectionNorm * projectileInitSpeed;
            }
            else {
                
                if (targetAcc.LengthSquared() < 1 && !hasGravity)
                    return estimatedImpactPoint;

                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else Vector3D.Normalize(ref aimDirection, out aimDirectionNorm);
                projectileVel += aimDirectionNorm * projectileMaxSpeed;
            }

            var count = projectileAccelerates ? 600 : hasGravity ? 320 : 60;

            double dt = Math.Max(MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS, timeToIntercept / count); // This can be a const somewhere
            double dtSqr = dt * dt;
            Vector3D targetAccStep = targetAcc * dt;
            Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt;

            Vector3D aimOffset = Vector3D.Zero;
            double minTime = 0;
            
            for (int i = 0; i < count; ++i) {
                
                targetVel += targetAccStep;

                if (targetVel.LengthSquared() > maxSpeedSqr) {
                    Vector3D targetNormVel;
                    Vector3D.Normalize(ref targetVel, out targetNormVel);
                    targetVel = targetNormVel * targetMaxSpeed;

                }

                targetPos += targetVel * dt;
                if (projectileAccelerates) {
                    
                    projectileVel += projectileAccStep;
                    if (projectileVel.LengthSquared() > projectileMaxSpeedSqr) {
                        Vector3D pNormVel;
                        Vector3D.Normalize(ref projectileVel, out pNormVel);
                        projectileVel = pNormVel * projectileMaxSpeed;
                    }
                }

                projectilePos += projectileVel * dt;
                Vector3D diff = (targetPos - projectilePos);
                double diffLenSq = diff.LengthSquared();
                aimOffset = diff;
                minTime = dt * (i + 1);

                if (diffLenSq < projectileMaxSpeedSqr * dtSqr || Vector3D.Dot(diff, aimDirectionNorm) < 0)
                    break;
            }
            Vector3D perpendicularAimOffset = aimOffset - Vector3D.Dot(aimOffset, aimDirectionNorm) * aimDirectionNorm;
            Vector3D gravityOffset = hasGravity ? -0.5 * minTime * minTime * gravity : Vector3D.Zero;
            return estimatedImpactPoint + perpendicularAimOffset + gravityOffset;
        }

        public void ManualShootRayCallBack(IHitInfo hitInfo)
        {
            Casting = false;
            var masterWeapon = TrackTarget ? this : Comp.TrackingWeapon;

            var grid = hitInfo.HitEntity as MyCubeGrid;
            if (grid != null)
            {
                if (grid.IsSameConstructAs(Comp.MyCube.CubeGrid))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed, false);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed, false);
                }
            }
        }

        public bool HitFriendlyShield(Vector3D weaponPos, Vector3D targetPos, Vector3D dir)
        {
            var testRay = new RayD(weaponPos, dir);
            Comp.Ai.TestShields.Clear();
            var checkDistanceSqr = Vector3.DistanceSquared(targetPos, weaponPos);

            for (int i = 0; i < Comp.Ai.NearByFriendlyShields.Count; i++)
            {
                var shield = Comp.Ai.NearByFriendlyShields[i];
                var dist = testRay.Intersects(shield.PositionComp.WorldVolume);
                if (dist != null && dist.Value * dist.Value <= checkDistanceSqr)
                    Comp.Ai.TestShields.Add(shield);
            }

            if (Comp.Ai.TestShields.Count == 0)
                return false;

            var result = Comp.Ai.Session.SApi.IntersectEntToShieldFast(Comp.Ai.TestShields, testRay, true, false, Comp.Ai.AiOwner, checkDistanceSqr);

            return result.Item1 && result.Item2 > 0;
        }

        public bool MuzzleHitSelf()
        {
            for (int i = 0; i < Muzzles.Length; i++)
            {
                var m = Muzzles[i];
                var grid = Comp.Ai.MyGrid;
                var dummy = Dummies[i];
                var newInfo = dummy.Info;
                m.Direction = newInfo.Direction;
                m.Position = newInfo.Position;
                m.LastUpdateTick = Comp.Session.Tick;

                var start = m.Position;
                var end = m.Position + (m.Direction * grid.PositionComp.LocalVolume.Radius);

                Vector3D? hit;
                if (GridIntersection.BresenhamGridIntersection(grid, ref start, ref end, out hit, Comp.MyCube, Comp.Ai))
                    return true;
            }
            return false;
        }

        private bool RayCheckTest()
        {
            var trackingCheckPosition = GetScope.Info.Position;

            if (System.Session.DebugLos && Target.Entity != null)
            {
                var trackPos = BarrelOrigin + (MyPivotFwd * MuzzleDistToBarrelCenter);
                var targetTestPos = Target.Entity.PositionComp.WorldAABB.Center;
                var topEntity = Target.Entity.GetTopMostParent();
                IHitInfo hitInfo;
                if (System.Session.Physics.CastRay(trackPos, targetTestPos, out hitInfo) && hitInfo.HitEntity == topEntity)
                {
                    var hitPos = hitInfo.Position;
                    double closestDist;
                    MyUtils.GetClosestPointOnLine(ref trackingCheckPosition, ref targetTestPos, ref hitPos, out closestDist);
                    var tDir = Vector3D.Normalize(targetTestPos - trackingCheckPosition);
                    var closestPos = trackingCheckPosition + (tDir * closestDist);

                    var missAmount = Vector3D.Distance(hitPos, closestPos);
                    System.Session.Rays++;
                    System.Session.RayMissAmounts += missAmount;

                }
            }
            
            var tick = Comp.Session.Tick;
            var masterWeapon = TrackTarget || Comp.TrackingWeapon == null ? this : Comp.TrackingWeapon;
            
            if (System.Values.HardPoint.Other.MuzzleCheck)
            {
                LastMuzzleCheck = tick;
                if (MuzzleHitSelf())
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckSelfHit, !Comp.FakeMode);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckSelfHit, !Comp.FakeMode);
                    return false;
                }
                if (tick - Comp.LastRayCastTick <= 29) return true;
            }
            
            if (Target.Entity is IMyCharacter && !Comp.Data.Repo.Base.Set.Overrides.Biologicals || Target.Entity is MyCubeBlock && !Comp.Data.Repo.Base.Set.Overrides.Grids)
            {
                masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                return false;
            }

            Comp.LastRayCastTick = tick;

            if (Target.IsFakeTarget)
            {
                Casting = true;
                Comp.Session.Physics.CastRayParallel(ref trackingCheckPosition, ref Target.TargetPos, CollisionLayers.DefaultCollisionLayer, ManualShootRayCallBack);
                return true;
            }

            if (Comp.FakeMode) return true;


            if (Target.IsProjectile)
            {
                if (!Comp.Ai.LiveProjectile.Contains(Target.Projectile))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                    return false;
                }
            }
            if (!Target.IsProjectile)
            {
                var character = Target.Entity as IMyCharacter;
                if ((Target.Entity == null || Target.Entity.MarkedForClose) || character != null && (character.IsDead || character.Integrity <= 0 || Comp.Session.AdminMap.ContainsKey(character)))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckOther);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckOther);
                    return false;
                }

                var cube = Target.Entity as MyCubeBlock;
                if (cube != null && !cube.IsWorking && !Comp.Ai.Construct.Focus.EntityIsFocused(Comp.Ai, cube.CubeGrid))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckDeadBlock);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckDeadBlock);
                    FastTargetResetTick = System.Session.Tick;
                    return false;
                }
                var topMostEnt = Target.Entity.GetTopMostParent();
                if (Target.TopEntityId != topMostEnt.EntityId || !Comp.Ai.Targets.ContainsKey(topMostEnt))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }
            }
            
            var targetPos = Target.Projectile?.Position ?? Target.Entity?.PositionComp.WorldMatrixRef.Translation ?? Vector3D.Zero;
            var distToTargetSqr = Vector3D.DistanceSquared(targetPos, trackingCheckPosition);
            if (distToTargetSqr > MaxTargetDistanceSqr && distToTargetSqr < MinTargetDistanceSqr)
            {
                masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckDistExceeded);
                if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckDistExceeded);
                return false;
            }
            WaterData water = null;
            if (System.Session.WaterApiLoaded && !ActiveAmmoDef.AmmoDef.IgnoreWater && Comp.Ai.InPlanetGravity && Comp.Ai.MyPlanet != null && System.Session.WaterMap.TryGetValue(Comp.Ai.MyPlanet.EntityId, out water))
            {
                var waterSphere = new BoundingSphereD(Comp.Ai.MyPlanet.PositionComp.WorldAABB.Center, water.MinRadius);
                if (waterSphere.Contains(targetPos) != ContainmentType.Disjoint)
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }
            }
            Casting = true;

            Comp.Session.Physics.CastRayParallel(ref trackingCheckPosition, ref targetPos, CollisionLayers.DefaultCollisionLayer, RayCallBack.NormalShootRayCallBack);
            return true;
        }

        internal void InitTracking()
        {
            RotationSpeed = System.AzStep;
            ElevationSpeed = System.ElStep;
            var minAz = System.MinAzimuth;
            var maxAz = System.MaxAzimuth;
            var minEl = System.MinElevation;
            var maxEl = System.MaxElevation;
            var toleranceRads = MathHelperD.ToRadians(System.Values.HardPoint.AimingTolerance);

            MinElevationRadians = MinElToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(minEl));
            MaxElevationRadians = MaxElToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(maxEl));

            MinAzimuthRadians = MinAzToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(minAz));
            MaxAzimuthRadians = MaxAzToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(maxAz));

            if (System.TurretMovement == WeaponSystem.TurretType.AzimuthOnly || System.Values.HardPoint.AddToleranceToTracking)
            {
                MinElToleranceRadians -= toleranceRads;
                MaxElToleranceRadians += toleranceRads;
            }
            else if (System.TurretMovement == WeaponSystem.TurretType.ElevationOnly || System.Values.HardPoint.AddToleranceToTracking)
            {
                MinAzToleranceRadians -= toleranceRads;
                MaxAzToleranceRadians += toleranceRads;
            }

            if (MinElToleranceRadians > MaxElToleranceRadians)
                MinElToleranceRadians -= 6.283185f;

            if (MinAzToleranceRadians > MaxAzToleranceRadians)
                MinAzToleranceRadians -= 6.283185f;
            
            var dummyInfo = Dummies[MiddleMuzzleIndex];
            MuzzleDistToBarrelCenter = Vector3D.Distance(dummyInfo.Info.Position, dummyInfo.Entity.PositionComp.WorldAABB.Center);
        }
    }
}