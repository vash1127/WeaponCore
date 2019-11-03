using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Input;
using VRageMath;
using WeaponCore.Support;
namespace WeaponCore
{
    internal partial class TargetUi
    {
        internal bool ActivateSelector()
        {
            _altPressed = false;
            _firstPerson = MyAPIGateway.Session.CameraController.IsInFirstPersonView;
            if (_firstPerson)
                _altPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Alt);

            if (_firstPerson && !_altPressed) return false;
            if (MyAPIGateway.Input.IsNewKeyReleased(MyKeys.Control)) _3RdPersonDraw = !_3RdPersonDraw;

            _ctrlPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Control);

            return _3RdPersonDraw || _ctrlPressed || _altPressed;
        }

        private void InitPointerOffset(double adjust)
        {
            var position = new Vector3D(_pointerPosition.X, _pointerPosition.Y, 0);
            var fov = _session.Session.Camera.FovWithZoom;
            double aspectratio = _session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);

            position.X *= scale * aspectratio;
            position.Y *= scale;

            PointerAdjScale = adjust * scale;

            PointerOffset = new Vector3D(position.X, position.Y, -0.1);
            _cachedPointerPos = true;
        }

        internal void SelectTarget()
        {
            var s = _session;
            var ai = s.TrackingAi;

            if (!_cachedPointerPos) InitPointerOffset(0.05);
            if (!_cachedTargetPos) InitTargetOffset();
            var cockPit = s.ActiveCockPit;
            Vector3D start;
            Vector3D end;
            Vector3D dir;
            if (!MyAPIGateway.Session.CameraController.IsInFirstPersonView)
            {
                var offetPosition = Vector3D.Transform(PointerOffset, s.CameraMatrix);
                start = offetPosition;
                dir = Vector3D.Normalize(start - s.CameraPos);
                end = offetPosition + (dir * ai.MaxTargetingRange);
            }
            else
            {
                if (!_altPressed)
                {
                    dir = Vector3D.Normalize(cockPit.PositionComp.WorldMatrix.Forward);
                    start = cockPit.PositionComp.WorldAABB.Center;
                    end = start + (dir * s.TrackingAi.MaxTargetingRange);
                }
                else
                {
                    var offetPosition = Vector3D.Transform(PointerOffset, s.CameraMatrix);
                    start = offetPosition;
                    dir = Vector3D.Normalize(start - s.CameraPos);
                    end = offetPosition + (dir * ai.MaxTargetingRange);
                }
            }
            _session.Physics.CastRay(start, end, _hitInfo);
            for (int i = 0; i < _hitInfo.Count; i++)
            {
                var hit = _hitInfo[i].HitEntity as MyCubeGrid;
                if (hit == null || hit.IsSameConstructAs(ai.MyGrid) || !ai.Targets.ContainsKey(hit)) continue;
                s.SetTarget(hit, ai);
                s.ResetGps();
                break;
            }

            // If Raycast misses, we will accept the closest entitySphere in its place.
            //var line = new LineD(start, end);


            var closestEnt = RayCheckTargets(start, dir);
            if (closestEnt != null)
            {
                s.SetTarget(closestEnt, ai);
                s.ResetGps();
            }
        }

        private MyEntity RayCheckTargets(Vector3D origin, Vector3D dir, bool reColorRectile = false)
        {
            var ai = _session.TrackingAi;
            var closestDist = double.MaxValue;
            MyEntity closestEnt = null;
            foreach (var info in ai.Targets.Keys)
            {
                var hit = info as MyCubeGrid;
                if (hit == null || hit.IsSameConstructAs(ai.MyGrid)) continue;
                var entWorldBox = info.PositionComp.WorldAABB;
                var ray = new RayD(origin, dir);
                double? dist;
                ray.Intersects(ref entWorldBox, out dist);
                if (dist.HasValue)
                {
                    if (dist.Value < closestDist)
                    {
                        closestDist = dist.Value;
                        closestEnt = hit;
                    }
                }
            }

            if (reColorRectile)
                _reticleColor = closestEnt != null ? Color.Red : Color.White;
            return closestEnt;
        }
    }
}
