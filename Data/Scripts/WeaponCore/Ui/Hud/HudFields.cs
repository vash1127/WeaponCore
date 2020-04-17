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

        ///
        ///weapon Hud Settings
        ///
        private const float _padding = 10 * _metersInPixel;
        private const float _WeaponHudFontSize = 3f;
        private const float _WeaponHudFontHeight = _WeaponHudFontSize * _metersInPixel;
        private const float _reloadWidth = 25 * _metersInPixel;
        private const float _reloadWidthOffset = _reloadWidth + _padding;
        private const float _reloadHeight = 20 * _metersInPixel;
        private const float _reloadHeightOffset = _reloadHeight * .625f;
        private const float _heatWidth = 40 * _metersInPixel;
        private const float _heatWidthOffset = _heatWidth + _padding;
        private const float _heatHeight = _heatWidth * 0.0625f;
        private const float _heatHeightOffset = _heatHeight * 2f;

        private readonly Vector2 _heatTexutureSize = new Vector2(1024, 128);
        private readonly TextureMap _reloadingTexture;
        private readonly TextureMap[] _heatBarTexture = new TextureMap[11];
        ///
        /// 
        ///

        private readonly ConcurrentQueue<TextureDrawData> _textureDrawPool = new ConcurrentQueue<TextureDrawData>();
        private readonly ConcurrentQueue<TextDrawRequest> _textDrawPool = new ConcurrentQueue<TextDrawRequest>();

        private Session _session;
        private Dictionary<char, TextureMap> _characterMap;
        private MyStringId _monoFontAtlas1 = MyStringId.GetOrCompute("MonoFontAtlas");
        private MatrixD _cameraWorldMatrix;
        private List<TextureDrawData> TextureAddList = new List<TextureDrawData>(256);
        private List<TextDrawRequest> TextAddList = new List<TextDrawRequest>(256);
        private List<TextureDrawData> UvDrawList = new List<TextureDrawData>(512);
        private List<TextureDrawData> SimpleDrawList = new List<TextureDrawData>(256);
        private float _aspectratio;

        internal int TexturesToAdd;
        internal Vector2 CurrWeaponDisplayPos;
        internal List<Weapon> WeaponsToDisplay = new List<Weapon>(128);


        internal Hud(Session session)
        {
            _session = session;
            LoadTextMaps(out _characterMap); // possible translations in future
            _reloadingTexture = GenerateMap(MyStringId.GetOrCompute("ReloadingText"), 0, 0, 128, 128, 128, 128);

            for (int i = 0; i < _heatBarTexture.Length; i++)
            {
                var offset = 64f * i;
                _heatBarTexture[i] = GenerateMap(MyStringId.GetOrCompute("HeatAtlasBar"), 0, offset, 1024, 64, 1024, 1024);
            }

            for (int i = 0; i < _initialPoolCapacity; i++)
            {
                _textureDrawPool.Enqueue(new TextureDrawData());
                _textDrawPool.Enqueue(new TextDrawRequest());
            }
        }

        internal class TextDrawRequest
        {
            internal string Text;
            internal Color Color;
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
            internal Vector2 P0;
            internal Vector2 P1;
            internal Vector2 P2;
            internal Vector2 P3;
            internal float Width;
            internal float Height;
            internal bool Persistant;
            internal MyBillboard.BlendTypeEnum Blend = PostPP;
        }

        internal struct TextureMap
        {
            internal MyStringId Material;
            internal Vector2 P0;
            internal Vector2 P1;
            internal Vector2 P2;
            internal Vector2 P3;
        }
    }
}
