using System;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using static VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{
    partial class Hud
    {
        
        internal void AddText(string text, Vector4 color, float x, float y, float fontSize = 10f)
        {
            var textInfo = _textDrawPool.Get();
            textInfo.Text = text;
            textInfo.Color = color;
            textInfo.X = x;
            textInfo.Y = y;
            textInfo.FontSize = fontSize;
            TextAddList.Add(textInfo);

            TexturesToAdd++;
        }

        internal void AddTexture(MyStringId material, Vector4 color, float x, float y, float width, float height, int textureSize, int uvOffsetX = 0, int uvOffsetY = 0, int uvSizeX = 1, int uvSizeY = 1)
        {
            var position = new Vector3D(x, y, -.1);
            var tdd = _textureDrawPool.Get();

            tdd.Material = material;
            tdd.Color = color;
            tdd.Position = position;
            tdd.Width = width / _pixelsInMeter;
            tdd.Height = height / _pixelsInMeter;
            tdd.UvOffset = new Vector2(uvOffsetX, uvOffsetY);
            tdd.UvSize = new Vector2(uvSizeX, uvSizeY);
            tdd.TextureSize = textureSize;

            TextureAddList.Add(tdd);

            TexturesToAdd++;
        }

        internal void CreateTextures()
        { 
            _aspectratio = _session.Camera.ViewportSize.X / _session.Camera.ViewportSize.Y;
            _cameraWorldMatrix = _session.Camera.WorldMatrix;
            _scale = 0.075 * Math.Tan(_session.Camera.FovWithZoom * .5f);

            for (int i = 0; i < TextAddList.Count; i++)
            {
                var textAdd = TextAddList[i];
                var position = new Vector3D(textAdd.X, textAdd.Y, -.1);
                position.X *= _scale * _aspectratio;
                position.Y *= _scale;
                position = Vector3D.Transform(position, _cameraWorldMatrix);

                var height = textAdd.FontSize / _pixelsInMeter;
                var width = height * (_aspectratio * .25f);
                var textPos = position;

                for (int j = 0; j < textAdd.Text.Length; j++)
                {
                    var cm = _characterMap[textAdd.Text[j]];
                    var tdd = _textureDrawPool.Get();

                    tdd.Material = cm.Material;
                    tdd.Color = textAdd.Color;
                    tdd.Position = textPos;
                    tdd.Up = _cameraWorldMatrix.Up;
                    tdd.Left = _cameraWorldMatrix.Left;
                    tdd.Width = width;
                    tdd.Height = height;
                    tdd.UvOffset = cm.UvOffset;
                    tdd.UvSize = cm.UvSize;
                    tdd.TextureSize = cm.TextureSize;

                    DrawList.Add(tdd);

                    textPos -= (_cameraWorldMatrix.Left * width * _aspectratio);
                }

                _textDrawPool.Return(textAdd);
            }
            TextAddList.Clear();

            for (int i = 0; i < TextureAddList.Count; i++)
            {
                var tdd = TextureAddList[i];

                tdd.Position.X *= _scale * _aspectratio;
                tdd.Position.Y *= _scale;
                tdd.Position = Vector3D.Transform(tdd.Position, _cameraWorldMatrix);
                tdd.Up = _cameraWorldMatrix.Up;
                tdd.Left = _cameraWorldMatrix.Left;
                DrawList.Add(tdd);
            }
            TextureAddList.Clear();

            TexturesToAdd = 0;
        }

        internal void DrawTextures()
        {
            for(int i = 0; i < DrawList.Count; i++)
            {
                var textureToDraw = DrawList[i];
                var p0 = new Vector2(textureToDraw.UvOffset.X, textureToDraw.UvOffset.Y) / textureToDraw.TextureSize;
                var p1 = new Vector2(textureToDraw.UvOffset.X + textureToDraw.UvSize.X, textureToDraw.UvOffset.Y) / textureToDraw.TextureSize;
                var p2 = new Vector2(textureToDraw.UvOffset.X, textureToDraw.UvOffset.Y + textureToDraw.UvSize.Y) / textureToDraw.TextureSize;
                var p3 = new Vector2(textureToDraw.UvOffset.X + textureToDraw.UvSize.X, textureToDraw.UvOffset.Y + textureToDraw.UvSize.Y) / textureToDraw.TextureSize;

                MyQuadD quad;
                MyUtils.GetBillboardQuadOriented(out quad, ref textureToDraw.Position, textureToDraw.Width, textureToDraw.Height, ref textureToDraw.Left, ref textureToDraw.Up);

                MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point1, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, p0, p1, p3, textureToDraw.Material, 0, textureToDraw.Position, textureToDraw.Color, textureToDraw.Blend);
                MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point3, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, p0, p2, p3, textureToDraw.Material, 0, textureToDraw.Position, textureToDraw.Color, textureToDraw.Blend);
                _textureDrawPool.Return(textureToDraw);
            }
            DrawList.Clear();
        }
    }
}
