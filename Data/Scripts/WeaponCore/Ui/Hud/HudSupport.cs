using System.Collections.Generic;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;

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

            var textureSize = new Vector2(textureSizeX, textureSizeY);

            tdd.Material = material;
            tdd.Color = color;
            tdd.Position = position;
            tdd.Width = width * _metersInPixel;
            tdd.Height = height * _metersInPixel;
            tdd.P0 = new Vector2(uvOffsetX, uvOffsetY) / textureSize;
            tdd.P1 = new Vector2(uvOffsetX + uvSizeX, uvOffsetY) / textureSize;
            tdd.P2 = new Vector2(uvOffsetX, uvOffsetY + uvSizeY) / textureSize;
            tdd.P3 = new Vector2(uvOffsetX + uvSizeX, uvOffsetY + uvSizeY) / textureSize;

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

        static void ShellSort(List<Weapon> list)
        {
            int length = list.Count;

            for (int h = length / 2; h > 0; h /= 2)
            {
                for (int i = h; i < length; i += 1)
                {
                    var tempValue = list[i];
                    var temp = list[i].State.Sync.Heat;

                    int j;
                    for (j = i; j >= h && list[j - h].State.Sync.Heat < temp; j -= h)
                    {
                        list[j] = list[j - h];
                    }

                    list[j] = tempValue;
                }
            }
        }
    }
}
