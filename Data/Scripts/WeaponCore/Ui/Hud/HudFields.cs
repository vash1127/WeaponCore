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
        private readonly MyConcurrentPool<TextureDrawData> _textureDrawPool = new MyConcurrentPool<TextureDrawData>(512, tdd => tdd.Clean());

        private readonly MyConcurrentPool<TextDrawRequest> _textDrawPool = new MyConcurrentPool<TextDrawRequest>(256, tdr => tdr.Clean());

        private Session _session;
        private Dictionary<char, TextureMap> _characterMap;
        private MyStringId _monoFontAtlas1 = MyStringId.GetOrCompute("MonoFontAtlas");
        private MatrixD _cameraWorldMatrix;
        private float _aspectratio;
        private double _scale;

        public int TexturesToAdd;

        public List<TextureDrawData> TextureAddList = new List<TextureDrawData>();
        public List<TextDrawRequest> TextAddList = new List<TextDrawRequest>();

        public List<TextureDrawData> UvDrawList = new List<TextureDrawData>();
        public List<TextureDrawData> SimpleDrawList = new List<TextureDrawData>();

        

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
            LoadTextMaps(out _characterMap); // possible translations in future
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
