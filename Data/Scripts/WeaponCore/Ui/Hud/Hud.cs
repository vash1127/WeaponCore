using Sandbox.ModAPI;
using System;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using VRageRender;
using WeaponCore.Support;
using static VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{
    partial class Hud
    {

        internal void AddText(string text, Vector4 color, float x, float y, float fontSize = 10f)
        {
            var textRequest = _textDrawPool.Get();
            textRequest.Text = text;
            textRequest.Color = color;
            textRequest.X = x;
            textRequest.Y = y;
            textRequest.FontSize = fontSize;
            TextAddList.Add(textRequest);
        }

        internal void CreateText()
        {
            for (int i = 0; i < TextAddList.Count; i++)
            {
                var tdr = TextAddList[i];
                
                var fov = _session.Camera.FovWithZoom;
                var aspectratio = _session.Camera.ViewportSize.X / _session.Camera.ViewportSize.Y;
                var cameraWorldMatrix = _session.Camera.WorldMatrix;
                var scale = 0.075 * Math.Tan(fov * .5f);
                var position = new Vector3D(tdr.X, tdr.Y, -.1);

                position.X *= scale * aspectratio;
                position.Y *= scale;
                position = Vector3D.Transform(position, cameraWorldMatrix);
                
                var left = cameraWorldMatrix.Left;
                var up = cameraWorldMatrix.Up;

                var height = tdr.FontSize / pixelsInMeter;
                var width = height * (aspectratio * .25f);
                var textPos = position;

                for (int j = 0; j < tdr.Text.Length; j++)
                {
                    var cm = _characterMap[tdr.Text[j]];

                    var tdd = _textureDrawPool.Get();

                    tdd.Material = cm.Material;
                    tdd.Color = tdr.Color;
                    tdd.Position = textPos;
                    tdd.Up = up;
                    tdd.Left = left;
                    tdd.Width = width;
                    tdd.Height = height;
                    tdd.UvOffset = cm.UvOffset;
                    tdd.UvSize = cm.UvSize;
                    tdd.TextureSize = cm.TextureSize;

                    DrawList.Add(tdd);

                    textPos -= (left * width * aspectratio);
                }
                _textDrawPool.Return(tdr);
            }
            TextAddList.Clear();
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
