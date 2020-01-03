using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace WeaponCore
{
    public class TargetStatus
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
        private readonly bool _canShift;
        private readonly float[] _adjustedScale;
        private readonly Vector3D[] _positionOffset;
        private readonly Vector3D[] _altPositionOffset;
        private readonly int[] _prevSlotId;

        public IconInfo(MyStringId textureName, double definedScale, Vector2D screenPosition, int slotId, bool canShift)
        {
            _textureName = textureName;
            _definedScale = definedScale;
            _screenPosition = screenPosition;
            _slotId = slotId;
            _canShift = canShift;
            _prevSlotId = new int[2];
            for (int i = 0; i < _prevSlotId.Length; i++)
                _prevSlotId[i] = -1;

            _adjustedScale = new float[2];
            _positionOffset = new Vector3D[2];
            _altPositionOffset = new Vector3D[2];
        }

        public void GetTextureInfo(int index, int displayCount, bool altPosition, Session session, out MyStringId textureName, out float scale, out Vector3D offset)
        {
            if (displayCount != _prevSlotId[index]) InitOffset(index, displayCount);
            textureName = _textureName;
            scale = _adjustedScale[index];
            offset = !altPosition ? Vector3D.Transform(_positionOffset[index], session.CameraMatrix) : Vector3D.Transform(_altPositionOffset[index], session.CameraMatrix);
            _prevSlotId[index] = displayCount;
        }

        private void InitOffset(int index, int displayCount)
        {
            var fov = MyAPIGateway.Session.Camera.FovWithZoom;
            var screenScale = 0.075 * Math.Tan(fov * 0.5);
            const float slotSpacing = 0.06f;
            var needShift = _slotId != displayCount;
            var shiftSize = _canShift && needShift ? -(slotSpacing * (_slotId - displayCount)) : 0;
            var position = new Vector3D(_screenPosition.X + shiftSize - (index * 0.45), _screenPosition.Y, 0);
            var altPosition = new Vector3D(_screenPosition.X + shiftSize - (index * 0.45), _screenPosition.Y - 0.75, 0);

            double aspectratio = MyAPIGateway.Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;

            position.X *= screenScale * aspectratio;
            position.Y *= screenScale;
            altPosition.X *= screenScale * aspectratio;
            altPosition.Y *= screenScale;
            _adjustedScale[index] = (float)(_definedScale * screenScale);
            _positionOffset[index] = new Vector3D(position.X, position.Y, -.1);
            _altPositionOffset[index] = new Vector3D(altPosition.X, altPosition.Y, -.1);
        }
    }
}
