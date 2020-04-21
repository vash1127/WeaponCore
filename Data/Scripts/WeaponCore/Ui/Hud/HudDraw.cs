using System;
using System.Linq;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    partial class Hud
    {
        internal void DrawTextures()
        {
            var ticksSinceUpdate = _session.Tick - _lastHudUpdateTick;
            var reset = false;
            _cameraWorldMatrix = _session.Camera.WorldMatrix;

            if (NeedsUpdate)
                UpdateHudSettings();

            if (WeaponsToDisplay.Count > 0)
            {
                if (ticksSinceUpdate >= _minUpdateTicks)
                {
                    _weapontoDraw = SortDisplayedWeapons(WeaponsToDisplay);
                    _lastHudUpdateTick = _session.Tick;
                }
                else if (ticksSinceUpdate + 1 >= _minUpdateTicks)
                    reset = true;

                DrawHud(reset);
            }

            #region Proccess Custom Additions
            for (int i = 0; i < _textAddList.Count; i++)
            {
                var textAdd = _textAddList[i];

                var height = textAdd.FontSize;
                var width = textAdd.FontSize * _aspectratioInv;
                textAdd.Position.Z = _viewPortSize.Z;
                var textPos = Vector3D.Transform(textAdd.Position, _cameraWorldMatrix);

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
                    tdd.P0 = cm.P0;
                    tdd.P1 = cm.P1;
                    tdd.P2 = cm.P2;
                    tdd.P3 = cm.P3;
                    tdd.UvDraw = true;
                    tdd.Simple = textAdd.Simple;

                    _drawList.Add(tdd);

                    textPos -= _cameraWorldMatrix.Left * height;
                }

                _textDrawPool.Enqueue(textAdd);
            }

            for (int i = 0; i < _textureAddList.Count; i++)
            {
                var tdd = _textureAddList[i];
                tdd.Position.Z = _viewPortSize.Z;
                tdd.Position = Vector3D.Transform(tdd.Position, _cameraWorldMatrix);
                tdd.Up = _cameraWorldMatrix.Up;
                tdd.Left = _cameraWorldMatrix.Left;
                _drawList.Add(tdd);
            }
            #endregion
            
            for (int i = 0; i < _drawList.Count; i++)
            {
                var textureToDraw = _drawList[i];

                if (textureToDraw.Simple)
                {
                    textureToDraw.Position.X = (textureToDraw.Position.X / (-_viewPortSize.X) * (_viewPortSize.X - 100) + 100);
                    textureToDraw.Position.Y = (textureToDraw.Position.Y / (-_viewPortSize.Y) * (_viewPortSize.Y - 100) + 100);
                }


                if (textureToDraw.UvDraw)
                {
                    MyQuadD quad;
                    MyUtils.GetBillboardQuadOriented(out quad, ref textureToDraw.Position, textureToDraw.Width, textureToDraw.Height, ref textureToDraw.Left, ref textureToDraw.Up);

                    if (textureToDraw.Color != Color.Transparent)
                    {
                        MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point1, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, textureToDraw.P0, textureToDraw.P1, textureToDraw.P3, textureToDraw.Material, 0, textureToDraw.Position, textureToDraw.Color, textureToDraw.Blend);
                        MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point3, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, textureToDraw.P0, textureToDraw.P2, textureToDraw.P3, textureToDraw.Material, 0, textureToDraw.Position, textureToDraw.Color, textureToDraw.Blend);
                    }
                    else
                    {
                        MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point1, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, textureToDraw.P0, textureToDraw.P1, textureToDraw.P3, textureToDraw.Material, 0, textureToDraw.Position, textureToDraw.Blend);
                        MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point3, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, textureToDraw.P0, textureToDraw.P2, textureToDraw.P3, textureToDraw.Material, 0, textureToDraw.Position, textureToDraw.Blend);
                    }
                }
                else
                {
                    textureToDraw.Position = Vector3D.Transform(textureToDraw.Position, _cameraWorldMatrix);

                    MyTransparentGeometry.AddBillboardOriented(textureToDraw.Material, textureToDraw.Color, textureToDraw.Position, _cameraWorldMatrix.Left, _cameraWorldMatrix.Up, textureToDraw.Height, textureToDraw.Blend);
                }
                _textureDrawPool.Enqueue(textureToDraw);
            }

            WeaponsToDisplay.Clear();
            _textAddList.Clear();
            _textureAddList.Clear();
            _drawList.Clear();
            TexturesToAdd = 0;
        }

        internal void DrawHud(bool reset)
        {
            var CurrWeaponDisplayPos = _currWeaponDisplayPos;

            if (_lastHudUpdateTick == _session.Tick)
            {
                var largestName = (_currentLargestName * _textWidth) + _stackPadding;
                var symbolWidth = _heatWidth + _reloadWidth + _padding;
                _bgWidth = largestName > symbolWidth ? largestName : symbolWidth;
                _bgBorderHeight = _bgWidth * _bgBorderRatio;
                _bgCenterHeight = _weapontoDraw.Count > 3 ? (_weapontoDraw.Count - 2) * _infoPaneloffset : _infoPaneloffset * 2;
            }

            var bgStartPosX = CurrWeaponDisplayPos.X - _bgWidth - _padding;
            var bgStartPosY = CurrWeaponDisplayPos.Y - _bgCenterHeight;

            #region Background draw
            TextureDrawData backgroundTexture;
            if (!_textureDrawPool.TryDequeue(out backgroundTexture))
                backgroundTexture = new TextureDrawData();

            backgroundTexture.Material = _infoBackground[1].Material;
            backgroundTexture.Color = _bgColor * (_session.Session.Config.HUDBkOpacity * 1.8f);
            backgroundTexture.Position.X = bgStartPosX;
            backgroundTexture.Position.Y = bgStartPosY;
            backgroundTexture.Width = _bgWidth;
            backgroundTexture.Height = _bgCenterHeight;
            backgroundTexture.P0 = _infoBackground[1].P0;
            backgroundTexture.P1 = _infoBackground[1].P1;
            backgroundTexture.P2 = _infoBackground[1].P2;
            backgroundTexture.P3 = _infoBackground[1].P3;
            backgroundTexture.UvDraw = true;

            _textureAddList.Add(backgroundTexture);

            if (!_textureDrawPool.TryDequeue(out backgroundTexture))
                backgroundTexture = new TextureDrawData();

            backgroundTexture.Material = _infoBackground[0].Material;
            backgroundTexture.Color = _bgColor * (_session.Session.Config.HUDBkOpacity * 1.8f);
            backgroundTexture.Position.X = bgStartPosX;
            backgroundTexture.Position.Y = bgStartPosY + _bgBorderHeight + _bgCenterHeight;
            backgroundTexture.Width = _bgWidth;
            backgroundTexture.Height = _bgBorderHeight;
            backgroundTexture.P0 = _infoBackground[0].P0;
            backgroundTexture.P1 = _infoBackground[0].P1;
            backgroundTexture.P2 = _infoBackground[0].P2;
            backgroundTexture.P3 = _infoBackground[0].P3;
            backgroundTexture.UvDraw = true;

            _textureAddList.Add(backgroundTexture);

            if (!_textureDrawPool.TryDequeue(out backgroundTexture))
                backgroundTexture = new TextureDrawData();

            backgroundTexture.Material = _infoBackground[2].Material;
            backgroundTexture.Color = _bgColor * (_session.Session.Config.HUDBkOpacity * 1.8f);
            backgroundTexture.Position.X = bgStartPosX;
            backgroundTexture.Position.Y = bgStartPosY - (_bgBorderHeight + _bgCenterHeight);
            backgroundTexture.Width = _bgWidth;
            backgroundTexture.Height = _bgBorderHeight;
            backgroundTexture.P0 = _infoBackground[2].P0;
            backgroundTexture.P1 = _infoBackground[2].P1;
            backgroundTexture.P2 = _infoBackground[2].P2;
            backgroundTexture.P3 = _infoBackground[2].P3;
            backgroundTexture.UvDraw = true;

            _textureAddList.Add(backgroundTexture);
            #endregion

            if (reset)
                _currentLargestName = 0;

            for (int i = 0; i < _weapontoDraw.Count; i++)
            {
                TextDrawRequest textInfo;
                TextureDrawData reloadTexture;
                TextureDrawData heatTexture;

                var weapon = _weapontoDraw[i].HighestValueWeapon;
                var name = weapon.System.WeaponName + ": ";
                var textOffset = ((name.Length * _textSize) + (_padding * 1.5));

                if (!_textDrawPool.TryDequeue(out textInfo))
                    textInfo = new TextDrawRequest();

                textInfo.Text = name;
                textInfo.Color = Color.White * _session.UiOpacity;
                textInfo.Position.X = CurrWeaponDisplayPos.X - textOffset;
                textInfo.Position.Y = CurrWeaponDisplayPos.Y;
                textInfo.FontSize = _textSize;
                textInfo.Simple = false;
                _textAddList.Add(textInfo);


                if (_weapontoDraw[i].WeaponStack > 1)
                {
                    if (!_textDrawPool.TryDequeue(out textInfo))
                        textInfo = new TextDrawRequest();

                    textInfo.Text = $"(x{_weapontoDraw[i].WeaponStack})";
                    textInfo.Color = Color.LightSteelBlue * _session.UiOpacity;
                    textInfo.Position.X = CurrWeaponDisplayPos.X - (textOffset + (_stextWidth * textInfo.Text.Length) + (_padding *.5f));
                    textInfo.Position.Y = CurrWeaponDisplayPos.Y;
                    textInfo.FontSize = _sTextSize;
                    textInfo.Simple = false;
                    _textAddList.Add(textInfo);
                }


                if (weapon.HeatPerc > 0)
                {
                    if (!_textureDrawPool.TryDequeue(out heatTexture))
                        heatTexture = new TextureDrawData();
                    int heatBarIndex;
                    if (weapon.State.Sync.Overheated)
                        heatBarIndex = 10;
                    else
                        heatBarIndex = (int)(weapon.HeatPerc * 10);


                    heatTexture.Material = _heatBarTexture[heatBarIndex].Material;
                    heatTexture.Color = Color.Transparent;
                    heatTexture.Position.X = CurrWeaponDisplayPos.X - _heatOffsetX;
                    heatTexture.Position.Y = CurrWeaponDisplayPos.Y - _heatOffsetY;
                    heatTexture.Width = _heatWidth;
                    heatTexture.Height = _heatHeight;
                    heatTexture.P0 = _heatBarTexture[heatBarIndex].P0;
                    heatTexture.P1 = _heatBarTexture[heatBarIndex].P1;
                    heatTexture.P2 = _heatBarTexture[heatBarIndex].P2;
                    heatTexture.P3 = _heatBarTexture[heatBarIndex].P3;

                    _textureAddList.Add(heatTexture);
                }

                if (weapon.State.Sync.Reloading && weapon.State.Sync.Reloading && weapon.Comp.Session.Tick - weapon.LastLoadedTick > 30)
                {
                    if (!_textureDrawPool.TryDequeue(out reloadTexture))
                        reloadTexture = new TextureDrawData();

                    var offsetX = weapon.HeatPerc > 0 ? _reloadWidth + _heatWidth + _heatOffsetX : _reloadOffset + _padding;

                    reloadTexture.Material = _reloadingTexture.Material;
                    reloadTexture.Color = Color.DarkRed * _session.UiOpacity;
                    reloadTexture.Position.X = CurrWeaponDisplayPos.X - offsetX;
                    reloadTexture.Position.Y = CurrWeaponDisplayPos.Y - _heatOffsetY;
                    reloadTexture.Width = _reloadWidth;
                    reloadTexture.Height = _reloadHeight;
                    reloadTexture.P0 = _reloadingTexture.P0;
                    reloadTexture.P1 = _reloadingTexture.P1;
                    reloadTexture.P2 = _reloadingTexture.P2;
                    reloadTexture.P3 = _reloadingTexture.P3;

                    _textureAddList.Add(reloadTexture);
                }

                CurrWeaponDisplayPos.Y -= _infoPaneloffset + (_padding * .5f);

                if (reset)
                    _weaponStackedInfoPool.Enqueue(_weapontoDraw[i]);
            }

            if (reset)
            {
                _weapontoDraw.Clear();
                _weaponInfoListPool.Enqueue(_weapontoDraw);
            }
        }

    }
}
