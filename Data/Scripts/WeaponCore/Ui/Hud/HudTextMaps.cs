using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Utils;
using VRageMath;

namespace WeaponCore
{
    partial class Hud
    {
        private TextureMap GenerateMap(MyStringId material, float uvOffsetX, float uvOffsetY, float uvSizeX, float uvSizeY, float textureSizeX, float textureSizeY)
        {
            var textureSize = new Vector2(textureSizeX, textureSizeY);

            return new TextureMap
            {
                Material = material,
                P0 = new Vector2(uvOffsetX, uvOffsetY) / textureSize,
                P1 = new Vector2(uvOffsetX + uvSizeX, uvOffsetY) / textureSize,
                P2 = new Vector2(uvOffsetX, uvOffsetY + uvSizeY) / textureSize,
                P3 = new Vector2(uvOffsetX + uvSizeX, uvOffsetY + uvSizeY) / textureSize,
            };
        }

        private void LoadTextMaps(out Dictionary<char, TextureMap> englishMono)
        {
            

            englishMono = new Dictionary<char, TextureMap>
            {
                [' '] = GenerateMap (_monoFontAtlas1, 0, 0, 30, 42, 1024, 1024),
                ['!'] = GenerateMap (_monoFontAtlas1, 30, 0, 30, 42, 1024, 1024),
                ['"'] = GenerateMap (_monoFontAtlas1, 60, 0, 30, 42, 1024, 1024),
                ['#'] = GenerateMap (_monoFontAtlas1, 90, 0, 30, 42, 1024, 1024),
                ['$'] = GenerateMap (_monoFontAtlas1, 120, 0, 30, 42, 1024, 1024),
                ['%'] = GenerateMap (_monoFontAtlas1, 150, 0, 30, 42, 1024, 1024),
                ['&'] = GenerateMap (_monoFontAtlas1, 180, 0, 30, 42, 1024, 1024),
                ['\''] = GenerateMap (_monoFontAtlas1, 210, 0, 30, 42, 1024, 1024),
                ['('] = GenerateMap (_monoFontAtlas1, 240, 0, 30, 42, 1024, 1024),
                [')'] = GenerateMap (_monoFontAtlas1, 270, 0, 30, 42, 1024, 1024),
                ['*'] = GenerateMap (_monoFontAtlas1, 300, 0, 30, 42, 1024, 1024),
                ['+'] = GenerateMap (_monoFontAtlas1, 330, 0, 30, 42, 1024, 1024),
                [','] = GenerateMap (_monoFontAtlas1, 360, 0, 30, 42, 1024, 1024),
                ['-'] = GenerateMap (_monoFontAtlas1, 390, 0, 30, 42, 1024, 1024),
                ['.'] = GenerateMap (_monoFontAtlas1, 420, 0, 30, 42, 1024, 1024),
                ['/'] = GenerateMap (_monoFontAtlas1, 450, 0, 30, 42, 1024, 1024),
                ['0'] = GenerateMap (_monoFontAtlas1, 480, 0, 30, 42, 1024, 1024),
                ['1'] = GenerateMap (_monoFontAtlas1, 510, 0, 30, 42, 1024, 1024),
                ['2'] = GenerateMap (_monoFontAtlas1, 540, 0, 30, 42, 1024, 1024),
                ['3'] = GenerateMap (_monoFontAtlas1, 570, 0, 30, 42, 1024, 1024),
                ['4'] = GenerateMap (_monoFontAtlas1, 600, 0, 30, 42, 1024, 1024),
                ['5'] = GenerateMap (_monoFontAtlas1, 630, 0, 30, 42, 1024, 1024),
                ['6'] = GenerateMap (_monoFontAtlas1, 660, 0, 30, 42, 1024, 1024),
                ['7'] = GenerateMap (_monoFontAtlas1, 690, 0, 30, 42, 1024, 1024),
                ['8'] = GenerateMap (_monoFontAtlas1, 720, 0, 30, 42, 1024, 1024),
                ['9'] = GenerateMap (_monoFontAtlas1, 750, 0, 30, 42, 1024, 1024),
                [':'] = GenerateMap (_monoFontAtlas1, 780, 0, 30, 42, 1024, 1024),
                [';'] = GenerateMap (_monoFontAtlas1, 810, 0, 30, 42, 1024, 1024),
                ['<'] = GenerateMap (_monoFontAtlas1, 840, 0, 30, 42, 1024, 1024),
                ['='] = GenerateMap (_monoFontAtlas1, 870, 0, 30, 42, 1024, 1024),
                ['>'] = GenerateMap (_monoFontAtlas1, 900, 0, 30, 42, 1024, 1024),
                ['?'] = GenerateMap (_monoFontAtlas1, 930, 0, 30, 42, 1024, 1024),
                ['@'] = GenerateMap (_monoFontAtlas1, 960, 0, 30, 42, 1024, 1024),
                ['A'] = GenerateMap (_monoFontAtlas1, 990, 0, 30, 42, 1024, 1024),
                ['B'] = GenerateMap (_monoFontAtlas1, 0, 44, 30, 42, 1024, 1024),
                ['C'] = GenerateMap (_monoFontAtlas1, 30, 44, 30, 42, 1024, 1024),
                ['D'] = GenerateMap (_monoFontAtlas1, 60, 44, 30, 42, 1024, 1024),
                ['E'] = GenerateMap (_monoFontAtlas1, 90, 44, 30, 42, 1024, 1024),
                ['F'] = GenerateMap (_monoFontAtlas1, 120, 44, 30, 42, 1024, 1024),
                ['G'] = GenerateMap (_monoFontAtlas1, 150, 44, 30, 42, 1024, 1024),
                ['H'] = GenerateMap (_monoFontAtlas1, 180, 44, 30, 42, 1024, 1024),
                ['I'] = GenerateMap (_monoFontAtlas1, 210, 44, 30, 42, 1024, 1024),
                ['J'] = GenerateMap (_monoFontAtlas1, 240, 44, 30, 42, 1024, 1024),
                ['K'] = GenerateMap (_monoFontAtlas1, 270, 44, 30, 42, 1024, 1024),
                ['L'] = GenerateMap (_monoFontAtlas1, 300, 44, 30, 42, 1024, 1024),
                ['M'] = GenerateMap (_monoFontAtlas1, 330, 44, 30, 42, 1024, 1024),
                ['N'] = GenerateMap (_monoFontAtlas1, 360, 44, 30, 42, 1024, 1024),
                ['O'] = GenerateMap (_monoFontAtlas1, 390, 44, 30, 42, 1024, 1024),
                ['P'] = GenerateMap (_monoFontAtlas1, 420, 44, 30, 42, 1024, 1024),
                ['Q'] = GenerateMap (_monoFontAtlas1, 450, 44, 30, 42, 1024, 1024),
                ['R'] = GenerateMap (_monoFontAtlas1, 480, 44, 30, 42, 1024, 1024),
                ['S'] = GenerateMap (_monoFontAtlas1, 510, 44, 30, 42, 1024, 1024),
                ['T'] = GenerateMap (_monoFontAtlas1, 540, 44, 30, 42, 1024, 1024),
                ['U'] = GenerateMap (_monoFontAtlas1, 570, 44, 30, 42, 1024, 1024),
                ['V'] = GenerateMap (_monoFontAtlas1, 600, 44, 30, 42, 1024, 1024),
                ['W'] = GenerateMap (_monoFontAtlas1, 630, 44, 30, 42, 1024, 1024),
                ['X'] = GenerateMap (_monoFontAtlas1, 660, 44, 30, 42, 1024, 1024),
                ['Y'] = GenerateMap (_monoFontAtlas1, 690, 44, 30, 42, 1024, 1024),
                ['Z'] = GenerateMap (_monoFontAtlas1, 720, 44, 30, 42, 1024, 1024),
                ['['] = GenerateMap (_monoFontAtlas1, 750, 44, 30, 42, 1024, 1024),
                ['\\'] = GenerateMap (_monoFontAtlas1, 780, 44, 30, 42, 1024, 1024),
                [']'] = GenerateMap (_monoFontAtlas1, 810, 44, 30, 42, 1024, 1024),
                ['^'] = GenerateMap (_monoFontAtlas1, 840, 44, 30, 42, 1024, 1024),
                ['_'] = GenerateMap (_monoFontAtlas1, 870, 44, 30, 42, 1024, 1024),
                ['`'] = GenerateMap (_monoFontAtlas1, 900, 44, 30, 42, 1024, 1024),
                ['a'] = GenerateMap (_monoFontAtlas1, 930, 44, 30, 42, 1024, 1024),
                ['b'] = GenerateMap (_monoFontAtlas1, 960, 44, 30, 42, 1024, 1024),
                ['c'] = GenerateMap (_monoFontAtlas1, 990, 44, 30, 42, 1024, 1024),
                ['d'] = GenerateMap (_monoFontAtlas1, 0, 88, 30, 42, 1024, 1024),
                ['e'] = GenerateMap (_monoFontAtlas1, 30, 88, 30, 42, 1024, 1024),
                ['f'] = GenerateMap (_monoFontAtlas1, 60, 88, 30, 42, 1024, 1024),
                ['g'] = GenerateMap (_monoFontAtlas1, 90, 88, 30, 42, 1024, 1024),
                ['h'] = GenerateMap (_monoFontAtlas1, 120, 88, 30, 42, 1024, 1024),
                ['i'] = GenerateMap (_monoFontAtlas1, 150, 88, 30, 42, 1024, 1024),
                ['j'] = GenerateMap (_monoFontAtlas1, 180, 88, 30, 42, 1024, 1024),
                ['k'] = GenerateMap (_monoFontAtlas1, 210, 88, 30, 42, 1024, 1024),
                ['l'] = GenerateMap (_monoFontAtlas1, 240, 88, 30, 42, 1024, 1024),
                ['m'] = GenerateMap (_monoFontAtlas1, 270, 88, 30, 42, 1024, 1024),
                ['n'] = GenerateMap (_monoFontAtlas1, 300, 88, 30, 42, 1024, 1024),
                ['o'] = GenerateMap (_monoFontAtlas1, 330, 88, 30, 42, 1024, 1024),
                ['p'] = GenerateMap (_monoFontAtlas1, 360, 88, 30, 42, 1024, 1024),
                ['q'] = GenerateMap (_monoFontAtlas1, 390, 88, 30, 42, 1024, 1024),
                ['r'] = GenerateMap (_monoFontAtlas1, 420, 88, 30, 42, 1024, 1024),
                ['s'] = GenerateMap (_monoFontAtlas1, 450, 88, 30, 42, 1024, 1024),
                ['t'] = GenerateMap (_monoFontAtlas1, 480, 88, 30, 42, 1024, 1024),
                ['u'] = GenerateMap (_monoFontAtlas1, 510, 88, 30, 42, 1024, 1024),
                ['v'] = GenerateMap (_monoFontAtlas1, 540, 88, 30, 42, 1024, 1024),
                ['w'] = GenerateMap (_monoFontAtlas1, 570, 88, 30, 42, 1024, 1024),
                ['x'] = GenerateMap (_monoFontAtlas1, 600, 88, 30, 42, 1024, 1024),
                ['y'] = GenerateMap (_monoFontAtlas1, 630, 88, 30, 42, 1024, 1024),
                ['z'] = GenerateMap (_monoFontAtlas1, 660, 88, 30, 42, 1024, 1024),
                ['{'] = GenerateMap (_monoFontAtlas1, 690, 88, 30, 42, 1024, 1024),
                ['|'] = GenerateMap (_monoFontAtlas1, 720, 88, 30, 42, 1024, 1024),
                ['}'] = GenerateMap (_monoFontAtlas1, 750, 88, 30, 42, 1024, 1024),
                ['~'] = GenerateMap (_monoFontAtlas1, 780, 88, 30, 42, 1024, 1024),
            };
        }

    }
}
