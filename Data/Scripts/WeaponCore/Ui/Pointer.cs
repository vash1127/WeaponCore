
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
using VRage.Game.ModAPI.Interfaces;
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

        internal void SelectTarget()
        {
            if (!_cachedPointerPos) InitPointerOffset();
            if (!_cachedTargetPos) InitTargetOffset();
            if (!Session.Instance.UpdateLocalAiAndCockpit()) return;
            var ai = Session.Instance.TrackingAi;
            var cockPit = Session.Instance.ActiveCockPit;
            Vector3D start;
            Vector3D end;
            if (!MyAPIGateway.Session.CameraController.IsInFirstPersonView)
            {
                var cameraWorldMatrix = Session.Instance.Camera.WorldMatrix;
                var offetPosition = Vector3D.Transform(PointerOffset, cameraWorldMatrix);
                start = offetPosition;
                var dir = Vector3D.Normalize(start - Session.Instance.CameraPos);
                end = offetPosition + (dir * ai.MaxTargetingRange);
            }
            else
            {
                start = cockPit.PositionComp.WorldAABB.Center;
                end = start + (Vector3D.Normalize(cockPit.PositionComp.WorldMatrix.Forward) * Session.Instance.TrackingAi.MaxTargetingRange);
            }
            Session.Instance.Physics.CastRay(start, end, _hitInfo);
            for (int i = 0; i < _hitInfo.Count; i++)
            {
                var hit = _hitInfo[i].HitEntity as MyCubeGrid;
                if (hit == null || hit.IsSameConstructAs(ai.MyGrid)) continue;
                Session.Instance.SetTarget(hit, ai);
                Session.Instance.ResetGps();

                Log.Line($"{hit.DebugName}");
                break;
            }
        }

        internal void DrawSelector()
        {
            if (!Session.Instance.UpdateLocalAiAndCockpit() || Session.Instance.Ui.WheelActive) return;
            if (Session.Instance.CheckTarget(Session.Instance.TrackingAi)) UpdateTarget();
            if (MyAPIGateway.Session.CameraController.IsInFirstPersonView) return;
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
            var ai = Session.Instance.TrackingAi;
            if (ai == null || !Session.Instance.CheckTarget(ai) || Session.Instance.TargetGps == null) return;

            var cameraWorldMatrix = Session.Instance.Camera.WorldMatrix;
            var offetPosition = Vector3D.Transform(TargetOffset, cameraWorldMatrix);
            double speed;
            string armedStr;
            string interceptStr;
            string shieldedStr;
            string threatStr;
            Session.Instance.GetTargetInfo(ai, out speed, out armedStr, out interceptStr, out shieldedStr, out threatStr);
            var gpsName = $"Status[ {armedStr}, {shieldedStr}, {interceptStr}, {threatStr} ]             Speed[ {speed} m/s ]";
            var distance = Vector3D.Distance(Session.Instance.Target.PositionComp.WorldAABB.Center, Session.Instance.ActiveCockPit.PositionComp.WorldAABB.Center);
            Session.Instance.SetGpsInfo(offetPosition, gpsName, distance);
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
