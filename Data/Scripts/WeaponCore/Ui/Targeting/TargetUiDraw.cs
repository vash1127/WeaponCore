using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{
    internal partial class TargetUi
    {
        internal void DrawTargetUi()
        {
            var s = _session;
            DrawReticle = false;
            if (!s.UpdateLocalAiAndCockpit() || s.TrackingAi == null) return;

            if (!s.WheelUi.WheelActive && ActivateSelector()) DrawSelector();
            if (s.UiInput.ShiftReleased) s.TrackingAi.Focus.NextActive();
            if (s.CheckTarget(s.TrackingAi) && s.TrackingAi.GetTargetState()) DrawTarget();
        }

        private void DrawSelector()
        {
            var s = _session;
            if (!_cachedPointerPos) InitPointerOffset(0.05);
            if (!_cachedTargetPos) InitTargetOffset();
            var offetPosition = Vector3D.Transform(PointerOffset, _session.CameraMatrix);

            if (_firstPerson)
            {
                if (!MyUtils.IsZero(_pointerPosition.Y))
                {
                    _pointerPosition.Y = 0f;
                    InitPointerOffset(0.05);
                    Log.Line("reset cursor");
                }
            }
            else if (_ctrlPressed)
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

            if (s.Tick10)
                RayCheckTargets(offetPosition, Vector3D.Normalize(offetPosition - s.CameraPos), true, true);

            MyTransparentGeometry.AddBillboardOriented(_cross, _reticleColor, offetPosition, s.CameraMatrix.Left, s.CameraMatrix.Up, (float)PointerAdjScale, BlendTypeEnum.PostPP);
            DrawReticle = true;
        }

        private void DrawTarget()
        {
            var s = _session;
            var focus = s.TrackingAi.Focus;
            for (int i = 0; i < focus.TargetState.Length; i++)
            {
                var displayCount = 0;   
                if (focus.Target[i] == null || s.WheelUi.WheelActive && i > 0) continue;

                var targetState = focus.TargetState[i];
                foreach (var icon in _targetIcons.Keys)
                {
                    int iconLevel;
                    if (!IconStatus(icon, targetState, out iconLevel)) continue;

                    Vector3D offset;
                    float scale;
                    MyStringId textureName;
                    _targetIcons[icon][iconLevel].GetTextureInfo(i, displayCount, s.WheelUi.WheelActive, s, out textureName, out scale, out offset);
                    MyTransparentGeometry.AddBillboardOriented(textureName, Color.White, offset, s.CameraMatrix.Left, s.CameraMatrix.Up, scale, BlendTypeEnum.PostPP);
                    displayCount++;
                }

                if (i == focus.ActiveId)
                {
                    var targetSphere = focus.Target[focus.ActiveId].PositionComp.WorldVolume;
                    var targetCenter = targetSphere.Center;
                    var screenPos = s.Camera.WorldToScreen(ref targetCenter);
                    var fov = MyAPIGateway.Session.Camera.FovWithZoom;
                    double aspectratio = MyAPIGateway.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
                    var screenScale = 0.1 * Math.Tan(fov * 0.5);
                    if (Vector3D.Transform(targetCenter, s.Camera.ViewMatrix).Z > 0)
                    {
                        screenPos.X *= -1;
                        screenPos.Y = -1;
                    }

                    var dotpos = new Vector2D(MathHelper.Clamp(screenPos.X, -0.98, 0.98), MathHelper.Clamp(screenPos.Y, -0.98, 0.98));

                    dotpos.X *= (float)(screenScale * aspectratio);
                    dotpos.Y *= (float)screenScale;
                    screenPos = Vector3D.Transform(new Vector3D(dotpos.X, dotpos.Y, -0.1), s.CameraMatrix);
                    MyTransparentGeometry.AddBillboardOriented(_cross, Color.Red, screenPos, s.CameraMatrix.Left, s.CameraMatrix.Up, (float)screenScale * 0.1f, BlendTypeEnum.PostPP);
                }
            }
        }

        private static bool IconStatus(string icon, TargetStatus targetState, out int iconLevel)
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
                    display = targetState.Distance > -1;
                    iconLevel = !display ? 0 : targetState.Distance;
                    break;
                default:
                    display = false;
                    iconLevel = 0;
                    break;
            }

            return display;
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

            TargetOffset = new Vector3D(position.X, position.Y, -0.1);
            _cachedTargetPos = true;
        }
    }
}
