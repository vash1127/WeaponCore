using System.Collections.Generic;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace CoreSystems
{
    partial class Hud
    {
        internal void DrawText()
        {
            _cameraWorldMatrix = _session.Camera.WorldMatrix;

            if (NeedsUpdate)
                UpdateHudSettings();
            
            AddAgingText();
            AgingTextDraw();
        }

        private void AddAgingText()
        {
            foreach (var aging in _agingTextRequests) {

                var textAdd = aging.Value;

                if (textAdd.Data.Count > 0)
                    continue;

                var scaleShadow = textAdd.Font == FontType.Shadow;
                var remap = scaleShadow ? _shadowCharWidthMap : _monoCharWidthMap;
                float messageLength = 0;
                for (int j = 0; j < textAdd.Text.Length; j++) {

                    var c = textAdd.Text[j];

                    float reSize;
                    var tooWide = remap.TryGetValue(c, out reSize);
  
                    var scaledWidth = textAdd.FontSize * (tooWide ? reSize : scaleShadow ? ShadowWidthScaler : MonoWidthScaler);
                    messageLength += scaledWidth;

                    var cm = CharacterMap[textAdd.Font][c];
                    var td = _textDataPool.Get();

                    td.Material = cm.Material;
                    td.P0 = cm.P0;
                    td.P1 = cm.P1;
                    td.P2 = cm.P2;
                    td.P3 = cm.P3;
                    td.UvDraw = true;
                    td.TooWide = tooWide;
                    td.ScaledWidth = scaledWidth;
                    textAdd.Data.Add(td);
                }
                textAdd.MessageWidth = messageLength;
                textAdd.Data.ApplyAdditions();
            }
        }

        private void AgingTextDraw()
        {
            var up = (Vector3)_cameraWorldMatrix.Up;
            var left = (Vector3)_cameraWorldMatrix.Left;

            foreach (var textAdd in _agingTextRequests.Values) {

                textAdd.Position.Z = _viewPortSize.Z;
                var requestPos = textAdd.Position;
                requestPos.Z = _viewPortSize.Z;
                var widthScaler = textAdd.Font == FontType.Shadow ? 1.5f : 1f;

                var textPos = Vector3D.Transform(requestPos, _cameraWorldMatrix);
                switch (textAdd.Justify)
                {
                    case Justify.Center:
                        textPos += _cameraWorldMatrix.Left * ((textAdd.MessageWidth * 0.5f) * widthScaler);
                        break;
                    case Justify.Right:
                        textPos -= _cameraWorldMatrix.Left * (textAdd.MessageWidth * widthScaler);
                        break;
                    case Justify.Left:
                        textPos -= _cameraWorldMatrix.Right * (textAdd.MessageWidth * widthScaler);
                        break;
                    case Justify.None:
                        textPos -= _cameraWorldMatrix.Left * ((textAdd.FontSize * 0.5f) * widthScaler);
                        break;
                }

                var height = textAdd.FontSize * textAdd.HeightScale;
                var width = (textAdd.FontSize * widthScaler) * _session.AspectRatioInv;
                var remove = textAdd.Ttl-- < 0;

                for (int i = 0; i < textAdd.Data.Count; i++) { 

                    var textData = textAdd.Data[i];
                    textData.WorldPos.Z = _viewPortSize.Z;

                    if (textData.UvDraw) {

                        MyQuadD quad;
                        MyUtils.GetBillboardQuadOriented(out quad, ref textPos, width, height, ref left, ref up);

                        if (textAdd.Color != Color.Transparent) {
                            MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point1, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, textData.P0, textData.P1, textData.P3, textData.Material, 0, textPos, textAdd.Color, textData.Blend);
                            MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point3, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, textData.P0, textData.P2, textData.P3, textData.Material, 0, textPos, textAdd.Color, textData.Blend);
                        }
                        else {
                            MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point1, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, textData.P0, textData.P1, textData.P3, textData.Material, 0, textPos, textData.Blend);
                            MyTransparentGeometry.AddTriangleBillboard(quad.Point0, quad.Point3, quad.Point2, Vector3.Zero, Vector3.Zero, Vector3.Zero, textData.P0, textData.P2, textData.P3, textData.Material, 0, textPos, textData.Blend);
                        }
                    }

                    textPos -= _cameraWorldMatrix.Left * textData.ScaledWidth;

                    if (remove) {
                        textAdd.Data.Remove(textData);
                        _textDataPool.Return(textData);
                    }
                }

                textAdd.Data.ApplyRemovals();
                AgingTextRequest request;
                if (textAdd.Data.Count == 0 && _agingTextRequests.TryRemove(textAdd.Type, out request))
                {
                    _agingTextRequests.Remove(textAdd.Type);
                    _agingTextRequestPool.Return(request);
                }

            }
            AgingTextures = _agingTextRequests.Count > 0;
        }

    }
}
