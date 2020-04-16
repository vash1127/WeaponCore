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
using static VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{
    partial class Hud
    {
        private const float _pixelsInMeter = 3779.52f;
        private const int _initialPoolCapacity = 512;

        private readonly ConcurrentQueue<TextureDrawData> _textureDrawPool = new ConcurrentQueue<TextureDrawData>();
        private readonly ConcurrentQueue<TextDrawRequest> _textDrawPool = new ConcurrentQueue<TextDrawRequest>();

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

            for (int i = 0; i < _initialPoolCapacity; i++)
            {
                _textureDrawPool.Enqueue(new TextureDrawData());
                _textDrawPool.Enqueue(new TextDrawRequest());
            }

        }

        public class TextDrawRequest
        {
            public string Text;
            public Vector4 Color;
            public float X;
            public float Y;
            public float FontSize = 10f;
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
        }
    }
}
