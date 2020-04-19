using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private const float _WeaponHudFontSize = 3.8f;
        private const float _WeaponHudFontHeight = _WeaponHudFontSize * _metersInPixel;
        private const float _reloadHeight = 2.2f * _metersInPixel;
        private const float _reloadWidth = _reloadHeight * (118 / 19);//size of reloading texture
        private const float _reloadHeightOffset = _reloadHeight * .625f;
        private const float _heatWidth = 40 * _metersInPixel;
        private const float _heatWidthOffset = _heatWidth + (_padding * 1.8f);
        private const float _heatHeight = _heatWidth * 0.0625f;
        private const float _heatHeightOffset = _heatHeight * 2f;
        private const float _infoPanelOffset = _WeaponHudFontHeight + _heatHeightOffset;
        private const float _defaultFov = 1.22f;
        private const float _bgBorderRatio = .166f;
        private const uint _minUpdateTicks = 60;

        private readonly TextureMap _reloadingTexture;
        private readonly TextureMap[] _infoBackground = new TextureMap[3];
        private readonly TextureMap[] _heatBarTexture = new TextureMap[11];
        private readonly Color _bgColor = new Color(40, 54, 62, 1);
        private int _currentLargestName;
        ///
        /// 
        ///

        private readonly ConcurrentQueue<TextureDrawData> _textureDrawPool = new ConcurrentQueue<TextureDrawData>();
        private readonly ConcurrentQueue<TextDrawRequest> _textDrawPool = new ConcurrentQueue<TextDrawRequest>();
        private readonly Queue<List<Weapon>> _weaponSortingListPool = new Queue<List<Weapon>>(_initialPoolCapacity);
        private readonly Queue<StackedWeaponInfo> _weaponStackedInfoPool = new Queue<StackedWeaponInfo>(_initialPoolCapacity);
        private readonly Queue<List<StackedWeaponInfo>> _weaponInfoListPool = new Queue<List<StackedWeaponInfo>>(_initialPoolCapacity);
        private readonly Queue<List<List<Weapon>>> _weaponSubListsPool = new Queue<List<List<Weapon>>>(_initialPoolCapacity);

        private Session _session;
        private Dictionary<char, TextureMap> _characterMap;
        private MyStringId _monoFontAtlas1 = MyStringId.GetOrCompute("MonoFontAtlas");
        private MatrixD _cameraWorldMatrix;
        private Vector4 _viewPortSize = new Vector4();
        private List<TextureDrawData> _textureAddList = new List<TextureDrawData>(256);
        private List<TextDrawRequest> _textAddList = new List<TextDrawRequest>(256);
        private List<TextureDrawData> _uvDrawList = new List<TextureDrawData>(_initialPoolCapacity);
        private List<TextureDrawData> _simpleDrawList = new List<TextureDrawData>(256);
        private List<StackedWeaponInfo> _weapontoDraw = new List<StackedWeaponInfo>(256);
        private float _aspectratio;
        private uint _lastHudUpdateTick;

        internal int TexturesToAdd;
        internal Vector2 CurrWeaponDisplayPos;
        internal List<Weapon> WeaponsToDisplay = new List<Weapon>(128);


        internal Hud(Session session)
        {
            _session = session;
            LoadTextMaps(out _characterMap); // possible translations in future
            _reloadingTexture = GenerateMap(MyStringId.GetOrCompute("ReloadingText"), 0, 0, 118, 19, 128, 64);

            _infoBackground[0] = GenerateMap(MyStringId.GetOrCompute("WeaponStatWindow"), 0, 0, 768, 128, 768, 384);
            _infoBackground[1] = GenerateMap(MyStringId.GetOrCompute("WeaponStatWindow"), 0, 128, 768, 128, 768, 384);
            _infoBackground[2] = GenerateMap(MyStringId.GetOrCompute("WeaponStatWindow"), 0, 256, 768, 128, 768, 384);

            for (int i = 0; i < _heatBarTexture.Length; i++)
            {
                var offset = 64f * i;
                _heatBarTexture[i] = GenerateMap(MyStringId.GetOrCompute("HeatAtlasBar"), 0, offset, 1024, 64, 1024, 1024);
            }

            for (int i = 0; i < _initialPoolCapacity; i++)
            {
                _textureDrawPool.Enqueue(new TextureDrawData());
                _textDrawPool.Enqueue(new TextDrawRequest());
                _weaponSortingListPool.Enqueue(new List<Weapon>());
                _weaponStackedInfoPool.Enqueue(new StackedWeaponInfo());
                _weaponInfoListPool.Enqueue(new List<StackedWeaponInfo>());
                _weaponSubListsPool.Enqueue(new List<List<Weapon>>());
            }
        }

        internal struct TextDrawRequest
        {
            internal string Text;
            internal Color Color;
            internal float X;
            internal float Y;
            internal float FontSize;
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

        internal struct StackedWeaponInfo
        {
            internal Weapon HighestValueWeapon;
            internal int WeaponStack;
        }
    }
}
