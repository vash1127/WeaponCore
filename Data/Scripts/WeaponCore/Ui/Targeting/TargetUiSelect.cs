using System;
using System.Collections.Generic;
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
            Vector3D end;

            if (!s.UiInput.FirstPersonView) {
                var offetPosition = Vector3D.Transform(PointerOffset, s.CameraMatrix);
                AimPosition = offetPosition;
                AimDirection = Vector3D.Normalize(AimPosition - s.CameraPos);
                end = offetPosition + (AimDirection * ai.MaxTargetingRange);
            }
            else {
                if (!_session.UiInput.AltPressed) {
                    AimDirection = cockPit.PositionComp.WorldMatrixRef.Forward;
                    AimPosition = cockPit.PositionComp.WorldAABB.Center;
                    end = AimPosition + (AimDirection * s.TrackingAi.MaxTargetingRange);
                }
                else {
                    var offetPosition = Vector3D.Transform(PointerOffset, s.CameraMatrix);
                    AimPosition = offetPosition;
                    AimDirection = Vector3D.Normalize(AimPosition - s.CameraPos);
                    end = offetPosition + (AimDirection * ai.MaxTargetingRange);
                }
            }

            var foundTarget = false;
            var rayOnlyHitSelf = false;
            var rayHitSelf = false;

            MyEntity closestEnt = null;
            _session.Physics.CastRay(AimPosition, end, _hitInfo);

            for (int i = 0; i < _hitInfo.Count; i++) {

                var hit = _hitInfo[i];
                closestEnt = hit.HitEntity.GetTopMostParent() as MyEntity;

                var hitGrid = closestEnt as MyCubeGrid;

                if (hitGrid != null && hitGrid.IsSameConstructAs(ai.MyGrid)) {
                    rayHitSelf = true;
                    rayOnlyHitSelf = true;
                    continue;
                }

                if (rayOnlyHitSelf) rayOnlyHitSelf = false;

                if (manualSelect) {
                    if (hitGrid == null || !_masterTargets.ContainsKey(hitGrid))
                        continue;

                    s.SetTarget(hitGrid, ai, _masterTargets);
                    return true;
                }

                foundTarget = true;
                ai.Session.PlayerDummyTargets[ai.Session.PlayerId].Update(hit.Position, ai, closestEnt);
                break;
            }

            if (rayHitSelf) {
                ReticleOnSelfTick = s.Tick;
                ReticleAgeOnSelf++;
                if (rayOnlyHitSelf) ai.Session.PlayerDummyTargets[ai.Session.PlayerId].Update(end, ai);
            }
            else ReticleAgeOnSelf = 0;

            Vector3D hitPos;
            bool foundOther = false;
            if (!foundTarget && RayCheckTargets(AimPosition, AimDirection, out closestEnt, out hitPos, out foundOther, !manualSelect)) {
                foundTarget = true;
                if (manualSelect) {
                    s.SetTarget(closestEnt, ai, _masterTargets);
                    return true;
                }
                ai.Session.PlayerDummyTargets[ai.Session.PlayerId].Update(hitPos, ai, closestEnt);
            }

            if (!manualSelect) {
                var activeColor = closestEnt != null && !_masterTargets.ContainsKey(closestEnt) || foundOther ? Color.DeepSkyBlue : Color.Red;
                _reticleColor = closestEnt != null && !(closestEnt is MyVoxelBase) ? activeColor : Color.White;
                if (!foundTarget) {
                    ai.Session.PlayerDummyTargets[ai.Session.PlayerId].Update(end, ai);
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

            var ent = _sortedMasterList[_currentIdx];
            if (ent == null || ent.MarkedForClose)
            {
                _endIdx = -1;
                return;
            } 

            s.SetTarget(ent, ai, _masterTargets);
        }

        private bool UpdateCache()
        {
            var ai = _session.TrackingAi;
            var focus = ai.Construct.Data.Repo.FocusData;
            _currentIdx = 0;
            BuildMasterCollections(ai);

            for (int i = 0; i < _sortedMasterList.Count; i++)
                if (focus.Target[focus.ActiveId] == _sortedMasterList[i].EntityId) _currentIdx = i;
            _endIdx = _sortedMasterList.Count - 1;
            return _endIdx >= 0;
        }

        private void BuildMasterCollections(GridAi ai)
        {
            _masterTargets.Clear();
            for (int i = 0; i < ai.Construct.RefreshedAis.Count; i++)  {

                var subTargets = ai.Construct.RefreshedAis[i].SortedTargets;
                for (int j = 0; j < subTargets.Count; j++) {
                    var tInfo = subTargets[j];
                    if (tInfo.Target.MarkedForClose) continue;
                    _masterTargets[tInfo.Target] = tInfo.OffenseRating;
                    _toPruneMasterDict[tInfo.Target] = tInfo;
                }
            }

            _sortedMasterList.Clear();
            _toSortMasterList.AddRange(_toPruneMasterDict.Values);
            _toPruneMasterDict.Clear();

            _toSortMasterList.Sort(_session.TargetCompare);

            for (int i = 0; i < _toSortMasterList.Count; i++)
                _sortedMasterList.Add(_toSortMasterList[i].Target);

            _toSortMasterList.Clear();
        }

        private bool RayCheckTargets(Vector3D origin, Vector3D dir, out MyEntity closestEnt, out Vector3D hitPos, out bool foundOther, bool checkOthers = false)
        {
            var ai = _session.TrackingAi;
            var closestDist = double.MaxValue;
            closestEnt = null;
            foreach (var info in _masterTargets.Keys)
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
