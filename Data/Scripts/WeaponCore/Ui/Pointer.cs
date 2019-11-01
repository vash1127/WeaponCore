using System;
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

        public struct TargetState
        {
            public int ShieldHealth;
            public int ThreatLvl;
            public int Size;
            public int Speed;
            public int Distance;
            public bool Intercept;
        }

        public class IconInfo
        {
            private readonly MyStringId _textureName;
            private readonly Vector2D _screenPosition;
            private readonly double _definedScale;
            private float _adjustedScale;
            private bool _inited;
            private Vector3D _positionOffset;

            public IconInfo(MyStringId textureName, double definedScale, Vector2D screenPosition)
            {
                _textureName = textureName;
                _definedScale = definedScale;
                _screenPosition = screenPosition;
            }

            public void GetTextureInfo(out MyStringId textureName, out float scale, out Vector3D offset, out Vector3D cameraLeft, out Vector3D cameraUp)
            {
                if (!_inited) InitOffset();
                textureName = _textureName;
                scale = _adjustedScale;
                var cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
                cameraLeft = cameraMatrix.Left;
                cameraUp = cameraMatrix.Up;
                offset = Vector3D.Transform(_positionOffset, cameraMatrix);
            }

            private void InitOffset()
            {
                var position = new Vector3D(_screenPosition.X, _screenPosition.Y, 0);
                var fov = MyAPIGateway.Session.Camera.FovWithZoom;
                double aspectratio = MyAPIGateway.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
                var screenScale = 0.075 * Math.Tan(fov * 0.5);

                position.X *= screenScale * aspectratio;
                position.Y *= screenScale;

                _adjustedScale = (float) (_definedScale * screenScale);

                _positionOffset = new Vector3D(position.X, position.Y, -.1);
                _inited = true;
            }
        }

        private readonly Dictionary<string, IconInfo[]> _targetIcons = new Dictionary<string, IconInfo[]>()
        {
            {"size", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetCapital"), 0.1, new Vector2D(0, 1f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetCruiser"), 0.1, new Vector2D(0, 1f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDestroyer"), 0.1, new Vector2D(0, 1f)),
            }},
            {"threat", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat1"), 0.05, new Vector2D(-0.05, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat2"), 0.05, new Vector2D(-0.05, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat3"), 0.05, new Vector2D(-0.05, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat4"), 0.05, new Vector2D(-0.05, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetThreat5"), 0.05, new Vector2D(-0.05, 0.85f)),
            }},
            {"distance", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceNear"), 0.05, new Vector2D(-0.1, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceNearMid"), 0.05, new Vector2D(-0.1, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceFarMid"), 0.05, new Vector2D(-0.1, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetDistanceFar"), 0.05, new Vector2D(-0.1, 0.85f)),
            }},
            {"speed", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed10"), 0.05, new Vector2D(-0.15, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed20"), 0.05, new Vector2D(-0.15, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed30"), 0.05, new Vector2D(-0.15, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed40"), 0.05, new Vector2D(-0.15, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed50"), 0.05, new Vector2D(-0.15, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed60"), 0.05, new Vector2D(-0.15, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed70"), 0.05, new Vector2D(-0.15, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed80"), 0.05, new Vector2D(-0.15, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed90"), 0.05, new Vector2D(-0.15, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetSpeed100"), 0.05, new Vector2D(-0.15, 0.85f)),
            }},
            {"shield", new[] {
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShieldLow"), 0.05,  new Vector2D(-0.2, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShieldMed"), 0.05, new Vector2D(-0.2, 0.85f)),
                new IconInfo(MyStringId.GetOrCompute("DS_TargetShieldHigh"), 0.05, new Vector2D(-0.2, 0.85f)),
            }},
        };

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
            UpdateTarget();

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
            var targetState = new TargetState();
            if (!GetTargetState(ref targetState)) return;

            foreach (var icon in _targetIcons.Keys)
            {
                bool displayIcon;
                int iconLevel;
                int iconSlot;
                IconStatus(icon, targetState, out displayIcon, out iconLevel, out iconSlot);
                if (!displayIcon) continue;

                Vector3D offset;
                float scale;
                MyStringId textureName;
                Vector3D cameraUp;
                Vector3D cameraLeft;

                var iconInfo = _targetIcons[icon];
                iconInfo[iconLevel].GetTextureInfo(out textureName, out scale, out offset, out cameraLeft, out cameraUp);

                MyTransparentGeometry.AddBillboardOriented(textureName, Color.White, offset, cameraLeft, cameraUp, scale, BlendTypeEnum.PostPP);
            }
        }

        private bool GetTargetState(ref TargetState targetState)
        {
            var ai = _session.TrackingAi;
            var target = ai.PrimeTarget;
            GridAi.TargetInfo targetInfo;
            if (!ai.Targets.TryGetValue(target, out targetInfo)) return false;

            var targetVel = target.Physics?.LinearVelocity ?? Vector3.Zero;
            if (MyUtils.IsZero(targetVel, 1E-02F)) targetVel = Vector3.Zero;
            var targetDir = Vector3D.Normalize(targetVel);
            var targetPos = target.PositionComp.WorldAABB.Center;
            var myPos = ai.MyGrid.PositionComp.WorldAABB.Center;
            var myHeading = Vector3D.Normalize(myPos - targetPos);

            targetState.Intercept = MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref myHeading, _session.ApproachDegrees);

            var speed = Math.Round(target.Physics?.Speed ?? 0, 1);

            var distanceFromCenters = Vector3D.Distance(ai.GridCenter, target.PositionComp.WorldAABB.Center);
            distanceFromCenters -= ai.GridRadius;
            distanceFromCenters -= target.PositionComp.LocalVolume.Radius;
            distanceFromCenters = distanceFromCenters <= 0 ? 0 : distanceFromCenters;
            var distPercent = (distanceFromCenters / ai.MaxTargetingRange) * 100;

            if (distPercent < 100 && distPercent > 66)
                targetState.Distance = 2;
            else if (distPercent > 33) targetState.Distance = 1;
            else if (distPercent >= 0) targetState.Distance = 0;
            else targetState.Distance = -1;

            if (speed <= 0) targetState.Speed = - 1;
            else
            {
                var speedPercent = (speed / _session.MaxEntitySpeed) * 100;
                if (speedPercent > 95) targetState.Speed = 9;
                else if (speedPercent > 90) targetState.Speed = 8;
                else if (speedPercent > 80) targetState.Speed = 7;
                else if (speedPercent > 70) targetState.Speed = 6;
                else if (speedPercent > 60) targetState.Speed = 5;
                else if (speedPercent > 50) targetState.Speed = 4;
                else if (speedPercent > 40) targetState.Speed = 3;
                else if (speedPercent > 30) targetState.Speed = 2;
                else if (speedPercent > 20) targetState.Speed = 1;
                else if (speedPercent > 0) targetState.Speed = 0;
                else targetState.Speed = -1;
            }

            MyTuple<bool, bool, float, float, float, int> shieldInfo = new MyTuple<bool, bool, float, float, float, int>();
            if (_session.ShieldApiLoaded) shieldInfo = _session.SApi.GetShieldInfo(target);
            if (shieldInfo.Item1)
            {
                var shieldPercent = shieldInfo.Item5;
                if (shieldPercent > 66) targetState.ShieldHealth = 2;
                else if (shieldPercent > 33) targetState.ShieldHealth = 1;
                else if (shieldPercent > 0) targetState.ShieldHealth = 0;
                else targetState.ShieldHealth = -1;
            }
            else targetState.ShieldHealth = -1;

            var grid = target as MyCubeGrid;
            var friend = false;
            if (grid != null && grid.BigOwners.Count != 0)
            {
                var relation = MyIDModule.GetRelationPlayerBlock(ai.MyOwner, grid.BigOwners[0], MyOwnershipShareModeEnum.Faction);
                if (relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.Friends) friend = true;
            }

            if (friend) targetState.ThreatLvl = -1;
            else
            {
                var offenseRating = targetInfo.OffenseRating;
                if (offenseRating > 2.5) targetState.ThreatLvl = 4;
                else if (offenseRating > 1.25) targetState.ThreatLvl = 3;
                else if (offenseRating > 0.5) targetState.ThreatLvl = 2;
                else if (offenseRating > 0.25) targetState.ThreatLvl = 1;
                else if (offenseRating > 0) targetState.ThreatLvl = 0;
                else targetState.ThreatLvl = -1;
            }

            return true;
        }

        private void IconStatus(string icon, TargetState targetState, out bool displayIcon, out int iconLevel, out int iconSlot)
        {
            bool disable;
            switch (icon)
            {
                case "speed":
                    disable = targetState.Speed == -1;
                    displayIcon = !disable;
                    iconLevel = disable ? 0 : targetState.Speed;
                    iconSlot = 0;
                    break;
                case "size":
                    disable = targetState.Size == -1;
                    displayIcon = !disable;
                    iconLevel = disable ? 0 : targetState.Size;
                    iconSlot = 0;
                    break;
                case "threat":
                    disable = targetState.ThreatLvl == -1;
                    displayIcon = !disable;
                    iconLevel = disable ? 0 : targetState.ThreatLvl;
                    iconSlot = 0;
                    break;
                case "shield":
                    disable = targetState.ShieldHealth == -1;
                    displayIcon = !disable;
                    iconLevel = disable ? 0 : targetState.ShieldHealth;
                    iconSlot = 0;
                    break;
                case "distance":
                    disable = targetState.Size == -1;
                    displayIcon = !disable;
                    iconLevel = disable ? 0 : targetState.Distance;
                    iconSlot = 0;
                    break;
                default:
                    disable = targetState.Size == -1;
                    displayIcon = !disable;
                    iconLevel = 0;
                    iconSlot = 0;
                    break;
            }
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
