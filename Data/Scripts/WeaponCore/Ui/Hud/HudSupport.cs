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
        internal uint TicksSinceUpdated => _session.Tick - _lastHudUpdateTick;
        internal bool KeepBackground => _session.Tick - _lastHudUpdateTick < MinUpdateTicks;

        internal void AddText(string text, float x, float y, ElementNames name, int ttl, Vector4 color, Justify justify = Justify.None, FontType fontType = FontType.Mono, float fontSize = 10f, float heightScale = 0.65f)
        {
            AgingTextures = true;

            AgingTextRequest request;
            if (_agingTextRequests.TryGetValue(name, out request))
            {
                request.RefreshTtl(ttl);
                return;
            }
            request = _agingTextRequestPool.Get();

            var pos = GetScreenSpace(new Vector2(x, y));
            request.Text = text;
            request.Color = color;
            request.Position.X = pos.X;
            request.Position.Y = pos.Y;
            request.FontSize = fontSize * MetersInPixel;
            request.Font = fontType;
            request.Ttl = ttl;
            request.Type = name;
            request.Justify = justify;
            request.HeightScale = heightScale;
            _agingTextRequests.TryAdd(name, request);
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
            tdd.Width = width * MetersInPixel;
            tdd.Height = height * MetersInPixel;
            tdd.P0 = new Vector2(uvOffsetX / textureSizeX, uvOffsetY / textureSizeY);
            tdd.P1 = new Vector2((uvOffsetX + uvSizeX) / textureSizeX, uvOffsetY / textureSizeY);
            tdd.P2 = new Vector2(uvOffsetX / textureSizeX, (uvOffsetY + uvSizeY) / textureSizeY);
            tdd.P3 = new Vector2((uvOffsetX + uvSizeX) / textureSizeX, (uvOffsetY + uvSizeY) / textureSizeY);
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
            tdd.Width = width * MetersInPixel;
            tdd.Height = height * MetersInPixel;
            tdd.P0 = new Vector2(uvOffsetX / textureSizeX, uvOffsetY / textureSizeY);
            tdd.P1 = new Vector2((uvOffsetX + uvSizeX) / textureSizeX, uvOffsetY / textureSizeY);
            tdd.P2 = new Vector2(uvOffsetX / textureSizeX, (uvOffsetY + uvSizeY) / textureSizeY);
            tdd.P3 = new Vector2((uvOffsetX + uvSizeX) / textureSizeX, (uvOffsetY + uvSizeY) / textureSizeY);
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
            tdd.Height = scale * MetersInPixel;
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
            tdd.Height = scale * MetersInPixel;
            tdd.UvDraw = false;
            _textureAddList.Add(tdd);

            TexturesToAdd++;
        }

        internal Vector2 GetScreenSpace(Vector2 offset)
        {
            Vector3 pos;
            pos.Y = (float) (2 * _session.Camera.NearPlaneDistance * _session.ScaleFov);
            pos.X = pos.Y * _session.AspectRatio;
            pos.Z = -(_session.Camera.NearPlaneDistance * 2);

            return new Vector2(pos.X * offset.X, pos.Y * offset.Y);
        }

        internal void UpdateHudSettings()
        {
            //runs once on first draw then only again if a menu is closed
            var fovModifier = _session.CurrentFovWithZoom / DefaultFov;
            NeedsUpdate = false;
            _lastHudUpdateTick = 0;
            _viewPortSize.Y = 2 * _session.Camera.NearPlaneDistance * _session.ScaleFov;
            _viewPortSize.X = (_viewPortSize.Y * _session.AspectRatio);
            _viewPortSize.Z = -(_session.Camera.NearPlaneDistance * 2);

            _currWeaponDisplayPos.X = _viewPortSize.X;
            _currWeaponDisplayPos.Y = _viewPortSize.Y * .6f;

            _padding = PaddingConst * fovModifier;

            _reloadWidth = ReloadWidthConst * fovModifier;
            _reloadHeight = ReloadHeightConst * fovModifier;
            _reloadOffset = _reloadWidth * fovModifier;
            _reloadHeightOffset = (ReloadHeightOffsetConst * (2 * fovModifier)) * fovModifier; //never used

            _textSize = WeaponHudFontHeight * fovModifier;
            _sTextSize = _textSize * .5f;
            _textWidth = (WeaponHudFontHeight * _session.AspectRatioInv) * fovModifier;
            _stextWidth = (_textWidth * .75f);
            _stackPadding = _stextWidth * 6; // gives max limit of 6 characters (x999)

            _heatWidth = HeatWidthConst * fovModifier;
            _heatHeight = HeatHeightConst * fovModifier;
            _heatOffsetX = HeatWidthOffset * fovModifier;
            _heatOffsetY = (_heatHeight * 3f) * fovModifier;

            _infoPaneloffset = InfoPanelOffset * fovModifier;
            _paddingHeat = _session.CurrentFovWithZoom < 1 ? MathHelper.Clamp(_session.CurrentFovWithZoom * 0.0001f, 0.0001f, 0.0003f) : 0;
            _paddingReload = _session.CurrentFovWithZoom < 1 ? MathHelper.Clamp(_session.CurrentFovWithZoom * 0.002f, 0.0002f, 0.001f) : 0.001f;

            _symbolWidth = _heatWidth + _padding;
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
                    var temp = list[i].State.Heat;

                    int j;
                    for (j = i; j >= h && list[j - h].State.Heat < temp; j -= h)
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

                    if (!_textureDrawPool.TryDequeue(out swi.CachedReloadTexture))
                        swi.CachedReloadTexture = new TextureDrawData();

                    if (!_textureDrawPool.TryDequeue(out swi.CachedHeatTexture))
                        swi.CachedHeatTexture = new TextureDrawData();

                    swi.CachedHeatTexture.Persistant = true;
                    swi.CachedReloadTexture.Persistant = true;
                    swi.ReloadIndex = 0;
                    swi.HighestValuePart = w;
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
                            if (last.HeatPerc - w.HeatPerc > .05f || last.Reloading != w.Reloading || last.State.Overheated != w.State.Overheated)
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

                            if (!_textureDrawPool.TryDequeue(out swi.CachedReloadTexture))
                                swi.CachedReloadTexture = new TextureDrawData();

                            if (!_textureDrawPool.TryDequeue(out swi.CachedHeatTexture))
                                swi.CachedHeatTexture = new TextureDrawData();

                            swi.CachedHeatTexture.Persistant = true;
                            swi.CachedReloadTexture.Persistant = true;
                            swi.ReloadIndex = 0;
                            swi.HighestValuePart = subL[0];
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

                        if (!_textureDrawPool.TryDequeue(out swi.CachedReloadTexture))
                            swi.CachedReloadTexture = new TextureDrawData();

                        if (!_textureDrawPool.TryDequeue(out swi.CachedHeatTexture))
                            swi.CachedHeatTexture = new TextureDrawData();

                        swi.CachedHeatTexture.Persistant = true;
                        swi.CachedReloadTexture.Persistant = true;
                        swi.ReloadIndex = 0;

                        swi.HighestValuePart = weapons[0];
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
            CharacterMap.Clear();
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
