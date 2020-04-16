using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Collections;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using VRageRender;
using static VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{
    partial class Hud
    {
        private const float _pixelsInMeter = 3779.52f;
        private readonly MyConcurrentPool<TextureDrawData> _textureDrawPool = new MyConcurrentPool<TextureDrawData>(256, tdd => tdd.Clean());

        private readonly MyConcurrentPool<TextDrawRequest> _textDrawPool = new MyConcurrentPool<TextDrawRequest>(256, tdr => tdr.Clean());

        private Session _session;
        private Dictionary<char, TextureMap> _characterMap;
        private MyStringId _monoFontAtlas1 = MyStringId.GetOrCompute("MonoFontAtlas");
        private MatrixD _cameraWorldMatrix;
        private uint _lastPostionUpdateTick;
        private float _aspectratio;
        private double _scale;

        public List<TextureDrawData> DrawList = new List<TextureDrawData>();
        public List<TextureDrawData> TextureAddList = new List<TextureDrawData>();
        public List<TextDrawRequest> TextAddList = new List<TextDrawRequest>();
        public int TexturesToAdd;

        internal struct TextureMap
        {
            internal MyStringId Material;
            internal Vector2 UvOffset;
            internal Vector2 UvSize;
            internal float TextureSize;
        }        

        public Hud(Session session)
        {
            _session = session;
            _characterMap = new Dictionary<char, TextureMap>
            {
                [' '] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(0, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['!'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(30, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['"'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(60, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['#'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(90, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['$'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(120, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['%'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(150, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['&'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(180, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['\''] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(210, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['('] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(240, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                [')'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(270, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['*'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(300, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['+'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(330, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                [','] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(360, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['-'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(390, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['.'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(420, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['/'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(450, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['0'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(480, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['1'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(510, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['2'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(540, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['3'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(570, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['4'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(600, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['5'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(630, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['6'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(660, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['7'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(690, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['8'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(720, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['9'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(750, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                [':'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(780, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                [';'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(810, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['<'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(840, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['='] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(870, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['>'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(900, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['?'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(930, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['@'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(960, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['A'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(990, 0), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['B'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(0, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['C'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(30, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['D'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(60, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['E'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(90, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['F'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(120, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['G'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(150, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['H'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(180, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['I'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(210, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['J'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(240, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['K'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(270, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['L'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(300, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['M'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(330, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['N'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(360, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['O'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(390, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['P'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(420, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['Q'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(450, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['R'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(480, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['S'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(510, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['T'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(540, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['U'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(570, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['V'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(600, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['W'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(630, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['X'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(660, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['Y'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(690, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['Z'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(720, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['['] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(750, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['\\'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(780, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                [']'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(810, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['^'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(840, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['_'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(870, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['`'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(900, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['a'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(930, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['b'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(960, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['c'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(990, 44), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['d'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(0, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['e'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(30, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['f'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(60, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['g'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(90, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['h'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(120, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['i'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(150, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['j'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(180, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['k'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(210, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['l'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(240, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['m'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(270, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['n'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(300, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['o'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(330, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['p'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(360, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['q'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(390, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['r'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(420, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['s'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(450, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['t'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(480, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['u'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(510, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['v'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(540, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['w'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(570, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['x'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(600, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['y'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(630, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['z'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(660, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['{'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(690, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['|'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(720, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['}'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(750, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
                ['~'] = new TextureMap { Material = _monoFontAtlas1, UvOffset = new Vector2(780, 88), UvSize = new Vector2(30, 42), TextureSize = 1024 },
            };
        }

        public class TextDrawRequest
        {
            public string Text;
            public Vector4 Color;
            public float X;
            public float Y;
            public float FontSize = 10f;

            public void Clean()
            {
                Text = null;
                Color = Vector4.Zero;
                X = 0;
                Y = 0;
                FontSize = 10f;
            }
        }

        public class TextureDrawData
        {
            public MyStringId Material;
            public Color Color;
            public Vector3D Position;
            public Vector3 Up;
            public Vector3 Left;
            public float Width;
            public float Height;
            public Vector2 UvOffset;
            public Vector2 UvSize;
            public float TextureSize;
            public MyBillboard.BlendTypeEnum Blend = PostPP;

            public TextureDrawData() { }

            public void Clean()
            {
                Material = MyStringId.NullOrEmpty;
                Color = Color.Transparent;
                Position = Vector3D.Zero;
                Up = Vector3.Zero;
                Left = Vector3.Zero;
                Width = 0;
                Height = 0;
                UvOffset = Vector2.Zero;
                UvSize = Vector2.Zero;
                TextureSize = 0;
                Blend = PostPP;
            }
        }
    }
}
