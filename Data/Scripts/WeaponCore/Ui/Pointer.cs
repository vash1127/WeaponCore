using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{

    internal class Pointer
    {
        private readonly MyStringId _cross = MyStringId.GetOrCompute("Crosshair");
        private readonly List<IHitInfo> _hitInfo = new List<IHitInfo>();
        private readonly List<MyLineSegmentOverlapResult<MyEntity>> _pruneInfo = new List<MyLineSegmentOverlapResult<MyEntity>>();
        private Vector2 _pointerPosition = new Vector2(0, 0.25f);
        private Vector2 _3RdPersonPos = new Vector2(0, 0.25f);
        private readonly Vector2 _targetDrawPosition = new Vector2(-1f, 1f);
        private readonly Session _session;
        private int _previousWheel;
        private int _currentWheel;

        private bool _cachedPointerPos;
        private bool _cachedTargetPos;
        private bool _altPressed;
        private bool _3RdPersonDraw;
        internal Vector3D PointerOffset;
        internal Vector3D TargetOffset;
        internal double AdjScale;

        internal Pointer(Session session)
        {
            _session = session;
        }

        internal void SelectTarget()
        {
            if (!_cachedPointerPos) InitPointerOffset(0.125);
            if (!_cachedTargetPos) InitTargetOffset();
            if (!_session.UpdateLocalAiAndCockpit()) return;
            var ai = _session.TrackingAi;
            var cockPit = _session.ActiveCockPit;
            Vector3D start;
            Vector3D end;
            if (!MyAPIGateway.Session.CameraController.IsInFirstPersonView)
            {
                var cameraWorldMatrix = _session.Camera.WorldMatrix;
                var offetPosition = Vector3D.Transform(PointerOffset, cameraWorldMatrix);
                start = offetPosition;
                var dir = Vector3D.Normalize(start - _session.CameraPos);
                end = offetPosition + (dir * ai.MaxTargetingRange);
            }
            else
            {
                if (!_altPressed)
                {
                    start = cockPit.PositionComp.WorldAABB.Center;
                    end = start + (Vector3D.Normalize(cockPit.PositionComp.WorldMatrix.Forward) * _session.TrackingAi.MaxTargetingRange);
                }
                else
                {
                    var cameraWorldMatrix = _session.Camera.WorldMatrix;
                    var offetPosition = Vector3D.Transform(PointerOffset, cameraWorldMatrix);
                    start = offetPosition;
                    var dir = Vector3D.Normalize(start - _session.CameraPos);
                    end = offetPosition + (dir * ai.MaxTargetingRange);
                }
            }
            _session.Physics.CastRay(start, end, _hitInfo);
            for (int i = 0; i < _hitInfo.Count; i++)
            {
                var hit = _hitInfo[i].HitEntity as MyCubeGrid;
                if (hit == null || hit.IsSameConstructAs(ai.MyGrid)) continue;
                _session.SetTarget(hit, ai);
                _session.ResetGps();
                break;
            }

            // If Raycast misses, we will accept the closest entitySphere in its place.
            var line = new LineD(start, end);
            _pruneInfo.Clear();
            MyGamePruningStructure.GetAllEntitiesInRay(ref line, _pruneInfo);
            var closestDist = double.MaxValue;
            MyEntity closestEnt = null;
            for (int i = 0; i < _pruneInfo.Count; i++)
            {
                var info = _pruneInfo[i];
                var hit = info.Element as MyCubeGrid;
                if (hit == null || hit.IsSameConstructAs(ai.MyGrid)) continue;
                if (info.Distance < closestDist)
                {
                    closestDist = info.Distance;
                    closestEnt = hit;
                }
            }

            if (closestEnt != null)
            {
                _session.SetTarget(closestEnt, ai);
                _session.ResetGps();
            }
        }

        internal void DrawSelector()
        {
            if (!_session.UpdateLocalAiAndCockpit() || _session.Ui.WheelActive) return;
            if (_session.CheckTarget(_session.TrackingAi)) UpdateTarget();

            _altPressed = false;
            var firstPerson = MyAPIGateway.Session.CameraController.IsInFirstPersonView;
            if (firstPerson)
                _altPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Alt);

            if (firstPerson && !_altPressed) return;
            if (MyAPIGateway.Input.IsNewKeyReleased(MyKeys.Control)) _3RdPersonDraw = !_3RdPersonDraw;

            var controlledPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Control);

            if (!_3RdPersonDraw && !controlledPressed && !_altPressed) return;
            if (!_cachedPointerPos) InitPointerOffset(0.125);
            if (!_cachedTargetPos) InitTargetOffset();
            var cameraWorldMatrix = _session.Camera.WorldMatrix;
            var offetPosition = Vector3D.Transform(PointerOffset, cameraWorldMatrix);
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;

            if (firstPerson)
            {
                if (!MyUtils.IsZero(_pointerPosition.Y))
                {
                    _pointerPosition.Y = 0f;
                    InitPointerOffset(0.1);
                    Log.Line("reset cursor");
                }
            }
            else if (controlledPressed)
            {
                _previousWheel = MyAPIGateway.Input.PreviousMouseScrollWheelValue();
                _currentWheel = MyAPIGateway.Input.MouseScrollWheelValue();
                if (_previousWheel != _currentWheel)
                {
                    var currentPos = _pointerPosition.Y;
                    if (_currentWheel > _previousWheel) currentPos += 0.1f;
                    else currentPos -= 0.1f;
                    var clampPos = MathHelper.Clamp(currentPos, -1.25f, 1.25f);
                    _3RdPersonPos.Y = clampPos;
                    InitPointerOffset(0.125);
                }
                if (!MyUtils.IsEqual(_pointerPosition, _3RdPersonPos))
                {
                    _pointerPosition = _3RdPersonPos;
                    InitPointerOffset(0.125);
                }
            }
            else if (!MyUtils.IsEqual(_pointerPosition, _3RdPersonPos))
            {
                _pointerPosition = _3RdPersonPos;
                InitPointerOffset(0.125);
            }

            MyTransparentGeometry.AddBillboardOriented(_cross, Color.White, offetPosition, left, up, (float)AdjScale, BlendTypeEnum.PostPP);
        }

        private void UpdateTarget()
        {
            var ai = _session.TrackingAi;
            if (ai == null || !_session.CheckTarget(ai) || _session.TargetGps == null) return;

            var cameraWorldMatrix = _session.Camera.WorldMatrix;
            var offetPosition = Vector3D.Transform(TargetOffset, cameraWorldMatrix);
            double speed;
            string armedStr;
            string interceptStr;
            string shieldedStr;
            string threatStr;
            _session.GetTargetInfo(ai, out speed, out armedStr, out interceptStr, out shieldedStr, out threatStr);
            var gpsName = $"Status[ {armedStr}, {shieldedStr}, {interceptStr}, {threatStr} ]             Speed[ {speed:0} m/s ]";
            var targetSphere = ai.PrimeTarget.PositionComp.WorldVolume;
            var cockPitCenter = _session.ActiveCockPit.PositionComp.WorldAABB.Center;
            var distance = MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref cockPitCenter, ref targetSphere);
            _session.SetGpsInfo(offetPosition, gpsName, distance);
        }

        private void InitPointerOffset(double adjust)
        {
            var position = new Vector3D(_pointerPosition.X, _pointerPosition.Y, 0);
            var fov = _session.Session.Camera.FovWithZoom;
            double aspectratio = _session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);

            position.X *= scale * aspectratio;
            position.Y *= scale;

            AdjScale = adjust * scale;

            PointerOffset = new Vector3D(position.X, position.Y, -.1);
            _cachedPointerPos = true;
        }

        private void InitTargetOffset()
        {
            var position = new Vector3D(_targetDrawPosition.X, _targetDrawPosition.Y, 0);
            var fov = _session.Session.Camera.FovWithZoom;
            double aspectratio = _session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);

            position.X *= scale * aspectratio;
            position.Y *= scale;

            AdjScale = 0.125 * scale;

            TargetOffset = new Vector3D(position.X, position.Y, -.1);
            _cachedTargetPos = true;
        }
    }
}
