using System;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static VRageRender.MyBillboard.BlendTypeEnum;

namespace WeaponCore
{
    partial class Hud
    {
        
        internal void AddText(string text, Vector4 color, float x, float y, float fontSize = 10f)
        {
            TextDrawRequest textInfo;

            if (!_textDrawPool.TryDequeue(out textInfo))
                textInfo = new TextDrawRequest();

            textInfo.Text = text;
            textInfo.Color = color;
            textInfo.X = x;
            textInfo.Y = y;
            textInfo.FontSize = fontSize;
            TextAddList.Add(textInfo);

            TexturesToAdd++;
        }

        internal void AddTexture(MyStringId material, Vector4 color, float x, float y, float width, float height, int textureSizeX, int textureSizeY, int uvOffsetX = 0, int uvOffsetY = 0, int uvSizeX = 1, int uvSizeY = 1)
        {
            var position = new Vector3D(x, y, -.1);
            TextureDrawData tdd;

            if (!_textureDrawPool.TryDequeue(out tdd))
                tdd = new TextureDrawData();

            tdd.Material = material;
            tdd.Color = color;
            tdd.Position = position;
            tdd.Width = width * _metersInPixel;
            tdd.Height = height * _metersInPixel;
            tdd.UvOffset = new Vector2(uvOffsetX, uvOffsetY);
            tdd.UvSize = new Vector2(uvSizeX, uvSizeY);
            tdd.TextureSize = new Vector2(textureSizeX, textureSizeY);

            TextureAddList.Add(tdd);

            TexturesToAdd++;
        }

        internal void AddTexture(MyStringId material, Vector4 color, float x, float y, float scale)
        {
            var position = new Vector3D(x, y, -.1);
            TextureDrawData tdd;

            if (!_textureDrawPool.TryDequeue(out tdd))
                tdd = new TextureDrawData();

            tdd.Material = material;
            tdd.Color = color;
            tdd.Position = position;
            tdd.Height = scale;

            SimpleDrawList.Add(tdd);

            TexturesToAdd++;
        }

