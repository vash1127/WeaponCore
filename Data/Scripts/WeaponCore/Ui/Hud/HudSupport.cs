using System.Collections.Generic;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;

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
            _textAddList.Add(textInfo);

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

            _textureAddList.Add(tdd);

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

            _simpleDrawList.Add(tdd);

            TexturesToAdd++;
        }

        internal List<StackedWeaponInfo> SortDisplayedWeapons(List<Weapon> list)
        {
            int length = list.Count;
            int finalCount = 0;
            List<StackedWeaponInfo> finalList;
            if (!_weaponInfoListPool.TryDequeue(out finalList))
                finalList = new List<StackedWeaponInfo>();

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

            if(list.Count > 50) //limit to top 50 based on heat
                list.RemoveRange(50, list.Count - 50);
            else if (list.Count <= 12)
            {
                for(int i = 0; i < list.Count; i++)
                {
                    var w = list[i];
                    if (w.System.WeaponName.Length > _currentLargestName) _currentLargestName = w.System.WeaponName.Length;

                    StackedWeaponInfo swi;
                    if (!_weaponStackedInfoPool.TryDequeue(out swi))
                        swi = new StackedWeaponInfo();

                    swi.HighestValueWeapon = w;
                    swi.WeaponStack = 1;
                    finalList.Add(swi);
                }
                return finalList;
            }


            Dictionary<int, List<Weapon>> weaponTypes = new Dictionary<int, List<Weapon>>();
            for (int i = 0; i < list.Count; i++) //sort list into groups of same weapon type
            {
                var w = list[i];

                if (!weaponTypes.ContainsKey(w.System.WeaponIdHash))
                {
                    List<Weapon> tmp;
                    if (!_weaponSortingListPool.TryDequeue(out tmp))
                        tmp = new List<Weapon>();

                    weaponTypes[w.System.WeaponIdHash] = tmp;
                }

                weaponTypes[w.System.WeaponIdHash].Add(w);
            }

            foreach (var weaponType in weaponTypes)
            {
                var weapons = weaponType.Value;
                if (weapons[0].System.WeaponName.Length > _currentLargestName) _currentLargestName = weapons[0].System.WeaponName.Length;


                if (weapons.Count > 1)
                {
                    List<List<Weapon>> subLists;
                    List<Weapon> subList;
                    var last = weapons[0];

                    if (!_weaponSubListsPool.TryDequeue(out subLists))
                        subLists = new List<List<Weapon>>();

                    if (!_weaponSortingListPool.TryDequeue(out subList))
                        subList = new List<Weapon>();

                    for (int i = 0; i < weapons.Count; i++)
                    {
                        var w = weapons[i];

                        if (i == 0)
                            subList.Add(w);
                        else
                        {
                            if (last.HeatPerc - w.HeatPerc > .05f || last.State.Sync.Reloading != w.State.Sync.Reloading || last.State.Sync.Overheated != w.State.Sync.Overheated)
                            {
                                subLists.Add(subList);
                                if (!_weaponSortingListPool.TryDequeue(out subList))
                                    subList = new List<Weapon>();
                            }

                            last = w;
                            subList.Add(w);

                            if(i == weapons.Count - 1)
                                subLists.Add(subList);
                        }
                    }

                    weapons.Clear();
                    _weaponSortingListPool.Enqueue(weapons);

                    for (int i = 0; i < subLists.Count; i++)
                    {
                        var subL = subLists[i];                        

                        if (finalCount < 12)
                        {
                            StackedWeaponInfo swi;
                            if (!_weaponStackedInfoPool.TryDequeue(out swi))
                                swi = new StackedWeaponInfo();

                            swi.HighestValueWeapon = subL[0];
                            swi.WeaponStack = subL.Count;

                            finalList.Add(swi);
                            finalCount++;
                        }

                        subL.Clear();
                        _weaponSortingListPool.Enqueue(subL);
                    }

                    subLists.Clear();
                    _weaponSubListsPool.Enqueue(subLists);
                }
                else
                {
                    if (finalCount < 12)
                    {
                        StackedWeaponInfo swi;
                        if (!_weaponStackedInfoPool.TryDequeue(out swi))
                            swi = new StackedWeaponInfo();

                        swi.HighestValueWeapon = weapons[0];
                        swi.WeaponStack = 1;

                    
                        finalList.Add(swi);
                        finalCount++;
                    }

                    weapons.Clear();
                    _weaponSortingListPool.Enqueue(weapons);
                }
            }

            return finalList;
        }
    }
}
