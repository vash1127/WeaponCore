using System;
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
        public float SizeExtended;
    }

    public class IconInfo
    {
        private readonly MyStringId _textureName;
        private readonly Vector2D _screenPosition;
        private readonly double _definedScale;
        private readonly int _slotId;
        private readonly bool _canShift;
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
        }

        public void GetTextureInfo(int index, int displayCount, Session session, out MyStringId textureName, out float scale, out Vector3D offset)
        {
            var screenScale = 0.075 * session.ScaleFov;
            var needShift = _slotId != displayCount;
            var shiftSize = _canShift && needShift ? -(0.06f * (_slotId - displayCount)) : 0;

            scale = (float)(_definedScale * screenScale);

            var position = new Vector3D(_screenPosition.X + shiftSize - (index * 0.45), _screenPosition.Y, 0);
            position.X *= screenScale * session.AspectRatio;
            position.Y *= screenScale;
            offset = Vector3D.Transform(new Vector3D(position.X, position.Y, -.1), session.CameraMatrix);

            textureName = _textureName;
            _prevSlotId[index] = displayCount;
        }
    }
}
