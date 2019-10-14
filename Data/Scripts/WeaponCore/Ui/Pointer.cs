using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
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
        private readonly Vector2 _pointerPosition = new Vector2(0, 0.25f);
        private readonly Vector2 _targetDrawPosition = new Vector2(-1f, 1f);
        private readonly Session Session;
        private bool _cachedPointerPos;
        private bool _cachedTargetPos;

        private bool _3RdPersonDraw;
        internal Vector3D PointerOffset;
        internal Vector3D TargetOffset;
        internal double AdjScale;

        internal Pointer(Session session)
        {
            Session = session;
        }

        internal void SelectTarget()
        {
            if (!_cachedPointerPos) InitPointerOffset();
            if (!_cachedTargetPos) InitTargetOffset();
            if (!Session.UpdateLocalAiAndCockpit()) return;
            var ai = Session.TrackingAi;
            var cockPit = Session.ActiveCockPit;
            Vector3D start;
            Vector3D end;
            if (!MyAPIGateway.Session.CameraController.IsInFirstPersonView)
            {
                var cameraWorldMatrix = Session.Camera.WorldMatrix;
                var offetPosition = Vector3D.Transform(PointerOffset, cameraWorldMatrix);
                start = offetPosition;
                var dir = Vector3D.Normalize(start - Session.CameraPos);
                end = offetPosition + (dir * ai.MaxTargetingRange);
            }
            else
            {
                start = cockPit.PositionComp.WorldAABB.Center;
                end = start + (Vector3D.Normalize(cockPit.PositionComp.WorldMatrix.Forward) * Session.TrackingAi.MaxTargetingRange);
            }
            Session.Physics.CastRay(start, end, _hitInfo);
            for (int i = 0; i < _hitInfo.Count; i++)
            {
                var hit = _hitInfo[i].HitEntity as MyCubeGrid;
                if (hit == null || hit.IsSameConstructAs(ai.MyGrid)) continue;
                Session.SetTarget(hit, ai);
                Session.ResetGps();

                Log.Line($"{hit.DebugName}");
                break;
            }
        }

        internal void DrawSelector()
        {
            if (!Session.UpdateLocalAiAndCockpit() || Session.Ui.WheelActive) return;
            if (Session.CheckTarget(Session.TrackingAi)) UpdateTarget();
            if (MyAPIGateway.Session.CameraController.IsInFirstPersonView) return;
            if (MyAPIGateway.Input.IsNewKeyReleased(MyKeys.Control)) _3RdPersonDraw = !_3RdPersonDraw;
            if (!_3RdPersonDraw) return;
            if (!_cachedPointerPos) InitPointerOffset();
            if (!_cachedTargetPos) InitTargetOffset();
            var cameraWorldMatrix = Session.Camera.WorldMatrix;
            var offetPosition = Vector3D.Transform(PointerOffset, cameraWorldMatrix);
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            MyTransparentGeometry.AddBillboardOriented(_cross, Color.White, offetPosition, left, up, (float)AdjScale, BlendTypeEnum.PostPP);
        }

        private void UpdateTarget()
        {
            var ai = Session.TrackingAi;
            if (ai == null || !Session.CheckTarget(ai) || Session.TargetGps == null) return;

            var cameraWorldMatrix = Session.Camera.WorldMatrix;
            var offetPosition = Vector3D.Transform(TargetOffset, cameraWorldMatrix);
            double speed;
            string armedStr;
            string interceptStr;
            string shieldedStr;
            string threatStr;
            Session.GetTargetInfo(ai, out speed, out armedStr, out interceptStr, out shieldedStr, out threatStr);
            var gpsName = $"Status[ {armedStr}, {shieldedStr}, {interceptStr}, {threatStr} ]             Speed[ {speed:0} m/s ]";
            var targetSphere = ai.PrimeTarget.PositionComp.WorldVolume;
            var cockPitCenter = Session.ActiveCockPit.PositionComp.WorldAABB.Center;
            var distance = MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref cockPitCenter, ref targetSphere);
            Session.SetGpsInfo(offetPosition, gpsName, distance);
        }

        private void InitPointerOffset()
        {
            var position = new Vector3D(_pointerPosition.X, _pointerPosition.Y, 0);
            var fov = Session.Session.Camera.FovWithZoom;
            double aspectratio = Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);

            position.X *= scale * aspectratio;
            position.Y *= scale;

            AdjScale = 0.125 * scale;

            PointerOffset = new Vector3D(position.X, position.Y, -.1);
            _cachedPointerPos = true;
        }

        private void InitTargetOffset()
        {
            var position = new Vector3D(_targetDrawPosition.X, _targetDrawPosition.Y, 0);
            var fov = Session.Session.Camera.FovWithZoom;
            double aspectratio = Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);

            position.X *= scale * aspectratio;
            position.Y *= scale;

            AdjScale = 0.125 * scale;

            TargetOffset = new Vector3D(position.X, position.Y, -.1);
            _cachedTargetPos = true;
        }
    }
}
