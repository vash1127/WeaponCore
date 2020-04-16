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
        internal void DrawText(string text, Vector4 color, Vector2 pos, float fontSize = 10f)
        {
            var position = new Vector3D(pos.X, pos.Y, 0);
            var fov = _session.Camera.FovWithZoom;
            var aspectratio = _session.Camera.ViewportSize.X / _session.Camera.ViewportSize.Y;
            var cameraWorldMatrix = _session.Camera.WorldMatrix;
            var scale = 0.075 * Math.Tan(fov * .5f);

            position.X *= scale * aspectratio;
            position.Y *= scale;
            position = Vector3D.Transform(new Vector3D(position.X, position.Y, -.1), cameraWorldMatrix);

            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;

            var height = fontSize / 3779.52f;// magic number, pixels in a meter
            var width = height * (aspectratio * .25f);

            var chars = text.ToCharArray();

            var textPos = position;
            for (int i = 0; i < chars.Length; i++)
            {
                var cm = _characterMap[chars[i]];

                var tdd = _textureDrawPool.Get();

                tdd.Material = cm.Material;
                tdd.Color = color;
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

        }

        internal void DrawTextures()
        {
            for(int i = DrawList.Count - 1; i >= 0 ; i--)
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
