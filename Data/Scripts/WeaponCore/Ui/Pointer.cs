﻿using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
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

        private TargetState _targetState;

        internal long _prevTargetId;
        private int _previousWheel;
        private int _currentWheel;
        private bool _cachedPointerPos;
        private bool _cachedTargetPos;
        private bool _altPressed;
        private bool _3RdPersonDraw;
        internal Vector3D PointerOffset;
        internal Vector3D TargetOffset;
        internal double PointerAdjScale = 0.05f;
        internal double AdjScale;

        internal Pointer(Session session)
        {
            _session = session;
        }

        public struct TargetState
        {
            public int ShieldHealth;
            public int ThreatLvl;
            public int Size;
            public int Speed;
            public int Distance;
            public int Engagement;
        }

        public class IconInfo
        {
            private readonly MyStringId _textureName;
            private readonly Vector2D _screenPosition;
            private readonly double _definedScale;
            private readonly int _slotId;
            private readonly bool _shift;
            private float _adjustedScale;
            private Vector3D _positionOffset;
            private int _prevSlotId = -1;

            public IconInfo(MyStringId textureName, double definedScale, Vector2D screenPosition, int slotId, bool shift)
            {
                _textureName = textureName;
                _definedScale = definedScale;
                _screenPosition = screenPosition;
                _slotId = slotId;
                _shift = shift;
            }

            public void GetTextureInfo(int displayCount, out MyStringId textureName, out float scale, out Vector3D offset, out Vector3D cameraLeft, out Vector3D cameraUp)
            {
                if (displayCount != _prevSlotId) InitOffset(displayCount);
                textureName = _textureName;
                scale = _adjustedScale;
                var cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
                cameraLeft = cameraMatrix.Left;
                cameraUp = cameraMatrix.Up;
                offset = Vector3D.Transform(_positionOffset, cameraMatrix);

                _prevSlotId = displayCount;
            }

            private void InitOffset(int displayCount)
            {
                var fov = MyAPIGateway.Session.Camera.FovWithZoom;
                var screenScale = 0.075 * Math.Tan(fov * 0.5);
                const float slotSpacing = 0.05f;
                var shiftSlots = (_slotId - displayCount) * -1;
                var shiftSize = _shift && shiftSlots > 0 ? slotSpacing * shiftSlots : 0;

                var position = new Vector3D(_screenPosition.X + shiftSize, _screenPosition.Y, 0);
                double aspectratio = MyAPIGateway.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;

                position.X *= screenScale * aspectratio;
                position.Y *= screenScale;
                _adjustedScale = (float) (_definedScale  * screenScale);

                _positionOffset = new Vector3D(position.X, position.Y, -.1);
            }
        }

        private readonly Dictionary<string, IconInfo[]> _targetIcons = new Dictionary<string, IconInfo[]>()
        {
            {"size", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetCapital"), 0.1, new Vector2D(0, 1f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetCruiser"), 0.1, new Vector2D(0, 1f), -1, false),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDestroyer"), 0.1, new Vector2D(0, 1f), -1, false),
            }},
            {"threat", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat1"), 0.05, new Vector2D(0, 0.85f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat2"), 0.05, new Vector2D(0, 0.85f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat3"), 0.05, new Vector2D(0, 0.85f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat4"), 0.05, new Vector2D(0, 0.85f), 0, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat5"), 0.05, new Vector2D(0, 0.85f), 0, true),
            }},
            {"distance", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceNear"), 0.05, new Vector2D(0.05, 0.85f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceNearMid"), 0.05, new Vector2D(0.05, 0.85f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceFarMid"), 0.05, new Vector2D(0.05, 0.85f), 1, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceFar"), 0.05, new Vector2D(0.05, 0.85f), 1, true),
            }},
            {"speed", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed10"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed20"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed30"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed40"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed50"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed60"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed70"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed80"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed90"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed100"), 0.05, new Vector2D(0.1, 0.85f), 2, true),
            }},
            {"engagement", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetIntercept"), 0.05,  new Vector2D(0.15, 0.85f), 3, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetRetreat"), 0.05, new Vector2D(0.15, 0.85f), 3, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetEngaged"), 0.05, new Vector2D(0.15, 0.85f), 3, true),
            }},
            {"shield", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShieldLow"), 0.05,  new Vector2D(0.2, 0.85f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShieldMed"), 0.05, new Vector2D(0.2, 0.85f), 4, true),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShieldHigh"), 0.05, new Vector2D(0.2, 0.85f), 4, true),
            }},
        };

        internal void SelectTarget()
        {
            if (!_cachedPointerPos) InitPointerOffset(0.05);
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
                if (hit == null || hit.IsSameConstructAs(ai.MyGrid) || !ai.Targets.ContainsKey(hit)) continue;
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
                if (hit == null || hit.IsSameConstructAs(ai.MyGrid) || !ai.Targets.ContainsKey(hit)) continue;
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
            UpdateTarget();

            _altPressed = false;
            var firstPerson = MyAPIGateway.Session.CameraController.IsInFirstPersonView;
            if (firstPerson)
                _altPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Alt);

            if (firstPerson && !_altPressed) return;
            if (MyAPIGateway.Input.IsNewKeyReleased(MyKeys.Control)) _3RdPersonDraw = !_3RdPersonDraw;

            var controlledPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Control);

            if (!_3RdPersonDraw && !controlledPressed && !_altPressed) return;
            if (!_cachedPointerPos) InitPointerOffset(0.05);
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
                    InitPointerOffset(0.05);
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
                    InitPointerOffset(0.05);
                }
                if (!MyUtils.IsEqual(_pointerPosition, _3RdPersonPos))
                {
                    _pointerPosition = _3RdPersonPos;
                    InitPointerOffset(0.05);
                }
            }
            else if (!MyUtils.IsEqual(_pointerPosition, _3RdPersonPos))
            {
                _pointerPosition = _3RdPersonPos;
                InitPointerOffset(0.05);
            }

            MyTransparentGeometry.AddBillboardOriented(_cross, Color.White, offetPosition, left, up, (float)PointerAdjScale, BlendTypeEnum.PostPP);
        }

        private void UpdateTarget()
        {
            var ai = _session.TrackingAi;
            if (ai == null || !_session.CheckTarget(ai) || _session.TargetGps == null) return;
            
            /*
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
            */

            DrawTarget();
        }

        private void DrawTarget()
        {
            if (!GetTargetState()) return;
            var displayCount = 0;
            foreach (var icon in _targetIcons.Keys)
            {
                int iconLevel;
                if (!IconStatus(icon, _targetState, out iconLevel)) continue;

                Vector3D offset;
                float scale;
                MyStringId textureName;
                Vector3D cameraUp;
                Vector3D cameraLeft;

                _targetIcons[icon][iconLevel].GetTextureInfo(displayCount, out textureName, out scale, out offset, out cameraLeft, out cameraUp);

                MyTransparentGeometry.AddBillboardOriented(textureName, Color.White, offset, cameraLeft, cameraUp, scale, BlendTypeEnum.PostPP);
                displayCount++;
            }
        }

        private static bool IconStatus(string icon, TargetState targetState, out int iconLevel)
        {
            bool display;
            switch (icon)
            {
                case "speed":
                    display = targetState.Speed > -1;
                    iconLevel = !display ? 0 : targetState.Speed;
                    break;
                case "size":
                    display = targetState.Size > -1;
                    iconLevel = !display ? 0 : targetState.Size;
                    break;
                case "threat":
                    display = targetState.ThreatLvl > -1;
                    iconLevel = !display ? 0 : targetState.ThreatLvl;
                    break;
                case "shield":
                    display = targetState.ShieldHealth > -1;
                    iconLevel = !display ? 0 : targetState.ShieldHealth;
                    break;
                case "engagement":
                    display = targetState.Engagement > -1;
                    iconLevel = !display ? 0 : targetState.Engagement;
                    break;
                case "distance":
                    display = targetState.Size > -1;
                    iconLevel = !display ? 0 : targetState.Distance;
                    break;
                default:
                    display = false;
                    iconLevel = 0;
                    break;
            }

            return display;
        }

        private bool GetTargetState()
        {
            var ai = _session.TrackingAi;
            var target = ai.PrimeTarget;
            GridAi.TargetInfo targetInfo;
            if (!ai.Targets.TryGetValue(target, out targetInfo)) return false;
            if (!_session.Tick20 || _prevTargetId == targetInfo.EntInfo.EntityId) return true;
            Log.Line($"primeTarget: oRating:{targetInfo.OffenseRating} - blocks:{targetInfo.PartCount} - {targetInfo.Target.DebugName}");
            _prevTargetId = targetInfo.EntInfo.EntityId;

            var targetVel = target.Physics?.LinearVelocity ?? Vector3.Zero;
            if (MyUtils.IsZero(targetVel, 1E-02F)) targetVel = Vector3.Zero;
            var targetDir = Vector3D.Normalize(targetVel);
            var targetRevDir = -targetDir;
            var targetPos = target.PositionComp.WorldAABB.Center;
            var myPos = ai.MyGrid.PositionComp.WorldAABB.Center;
            var myHeading = Vector3D.Normalize(myPos - targetPos);

            var intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, _session.ApproachDegrees);
            var retreat = MathFuncs.IsDotProductWithinTolerance(ref targetRevDir, ref myHeading, _session.ApproachDegrees);
            if (intercept)_targetState.Engagement = 0;
            else if (retreat) _targetState.Engagement = 1;
            else _targetState.Engagement = -1;

            var speed = Math.Round(target.Physics?.Speed ?? 0, 1);

            var distanceFromCenters = Vector3D.Distance(ai.GridCenter, target.PositionComp.WorldAABB.Center);
            distanceFromCenters -= ai.GridRadius;
            distanceFromCenters -= target.PositionComp.LocalVolume.Radius;
            distanceFromCenters = distanceFromCenters <= 0 ? 0 : distanceFromCenters;
            var distPercent = (distanceFromCenters / ai.MaxTargetingRange) * 100;

            if (distPercent < 100 && distPercent > 66)
                _targetState.Distance = 2;
            else if (distPercent > 33) _targetState.Distance = 1;
            else if (distPercent >= 0) _targetState.Distance = 0;
            else _targetState.Distance = -1;

            if (speed <= 0) _targetState.Speed = - 1;
            else
            {
                var speedPercent = (speed / _session.MaxEntitySpeed) * 100;
                if (speedPercent > 95) _targetState.Speed = 9;
                else if (speedPercent > 90) _targetState.Speed = 8;
                else if (speedPercent > 80) _targetState.Speed = 7;
                else if (speedPercent > 70) _targetState.Speed = 6;
                else if (speedPercent > 60) _targetState.Speed = 5;
                else if (speedPercent > 50) _targetState.Speed = 4;
                else if (speedPercent > 40) _targetState.Speed = 3;
                else if (speedPercent > 30) _targetState.Speed = 2;
                else if (speedPercent > 20) _targetState.Speed = 1;
                else if (speedPercent > 0) _targetState.Speed = 0;
                else _targetState.Speed = -1;
            }

            MyTuple<bool, bool, float, float, float, int> shieldInfo = new MyTuple<bool, bool, float, float, float, int>();
            if (_session.ShieldApiLoaded) shieldInfo = _session.SApi.GetShieldInfo(target);
            if (shieldInfo.Item1)
            {
                var shieldPercent = shieldInfo.Item5;
                if (shieldPercent > 66) _targetState.ShieldHealth = 2;
                else if (shieldPercent > 33) _targetState.ShieldHealth = 1;
                else if (shieldPercent > 0) _targetState.ShieldHealth = 0;
                else _targetState.ShieldHealth = -1;
            }
            else _targetState.ShieldHealth = -1;

            var grid = target as MyCubeGrid;
            var friend = false;
            if (grid != null && grid.BigOwners.Count != 0)
            {
                var relation = MyIDModule.GetRelationPlayerBlock(ai.MyOwner, grid.BigOwners[0], MyOwnershipShareModeEnum.Faction);
                if (relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.Friends) friend = true;
            }

            if (friend) _targetState.ThreatLvl = -1;
            else
            {
                var offenseRating = targetInfo.OffenseRating;
                if (offenseRating > 2.5) _targetState.ThreatLvl = 4;
                else if (offenseRating > 1.25) _targetState.ThreatLvl = 3;
                else if (offenseRating > 0.5) _targetState.ThreatLvl = 2;
                else if (offenseRating > 0.25) _targetState.ThreatLvl = 1;
                else if (offenseRating > 0) _targetState.ThreatLvl = 0;
                else _targetState.ThreatLvl = -1;
            }
            return true;
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
