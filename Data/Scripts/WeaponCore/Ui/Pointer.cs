
using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using Havok;
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
        private GridAi _ai;
        private MyCockpit _cockPit;
        private readonly MyStringId _cross = MyStringId.GetOrCompute("Crosshair");
        private readonly List<IHitInfo> _hitInfo = new List<IHitInfo>();
        private readonly Vector2 _pointerPosition = new Vector2(0, 0.15f);
        private bool _cachedPos;
        private bool _3RdPersonDraw;
        internal Vector3D Offset;
        internal double AdjScale;
        internal MyEntity Target;

        internal bool GetAi()
        {
            _cockPit = Session.Instance.Session.ControlledObject as MyCockpit;
            return _cockPit != null && Session.Instance.GridTargetingAIs.TryGetValue(_cockPit.CubeGrid, out _ai);
        }

        internal void SelectTarget()
        {
            if (!GetAi()) return;
            if (!_cachedPos) InitOffset();

            Vector3D start;
            Vector3D end;
            if (!MyAPIGateway.Session.CameraController.IsInFirstPersonView)
            {
                var cameraWorldMatrix = Session.Instance.Camera.WorldMatrix;
                var offetPosition = Vector3D.Transform(Offset, cameraWorldMatrix);
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
                Target = hit;
                Log.Line($"{hit.DebugName}");
                break;
            }
        }

        internal void DrawSelector()
        {
            if (MyAPIGateway.Session.CameraController.IsInFirstPersonView || !GetAi()) return;
            if (MyAPIGateway.Input.IsNewKeyReleased(MyKeys.Control)) _3RdPersonDraw = !_3RdPersonDraw;
            if (!_3RdPersonDraw) return;

            if (!_cachedPos) InitOffset();
            var cameraWorldMatrix = Session.Instance.Camera.WorldMatrix;
            var offetPosition = Vector3D.Transform(Offset, cameraWorldMatrix);
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            MyTransparentGeometry.AddBillboardOriented(_cross, Color.White, offetPosition, left, up, (float)AdjScale, BlendTypeEnum.PostPP);
        }

        private void InitOffset()
        {
            var position = new Vector3D(_pointerPosition.X, _pointerPosition.Y, 0);
            var fov = Session.Instance.Session.Camera.FovWithZoom;
            double aspectratio = Session.Instance.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);

            position.X *= scale * aspectratio;
            position.Y *= scale;

            AdjScale = 0.125 * scale;

            Offset = new Vector3D(position.X, position.Y, -.1);
            _cachedPos = true;
        }
    }
}