        internal void DrawTextures()
        {
            _aspectratio = _session.Camera.ViewportSize.Y / _session.Camera.ViewportSize.X;
            _cameraWorldMatrix = _session.Camera.WorldMatrix;

            CurrWeaponDisplayPos = new Vector2((_session.Camera.ViewportSize.X * .25f) * _metersInPixel, (_session.Camera.ViewportSize.Y * .125f) * _metersInPixel);

            #region WeaponHudDisplay

            for (int i = 0; i < WeaponsToDisplay.Count; i++)
            {
                TextDrawRequest textInfo;
                TextureDrawData tdd;
                TextureDrawData tdd2;

                var weapon = WeaponsToDisplay[i];
                var name = weapon.System.WeaponName + ": ";
                var textOffset = name.Length * _WeaponHudFontHeight;
                if (weapon.State.Sync.Reloading)
                {
                    textOffset += _reloadWidthOffset * 1.5f;

                    if (!_textureDrawPool.TryDequeue(out tdd))
                        tdd = new TextureDrawData();

                    tdd.Material = MyStringId.GetOrCompute("ReloadingText");
                    tdd.Color = Color.Red;
                    tdd.Position = new Vector3D(CurrWeaponDisplayPos.X - _reloadWidthOffset, CurrWeaponDisplayPos.Y + _reloadHeightOffset, -.1f);
                    tdd.Width = _reloadWidth;
                    tdd.Height = _reloadHeight;
                    tdd.UvOffset = new Vector2(0, 0);
                    tdd.UvSize = new Vector2(128, 128);
                    tdd.TextureSize = new Vector2(128, 128);

                    TextureAddList.Add(tdd);
                }

                
                if (!_textDrawPool.TryDequeue(out textInfo))
                    textInfo = new TextDrawRequest();

                textInfo.Text = name;
                textInfo.Color = Color.White;
                textInfo.X = CurrWeaponDisplayPos.X - textOffset;
                textInfo.Y = CurrWeaponDisplayPos.Y;
                textInfo.FontSize = _WeaponHudFontSize;
                TextAddList.Add(textInfo);

                /* heat texture
                if (!_textureDrawPool.TryDequeue(out tdd2))
                    tdd2 = new TextureDrawData();

                tdd2.Material = _heatAtlas;
                tdd2.Color = Color.Red;
                tdd2.Position = new Vector3D(0, 0, -.1f);
                tdd2.Width = 100;
                tdd2.Height = 100;
                tdd2.UvOffset = new Vector2(0, 0);
                tdd2.UvSize = new Vector2(640, 71);
                tdd2.TextureSize = new Vector2(640, 71);

                TextureAddList.Add(tdd2);*/

                CurrWeaponDisplayPos.Y -= _WeaponHudFontHeight * 1.7f;
            }
            #endregion

            #region UV Offset based draws
            for (int i = 0; i < TextAddList.Count; i++)
            {
                var textAdd = TextAddList[i];
                var position = new Vector3D(textAdd.X, textAdd.Y, -.1);
                position = Vector3D.Transform(position, _cameraWorldMatrix);

                var height = textAdd.FontSize * _metersInPixel;
                var width = height * _aspectratio;
                var textPos = position;

                for (int j = 0; j < textAdd.Text.Length; j++)
                {
                    var cm = _characterMap[textAdd.Text[j]];
                    TextureDrawData tdd;

                    if (!_textureDrawPool.TryDequeue(out tdd))
                        tdd = new TextureDrawData();

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

                    UvDrawList.Add(tdd);

                    textPos -= (_cameraWorldMatrix.Left * height);
                }

                _textDrawPool.Enqueue(textAdd);
            }

            for (int i = 0; i < TextureAddList.Count; i++)
            {
                var tdd = TextureAddList[i];
                
                tdd.Position = Vector3D.Transform(tdd.Position, _cameraWorldMatrix);
                tdd.Up = _cameraWorldMatrix.Up;
                tdd.Left = _cameraWorldMatrix.Left;
                UvDrawList.Add(tdd);
            }

            for (int i = 0; i < UvDrawList.Count; i++)
            {
                var textureToDraw = UvDrawList[i];
                var p0 = new Vector2(textureToDraw.UvOffset.X, textureToDraw.UvOffset.Y) / textureToDraw.TextureSize;
                var p1 = new Vector2(textureToDraw.UvOffset.X + textureToDraw.UvSize.X, textureToDraw.UvOffset.Y) / textureToDraw.TextureSize;
                var p2 = new Vector2(textureToDraw.UvOffset.X, textureToDraw.UvOffset.Y + textureToDraw.UvSize.Y) / textureToDraw.TextureSize;
                var p3 = new Vector2(textureToDraw.UvOffset.X + textureToDraw.UvSize.X, textureToDraw.UvOffset.Y + textureToDraw.UvSize.Y) / textureToDraw.TextureSize;

                MyQuadD quad;
                MyUtils.GetBillboardQuadOriented(out quad, ref textureToDraw.Position, textureToDraw.Width, textureToDraw.Height, ref textureToDraw.Left, ref textureToDraw.Up);

                MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point1, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, p0, p1, p3, textureToDraw.Material, 0, textureToDraw.Position, textureToDraw.Color, textureToDraw.Blend);
                MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point3, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, p0, p2, p3, textureToDraw.Material, 0, textureToDraw.Position, textureToDraw.Color, textureToDraw.Blend);

                _textureDrawPool.Enqueue(textureToDraw);
            }
            #endregion

            #region Simple based draws
            for (int i = 0; i < SimpleDrawList.Count; i++)
            {
                var textureToDraw = SimpleDrawList[i];
                var scale = 0.075 * Math.Tan(_session.Camera.FovWithZoom * textureToDraw.Height);
                
                textureToDraw.Position = Vector3D.Transform(textureToDraw.Position, _cameraWorldMatrix);                
                scale = 1 * scale;

                MyTransparentGeometry.AddBillboardOriented(textureToDraw.Material, textureToDraw.Color, textureToDraw.Position, _cameraWorldMatrix.Left, _cameraWorldMatrix.Up, (float)scale, textureToDraw.Blend);

                _textureDrawPool.Enqueue(textureToDraw);
            }
            #endregion

            WeaponsToDisplayCheck.Clear();
            WeaponsToDisplay.Clear();
            TextAddList.Clear();
            TextureAddList.Clear();
            UvDrawList.Clear();
            SimpleDrawList.Clear();
            TexturesToAdd = 0;
        }
    }
}
