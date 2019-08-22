
using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{

    internal class Pointer
    {
        private GridAi Ai;
        private MyCockpit _cockPit;
        private MyEntity _target;
        private bool _inView;
        private readonly MyStringId _cross = MyStringId.GetOrCompute("Crosshair");
        private readonly List<IHitInfo> _hitInfo = new List<IHitInfo>();
        private readonly Vector2 _pointerPosition = new Vector2(0, 0.15f);

        internal bool GetAi()
        {
            _cockPit = Session.Instance.Session.ControlledObject as MyCockpit;
            return _cockPit != null && Session.Instance.GridTargetingAIs.TryGetValue(_cockPit.CubeGrid, out Ai);
        }

        internal void PointingAt()
        {
            return;
            if (!GetAi()) return;
            Vector3D start;
            Vector3D end;
            Vector3D targetDir;
            Vector3D cameraUp;
            /*
            if (MyAPIGateway.Session.CameraController.IsInFirstPersonView)
            {
                fromPos = _cockPit.PositionComp.WorldAABB.Center;
                targetDir = Vector3.Normalize(_cockPit.PositionComp.WorldMatrix.Forward);
                toPos = fromPos + (targetDir * Ai.MaxTargetingRange);
            }
            else
            {
                targetDir = Vector3.Normalize(Session.Instance.Camera.WorldMatrix.Forward);
                cameraUp = Vector3.Normalize(Session.Instance.Camera.WorldMatrix.Up);

                fromPos = Session.Instance.CameraPos + (targetDir * 10);
                fromPos += (cameraUp * 1);
                toPos = fromPos + (targetDir * Ai.MaxTargetingRange);
            }
            */

            DrawSelector(out start, out end);
            Log.Line($"{start} - {end}");
            Session.Instance.Physics.CastRay(start, end, _hitInfo);
            for (int i = 0; i < _hitInfo.Count; i++)
            {
                var hit = _hitInfo[i].HitEntity as MyCubeGrid;
                if (hit == null) continue;
                Log.Line($"{hit.DebugName}");
            }
        }

        internal void DrawSelector(out Vector3D start, out Vector3D end)
        {
            var position = new Vector3D(_pointerPosition.X, _pointerPosition.Y, 0);
            var fov = Session.Instance.Session.Camera.FovWithZoom;
            double aspectratio = Session.Instance.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);
            position.X *= scale * aspectratio;
            position.Y *= scale;
            var cameraWorldMatrix = Session.Instance.Session.Camera.WorldMatrix;
            position = Vector3D.Transform(new Vector3D(position.X, position.Y, -.1), cameraWorldMatrix);

            var origin = position;
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            scale = 0.125 * scale;
            start = position;
            var camForward = Session.Instance.Camera.WorldMatrix.Forward;
            end = position + (camForward * 5000);
            DsDebugDraw.DrawLine(start, end, Color.Yellow, 0.1f);
            MyTransparentGeometry.AddBillboardOriented(_cross, Color.White, origin, left, up, (float)scale, BlendTypeEnum.PostPP);
        }
    }
}
