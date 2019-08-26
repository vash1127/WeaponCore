
using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using Havok;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{

    internal class Pointer
    {
        private GridAi _ai;
        private MyCockpit _cockPit;
        private readonly MyStringId _cross = MyStringId.GetOrCompute("Crosshair");
        private readonly List<IHitInfo> _hitInfo = new List<IHitInfo>();
        private readonly Vector2 _pointerPosition = new Vector2(0, 0.25f);
        private readonly Vector2 _targetDrawPosition = new Vector2(-1f, 1f);

        private bool _cachedPointerPos;
        private bool _cachedTargetPos;

        private bool _3RdPersonDraw;
        internal Vector3D PointerOffset;
        internal Vector3D TargetOffset;
        internal double AdjScale;

        internal bool GetAi()
        {
            _cockPit = Session.Instance.Session.ControlledObject as MyCockpit;
            return _cockPit != null && Session.Instance.GridTargetingAIs.TryGetValue(_cockPit.CubeGrid, out _ai);
        }

        internal void SelectTarget()
        {
            if (!GetAi()) return;
            if (!_cachedPointerPos) InitPointerOffset();
            if (!_cachedTargetPos) InitTargetOffset();
            Vector3D start;
            Vector3D end;
            if (!MyAPIGateway.Session.CameraController.IsInFirstPersonView)
            {
                var cameraWorldMatrix = Session.Instance.Camera.WorldMatrix;
                var offetPosition = Vector3D.Transform(PointerOffset, cameraWorldMatrix);
                start = offetPosition;
                var dir = Vector3D.Normalize(start - Session.Instance.CameraPos);
                end = offetPosition + (dir * _ai.MaxTargetingRange);
            }
            else
            {
                start = _cockPit.PositionComp.WorldAABB.Center;
                end = start + (Vector3D.Normalize(_cockPit.PositionComp.WorldMatrix.Forward) * _ai.MaxTargetingRange);
            }
            Session.Instance.Physics.CastRay(start, end, _hitInfo);
            for (int i = 0; i < _hitInfo.Count; i++)
            {
                var hit = _hitInfo[i].HitEntity as MyCubeGrid;
                if (hit == null || hit.IsSameConstructAs(_ai.MyGrid)) continue;

                Session.Instance.SetTarget(hit, _ai);

                Log.Line($"{hit.DebugName}");
                break;
            }
        }

        internal void DrawSelector()
        {
            if (!GetAi() || Session.Instance.Ui.WheelActive) return;
            if (Session.Instance.CheckTarget(_ai)) UpdateTarget();
            if (MyAPIGateway.Session.CameraController.IsInFirstPersonView || !GetAi()) return;
            if (MyAPIGateway.Input.IsNewKeyReleased(MyKeys.Control)) _3RdPersonDraw = !_3RdPersonDraw;
            if (!_3RdPersonDraw) return;
            if (!_cachedPointerPos) InitPointerOffset();
            if (!_cachedTargetPos) InitTargetOffset();
            var cameraWorldMatrix = Session.Instance.Camera.WorldMatrix;
            var offetPosition = Vector3D.Transform(PointerOffset, cameraWorldMatrix);
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            MyTransparentGeometry.AddBillboardOriented(_cross, Color.White, offetPosition, left, up, (float)AdjScale, BlendTypeEnum.PostPP);
        }

        private void UpdateTarget()
        {
            if (!Session.Instance.CheckTarget(_ai) || Session.Instance.TargetGps == null) return;
            var cameraWorldMatrix = Session.Instance.Camera.WorldMatrix;
            var offetPosition = Vector3D.Transform(TargetOffset, cameraWorldMatrix);

            double speed;
            string armedStr;
            string interceptStr;
            Session.Instance.GetTargetInfo(_ai, out speed, out armedStr, out interceptStr);

            var gpsName = $"Speed: {speed} m/s - Armed: {armedStr}\n Threat:  High - Intercept: {interceptStr}\n";
            Session.Instance.SetGpsInfo(offetPosition, gpsName);
        }

        private void InitPointerOffset()
        {
            var position = new Vector3D(_pointerPosition.X, _pointerPosition.Y, 0);
            var fov = Session.Instance.Session.Camera.FovWithZoom;
            double aspectratio = Session.Instance.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
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
            var fov = Session.Instance.Session.Camera.FovWithZoom;
            double aspectratio = Session.Instance.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);

            position.X *= scale * aspectratio;
            position.Y *= scale;

            AdjScale = 0.125 * scale;

            TargetOffset = new Vector3D(position.X, position.Y, -.1);
            _cachedTargetPos = true;
        }
    }
}
