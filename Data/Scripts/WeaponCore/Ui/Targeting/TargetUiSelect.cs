using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;
using WeaponCore.Support;
namespace WeaponCore
{
    internal partial class TargetUi
    {
        internal bool ActivateSelector()
        {
            if (_session.UiInput.FirstPersonView && !_session.UiInput.AltPressed) return false;
            if (MyAPIGateway.Input.IsNewKeyReleased(MyKeys.Control)) _3RdPersonDraw = !_3RdPersonDraw;

            var enableActivator = _3RdPersonDraw || _session.UiInput.CtrlPressed || _session.UiInput.FirstPersonView && _session.UiInput.AltPressed;
            return enableActivator;
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

        internal bool SelectTarget(bool manualSelect = true)
        {
            var s = _session;
            var ai = s.TrackingAi;
            if (!_cachedPointerPos) InitPointerOffset(0.05);
            if (!_cachedTargetPos) InitTargetOffset();
            var cockPit = s.ActiveCockPit;
            Vector3D start;
            Vector3D end;
            Vector3D dir;
            if (!s.UiInput.FirstPersonView)
            {
                var offetPosition = Vector3D.Transform(PointerOffset, s.CameraMatrix);
                start = offetPosition;
                dir = Vector3D.Normalize(start - s.CameraPos);
                end = offetPosition + (dir * ai.MaxTargetingRange);
            }
            else
            {
                if (!_session.UiInput.AltPressed)
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
            

            var foundTarget = false;
            MyEntity closestEnt = null;
            _session.Physics.CastRay(start, end, _hitInfo);
            for (int i = 0; i < _hitInfo.Count; i++)
            {
                var hit = _hitInfo[i];
                closestEnt = hit.HitEntity as MyEntity;
                var hitGrid = closestEnt as MyCubeGrid;


                if (manualSelect)
                {
                    if (hitGrid == null || hitGrid.IsSameConstructAs(ai.MyGrid) || !ai.Targets.ContainsKey(hitGrid)) continue;
                    s.SetTarget(hitGrid, ai);
                    return true;
                }

                if (hitGrid != null && hitGrid.IsSameConstructAs(ai.MyGrid))
                {
                    ReticleOnSelf = true;
                    ai.DummyTarget.Update(end);
                    return false;
                }

                foundTarget = true;
                ai.DummyTarget.Update(hit.Position, closestEnt);
                break;
            }

            // If Raycast misses, we will accept the closest entitySphere in its place.
            Vector3D hitPos;
            bool foundOther = false;
            if (!foundTarget && RayCheckTargets(start, dir, out closestEnt, out hitPos, out foundOther, !manualSelect))
            {
                foundTarget = true;
                if (manualSelect)
                {
                    s.SetTarget(closestEnt, ai);
                    return true;
                }
                ai.DummyTarget.Update(hitPos, closestEnt);
            }

            if (!manualSelect)
            {
                var activeColor = closestEnt != null && !ai.Targets.ContainsKey(closestEnt) || foundOther ? Color.DeepSkyBlue : Color.Red;
                _reticleColor = closestEnt != null ? activeColor : Color.White;
                if (!foundTarget)
                {
                    ai.DummyTarget.Update(end);
                }
            }

            return foundTarget || foundOther;
        }

        internal void SelectNext()
        {
            var s = _session;
            var ai = s.TrackingAi;

            if (!_cachedPointerPos) InitPointerOffset(0.05);
            if (!_cachedTargetPos) InitTargetOffset();

            var updateTick = s.Tick - _cacheIdleTicks > 600 || _endIdx == -1;
            if (s.UiInput.ShiftPressed || s.UiInput.AltPressed || s.UiInput.CtrlPressed || updateTick && !UpdateCache()) return;
            _cacheIdleTicks = s.Tick;

            if (s.UiInput.WheelForward)
                if (_currentIdx + 1 <= _endIdx)
                    _currentIdx += 1;
                else _currentIdx = 0;
            else if (s.UiInput.WheelBackward)
                if (_currentIdx - 1 >= 0)
                    _currentIdx -= 1;
                else _currentIdx = _endIdx;

            var ent = _targetCache[_currentIdx];
            if (!updateTick && ent.MarkedForClose)
            {
                _endIdx = -1;
                return;
            } 

            if (ent != null)
                s.SetTarget(ent, ai);
        }

        private bool UpdateCache()
        {
            var ai = _session.TrackingAi;
            var focus = ai.Focus;
            _targetCache.Clear();
            _currentIdx = 0;
            for (int i = 0; i < ai.SortedTargets.Count; i++)
            {
                var target = ai.SortedTargets[i].Target;
                if (target.MarkedForClose) continue;

                _targetCache.Add(target);
                if (focus.Target[focus.ActiveId] == target) _currentIdx = i;
            }
            _endIdx = _targetCache.Count - 1;
            return _endIdx >= 0;
        }

        private bool RayCheckTargets(Vector3D origin, Vector3D dir, out MyEntity closestEnt, out Vector3D hitPos, out bool foundOther, bool checkOthers = false)
        {
            var ai = _session.TrackingAi;
            var closestDist = double.MaxValue;
            closestEnt = null;
            foreach (var info in ai.Targets.Keys)
            {
                var hit = info as MyCubeGrid;
                if (hit == null) continue;
                var ray = new RayD(origin, dir);
                var dist = ray.Intersects(info.PositionComp.WorldVolume);
                if (dist.HasValue)
                {
                    if (dist.Value < closestDist)
                    {
                        closestDist = dist.Value;
                        closestEnt = hit;
                    }
                }
            }

            foundOther = false;
            if (checkOthers)
            {
                for (int i = 0; i < ai.Obstructions.Count; i++)
                {
                    var otherEnt = ai.Obstructions[i];
                    if (otherEnt is MyCubeGrid)
                    {
                        var ray = new RayD(origin, dir);
                        var dist = ray.Intersects(otherEnt.PositionComp.WorldVolume);
                        if (dist.HasValue)
                        {
                            if (dist.Value < closestDist)
                            {
                                closestDist = dist.Value;
                                closestEnt = otherEnt;
                                foundOther = true;
                            }
                        }
                    }
                }
            }

            if (closestDist < double.MaxValue)
                hitPos = origin + (dir * closestDist);
            else hitPos = Vector3D.Zero;

            return closestEnt != null;
        }
    }
}
