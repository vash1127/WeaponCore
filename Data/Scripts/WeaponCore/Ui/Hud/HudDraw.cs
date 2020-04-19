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
            _aspectratio = _session.Camera.ViewportSize.X / _session.Camera.ViewportSize.Y;
            _cameraWorldMatrix = _session.Camera.WorldMatrix;

            var frustHeight = (float)(.1f * Math.Tan(_session.Camera.FovWithZoom * 0.5f));

            _viewPortSize.X = (frustHeight * (_session.Camera.ViewportSize.X / _session.Camera.ViewportSize.Y));
            _viewPortSize.Y = frustHeight;

            #region WeaponHudDisplay 
            //inlined becuase there can be many and too many method calls
            if (WeaponsToDisplay.Count > 0)
            {
                var fovModifier = _session.Camera.FovWithZoom / _defaultFov;
                CurrWeaponDisplayPos = new Vector2(_viewPortSize.X, _viewPortSize.Y * .6f);

                var ticksSinceUpdate = _session.Tick - _lastHudUpdateTick;

                var reset = false;
                if (ticksSinceUpdate >= _minUpdateTicks)
                {
                    _weapontoDraw = SortDisplayedWeapons(WeaponsToDisplay);
                    _lastHudUpdateTick = _session.Tick;
                }
                else if (ticksSinceUpdate + 1 >= _minUpdateTicks)
                    reset = true;

                #region Background draw
                TextureDrawData backgroundTexture;
                if (!_textureDrawPool.TryDequeue(out backgroundTexture))
                    backgroundTexture = new TextureDrawData();

                var padding = _padding * fovModifier;
                var reloadWidth = _reloadWidth * fovModifier;
                var reloadOffset = reloadWidth * (1.6f * fovModifier) + padding;
                var heatOffsetX = _heatWidthOffset * fovModifier;
                var heatOffsetY = _heatHeightOffset * fovModifier;

                var bgWidth = (((_currentLargestName + 6) * _WeaponHudFontHeight) + reloadWidth) * fovModifier;
                var bgBorderHeight = bgWidth * _bgBorderRatio;
                var bgCenterHeight = _weapontoDraw.Count > 3 ? (_weapontoDraw.Count - 2) * _infoPanelOffset : _infoPanelOffset * 2;

                var bgStartPosX = CurrWeaponDisplayPos.X - bgWidth - padding;
                var bgStartPosY = CurrWeaponDisplayPos.Y - bgCenterHeight;

                backgroundTexture.Material = _infoBackground[1].Material;
                backgroundTexture.Color = _bgColor * (_session.Session.Config.HUDBkOpacity * 1.8f);
                backgroundTexture.Position = new Vector3D(bgStartPosX, bgStartPosY, -.1f);
                backgroundTexture.Width = bgWidth;
                backgroundTexture.Height = bgCenterHeight;
                backgroundTexture.P0 = _infoBackground[1].P0;
                backgroundTexture.P1 = _infoBackground[1].P1;
                backgroundTexture.P2 = _infoBackground[1].P2;
                backgroundTexture.P3 = _infoBackground[1].P3;

                _textureAddList.Add(backgroundTexture);
                
                if (!_textureDrawPool.TryDequeue(out backgroundTexture))
                    backgroundTexture = new TextureDrawData();

                backgroundTexture.Material = _infoBackground[0].Material;
                backgroundTexture.Color = _bgColor * (_session.Session.Config.HUDBkOpacity * 1.8f);
                backgroundTexture.Position = new Vector3D(bgStartPosX, bgStartPosY + bgBorderHeight + bgCenterHeight, -.1f);
                backgroundTexture.Width = bgWidth;
                backgroundTexture.Height = bgBorderHeight;
                backgroundTexture.P0 = _infoBackground[0].P0;
                backgroundTexture.P1 = _infoBackground[0].P1;
                backgroundTexture.P2 = _infoBackground[0].P2;
                backgroundTexture.P3 = _infoBackground[0].P3;

                _textureAddList.Add(backgroundTexture);

                if (!_textureDrawPool.TryDequeue(out backgroundTexture))
                    backgroundTexture = new TextureDrawData();

                backgroundTexture.Material = _infoBackground[2].Material;
                backgroundTexture.Color = _bgColor * (_session.Session.Config.HUDBkOpacity * 1.8f);
                backgroundTexture.Position = new Vector3D(bgStartPosX, bgStartPosY - (bgBorderHeight + bgCenterHeight), -.1f);
                backgroundTexture.Width = bgWidth;
                backgroundTexture.Height = bgBorderHeight;
                backgroundTexture.P0 = _infoBackground[2].P0;
                backgroundTexture.P1 = _infoBackground[2].P1;
                backgroundTexture.P2 = _infoBackground[2].P2;
                backgroundTexture.P3 = _infoBackground[2].P3;

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
                    var textSize = _WeaponHudFontHeight * fovModifier;
                    var textOffset = ((name.Length * (textSize * _aspectratio)) + padding);

                    if (!_textDrawPool.TryDequeue(out textInfo))
                        textInfo = new TextDrawRequest();

                    textInfo.Text = name;
                    textInfo.Color = Color.White * _session.UiOpacity;
                    textInfo.X = (CurrWeaponDisplayPos.X - textOffset);
                    textInfo.Y = CurrWeaponDisplayPos.Y;
                    textInfo.FontSize = textSize;
                    _textAddList.Add(textInfo);


                    if (_weapontoDraw[i].WeaponStack > 1)
                    {
                        if (!_textDrawPool.TryDequeue(out textInfo))
                            textInfo = new TextDrawRequest();

                        var stextSize = textSize * .75f;

                        textInfo.Text = $"(x{_weapontoDraw[i].WeaponStack})";
                        textInfo.Color = Color.LightSteelBlue * _session.UiOpacity;
                        textInfo.X = CurrWeaponDisplayPos.X - (textOffset + ((stextSize * _aspectratio) * textInfo.Text.Length));
                        textInfo.Y = CurrWeaponDisplayPos.Y;
                        textInfo.FontSize = stextSize;
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
                        heatTexture.Position = new Vector3D(CurrWeaponDisplayPos.X -  heatOffsetX, CurrWeaponDisplayPos.Y - heatOffsetY, -.1f);
                        heatTexture.Width = _heatWidth * fovModifier;
                        heatTexture.Height = _heatHeight * fovModifier;
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


                        var reloadHeight = _reloadHeight * fovModifier;

                        var offsetX = weapon.HeatPerc > 0 ? reloadWidth + _heatWidth + heatOffsetX : reloadOffset + padding;

                        reloadTexture.Material = _reloadingTexture.Material;
                        reloadTexture.Color = Color.DarkRed * _session.UiOpacity;
                        reloadTexture.Position = new Vector3D(CurrWeaponDisplayPos.X - offsetX, CurrWeaponDisplayPos.Y - heatOffsetY, -.1f);
                        reloadTexture.Width = reloadWidth;
                        reloadTexture.Height = reloadHeight;
                        reloadTexture.P0 = _reloadingTexture.P0;
                        reloadTexture.P1 = _reloadingTexture.P1;
                        reloadTexture.P2 = _reloadingTexture.P2;
                        reloadTexture.P3 = _reloadingTexture.P3;

                        _textureAddList.Add(reloadTexture);
                    }

                    CurrWeaponDisplayPos.Y -= _infoPanelOffset + (padding * .5f);

                    if(reset)
                        _weaponStackedInfoPool.Enqueue(_weapontoDraw[i]);
                }

                if (reset)
                {
                    _weapontoDraw.Clear();
                    _weaponInfoListPool.Enqueue(_weapontoDraw);
                }
            }
            #endregion

            #region UV Offset based draws
            for (int i = 0; i < _textAddList.Count; i++)
            {
                var textAdd = _textAddList[i];
                var position = new Vector3D(textAdd.X, textAdd.Y, -.1);
                position = Vector3D.Transform(position, _cameraWorldMatrix);

                var height = textAdd.FontSize;
                var width = textAdd.FontSize;
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
                    tdd.P0 = cm.P0;
                    tdd.P1 = cm.P1;
                    tdd.P2 = cm.P2;
                    tdd.P3 = cm.P3;

                    _uvDrawList.Add(tdd);

                    textPos -= (_cameraWorldMatrix.Left * (height * _aspectratio));
                }

                _textDrawPool.Enqueue(textAdd);
            }

            for (int i = 0; i < _textureAddList.Count; i++)
            {
                var tdd = _textureAddList[i];

                tdd.Position = Vector3D.Transform(tdd.Position, _cameraWorldMatrix);
                tdd.Up = _cameraWorldMatrix.Up;
                tdd.Left = _cameraWorldMatrix.Left;
                _uvDrawList.Add(tdd);
            }

            for (int i = 0; i < _uvDrawList.Count; i++)
            {
                var textureToDraw = _uvDrawList[i];

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

                _textureDrawPool.Enqueue(textureToDraw);
            }
            #endregion

            #region Simple based draws
            for (int i = 0; i < _simpleDrawList.Count; i++)
            {
                var textureToDraw = _simpleDrawList[i];
                var scale = 0.005 * Math.Tan(MathHelper.ToRadians(_session.Camera.FieldOfViewAngle * .5f));

                textureToDraw.Position = Vector3D.Transform(textureToDraw.Position, _cameraWorldMatrix);
                scale = 1 * scale;

                MyTransparentGeometry.AddBillboardOriented(textureToDraw.Material, textureToDraw.Color, textureToDraw.Position, _cameraWorldMatrix.Left, _cameraWorldMatrix.Up, (float)scale, textureToDraw.Blend);

                _textureDrawPool.Enqueue(textureToDraw);
            }
            #endregion

            WeaponsToDisplay.Clear();
            _textAddList.Clear();
            _textureAddList.Clear();
            _uvDrawList.Clear();
            _simpleDrawList.Clear();
            TexturesToAdd = 0;
        }

    }
}
