using ParallelTasks;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Collections;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using VRageRender;
using WeaponCore.Platform;
using static VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{
    partial class Hud
    {
        private const float _metersInPixel = 0.0002645833f;
        private const int _initialPoolCapacity = 512;
        
        //weapon Hud Settings
        private const float _WeaponHudFontSize = 5f;
        private const float _WeaponHudFontHeight = _WeaponHudFontSize * _metersInPixel;
        private const float _reloadWidth = 25 * _metersInPixel;
        private const float _reloadWidthOffset = 30 * _metersInPixel;
        private const float _reloadHeight = 35 * _metersInPixel;
        private const float _reloadHeightOffset = _reloadHeight * .625f;


        private readonly ConcurrentQueue<TextureDrawData> _textureDrawPool = new ConcurrentQueue<TextureDrawData>();
        private readonly ConcurrentQueue<TextDrawRequest> _textDrawPool = new ConcurrentQueue<TextDrawRequest>();

        private Session _session;
        private Dictionary<char, TextureMap> _characterMap;
        private MyStringId _monoFontAtlas1 = MyStringId.GetOrCompute("MonoFontAtlas");
        private MyStringId _heatAtlas = MyStringId.GetOrCompute("HeatAtlas");
        private MatrixD _cameraWorldMatrix;
        private float _aspectratio;

        internal int TexturesToAdd;
        internal Vector2 CurrWeaponDisplayPos;

        internal List<TextureDrawData> TextureAddList = new List<TextureDrawData>(256);
        internal List<TextDrawRequest> TextAddList = new List<TextDrawRequest>(256);
        internal List<TextureDrawData> UvDrawList = new List<TextureDrawData>(512);
        internal List<TextureDrawData> SimpleDrawList = new List<TextureDrawData>(256);
        internal List<Weapon> WeaponsToDisplay = new List<Weapon>(128);
        internal HashSet<Weapon> WeaponsToDisplayCheck = new HashSet<Weapon>();


        internal struct TextureMap
        {
            internal MyStringId Material;
            internal Vector2 UvOffset;
            internal Vector2 UvSize;
            internal Vector2 TextureSize;
        }

        internal Hud(Session session)
        {
            _session = session;
            LoadTextMaps(out _characterMap); // possible translations in future

            for (int i = 0; i < _initialPoolCapacity; i++)
            {
                _textureDrawPool.Enqueue(new TextureDrawData());
                _textDrawPool.Enqueue(new TextDrawRequest());
            }
        }

        internal class TextDrawRequest
        {
            internal string Text;
            internal Vector4 Color;
            internal float X;
            internal float Y;
            internal float FontSize = 10f;
        }

        internal class TextureDrawData
        {
            internal MyStringId Material;
            internal Color Color;
            internal Vector3D Position;
            internal Vector3 Up;
            internal Vector3 Left;
            internal Vector2 UvOffset;
            internal Vector2 UvSize;
            internal Vector2 TextureSize;
            internal float Width;
            internal float Height;
            internal bool Persistant;
            internal MyBillboard.BlendTypeEnum Blend = PostPP;
        }
    }
}
