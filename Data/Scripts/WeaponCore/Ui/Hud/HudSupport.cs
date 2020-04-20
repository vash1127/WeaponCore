using System;
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
            textInfo.Position.X = x;
            textInfo.Position.Y = y;
            textInfo.FontSize = fontSize * _metersInPixel;
            textInfo.Simple = false;
            _textAddList.Add(textInfo);

            TexturesToAdd++;
        }

        internal void AddTextSimple(string text, Vector4 color, float x, float y, float fontSize = 10f)
        {
            TextDrawRequest textInfo;

            if (!_textDrawPool.TryDequeue(out textInfo))
                textInfo = new TextDrawRequest();

            textInfo.Text = text;
            textInfo.Color = color;
            textInfo.Position.X = x;
            textInfo.Position.Y = y;
            textInfo.FontSize = fontSize * _metersInPixel;
            textInfo.Simple = true;
            _textAddList.Add(textInfo);

            TexturesToAdd++;
        }

        internal void AddTextureUVSimple(MyStringId material, Vector4 color, float x, float y, float width, float height, int textureSizeX, int textureSizeY, int uvOffsetX = 0, int uvOffsetY = 0, int uvSizeX = 1, int uvSizeY = 1)
        {
            TextureDrawData tdd;
            if (!_textureDrawPool.TryDequeue(out tdd))
                tdd = new TextureDrawData();

            tdd.Material = material;
            tdd.Color = color;
            tdd.Position.X = x;
            tdd.Position.Y = y;
            tdd.Width = width * _metersInPixel;
            tdd.Height = height * _metersInPixel;
            tdd.P0 = new Vector2(uvOffsetX / textureSizeX, uvOffsetY / textureSizeY);
            tdd.P1 = new Vector2((uvOffsetX + uvSizeX) / textureSizeX, uvOffsetY / textureSizeY);
            tdd.P2 = new Vector2(uvOffsetX / textureSizeX, (uvOffsetY + uvSizeY) / textureSizeY);
            tdd.P3 = new Vector2((uvOffsetX + uvSizeX) / textureSizeX, (uvOffsetY + uvSizeY) / textureSizeY);
            tdd.Simple = true;
            tdd.UvDraw = true;
            _textureAddList.Add(tdd);

            TexturesToAdd++;

            TexturesToAdd++;
        }

        internal void AddTextureUV(MyStringId material, Vector4 color, float x, float y, float width, float height, int textureSizeX, int textureSizeY, int uvOffsetX = 0, int uvOffsetY = 0, int uvSizeX = 1, int uvSizeY = 1)
        {
            TextureDrawData tdd;
            if (!_textureDrawPool.TryDequeue(out tdd))
                tdd = new TextureDrawData();

            tdd.Material = material;
            tdd.Color = color;
            tdd.Position.X = x;
            tdd.Position.Y = y;
            tdd.Width = width * _metersInPixel;
            tdd.Height = height * _metersInPixel;
            tdd.P0 = new Vector2(uvOffsetX / textureSizeX, uvOffsetY / textureSizeY);
            tdd.P1 = new Vector2((uvOffsetX + uvSizeX) / textureSizeX, uvOffsetY / textureSizeY);
            tdd.P2 = new Vector2(uvOffsetX / textureSizeX, (uvOffsetY + uvSizeY) / textureSizeY);
            tdd.P3 = new Vector2((uvOffsetX + uvSizeX) / textureSizeX, (uvOffsetY + uvSizeY) / textureSizeY);
            tdd.Simple = false;
            tdd.UvDraw = true;
            _textureAddList.Add(tdd);

            TexturesToAdd++;
        }

        internal void AddTexture(MyStringId material, Vector4 color, float x, float y, float scale)
        {
            TextureDrawData tdd;
            if (!_textureDrawPool.TryDequeue(out tdd))
                tdd = new TextureDrawData();

            tdd.Material = material;
            tdd.Color = color;
            tdd.Position.X = x;
            tdd.Position.Y = y;
            tdd.Height = scale * _metersInPixel;
            tdd.Simple = false;
            tdd.UvDraw = false;
            _textureAddList.Add(tdd);

            TexturesToAdd++;
        }


        internal void AddTextureSimple(MyStringId material, Vector4 color, float x, float y, float scale)
        {
            TextureDrawData tdd;

            if (!_textureDrawPool.TryDequeue(out tdd))
                tdd = new TextureDrawData();

            tdd.Material = material;
            tdd.Color = color;
            tdd.Position.X = x;
            tdd.Position.Y = y;
            tdd.Height = scale * _metersInPixel;
            tdd.Simple = true;
            tdd.UvDraw = false;
            _textureAddList.Add(tdd);

            TexturesToAdd++;
        }

        internal void UpdateHudSettings()
        {
            //runs once on first draw then only again if a menu is closed
            var fovModifier = _session.Camera.FovWithZoom / _defaultFov;
            NeedsUpdate = false;

            _aspectratio = _session.Camera.ViewportSize.X / _session.Camera.ViewportSize.Y;
            _aspectratioInv = _session.Camera.ViewportSize.Y / _session.Camera.ViewportSize.X;
            _viewPortSize.Y = 2 * _session.Camera.NearPlaneDistance * Math.Tan(_session.Camera.FovWithZoom * 0.5f);
            _viewPortSize.X = (_viewPortSize.Y * _aspectratio);
            _viewPortSize.Z = -(_session.Camera.NearPlaneDistance * 2);

            _currWeaponDisplayPos.X = _viewPortSize.X;
            _currWeaponDisplayPos.Y = _viewPortSize.Y * .6f;

            _padding = _paddingConst * fovModifier;
            _reloadWidth = _reloadWidthConst * fovModifier;
            _reloadHeight = _reloadHeightConst * fovModifier;
            _reloadOffset = _reloadWidth * (1.6f * fovModifier) + _padding;
            _heatOffsetX = _heatWidthOffset * fovModifier;
            _heatOffsetY = _heatHeightOffset * fovModifier;
            _textSize = _WeaponHudFontHeight * fovModifier;
            _sTextSize = _textSize * .75f;
            _textWidth = (_WeaponHudFontHeight * _aspectratioInv) * fovModifier;
            _stextWidth = (_textWidth * .75f);
            _stackPadding = _stextWidth * 6; // gives max limit of 6 characters (x999)
            _heatWidth = _heatWidthConst * fovModifier;
            _heatHeight = _heatHeightConst * fovModifier;
            _infoPaneloffset = _infoPanelOffset * fovModifier;

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

        internal void Purge()
        {

            _textureDrawPool.Clear();
            _textDrawPool.Clear();
            _weaponSortingListPool.Clear();
            _weaponStackedInfoPool.Clear();
            _characterMap.Clear();
            _textureAddList.Clear();
            _textAddList.Clear();
            _drawList.Clear();
            _weapontoDraw.Clear();
            WeaponsToDisplay.Clear();

            List<StackedWeaponInfo> removeList;
            while (_weaponInfoListPool.TryDequeue(out removeList))
                removeList.Clear();

            List<List<Weapon>> removeList1;
            while (_weaponSubListsPool.TryDequeue(out removeList1))
            {
                for (int i = 0; i < removeList1.Count; i++)
                    removeList1[i].Clear();

                removeList1.Clear();
            }
        }
    }
}
