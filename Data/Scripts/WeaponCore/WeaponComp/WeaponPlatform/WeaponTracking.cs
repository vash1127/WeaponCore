using System;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using IMyLargeTurretBase = Sandbox.ModAPI.IMyLargeTurretBase;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal void Rotate(float speed)
        {
            var myCube = Comp.MyCube;
            var myMatrix = myCube.PositionComp.WorldMatrix;
            var myPivotPos = myCube.PositionComp.WorldAABB.Center;
            myPivotPos += myMatrix.Up * _upPivotOffsetLen;
            MyPivotPos = myPivotPos;
            var predictedPos = GetPredictedTargetPosition(Target);
            GetTurretAngles(ref predictedPos, ref MyPivotPos, Comp.Turret, speed, out _azimuth, out _elevation, out _desiredAzimuth, out _desiredElevation);
            var azDiff = 100 * (_desiredAzimuth - _azimuth) / _azimuth;
            var elDiff = 100 * (_desiredElevation - _elevation) / _elevation;

            _azOk = azDiff > -101 && azDiff < -99 || azDiff > -1 && azDiff < 1;
            _elOk = elDiff > -101 && elDiff < -99 || elDiff > -1 && elDiff < 1;
            Comp.Turret.Azimuth = (float)_azimuth;
            Comp.Turret.Elevation = (float)_elevation;
            //Rotation = (float)_desiredAzimuth;
            //Elevation = (float)_desiredElevation;
            //RotateModels();

            //Log.Line($"{_azimuth}({azDiff})[{_desiredAzimuth}] - {_elevation}({elDiff})[{_desiredElevation}]");
        }

        internal void GetTurretAngles(ref Vector3D targetPositionWorld, ref Vector3D turretPivotPointWorld, IMyLargeTurretBase turret, double maxAngularStep, out double azimuth, out double elevation, out double desiredAzimuth, out double desiredElevation)
        {
            // Get current turret facing
            Vector3D currentVector;
            Vector3D.CreateFromAzimuthAndElevation(turret.Azimuth, turret.Elevation, out currentVector);
            currentVector = Vector3D.Rotate(currentVector, turret.WorldMatrix);

            var up = turret.WorldMatrix.Up;
            var left = Vector3D.Cross(up, currentVector);
            if (!Vector3D.IsUnit(ref left) && !Vector3D.IsZero(left))
                left.Normalize();
            var forward = Vector3D.Cross(left, up);

            var matrix = new MatrixD()
            {
                Forward = forward,
                Left = left,
                Up = up,
            };

            // Get desired angles
            var targetDirection = targetPositionWorld - turretPivotPointWorld;
            GetRotationAngles(ref targetDirection, ref matrix, out desiredAzimuth, out desiredElevation);

            // Get control angles
            azimuth = turret.Azimuth + MathHelper.Clamp(desiredAzimuth, -maxAngularStep, maxAngularStep);
            elevation = turret.Elevation + MathHelper.Clamp(desiredElevation - turret.Elevation, -maxAngularStep, maxAngularStep);
        }

        /*
        /// Whip's Get Rotation Angles Method v14 - 9/25/18 ///
        Dependencies: AngleBetween
        */

        void GetRotationAngles(ref Vector3D targetVector, ref MatrixD worldMatrix, out double yaw, out double pitch)
        {
            var localTargetVector = Vector3D.Rotate(targetVector, MatrixD.Transpose(worldMatrix));
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
            yaw = AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is negative

            if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
        }

        /// <summary>
        /// Computes angle between 2 vectors
        /// </summary>
        public static double AngleBetween(Vector3D a, Vector3D b) //returns radians
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }

        public Vector3D GetPredictedTargetPosition(MyEntity target)
        {
            var thisTick = Comp.MyAi.MySession.Tick;
            if (thisTick == _lastPredictionTick && _lastTarget == target)
                return _lastPredictedPos;
            //if (thisTick != _lastPredictionTick)
                //this.m_muzzleWorldPosition = this.Turret.GunBase.GetMuzzleWorldPosition();
            _lastTarget = target;
            _lastPredictionTick = thisTick;
            if (target == null)
            {
                _lastPredictedPos = Vector3D.Zero;
                return _lastPredictedPos;
            }
            if (target.MarkedForClose)
            {
                _lastPredictedPos = target.PositionComp.GetPosition();
                return _lastPredictedPos;
            }
            var center = target.PositionComp.WorldAABB.Center;
            var deltaPos = center - MyPivotPos;
            //if (true)
            {
                DsDebugDraw.DrawLine(MyPivotPos, center, Color.Lime, 0.1f);
            }

            var projectileVel = WeaponType.AmmoDef.DesiredSpeed;
            var maxTrajectory = WeaponType.AmmoDef.MaxTrajectory;

            maxTrajectory += WeaponType.AmmoDef.AreaEffectRadius;
            var flag = !WeaponType.SkipAcceleration;

            var maxVel = projectileVel < 9.99999974737875E-06 ? 1E-06f : maxTrajectory / projectileVel;
            var targetVel = Vector3.Zero;
            if (target.Physics != null)
            {
                targetVel = target.Physics.LinearVelocity;
            }
            else
            {
                var topMostParent = target.GetTopMostParent();
                if (topMostParent?.Physics != null)
                    targetVel = topMostParent.Physics.LinearVelocity;
            }
            var myVel = ((IMyCubeGrid)Comp.MyGrid)?.Physics.LinearVelocity ?? Vector3.Zero;
            var deltaVel = targetVel - myVel;
            var num3 = MathHelper.Clamp(Intercept(deltaPos, deltaVel, projectileVel), 0.0, maxVel);
            var vector3D = center + (float)num3 * targetVel;
            _lastPredictedPos = flag ? vector3D - (float)num3 / maxVel * myVel : vector3D - (float)num3 * myVel;
            //if (true)
            {
                DsDebugDraw.DrawLine(MyPivotPos, _lastPredictedPos, Color.Orange, 0.1f);
            }
            return _lastPredictedPos;
        }

        private static double Intercept(Vector3D deltaPos, Vector3D deltaVel, float projectileVel)
        {
            var num1 = Vector3D.Dot(deltaVel, deltaVel) - projectileVel * projectileVel;
            var num2 = 2.0 * Vector3D.Dot(deltaVel, deltaPos);
            var num3 = Vector3D.Dot(deltaPos, deltaPos);
            var d = num2 * num2 - 4.0 * num1 * num3;
            if (d <= 0.0)
                return -1.0;
            return 2.0 * num3 / (Math.Sqrt(d) - num2);
        }

        private int _randomStandbyChange_ms;
        private int _randomStandbyChangeConst_ms;
        private float _randomStandbyRotation;
        private float _randomStandbyElevation;
        private float _rotationSpeed;
        private float _elevationSpeed;
        private float _maxAzimuthRadians;
        private float _minAzimuthRadians;
        private float _maxElevationRadians;
        private float _minElevationRadians;
        private float _rotationLast;
        private float _elevationLast;
        private float _gunIdleElevation;
        private float _gunIdleAzimuth;
        private bool _gunIdleElevationAzimuthUnknown;
        private bool _enableIdleRotation;
        private bool _randomIsMoving;
        private bool _transformDirty = true;
        private bool _wasAnimatedLastFrame;
        internal float Rotation;
        internal float Elevation;
        internal float BarrelElevationMin;
        internal bool IsAimed;
        private IMyEntity _barrel;
        private IMyEntity _base1;
        private IMyEntity _base2;

        public void InitTurretBase()
        {
            _barrel = EntityPart;
            _base1 = EntityPart.Parent.Parent;
            _base2 = EntityPart.Parent;
            /*
            this.m_shootIgnoreEntities = new VRage.Game.Entity.MyEntity[1]
            {
                (VRage.Game.Entity.MyEntity) this
            };
            */
            //this.CreateTerminalControls();
            //this.m_status = MyLargeTurretBase.MyLargeShipGunStatus.MyWeaponStatus_Deactivated;
            _randomStandbyChange_ms = MyAPIGateway.Session.ElapsedPlayTime.Milliseconds;
            _randomStandbyChangeConst_ms = MyUtils.GetRandomInt(3500, 4500);
            _randomStandbyRotation = 0.0f;
            _randomStandbyElevation = 0.0f;
            Rotation = 0.0f;
            Elevation = 0.0f;
            _rotationSpeed = 0.005f;
            _elevationSpeed = 0.005f;
            /*
                < MinElevationDegrees > -9 </ MinElevationDegrees >
                < MaxElevationDegrees > 50 </ MaxElevationDegrees >
                < MinAzimuthDegrees > -180 </ MinAzimuthDegrees >
                < MaxAzimuthDegrees > 180 </ MaxAzimuthDegrees >
            */
            _minElevationRadians = -9 / 0.0174533f;
            _maxElevationRadians = 50 / 0.0174533f;
            _minAzimuthRadians = -180 / 0.0174533f;
            _maxAzimuthRadians = 180 / 0.0174533f;
            //this.m_shootDelayIntervalConst_ms = 200;
            //this.m_shootIntervalConst_ms = 1200;
            //this.m_shootIntervalVarianceConst_ms = 500;
            //this.m_shootStatusChanged_ms = 0;
            //this.m_isPotentialTarget = false;
            //this.m_targetPrediction = (MyLargeTurretBase.IMyPredicionType)new MyLargeTurretBase.MyTargetPredictionType(this);
            //this.m_currentPrediction = this.m_targetPrediction;
            //this.m_positionPrediction = (MyLargeTurretBase.IMyPredicionType)new MyLargeTurretBase.MyPositionPredictionType(this);
            //this.m_soundEmitter = new MyEntity3DSoundEmitter((VRage.Game.Entity.MyEntity)this, true, 1f);
            //this.m_soundEmitterForRotation = new MyEntity3DSoundEmitter((VRage.Game.Entity.MyEntity)this, true, 1f);
            //this.ControllerInfo.ControlReleased += new Action<MyEntityController>(this.OnControlReleased);
            //this.m_gunBase = new MyGunBase();
            //this.m_outOfAmmoNotification = new MyHudNotification(MyCommonTexts.OutOfAmmo, 1000, "Blue", MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, 0, MyNotificationLevel.Important);
            //this.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            //this.SyncType.Append((object)this.m_gunBase);
            //this.m_shootingRange.ValueChanged += (Action<SyncBase>)(x => this.ShootingRangeChanged());
            //this.m_rotationAndElevationSync.ValueChanged += (Action<SyncBase>)(x => this.RotationAndElevationSync());
            //this.m_targetSync.AlwaysReject<MyLargeTurretBase.CurrentTargetSync, SyncDirection.FromServer>();
            //this.m_targetSync.ValueChanged += (Action<SyncBase>)(x => this.TargetChanged());
            //this.m_toolbar = new MyToolbar(this.ToolbarType, 9, 9);
        }


        private void RotateModels()
        {
            if (_base1 == null || _barrel == null || !_base1.Render.IsChild(0))
                return;
            if (!_transformDirty)
            {
                var physics = Comp.Physics;
                if (physics != null)
                {
                    var flag = !Comp.MyAi.MySession.IsServer && !Comp.MyGrid.IsClientPredicted;
                    var linearVelocity = physics.LinearVelocity;
                    var vector3 = flag ? physics.AngularVelocityLocal : physics.AngularVelocity;
                    if (linearVelocity.Equals(Vector3.Zero) && vector3.Equals(Vector3.Zero) && _wasAnimatedLastFrame == flag)
                        return;
                    _wasAnimatedLastFrame = flag;
                }
            }
            //ClampRotationAndElevation();
            Matrix rotMatrix;
            Matrix.CreateRotationY(Rotation, out rotMatrix);

            var baseMatrix = Comp.MyCube.PositionComp.LocalMatrix;
            var baseMatrixTran = baseMatrix.Translation;
            baseMatrixTran.Z += 1.09354f;
            baseMatrix.Translation = baseMatrixTran;

            Matrix rotDoneMatrix;
            Matrix.Multiply(ref rotMatrix, ref baseMatrix, out rotDoneMatrix);
            _base1.PositionComp.SetLocalMatrix(ref rotMatrix, _base1.Physics, false, ref rotDoneMatrix, true);

            Matrix elMatrix;
            Matrix.CreateRotationX(Elevation, out elMatrix);

            Matrix elDoneMatrix;
            Matrix.Multiply(ref elMatrix, ref rotDoneMatrix, out elDoneMatrix);
            var tmpTran = elDoneMatrix.Translation;
            tmpTran.Z -= 1.04848f;
            tmpTran.Y += 0.37814f;

            elDoneMatrix.Translation = tmpTran;
            Log.Line($"baseTran:{baseMatrix.Translation} - base2Tran:{_base1.PositionComp.LocalMatrix.Translation} - rotDoneTran:{rotDoneMatrix.Translation} - elDoneTran:{elDoneMatrix.Translation} - elMatrixTran:{elMatrix.Translation}");
            _base2.PositionComp.SetLocalMatrix(ref elMatrix, _base2.Physics, true, ref elDoneMatrix, true);
            //_barrel.WorldPositionChanged();
            _barrel.Render.UpdateRenderObject(true, true);
            _transformDirty = false;
        }

        private Vector3 LookAt(Vector3D target)
        {
            var muzzleWorldPosition = MyPivotPos;
            float azimuth;
            float elevation;
            Vector3.GetAzimuthAndElevation(Vector3.Normalize(Vector3D.TransformNormal(target - muzzleWorldPosition, Comp.MyCube.PositionComp.WorldMatrixInvScaled)), out azimuth, out elevation);
            if (_gunIdleElevationAzimuthUnknown)
            {
                Vector3.GetAzimuthAndElevation(EntityPart.LocalMatrix.Forward, out _gunIdleAzimuth, out _gunIdleElevation);
                _gunIdleElevationAzimuthUnknown = false;
            }
            return new Vector3(elevation - _gunIdleElevation, MathHelper.WrapAngle(azimuth - _gunIdleAzimuth), 0.0f);
        }

        private void ResetRandomAiming()
        {
            if (MyAPIGateway.Session.ElapsedPlayTime.Milliseconds - _randomStandbyChange_ms <= _randomStandbyChangeConst_ms)
                return;
            _randomStandbyRotation = MyUtils.GetRandomFloat(-3.141593f, 3.141593f);
            _randomStandbyElevation = MyUtils.GetRandomFloat(0.0f, 1.570796f);
            _randomStandbyChange_ms = MyAPIGateway.Session.ElapsedPlayTime.Milliseconds;
        }

        private void RandomMovement()
        {
            if (!_enableIdleRotation || Gunner)
                return;
            ResetRandomAiming();
            var randomStandbyRotation = _randomStandbyRotation;
            var standbyElevation = _randomStandbyElevation;
            var max1 = _rotationSpeed * 16f;
            var flag = false;
            var rotation = Rotation;
            var num1 = (randomStandbyRotation - rotation);
            if (num1 * num1 > 9.99999943962493E-11)
            {
                Rotation += MathHelper.Clamp(num1, -max1, max1);
                flag = true;
            }
            if (standbyElevation > BarrelElevationMin)
            {
                var max2 = _elevationSpeed * 16f;
                var num2 = standbyElevation - Elevation;
                if (num2 * num2 > 9.99999943962493E-11)
                {
                    Elevation += MathHelper.Clamp(num2, -max2, max2);
                    flag = true;
                }
            }
            //this.m_playAimingSound = flag;
            ClampRotationAndElevation();
            if (_randomIsMoving)
            {
                if (flag)
                    return;
                //this.SetupSearchRaycast();
                _randomIsMoving = false;
            }
            else
            {
                if (!flag)
                    return;
                _randomIsMoving = true;
            }
        }

        protected void ResetRotation()
        {
            Rotation = 0.0f;
            Elevation = 0.0f;
            ClampRotationAndElevation();
            _randomStandbyElevation = 0.0f;
            _randomStandbyRotation = 0.0f;
            _randomStandbyChange_ms = MyAPIGateway.Session.ElapsedPlayTime.Milliseconds;
        }

        public bool RotationAndElevation()
        {
            var vector3 = Vector3.Zero;
            if (Target != null)
            {
                var predictedTargetPosition = GetPredictedTargetPosition(Target);
                vector3 = LookAt(predictedTargetPosition);
            }
            var y = vector3.Y;
            var x = vector3.X;
            var max1 = _rotationSpeed * 16f;
            var num1 = MathHelper.WrapAngle(y - Rotation);
            Rotation += MathHelper.Clamp(num1, -max1, max1);
            //var flag = num1 * num1 > 9.99999943962493E-11;
            if (Rotation > 3.14159297943115)
                Rotation -= 6.283185f;
            else if (Rotation < -3.14159297943115)
                Rotation += 6.283185f;
            var max2 = _elevationSpeed * 16f;
            var num2 = Math.Max(x, BarrelElevationMin) - Elevation;
            Elevation += MathHelper.Clamp(num2, -max2, max2);
            //this.m_playAimingSound = flag || (double)num2 * (double)num2 > 9.99999943962493E-11;
            ClampRotationAndElevation();
            RotateModels();
            if (Target != null)
            {
                var num3 = Math.Abs(y - Rotation);
                var num4 = Math.Abs(x - Elevation);
                IsAimed = num3 <= 1.40129846432482E-45 && num4 <= 0.00999999977648258;
            }
            else
                IsAimed = false;
            return IsAimed;
        }

        private void ClampRotationAndElevation()
        {
            Rotation = ClampRotation(Rotation);
            Elevation = ClampElevation(Elevation);
        }


        private float ClampRotation(float value)
        {
            if (IsRotationLimited())
                value = Math.Min(_maxAzimuthRadians, Math.Max(_minAzimuthRadians, value));
            return value;
        }

        private bool IsRotationLimited()
        {
            return Math.Abs((float)(_maxAzimuthRadians - _minAzimuthRadians - 6.28318548202515)) > 0.01;
        }

        private float ClampElevation(float value)
        {
            if (IsElevationLimited())
                value = Math.Min(_maxElevationRadians, Math.Max(_minElevationRadians, value));
            return value;
        }

        private bool IsElevationLimited()
        {
            return Math.Abs((float)(_maxElevationRadians - _minElevationRadians - 6.28318548202515)) > 0.01;
        }

        private bool HasElevationOrRotationChanged()
        {
            return Math.Abs(_rotationLast - Rotation) > 0.00700000021606684 || Math.Abs(_elevationLast - Elevation) > 0.00700000021606684;
        }
        /*
        private void UpdateControlledWeapon()
        {
            if (this.HasElevationOrRotationChanged())
                this.m_stopShootingTime = 0.0f;
            else if ((double)this.m_stopShootingTime <= 0.0)
                this.m_stopShootingTime = (float)MySandboxGame.TotalGamePlayTimeInMilliseconds;
            else if ((double)this.m_stopShootingTime + 120.0 < (double)MySandboxGame.TotalGamePlayTimeInMilliseconds)
                this.StopAimingSound();
            this.m_rotationLast = this.Rotation;
            this.m_elevationLast = this.Elevation;
            this.RotateModels();
            if (this.m_status != MyLargeTurretBase.MyLargeShipGunStatus.MyWeaponStatus_Shooting)
                return;
            this.m_barrel.StopShooting();
            this.m_status = MyLargeTurretBase.MyLargeShipGunStatus.MyWeaponStatus_Searching;
        }

        private void SetupSearchRaycast()
        {
            MatrixD muzzleWorldMatrix = this.m_gunBase.GetMuzzleWorldMatrix();
            Vector3D vector3D = muzzleWorldMatrix.Translation + muzzleWorldMatrix.Forward * (double)this.m_searchingRange;
            this.m_laserLength = (double)this.m_searchingRange;
            this.m_hitPosition = vector3D;
        }

        private void GetCameraDummy()
        {
            if (this.m_base2.Model == null || !this.m_base2.Model.Dummies.ContainsKey("camera"))
                return;
            this.CameraDummy = this.m_base2.Model.Dummies["camera"];
        }

        public void UpdateVisual()
        {
            base.UpdateVisual();
            this.m_transformDirty = true;
        }

        void GetTurretAngles2(ref Vector3D targetPositionWorld, ref Vector3D turretPivotPointWorld, ref MatrixD turretWorldMatrix, out double azimuth, out double elevation)
        {
            Vector3D localTargetPosition = targetPositionWorld - turretPivotPointWorld;
            GetRotationAngles2(ref localTargetPosition, ref turretWorldMatrix, out azimuth, out elevation);
        }

        /*
        /// Whip's Get Rotation Angles Method v14 - 9/25/18 ///
        Dependencies: AngleBetween
        void GetRotationAngles2(ref Vector3D targetVector, ref MatrixD worldMatrix, out double yaw, out double pitch)
        {
            var localTargetVector = Vector3D.Rotate(targetVector, MatrixD.Transpose(worldMatrix));
            var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

            yaw = AngleBetween(Vector3D.Forward, flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is negative
            if (Math.Abs(yaw) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
                yaw = Math.PI;

            if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
                pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
            else
                pitch = AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
        }


        /// <summary>
        /// Returns if the normalized dot product between two vectors is greater than the tolerance.
        /// This is helpful for determining if two vectors are "more parallel" than the tolerance.
        /// </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <param name="tolerance">Cosine of maximum angle</param>
        /// <returns></returns>
        public static bool IsDotProductWithinTolerance(ref Vector3D a, ref Vector3D b, double tolerance)
        {
            double dot = Vector3D.Dot(a, b);
            double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Sign(tolerance);
            return Math.Sign(dot) * dot > num;
        }

        bool sameSign(float num1, double num2)
        {
            if (num1 > 0 && num2 < 0)
                return false;
            if (num1 < 0 && num2 > 0)
                return false;
            return true;
        }
        */
    }
}
